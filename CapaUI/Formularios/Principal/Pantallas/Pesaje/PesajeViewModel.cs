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
        public const int MaxCamiones = 3;

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

        public bool HayCamion     => SelectedCamion is not null;
        public bool HayProducto   => SelectedProducto is not null;
        public bool CamionCerrado => SelectedCamion?.Estado == "Cerrado";
        public string ModoEfectivo => SelectedProducto is not null ? VistaEntradas : "camion";

        public int    CamionesCount      => Camiones.Count;
        public int    CamionesActivos    => Camiones.Count(c => c.Estado == "Abierto");
        public string CamionesTexto      => $"{Camiones.Count}/{MaxCamiones}";
        public bool   PuedeAgregarCamion => Camiones.Count(c => c.Estado == "Abierto") < MaxCamiones;

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
        /// Persiste el proceso de descarga completo (camión + productos) en una sola
        /// operación, tanto para alta (wizard) como para edición (megamodal).
        /// <para/>
        /// Orden: primero el camión (para tener su id), después los productos quitados,
        /// después los nuevos, y al final las actualizaciones de los ya existentes.
        /// Si falla el camión se corta: sin id no hay dónde colgar los productos.
        /// <para/>
        /// La tara extra NO se toca acá: se pesa por entrada, no por camión.
        /// </summary>
        public async Task<bool> GuardarProcesoAsync(
            CamionPesaje? camionExistente,
            string placa, string proveedor, int? idProveedor, string obs,
            IReadOnlyList<(int IdMovProducto, int IdProducto, double PesoManifestado, int BultosDeclarados)> productos,
            IReadOnlyList<int> idsQuitados)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return false; }
            if (!HaySesionActiva("guardar el proceso de descarga")) return false;

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

            // Quitados: se anulan por estado, nunca se borran (no se pierde auditoría).
            foreach (var idMovProd in idsQuitados)
            {
                var rDel = await _repo.AnularProductoAsync(idMovProd);
                if (!rDel.Success) Toast?.Invoke(rDel.Error ?? "No se pudo quitar un producto");
            }

            foreach (var p in productos)
            {
                if (p.IdMovProducto == 0)
                {
                    var rAdd = await _repo.AgregarProductoAsync(
                        idCamion, p.IdProducto, p.PesoManifestado, p.BultosDeclarados, "");
                    if (!rAdd.Success) Toast?.Invoke(rAdd.Error ?? $"No se pudo agregar un producto");
                }
                else
                {
                    var rUpd = await _repo.ActualizarProductoAsync(
                        p.IdMovProducto, p.PesoManifestado, p.BultosDeclarados, "");
                    if (!rUpd.Success) Toast?.Invoke(rUpd.Error ?? "No se pudo actualizar un producto");
                }
            }

            await RecargarCamionesAsync(seleccionarId: idCamion);
            Toast?.Invoke(camionExistente is null ? "Proceso de descarga iniciado" : "Cambios guardados");
            return true;
        }

        public async Task QuitarCamionAsync()
        {
            if (SelectedCamion is null) return;
            var r = await _repo.AnularCamionAsync(SelectedCamion.Id);
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
        public async Task AgregarProductoAsync(int idProducto, double pesoManifestado, int bultosTeoricos, string obs)
        {
            if (SelectedCamion is null) return;
            var camion = SelectedCamion;
            var r = await _repo.AgregarProductoAsync(camion.Id, idProducto, pesoManifestado, bultosTeoricos, obs);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo agregar"); return; }

            await CargarProductosAsync(camion);
            SelectedProducto = camion.Productos.FirstOrDefault(p => p.Id == r.Value);
            RecalcularFilas();
            Toast?.Invoke("Producto agregado al camión");
        }

        public async Task ActualizarProductoAsync(ProductoCamion p, double pesoManifestado, int bultosTeoricos, string obs)
        {
            if (SelectedCamion is null) return;
            var r = await _repo.ActualizarProductoAsync(p.Id, pesoManifestado, bultosTeoricos, obs);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo actualizar"); return; }

            int id = p.Id;
            await CargarProductosAsync(SelectedCamion);
            SelectedProducto = SelectedCamion.Productos.FirstOrDefault(x => x.Id == id);
            RecalcularFilas();
            Toast?.Invoke("Producto actualizado");
        }

        public async Task QuitarProductoAsync()
        {
            if (SelectedCamion is null || SelectedProducto is null) return;
            var camion = SelectedCamion;
            var r = await _repo.AnularProductoAsync(SelectedProducto.Id);
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
        public async Task GuardarEntradaAsync(ProductoCamion producto, EntradaPesaje snapshot, EntradaPesaje? editando)
        {
            if (SelectedCamion is null) return;
            if (!HaySesionActiva("guardar pesaje")) return;

            if (editando is not null)
            {
                var ra = await _repo.AnularEntradaAsync(editando.Id);
                if (!ra.Success) { Toast?.Invoke(ra.Error ?? "No se pudo editar"); return; }
            }

            // La tara extra es un peso real que se captura en el modal (las tarimas que
            // vinieron con esta pesada). Ya no se prorratea nada: si no se pesó acá, se
            // carga después el total y el sistema lo reparte entre las pesadas.
            var r = await _repo.CrearEntradaAsync(
                producto.Id, producto.IdProducto, snapshot.Bruto, snapshot.TaraExtra,
                snapshot.Observaciones, UsuarioActual);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo guardar el pesaje"); return; }

            int id = producto.Id;
            await CargarProductosAsync(SelectedCamion);
            SelectedProducto = SelectedCamion.Productos.FirstOrDefault(x => x.Id == id);
            RecalcularFilas();
            Toast?.Invoke("Pesaje guardado");
        }

        public async Task QuitarEntradaAsync(EntradaPesaje entrada)
        {
            if (SelectedCamion is null) return;
            var r = await _repo.AnularEntradaAsync(entrada.Id);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo quitar"); return; }

            int? id = SelectedProducto?.Id;
            await CargarProductosAsync(SelectedCamion);
            SelectedProducto = id.HasValue ? SelectedCamion.Productos.FirstOrDefault(x => x.Id == id.Value) : null;
            SelectedEntrada  = null;
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
            if (SelectedCamion is null) { OnPropertyChanged(nameof(ModoEfectivo)); return; }

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
