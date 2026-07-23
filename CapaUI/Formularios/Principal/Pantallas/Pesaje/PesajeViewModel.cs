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

        public event Action<string>? Toast;

        public PesajeViewModel(IPesajeRepository repo, IUsuarioSesionService sesionService)
        {
            _repo = repo;
            _sesionService = sesionService;
        }

        private int UsuarioActual => _sesionService.SesionActual?.IdUsuario ?? 0;

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
        public async Task RegistrarCamionAsync(string placa, string proveedor, int? idProveedor, string obs)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return; }
            var r = await _repo.CrearCamionAsync(idProveedor.Value, placa, obs, UsuarioActual);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo registrar"); return; }

            await RecargarCamionesAsync(seleccionarId: r.Value);
            Toast?.Invoke("Camión registrado");
        }

        public async Task ActualizarCamionAsync(CamionPesaje c, string placa, string proveedor, int? idProveedor, string obs)
        {
            if (idProveedor is null) { Toast?.Invoke("Selecciona un proveedor válido"); return; }
            var r = await _repo.ActualizarCamionAsync(c.Id, idProveedor.Value, placa, obs);
            if (!r.Success) { Toast?.Invoke(r.Error ?? "No se pudo actualizar"); return; }

            c.Placa = placa; c.Proveedor = proveedor; c.IdProveedor = idProveedor; c.Observaciones = obs;
            foreach (var p in c.Productos) p.ProveedorNombre = proveedor;
            OnPropertyChanged(nameof(SelectedCamion));
            RecalcularFilas();
            Toast?.Invoke("Camión actualizado");
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

            if (editando is not null)
            {
                var ra = await _repo.AnularEntradaAsync(editando.Id);
                if (!ra.Success) { Toast?.Invoke(ra.Error ?? "No se pudo editar"); return; }
            }

            var r = await _repo.CrearEntradaAsync(
                producto.Id, producto.IdProducto, snapshot.Bruto, snapshot.TaraExtra,
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
        }

        private static CamionPesaje MapCamion(CamionDto c) => new()
        {
            Id = c.Id, Placa = c.Placa, Proveedor = c.Proveedor, IdProveedor = c.IdProveedor,
            FechaAsignacion = c.FechaAsignacion, Observaciones = c.Observaciones,
            Estado = c.Cerrado ? "Cerrado" : "Abierto",
        };

        private static ProductoCamion MapProducto(MovProductoDto p, string proveedor)
        {
            var pc = new ProductoCamion
            {
                Id = p.Id, IdProducto = p.IdProducto, ProductoCodigo = p.Codigo, ProductoNombre = p.Nombre,
                TaraUnitaria = p.TaraUnitaria, PesoManifestado = p.PesoManifestado, BultosTeoricos = p.BultosTeoricos,
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
