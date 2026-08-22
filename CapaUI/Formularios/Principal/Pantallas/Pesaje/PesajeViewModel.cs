using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CapaAplicacion.Pesaje.Dtos;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje
{
    /// <summary>
    /// Estado y lógica de "Recepción de Materia Prima" (Fase 2, persistencia real).
    /// Lee/escribe en movimientos / movimiento_productos / entradas_producto vía IPesajeRepository.
    /// </summary>
    public partial class PesajeViewModel : ObservableObject
    {
        /// <summary>
        /// Camiones físicos simultáneos en el andén. Se cuenta por <b>placa distinta</b>,
        /// no por movimiento: un camión que trae carga de dos proveedores son dos
        /// recepciones en la base pero un solo camión en la puerta.
        /// </summary>
        public const int MaxCamiones = 5;

        private readonly IPesajeRepository _repo;
        private readonly IUsuarioSesionService _sesionService;

        public ObservableCollection<CamionPesaje>  Camiones      { get; } = new();
        public ObservableCollection<EntradaPesaje> FilasEntradas { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CamionCerrado))]
        [NotifyPropertyChangedFor(nameof(HayCamion))]
        private CamionPesaje? _selectedCamion;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HayProducto))]
        private ProductoCamion? _selectedProducto;

        [ObservableProperty] private EntradaPesaje? _selectedEntrada;
        [ObservableProperty] private string _vistaEntradas = "producto";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MostrarEstadoVacio))]
        private bool _isLoading;

        /// <summary>
        /// Hay al menos un camión (placa) con 2+ recepciones abiertas en la lista. Controla
        /// que TODOS los encabezados de placa se muestren en <see cref="Camiones"/> —
        /// incluidos los camiones de 1 sola recepción — apenas exista alguno agrupado, en
        /// vez de decidirlo grupo por grupo.
        /// </summary>
        [ObservableProperty] private bool _hayPlacaCompartida;

        public bool HayCamion     => SelectedCamion is not null;
        public bool HayProducto   => SelectedProducto is not null;
        public bool CamionCerrado => SelectedCamion?.Estado == "Cerrado";
        public string ModoEfectivo => SelectedProducto is not null ? VistaEntradas : "camion";

        /// <summary>Placas distintas abiertas — camiones físicos, no movimientos.</summary>
        private int PlacasAbiertas => Camiones
            .Where(c => c.Estado == "Abierto")
            .Select(c => (c.Placa ?? "").Trim().ToUpperInvariant())
            .Distinct()
            .Count();

        public int    CamionesCount      => PlacasAbiertas;
        public string CamionesTexto      => $"{PlacasAbiertas}/{MaxCamiones}";
        public bool   PuedeAgregarCamion => PlacasAbiertas < MaxCamiones;

        /// <summary>
        /// Movimientos abiertos, no placas: "Cerrar todos" cierra recepción por
        /// recepción, así que acá sí se cuenta de a una.
        /// </summary>
        public int    CamionesActivos    => Camiones.Count(c => c.Estado == "Abierto");

        // ── Totales de la fila al pie de Entradas ─────────────────────────────
        // Suman lo que ya está en memoria (FilasEntradas), que es exactamente lo
        // que la grilla tiene a la vista: cambian solos al alternar entre
        // "Producto actual" y "Todo el camión". No consultan al repositorio.
        public double TotalBruto        => FilasEntradas.Sum(e => e.Bruto);
        public double TotalTara         => FilasEntradas.Sum(e => e.TaraTotal);
        public double TotalTaraExtra    => FilasEntradas.Sum(e => e.TaraExtra);
        public double TotalNeto         => FilasEntradas.Sum(e => e.Neto);

        /// <summary>El dato real cuando existe, si no la estimación — igual que <see cref="EntradaPesaje.BultosTexto"/>.</summary>
        public double TotalBultos       => FilasEntradas.Sum(e => e.BultosCapturados ?? e.BultosTeoricos ?? 0);

        public string TotalPesajesTexto => $"{FilasEntradas.Count} pesaje(s)";

        /// <summary>
        /// No hay ninguna descarga en curso: la pantalla muestra el estado vacío con el
        /// botón para iniciar el proceso, en vez de tres paneles vacíos sin contexto.
        /// "Descargándose" = camión abierto.
        /// <para/>
        /// El <c>!IsLoading</c> es obligatorio: mientras se consultan los camiones la
        /// colección está vacía, así que sin esa guarda el estado vacío aparecía un
        /// instante en cada carga aunque sí hubiera camiones abiertos — se veía como
        /// si se abriera solo el formulario de iniciar descarga y después cambiara.
        /// </summary>
        public bool MostrarEstadoVacio => !IsLoading && CamionesActivos == 0;

        public event Action<string>? Toast;

        public PesajeViewModel(IPesajeRepository repo, IUsuarioSesionService sesionService)
        {
            _repo = repo;
            _sesionService = sesionService;
        }

        /// <summary>
        /// Id del usuario autenticado. Fail-loud: si no hay sesión activa lanza
        /// excepción en vez de retornar 0 — un pesaje jamás debe registrarse con
        /// usuario fantasma (id_usuario = 0). Los llamadores validan antes con
        /// <see cref="HaySesionActiva"/> para dar feedback amigable vía Toast.
        /// </summary>
        private int UsuarioActual => _sesionService.SesionActual?.IdUsuario
            ?? throw new InvalidOperationException(
                "No hay sesión activa; no se puede registrar la operación de pesaje.");

        /// <summary>Guarda amigable: true si hay sesión; si no, loguea y notifica.</summary>
        private bool HaySesionActiva(string operacion)
        {
            if (_sesionService.SesionActual is not null) return true;
            Serilog.Log.Warning("PesajeVM: intento de {Operacion} sin sesión activa", operacion);
            Toast?.Invoke("Sesión expirada. Vuelve a iniciar sesión.");
            return false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Carga
        // ══════════════════════════════════════════════════════════════════════
        public async Task CargarAsync()
        {
            IsLoading = true;
            var r = await _repo.GetCamionesActivosAsync();
            if (!r.Success) { IsLoading = false; Toast?.Invoke(r.Error ?? "Error al cargar camiones"); return; }

            Camiones.Clear();
            foreach (var c in r.Value!) Camiones.Add(MapCamion(c));
            RecalcularRecepcionesPorPlaca();
            NotificarStats();

            SelectedCamion   = Camiones.FirstOrDefault();
            SelectedProducto = null;
            SelectedEntrada  = null;
            if (SelectedCamion != null) await CargarProductosAsync(SelectedCamion);
            RecalcularFilas();
            IsLoading = false;
        }

        private async Task CargarProductosAsync(CamionPesaje camion)
        {
            var r = await _repo.GetProductosAsync(camion.Id);
            camion.Productos.Clear();
            if (!r.Success) { Toast?.Invoke(r.Error ?? "Error al cargar productos"); return; }
            foreach (var p in r.Value!) camion.Productos.Add(MapProducto(p, camion.Proveedor));

            // Los totales de tara extra del camión son la suma de la de sus productos,
            // que recién se conoce con los productos ya cargados.
            camion.NotificarTotales();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Selección
        // ══════════════════════════════════════════════════════════════════════
        public async Task SeleccionarCamionAsync(CamionPesaje? c)
        {
            SelectedCamion   = c;
            SelectedProducto = null;
            SelectedEntrada  = null;
            if (c != null) await CargarProductosAsync(c);
            RecalcularFilas();
            NotificarStats();
        }

        public void SeleccionarProducto(ProductoCamion? p)
        {
            SelectedProducto = p;
            SelectedEntrada  = null;
            RecalcularFilas();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Camiones
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Persiste los datos del camión (alta o edición) — nada más. Los productos ya
        /// no viajan acá: se agregan/editan/quitan uno a la vez desde
        /// <see cref="AgregarProductoAsync"/>/<see cref="ActualizarProductoAsync"/>/
        /// <see cref="QuitarProductoAsync"/>, disparados desde <c>ProductoCamionModal</c>
        /// en la pantalla principal.
        /// <para/>
        /// La tara extra NO se toca acá: se pesa por entrada, no por camión.
        /// </summary>
        public async Task<bool> GuardarCamionAsync(
            CamionPesaje? camionExistente, string placa, string proveedor, int? idProveedor, string obs)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return false; }
            if (!HaySesionActiva("guardar el camión")) return false;

            int idCamion;

            if (camionExistente is null)
            {
                var rNuevo = await _repo.CrearCamionAsync(idProveedor.Value, placa, obs, UsuarioActual);
                if (!rNuevo.Success) { Toast?.Invoke(rNuevo.Error ?? "No se pudo registrar el camión"); return false; }
                idCamion = rNuevo.Value;
            }
            else
            {
                var rEdit = await _repo.ActualizarCamionAsync(camionExistente.Id, idProveedor.Value, placa, obs);
                if (!rEdit.Success) { Toast?.Invoke(rEdit.Error ?? "No se pudo actualizar el camión"); return false; }
                idCamion = camionExistente.Id;
            }

            await RecargarCamionesAsync(seleccionarId: idCamion);
            Toast?.Invoke(camionExistente is null ? "Camión registrado" : "Cambios guardados");
            return true;
        }

        /// <summary>
        /// Recibe el camión explícito (no <see cref="SelectedCamion"/>): lo dispara el
        /// ícono de basurero de su propia fila en la lista, así que borra el que se tocó
        /// sin depender de que ese clic también haya cambiado la selección.
        /// </summary>
        public async Task QuitarCamionAsync(CamionPesaje camion)
        {
            var r = await _repo.AnularCamionAsync(camion.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo quitar"); return; }

            await RecargarCamionesAsync();
            Toast?.Invoke("Camión eliminado");
        }

        public async Task<bool> DescargarCamionAsync()
        {
            if (SelectedCamion is null || CamionCerrado) return false;
            var r = await _repo.CerrarCamionAsync(SelectedCamion.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo cerrar"); return false; }

            SelectedCamion.Estado = "Cerrado";
            OnPropertyChanged(nameof(CamionCerrado));
            NotificarStats();
            return true;
        }

        public async Task<System.Collections.Generic.List<CamionPesaje>> CerrarTodosAsync()
        {
            var abiertos = Camiones.Where(c => c.Estado == "Abierto").ToList();
            foreach (var c in abiertos)
            {
                var r = await _repo.CerrarCamionAsync(c.Id);
                if (r.Success) c.Estado = "Cerrado";
                else Toast?.Invoke(r.Error ?? $"No se pudo cerrar {c.Placa}");
            }
            OnPropertyChanged(nameof(CamionCerrado));
            NotificarStats();
            return abiertos;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Productos
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Agrega un producto resolviendo el destino por <b>placa + proveedor</b>, no por
        /// el camión seleccionado.
        /// <para/>
        /// Un camión puede traer carga de dos proveedores, y en la base eso son dos
        /// <c>movimientos</c> con la misma placa (uno por proveedor): así cada movimiento
        /// queda con su proveedor real y solo sus productos, y cada manifiesto se firma y
        /// se reporta por separado. Acá se busca la recepción de ese proveedor para esa
        /// placa y, si no existe, se crea — el operador no tiene que darla de alta a mano.
        /// </summary>
        public async Task<bool> AgregarProductoAsync(
            string placa, int idProveedor, string nombreProveedor,
            int idProducto, double pesoManifestado, int bultosTeoricos, string obs)
        {
            var destino = Camiones.FirstOrDefault(c =>
                c.Estado == "Abierto"
                && string.Equals(c.Placa?.Trim(), placa.Trim(), StringComparison.OrdinalIgnoreCase)
                && c.IdProveedor == idProveedor);

            bool recepcionNueva = destino is null;
            int idMovimiento;

            if (destino is not null)
            {
                idMovimiento = destino.Id;
            }
            else
            {
                // Crear la recepción del otro proveedor no consume cupo: el límite cuenta
                // placas distintas, y esta placa ya estaba abierta.
                if (!HaySesionActiva("abrir la recepción del otro proveedor")) return false;

                var rNueva = await _repo.CrearCamionAsync(idProveedor, placa.Trim(), "", UsuarioActual);
                if (!rNueva.Success)
                {
                    Toast?.Invoke(rNueva.Error ?? "No se pudo abrir la recepción del proveedor");
                    return false;
                }
                idMovimiento = rNueva.Value;
            }

            var r = await _repo.AgregarProductoAsync(idMovimiento, idProducto, pesoManifestado, bultosTeoricos, obs);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo agregar"); return false; }

            await RecargarCamionesAsync(seleccionarId: idMovimiento);
            SelectedProducto = SelectedCamion?.Productos.FirstOrDefault(p => p.Id == r.Value);
            RecalcularFilas();

            // La recepción nueva no debe aparecer por arte de magia: se dice qué pasó.
            Toast?.Invoke(recepcionNueva
                ? $"Producto agregado — se abrió una recepción aparte para {nombreProveedor}"
                : "Producto agregado al camión");
            return true;
        }

        /// <summary>
        /// Escribe en cada camión cuántas recepciones abiertas comparten su placa. Es lo
        /// que le permite a la lista mostrar un camión con dos proveedores como UN camión
        /// agrupado y no como dos registros sueltos.
        /// </summary>
        private void RecalcularRecepcionesPorPlaca()
        {
            var porPlaca = Camiones
                .Where(c => c.Estado == "Abierto")
                .GroupBy(c => (c.Placa ?? "").Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var c in Camiones)
                c.RecepcionesEnPlaca = porPlaca.TryGetValue((c.Placa ?? "").Trim().ToUpperInvariant(), out var n)
                    ? n : 1;

            HayPlacaCompartida = porPlaca.Values.Any(n => n > 1);
        }

        /// <summary>Recepciones abiertas que comparten placa con <paramref name="placa"/>.</summary>
        public IReadOnlyList<CamionPesaje> RecepcionesDePlaca(string placa) =>
            Camiones.Where(c => c.Estado == "Abierto"
                                && string.Equals(c.Placa?.Trim(), placa.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();

        /// <summary>
        /// Devuelve si guardó, igual que <see cref="AgregarProductoAsync"/>: el modal
        /// espera el resultado para saber si puede cerrarse o limpiarse.
        /// </summary>
        public async Task<bool> ActualizarProductoAsync(ProductoCamion p, double pesoManifestado, int bultosTeoricos, string obs)
        {
            if (SelectedCamion is null) return false;
            var r = await _repo.ActualizarProductoAsync(p.Id, pesoManifestado, bultosTeoricos, obs);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo actualizar"); return false; }

            int id = p.Id;
            await CargarProductosAsync(SelectedCamion);
            SelectedProducto = SelectedCamion.Productos.FirstOrDefault(x => x.Id == id);
            RecalcularFilas();
            Toast?.Invoke("Producto actualizado");
            return true;
        }

        /// <summary>
        /// Recibe el producto explícito (no <see cref="SelectedProducto"/>): lo dispara
        /// el botón "Quitar producto" de <c>ProductoCamionModal</c>, ya abierto sobre
        /// ese producto puntual — mismo criterio que <see cref="QuitarCamionAsync"/>.
        /// </summary>
        public async Task QuitarProductoAsync(ProductoCamion producto)
        {
            if (SelectedCamion is null) return;
            var camion = SelectedCamion;
            var r = await _repo.AnularProductoAsync(producto.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo quitar"); return; }

            await CargarProductosAsync(camion);
            SelectedProducto = null;
            SelectedEntrada  = null;
            RecalcularFilas();
            Toast?.Invoke("Producto eliminado");
        }

        public async Task ToggleEstadoProductoAsync(ProductoCamion p)
        {
            if (CamionCerrado) return;
            bool cerrar = p.Estado == "Abierto";
            var r = await _repo.SetEstadoProductoAsync(p.Id, cerrar);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo cambiar estado"); return; }
            p.Estado = cerrar ? "Cerrado" : "Abierto";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Pesajes
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Registra un pesaje y refleja el resultado SIN recargar el camión.
        /// <para/>
        /// El INSERT ya devuelve la fila con los derivados que calculó el trigger de BD, así
        /// que aplicar ese DTO a la colección en memoria deja la pantalla exactamente igual
        /// que un refetch — pero con un solo round trip en vez de cuatro. Con la latencia de
        /// la red de planta esa diferencia es la que hacía que "Seguir pesando" se sintiera
        /// colgado.
        /// <para/>
        /// No es un guardado optimista: nada se muestra hasta que la BD confirmó, y los
        /// números que se muestran son los que la BD devolvió, no los que calculó el modal.
        /// </summary>
        /// <returns><c>true</c> si el pesaje quedó guardado.</returns>
        public async Task<bool> GuardarEntradaAsync(ProductoCamion producto, EntradaPesaje snapshot, EntradaPesaje? editando)
        {
            if (SelectedCamion is null) return false;
            if (!HaySesionActiva("guardar pesaje")) return false;

            if (editando is not null)
            {
                var ra = await _repo.AnularEntradaAsync(editando.Id);
                if (!ra.Success) { Toast?.Invoke(ra.Error ?? "No se pudo editar"); return false; }
                producto.Entradas.Remove(editando);
            }

            // La tara extra es un peso real que se captura en el modal (las tarimas que
            // vinieron con esta pesada). Ya no se prorratea nada: si no se pesó acá, se
            // carga después el total y el sistema lo reparte entre las pesadas.
            var r = await _repo.CrearEntradaAsync(
                producto.Id, producto.IdProducto, snapshot.Bruto, snapshot.TaraExtra,
                snapshot.Observaciones, UsuarioActual);

            if (!r.Success)
            {
                Toast?.Invoke(r.Error ?? "No se pudo guardar el pesaje");

                // Si veníamos de una edición, la entrada vieja ya se anuló en la BD y se quitó
                // de la colección: sin el INSERT que la reemplace, la memoria y la BD podrían
                // no coincidir. Acá sí vale el refetch completo — es el camino de error.
                if (editando is not null) await CargarProductosAsync(SelectedCamion);
                RecalcularFilas();
                return false;
            }

            AgregarEntradaEnMemoria(producto, r.Value!);
            RecalcularFilas();
            Toast?.Invoke("Pesaje guardado");
            return true;
        }

        /// <summary>
        /// Inserta la pesada recién confirmada por la BD en la colección del producto y
        /// refresca los agregados derivados (recibido, tara extra, bultos estimados).
        /// <para/>
        /// Replica el mapeo de <see cref="MapProducto"/> para una sola entrada: los bultos
        /// teóricos son lo único que no viaja en el DTO porque no se persiste, se estima
        /// desde el peso del producto.
        /// </summary>
        private void AgregarEntradaEnMemoria(ProductoCamion producto, EntradaDto e)
        {
            producto.Entradas.Add(new EntradaPesaje
            {
                Id = e.Id, Bruto = e.Bruto, TaraInd = e.TaraInd, TaraExtra = e.TaraExtra,
                TaraTotal = e.TaraTotal, Neto = e.Neto, Fecha = e.Fecha, Hora = e.Hora,
                BultosCapturados = e.BultosCapturados,
                BultosTeoricos = PesajeCalc.BultosTeoricos(
                    e.Bruto, e.TaraExtra, producto.PesoTeorico, producto.TaraUnitaria),
                Observaciones = e.Observaciones,
                ProdId = producto.Id, ProdNombre = producto.ProductoNombre,
            });

            producto.NotificarAgregados();
            SelectedCamion?.NotificarTotales();
        }

        /// <summary>
        /// Anula una pesada y la quita de la colección en memoria.
        /// <para/>
        /// Mismo criterio que <see cref="GuardarEntradaAsync"/>: anular es un solo round trip
        /// y ya sabemos exactamente qué fila desapareció, así que recargar el camión entero
        /// solo agregaría tres viajes para llegar al mismo estado.
        /// </summary>
        public async Task QuitarEntradaAsync(EntradaPesaje entrada)
        {
            if (SelectedCamion is null) return;
            var r = await _repo.AnularEntradaAsync(entrada.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo quitar"); return; }

            // La entrada sabe a qué producto pertenece (RecalcularFilas le setea ProdId), así
            // que se ubica sin depender de cuál esté seleccionado: en vista "Todo el camión"
            // se puede borrar una pesada de un producto distinto al seleccionado.
            var producto = SelectedCamion.Productos.FirstOrDefault(p => p.Id == entrada.ProdId);
            if (producto is not null)
            {
                producto.Entradas.Remove(entrada);
                producto.NotificarAgregados();
                SelectedCamion.NotificarTotales();
            }

            SelectedEntrada = null;
            RecalcularFilas();
            Toast?.Invoke("Entrada eliminada");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Tara extra — reparto de un total ya pesado
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reparte una tara extra TOTAL ya pesada entre las pesadas del alcance elegido
        /// (un producto o el camión entero) y la escribe en cada entrada.
        /// <para/>
        /// Es un UPDATE en el lugar, NO el patrón anular+insertar que usa la edición de un
        /// pesaje: reemitir N ids, fechas y usuarios destruiría la auditoría de pesadas que
        /// no se están corrigiendo — sólo se les está completando un dato.
        /// <para/>
        /// Pre-vuelo obligatorio: si alguna pesada quedara con neto ≤ 0 no se escribe NADA.
        /// No hay transacción (son N PATCH sueltos), y el <c>peso_neto</c> es lo que se le
        /// paga al proveedor: mejor fallar entero que dejar un reparto a medias.
        /// </summary>
        public async Task<bool> RepartirTaraExtraAsync(IReadOnlyList<EntradaPesaje> entradas, double totalKg)
        {
            if (SelectedCamion is null) return false;
            if (!HaySesionActiva("repartir la tara extra")) return false;

            if (entradas.Count == 0)
            {
                Toast?.Invoke("Registrá al menos una pesada antes de cargar la tara extra.");
                return false;
            }
            if (totalKg < 0) { Toast?.Invoke("La tara extra no puede ser negativa."); return false; }

            var cuotas = PesajeCalc.RepartirTaraExtra(totalKg, entradas.Count);

            // Pre-vuelo: ninguna escritura hasta saber que todas pasan el CHECK.
            for (int i = 0; i < entradas.Count; i++)
            {
                double neto = entradas[i].Bruto - entradas[i].TaraInd - cuotas[i];
                if (neto <= 0)
                {
                    double max = PesajeCalc.TaraExtraMaximaRepartible(
                        entradas.Select(x => (x.Bruto, x.TaraInd)));
                    Toast?.Invoke(
                        $"No se puede repartir {totalKg:N2} kg: una pesada quedaría con neto {neto:N2} kg. " +
                        $"Máximo repartible: {max:N2} kg.");
                    return false;
                }
            }

            int fallidas = 0;
            for (int i = 0; i < entradas.Count; i++)
            {
                var e = entradas[i];
                double taraTotal = e.TaraInd + cuotas[i];
                var r = await _repo.ActualizarTaraExtraEntradaAsync(
                    e.Id, cuotas[i], taraTotal, e.Bruto - taraTotal);
                if (!r.Success)
                {
                    fallidas++;
                    Serilog.Log.Error("PesajeVM: falló el reparto de tara extra en la entrada {Id}: {Error}",
                        e.Id, r.Error);
                }
            }

            int? idProd = SelectedProducto?.Id;
            await CargarProductosAsync(SelectedCamion);
            SelectedProducto = idProd.HasValue
                ? SelectedCamion.Productos.FirstOrDefault(x => x.Id == idProd.Value)
                : null;
            RecalcularFilas();

            Toast?.Invoke(fallidas == 0
                ? $"Tara extra repartida entre {entradas.Count} pesada(s)"
                : $"Atención: {fallidas} de {entradas.Count} pesadas no se actualizaron. Volvé a aplicar el total.");
            return fallidas == 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Filas de entradas
        // ══════════════════════════════════════════════════════════════════════
        public void RecalcularFilas()
        {
            FilasEntradas.Clear();
            if (SelectedCamion is null) { OnPropertyChanged(nameof(ModoEfectivo)); NotificarTotales(); return; }

            if (ModoEfectivo == "producto" && SelectedProducto is not null)
            {
                foreach (var e in SelectedProducto.Entradas)
                {
                    e.ProdId = SelectedProducto.Id; e.ProdNombre = SelectedProducto.ProductoNombre;
                    FilasEntradas.Add(e);
                }
            }
            else
            {
                foreach (var p in SelectedCamion.Productos)
                    foreach (var e in p.Entradas)
                    {
                        e.ProdId = p.Id; e.ProdNombre = p.ProductoNombre;
                        FilasEntradas.Add(e);
                    }
            }
            OnPropertyChanged(nameof(ModoEfectivo));
            NotificarTotales();
        }

        /// <summary>
        /// Los totales derivan de FilasEntradas, que se reconstruye entera en cada
        /// RecalcularFilas: no hay forma de notificarlos por cambio de item, así que
        /// se avisan acá, en el único punto que la rearma.
        /// </summary>
        private void NotificarTotales()
        {
            OnPropertyChanged(nameof(TotalBruto));
            OnPropertyChanged(nameof(TotalTara));
            OnPropertyChanged(nameof(TotalTaraExtra));
            OnPropertyChanged(nameof(TotalNeto));
            OnPropertyChanged(nameof(TotalBultos));
            OnPropertyChanged(nameof(TotalPesajesTexto));
        }

        public void CambiarVista(string modo) { VistaEntradas = modo; RecalcularFilas(); }

        // ══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════════
        private async Task RecargarCamionesAsync(int? seleccionarId = null)
        {
            var r = await _repo.GetCamionesActivosAsync();
            if (!r.Success) { Toast?.Invoke(r.Error ?? "Error al recargar"); return; }

            Camiones.Clear();
            foreach (var c in r.Value!) Camiones.Add(MapCamion(c));
            RecalcularRecepcionesPorPlaca();
            NotificarStats();

            SelectedCamion = seleccionarId.HasValue
                ? Camiones.FirstOrDefault(c => c.Id == seleccionarId.Value) ?? Camiones.FirstOrDefault()
                : Camiones.FirstOrDefault();
            SelectedProducto = null;
            SelectedEntrada  = null;
            if (SelectedCamion != null) await CargarProductosAsync(SelectedCamion);
            RecalcularFilas();
        }

        private void NotificarStats()
        {
            OnPropertyChanged(nameof(CamionesCount));
            OnPropertyChanged(nameof(CamionesActivos));
            OnPropertyChanged(nameof(CamionesTexto));
            OnPropertyChanged(nameof(PuedeAgregarCamion));
            OnPropertyChanged(nameof(MostrarEstadoVacio));
        }

        private static CamionPesaje MapCamion(CamionDto c) => new()
        {
            Id = c.Id, Placa = c.Placa, Proveedor = c.Proveedor, IdProveedor = c.IdProveedor,
            FechaAsignacion = c.FechaAsignacion, Observaciones = c.Observaciones,
            Estado = c.Cerrado ? "Cerrado" : "Abierto",
            TaraExtraLegado = c.TaraExtraLegado,
        };

        private static ProductoCamion MapProducto(MovProductoDto p, string proveedor)
        {
            var pc = new ProductoCamion
            {
                Id = p.Id, IdProducto = p.IdProducto, ProductoCodigo = p.Codigo, ProductoNombre = p.Nombre,
                TaraUnitaria = p.TaraUnitaria, PesoTeorico = p.PesoTeorico,
                PesoManifestado = p.PesoManifestado, BultosDeclarados = p.BultosDeclarados,
                Observaciones = p.Observaciones, Estado = p.Cerrado ? "Cerrado" : "Abierto", ProveedorNombre = proveedor,
            };
            // Único punto donde conviven la entrada y los datos del producto: acá se calcula
            // la estimación de bultos, que no se persiste.
            foreach (var e in p.Entradas)
                pc.Entradas.Add(new EntradaPesaje
                {
                    Id = e.Id, Bruto = e.Bruto, TaraInd = e.TaraInd, TaraExtra = e.TaraExtra,
                    TaraTotal = e.TaraTotal, Neto = e.Neto, Fecha = e.Fecha, Hora = e.Hora,
                    BultosCapturados = e.BultosCapturados,
                    BultosTeoricos = PesajeCalc.BultosTeoricos(
                        e.Bruto, e.TaraExtra, p.PesoTeorico, p.TaraUnitaria),
                    Observaciones = e.Observaciones, ProdId = p.Id, ProdNombre = p.Nombre,
                });
            pc.NotificarAgregados();
            return pc;
        }
    }
}
