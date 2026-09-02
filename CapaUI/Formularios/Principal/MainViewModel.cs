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
using CapaUI.Formularios.Principal.Pantallas.Notificaciones;
using CapaUI.ViewModels.Search;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal
{
    public partial class MainViewModel : ObservableObject, IDisposable, CapaAplicacion.Common.Interfaces.INavegacionService
    {
        private readonly IUsuarioSesionService     _sesionService;
        private readonly UniversalSearchViewModel _searchVm;
        private string? _rutaActual;
        private readonly IConexionMonitor         _conexionMonitor;
        private readonly Dictionary<string, Func<object>> _routes;
        private readonly Dictionary<string, Permiso[]> _routePermissions;
        private bool _disposed;
        public NotificacionesViewModel Notificaciones { get; }

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
        public bool VerPesajes     => SesionPermisos.TieneAlguno(Permiso.ConsultarPesaje, Permiso.RegistrarEntrada, Permiso.ModificarPesaje, Permiso.CompletarPesaje, Permiso.CancelarPesaje);
        public bool VerEmpleados   => SesionPermisos.TieneAlguno(Permiso.ConsultarEmpleado, Permiso.CrearEmpleado, Permiso.ModificarEmpleado, Permiso.EliminarEmpleado);
        public bool VerUsuarios    => SesionPermisos.TieneAlguno(
            Permiso.ConsultarUsuario, Permiso.CrearUsuario, Permiso.ModificarUsuario, Permiso.EliminarUsuario,
            Permiso.ConsultarRol, Permiso.CrearRol, Permiso.ModificarRol, Permiso.CambiarEstadoRol,
            Permiso.AsignarPermisosRol, Permiso.AsignarRolUsuario);
        public bool VerProductos   => SesionPermisos.TieneAlguno(Permiso.ConsultarProducto, Permiso.CrearProducto, Permiso.ModificarProducto, Permiso.EliminarProducto);
        public bool VerProveedores => SesionPermisos.TieneAlguno(Permiso.ConsultarProveedor, Permiso.CrearProveedor, Permiso.ModificarProveedor, Permiso.EliminarProveedor);
        public bool VerReportes    => SesionPermisos.Tiene(Permiso.ConsultarReporte);
        public bool VerNotificaciones => SesionPermisos.Tiene(Permiso.ConsultarNotificaciones);

        // ── Eventos ──────────────────────────────────────────────────────
        public event EventHandler? CierreRequerido;

        public MainViewModel(IUsuarioSesionService sesionService,
                             UniversalSearchViewModel searchVm,
                             IConexionMonitor conexionMonitor,
                             NotificacionesViewModel notificaciones)
        {
            _sesionService = sesionService;
            _searchVm      = searchVm;
            Notificaciones = notificaciones;
            Notificaciones.NavegacionService = this;
            _searchVm.ResultSelected += OnResultadoBusquedaSeleccionado;

            _conexionMonitor = conexionMonitor;
            _conectividad    = _conexionMonitor.Estado;   // estado actual al construir
            _conexionMonitor.EstadoCambiado += OnEstadoConexionCambiado;

            _routes = new Dictionary<string, Func<object>>
            {
                // Usuarios
                [Routes.Usuarios]  = () => new UsuariosScreenVM(),
                [Routes.Empleados] = () => new EmpleadosVM(),
                [Routes.Roles]     = () => new RolesVM(),
                [Routes.Bitacora]  = () => new BitacoraVM(),
                [Routes.Notificaciones] = () => Notificaciones,
                // Productos
                [Routes.Productos]            = () => new ProductosVM(),
                [Routes.Proveedores]          = () => new ProveedoresVM(),
                [Routes.Fabricantes]          = () => new FabricantesVM(),
                [Routes.Categorias]           = () => new CategoriasVM(),
                [Routes.Presentaciones]       = () => new PresentacionesVM(),
                [Routes.ContactosProveedores] = () => new ContactosProveedoresVM(),
                [Routes.ContactosFabricantes] = () => new ContactosFabricantesVM(),
                // Pesajes
                [Routes.Pesajes]       = () => new PesajesVM(),
                // Reportería
                [Routes.Dashboard]     = () => new Dashboard.DashboardVM(),
                [Routes.CrearReportes] = () => new ReporteriaVM(),
                // Especiales
                [Routes.Bienvenida] = () => new WelcomeVM(),
                [Routes.MiUsuario]  = () => new ConstructionVM("Mi Usuario", ""),
            };

            _routePermissions = new Dictionary<string, Permiso[]>
            {
                [Routes.Usuarios] = [Permiso.ConsultarUsuario, Permiso.CrearUsuario, Permiso.ModificarUsuario, Permiso.EliminarUsuario, Permiso.AsignarRolUsuario],
                [Routes.Empleados] = [Permiso.ConsultarEmpleado, Permiso.CrearEmpleado, Permiso.ModificarEmpleado, Permiso.EliminarEmpleado],
                [Routes.Roles] = [Permiso.ConsultarRol],
                [Routes.Bitacora] = [Permiso.ConsultarUsuario],
                [Routes.Notificaciones] = [Permiso.ConsultarNotificaciones],
                [Routes.Productos] = [Permiso.ConsultarProducto, Permiso.CrearProducto, Permiso.ModificarProducto, Permiso.EliminarProducto],
                [Routes.Proveedores] = [Permiso.ConsultarProveedor, Permiso.CrearProveedor, Permiso.ModificarProveedor, Permiso.EliminarProveedor],
                [Routes.Fabricantes] = [Permiso.ConsultarFabricante, Permiso.CrearFabricante, Permiso.ModificarFabricante],
                [Routes.Categorias] = [Permiso.ConsultarCategoria, Permiso.CrearCategoria, Permiso.ModificarCategoria, Permiso.DesactivarCategoria, Permiso.ActivarCategoria],
                [Routes.Presentaciones] = [Permiso.ModificarConfiguracion],
                [Routes.ContactosProveedores] = [Permiso.ConsultarProveedor, Permiso.CrearProveedor, Permiso.ModificarProveedor, Permiso.EliminarProveedor],
                [Routes.ContactosFabricantes] = [Permiso.ConsultarFabricante, Permiso.CrearFabricante, Permiso.ModificarFabricante],
                [Routes.Pesajes] = [Permiso.ConsultarPesaje, Permiso.RegistrarEntrada, Permiso.ModificarPesaje, Permiso.CompletarPesaje, Permiso.CancelarPesaje],
                [Routes.Dashboard] = [Permiso.ConsultarReporte],
                [Routes.CrearReportes] = [Permiso.ConsultarReporte],
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
            if (_vistaActual is not NotificacionesViewModel)
                (_vistaActual as IDisposable)?.Dispose();
        }

        // ── Navegación ───────────────────────────────────────────────────
        [RelayCommand]
        public void Navigate(string routeId) => TryNavigate(routeId, out _);

        public bool TryNavigate(string routeId, out string? motivo)
        {
            motivo = null;
            if (string.IsNullOrEmpty(routeId) || !_routes.TryGetValue(routeId, out var factory))
            {
                motivo = "El registro relacionado ya no tiene un módulo disponible.";
                return false;
            }
            if (_routePermissions.TryGetValue(routeId, out var requeridos) &&
                !SesionPermisos.TieneAlguno(requeridos))
            {
                Serilog.Log.Warning(
                    "Navegación denegada a {Ruta} para el rol {Rol}",
                    routeId,
                    _sesionService.SesionActual?.IdRol);
                motivo = "No tienes permiso para abrir el módulo relacionado.";
                return false;
            }

            // Ya estamos en esa pantalla: no hay nada que hacer.
            // Sin esta guarda, volver a tocar el mismo ítem del sidebar destruye
            // la vista y la reconstruye entera — nuevo ViewModel y nueva consulta
            // a la base de datos — porque los VM de ruta son clases y su igualdad
            // es por referencia, así que el setter siempre detecta un cambio.
            if (_rutaActual == routeId) return true;

            _rutaActual = routeId;
            VistaActual = factory();
            return true;
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
            _rutaActual = null;
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
            Notificaciones.Dispose();
        }
    }

    // ── VMs marcadores (DataTemplate triggers) ───────────────────────────
    public class WelcomeVM                : ViewModelBase { }
    public class ProductosVM              : ViewModelBase { }
    public class ProveedoresVM            : ViewModelBase { }
    public class FabricantesVM            : ViewModelBase { }
    public class CategoriasVM             : ViewModelBase { }
    public class PresentacionesVM         : ViewModelBase { }
    public class ContactosFabricantesVM   : ViewModelBase { }
    public class ContactosProveedoresVM   : ViewModelBase { }
    public class PesajesVM                 : ViewModelBase { }
    public class UsuariosScreenVM          : ViewModelBase { }
    public class EmpleadosVM               : ViewModelBase { }
    public class BitacoraVM                : ViewModelBase { }
    public class RolesVM                   : ViewModelBase { }
    public class ReporteriaVM              : ViewModelBase { }

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
