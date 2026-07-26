using System;
using System.Collections.Generic;
using System.Windows.Media;
using CapaAplicacion.Conexion;
using CapaAplicacion.Search.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.MVVM;
using CapaUI.Core.Permisos;
using CapaUI.Navigation;
using CapaUI.Formularios.Principal.Pantallas.Usuarios;
using CapaUI.ViewModels.Search;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IUsuarioSesionService     _sesionService;
        private readonly UniversalSearchViewModel _searchVm;
        private readonly IConexionMonitor         _conexionMonitor;
        private readonly Dictionary<string, Func<object>> _routes;
        private bool _disposed;

        // ── Vista actual ─────────────────────────────────────────────────
        [ObservableProperty] private object? _vistaActual;

        // ── Estado de conexión (label del top bar) ───────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EstadoTexto))]
        [NotifyPropertyChangedFor(nameof(EstadoBrush))]
        private EstadoConexion _conectividad = EstadoConexion.Desconocido;

        public string EstadoTexto => Conectividad switch
        {
            EstadoConexion.Conectado   => "Conectado",
            EstadoConexion.Degradado   => "Sin internet",
            EstadoConexion.SinConexion => "Sin conexión",
            _                          => "Verificando…"
        };

        public Brush EstadoBrush => Conectividad switch
        {
            EstadoConexion.Conectado   => _brushVerde,
            EstadoConexion.Degradado   => _brushAmbar,
            EstadoConexion.SinConexion => _brushRojo,
            _                          => _brushGris
        };

        private static readonly Brush _brushVerde = CrearBrush("#10B981");
        private static readonly Brush _brushAmbar = CrearBrush("#F59E0B");
        private static readonly Brush _brushRojo  = CrearBrush("#EF4444");
        private static readonly Brush _brushGris  = CrearBrush("#9CA3AF");

        private static Brush CrearBrush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        // ── Info de usuario ──────────────────────────────────────────────
        public string NombreUsuario => _sesionService.SesionActual?.NombreCompleto
                                       ?? _sesionService.SesionActual?.Email ?? "";
        public string Iniciales     => _sesionService.SesionActual?.Iniciales ?? "??";
        public string NombreRol     => _sesionService.SesionActual?.NombreRol  ?? "";

        // ── Visibilidad de módulos ───────────────────────────────────────
        public bool VerPesajes     => SesionPermisos.TieneAlguno(Permiso.Pesajes_Ver,    Permiso.Pesajes_Crear,    Permiso.Pesajes_Modificar);
        public bool VerEmpleados   => SesionPermisos.TieneAlguno(Permiso.Empleados_Ver,  Permiso.Empleados_Crear,  Permiso.Empleados_Modificar);
        public bool VerUsuarios    => SesionPermisos.TieneAlguno(Permiso.Usuarios_Ver,   Permiso.Usuarios_Crear,   Permiso.Usuarios_Modificar);
        public bool VerProductos   => SesionPermisos.TieneAlguno(Permiso.Productos_Ver,  Permiso.Productos_Crear,  Permiso.Productos_Modificar);
        public bool VerProveedores => SesionPermisos.TieneAlguno(Permiso.Proveedores_Ver,Permiso.Proveedores_Crear,Permiso.Proveedores_Modificar);
        public bool VerReportes    => SesionPermisos.Tiene(Permiso.Reportes_Ver);

        // ── Eventos ──────────────────────────────────────────────────────
        public event EventHandler? CierreRequerido;

        public MainViewModel(IUsuarioSesionService sesionService,
                             UniversalSearchViewModel searchVm,
                             IConexionMonitor conexionMonitor)
        {
            _sesionService = sesionService;
            _searchVm      = searchVm;
            _searchVm.ResultSelected += OnResultadoBusquedaSeleccionado;

            _conexionMonitor = conexionMonitor;
            _conectividad    = _conexionMonitor.Estado;   // estado actual al construir
            _conexionMonitor.EstadoCambiado += OnEstadoConexionCambiado;

            _routes = new Dictionary<string, Func<object>>
            {
                // Usuarios
                [Routes.Usuarios]  = () => new UsuariosScreenVM(),
                [Routes.Empleados] = () => new EmpleadosVM(),
                [Routes.Roles]     = () => new ConstructionVM("Gestión de Roles",     "Usuarios"),
                [Routes.Bitacora]  = () => new BitacoraVM(),
                // Productos
                [Routes.Productos]            = () => new ProductosVM(),
                [Routes.Proveedores]          = () => new ProveedoresVM(),
                [Routes.Fabricantes]          = () => new FabricantesVM(),
                [Routes.Categorias]           = () => new CategoriasVM(),
                [Routes.ContactosProveedores] = () => new ContactosProveedoresVM(),
                [Routes.ContactosFabricantes] = () => new ContactosFabricantesVM(),
                // Pesajes
                [Routes.Pesajes]       = () => new PesajesVM(),
                // Reportería
                [Routes.Dashboard]     = () => new Dashboard.DashboardVM(),
                [Routes.CrearReportes] = () => new ConstructionVM("Crear Reportes", "Reportería"),
                // Especiales
                [Routes.Bienvenida] = () => new WelcomeVM(),
                [Routes.MiUsuario]  = () => new ConstructionVM("Mi Usuario", ""),
            };

            VistaActual = new WelcomeVM();
        }

        /// <summary>
        /// Dispone el ViewModel anterior al cambiar de vista,
        /// permitiendo que los VMs liberen suscripciones Realtime.
        /// Nota: en OnChanging, _vistaActual todavía tiene el valor VIEJO.
        /// El parámetro value es el valor NUEVO que se va a asignar.
        /// </summary>
        partial void OnVistaActualChanging(object? value)
        {
            (_vistaActual as IDisposable)?.Dispose();
        }

        // ── Navegación ───────────────────────────────────────────────────
        [RelayCommand]
        private void Navigate(string? routeId)
        {
            if (string.IsNullOrEmpty(routeId)) return;
            if (!_routes.TryGetValue(routeId, out var factory)) return;
            VistaActual = factory();
        }

        // ── Estado de conexión (label del top bar) ───────────────────────
        // El evento del monitor llega ya en el UI thread (SynchronizationContext).
        // La recarga de datos al reconectar la maneja cada VM (RealtimeAwareViewModel).
        private void OnEstadoConexionCambiado(object? sender, EstadoConexion nuevo)
            => Conectividad = nuevo;

        // ── Búsqueda ─────────────────────────────────────────────────────
        [RelayCommand]
        private void Buscar(string? term)
        {
            VistaActual = _searchVm;
            if (!string.IsNullOrWhiteSpace(term))
                _searchVm.TriggerSearch(term);
        }

        // ── Resultado del buscador universal seleccionado ────────────────
        private void OnResultadoBusquedaSeleccionado(SearchResultDto result)
        {
            var routeId = result.EntityType switch
            {
                "Producto" => Routes.Productos,
                "Empleado" => Routes.Empleados,
                _          => null
            };
            if (routeId is not null)
                Navigate(routeId);
        }

        // ── Cierre de sesión ─────────────────────────────────────────────
        [RelayCommand]
        private void CerrarSesion() => CierreRequerido?.Invoke(this, EventArgs.Empty);

        // ── IDisposable ─────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Desuscribir del monitor de conexión (singleton — no debe retener este VM)
            _conexionMonitor.EstadoCambiado -= OnEstadoConexionCambiado;

            // Disponer la vista actual (si es IDisposable)
            (VistaActual as IDisposable)?.Dispose();

            // Desuscribir del SearchVM y disponerlo
            _searchVm.ResultSelected -= OnResultadoBusquedaSeleccionado;
            (_searchVm as IDisposable)?.Dispose();
        }
    }

    // ── VMs marcadores (DataTemplate triggers) ───────────────────────────
    public class WelcomeVM                : ViewModelBase { }
    public class ProductosVM              : ViewModelBase { }
    public class ProveedoresVM            : ViewModelBase { }
    public class FabricantesVM            : ViewModelBase { }
    public class CategoriasVM             : ViewModelBase { }
    public class ContactosFabricantesVM   : ViewModelBase { }
    public class ContactosProveedoresVM   : ViewModelBase { }
    public class PesajesVM                 : ViewModelBase { }
    public class UsuariosScreenVM          : ViewModelBase { }
    public class EmpleadosVM               : ViewModelBase { }
    public class BitacoraVM                : ViewModelBase { }

    public class ConstructionVM : ViewModelBase
    {
        public string NombreModulo { get; }
        public string ModuloPadre  { get; }
        public ConstructionVM(string nombre, string padre = "")
        { NombreModulo = nombre; ModuloPadre = padre; }
    }

    /// <summary>Mantenido por compatibilidad con código existente.</summary>
    public class PlaceholderVM : ViewModelBase
    {
        public string NombreModulo { get; }
        public PlaceholderVM(string nombre) => NombreModulo = nombre;
    }
}
