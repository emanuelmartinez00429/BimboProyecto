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
        [ObservableProperty] private bool _isLoading;

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
        /// </summary>
        public bool MostrarEstadoVacio => CamionesActivos == 0;

        /// <summary>Hay camiones ya cerrados aunque ninguno esté descargándose.</summary>
        public bool HayCerrados => Camiones.Any(c => c.Estado == "Cerrado");

        public int CerradosCount => Camiones.Count(c => c.Estado == "Cerrado");

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

            // El prorrateo de la tara extra depende del total de bultos declarados,
            // que recién se conoce con los productos ya cargados.
            camion.NotificarTaraExtra();
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
        public async Task<int?> RegistrarCamionAsync(string placa, string proveedor, int? idProveedor, string obs, double taraExtraTotal = 0)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return null; }
            if (!HaySesionActiva("registrar camión")) return null;
            var r = await _repo.CrearCamionAsync(idProveedor.Value, placa, obs, taraExtraTotal, UsuarioActual);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo registrar"); return null; }

            await RecargarCamionesAsync(seleccionarId: r.Value);
            Toast?.Invoke("Camión registrado");
            return r.Value;
        }

        public async Task ActualizarCamionAsync(CamionPesaje c, string placa, string proveedor, int? idProveedor, string obs, double taraExtraTotal)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return; }
            var r = await _repo.ActualizarCamionAsync(c.Id, idProveedor.Value, placa, obs, taraExtraTotal);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo actualizar"); return; }

            c.Placa = placa; c.Proveedor = proveedor; c.IdProveedor = idProveedor; c.Observaciones = obs;
            c.TaraExtraTotal = taraExtraTotal;
            foreach (var p in c.Productos) p.ProveedorNombre = proveedor;
            c.NotificarTaraExtra();
            OnPropertyChanged(nameof(SelectedCamion));
            RecalcularFilas();
            Toast?.Invoke("Camión actualizado");
        }

        /// <summary>
        /// Persiste el proceso de descarga completo (camión + productos + tara extra)
        /// en una sola operación, tanto para alta (wizard) como para edición (megamodal).
        /// <para/>
        /// Orden: primero el camión (para tener su id), después los productos quitados,
        /// después los nuevos, y al final las actualizaciones de los ya existentes.
        /// Si falla el camión se corta: sin id no hay dónde colgar los productos.
        /// </summary>
        public async Task<bool> GuardarProcesoAsync(
            CamionPesaje? camionExistente,
            string placa, string proveedor, int? idProveedor, string obs, double taraExtraTotal,
            IReadOnlyList<(int IdMovProducto, int IdProducto, double PesoManifestado, int BultosDeclarados)> productos,
            IReadOnlyList<int> idsQuitados)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return false; }
            if (!HaySesionActiva("guardar el proceso de descarga")) return false;

            int idCamion;

            if (camionExistente is null)
            {
                var rNuevo = await _repo.CrearCamionAsync(idProveedor.Value, placa, obs, taraExtraTotal, UsuarioActual);
                if (!rNuevo.Success) { Toast?.Invoke(rNuevo.Error ?? "No se pudo registrar el camión"); return false; }
                idCamion = rNuevo.Value;
            }
            else
            {
                var rEdit = await _repo.ActualizarCamionAsync(camionExistente.Id, idProveedor.Value, placa, obs, taraExtraTotal);
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

            // La tara extra se pesa UNA sola vez para todo el camión: a esta pesada le
            // toca la parte proporcional a sus bultos. Se calcula acá (fuente única de
            // verdad), no se confía en lo que traiga el snapshot del modal.
            double taraExtraProrrateada = SelectedCamion.TaraExtraPorBulto * snapshot.Bultos;

            var r = await _repo.CrearEntradaAsync(
                producto.Id, producto.IdProducto, snapshot.Bruto, taraExtraProrrateada,
                snapshot.Bultos, snapshot.Observaciones, UsuarioActual);
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
            OnPropertyChanged(nameof(HayCerrados));
            OnPropertyChanged(nameof(CerradosCount));
        }

        private static CamionPesaje MapCamion(CamionDto c) => new()
        {
            Id = c.Id, Placa = c.Placa, Proveedor = c.Proveedor, IdProveedor = c.IdProveedor,
            FechaAsignacion = c.FechaAsignacion, Observaciones = c.Observaciones,
            Estado = c.Cerrado ? "Cerrado" : "Abierto",
            TaraExtraTotal = c.TaraExtraTotal,
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
            foreach (var e in p.Entradas)
                pc.Entradas.Add(new EntradaPesaje
                {
                    Id = e.Id, Bruto = e.Bruto, TaraInd = e.TaraInd, TaraExtra = e.TaraExtra,
                    TaraTotal = e.TaraTotal, Neto = e.Neto, Bultos = e.Bultos, Fecha = e.Fecha, Hora = e.Hora,
                    Observaciones = e.Observaciones, ProdId = p.Id, ProdNombre = p.Nombre,
                });
            pc.NotificarAgregados();
            return pc;
        }
    }
}
