using System;
using System.Collections.Generic;
using CapaAplicacion.Perfil;
using CapaAplicacion.Search.Dtos;
using CapaUI.Core.MVVM;
using CapaUI.Core.Permisos;
using CapaUI.Navigation;
using CapaUI.ViewModels.Search;
using CapaDominio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IPerfilUsuarioService    _perfilService;
        private readonly UniversalSearchViewModel _searchVm;
        private readonly Dictionary<string, Func<object>> _routes;
        private bool _disposed;

        // ── Vista actual ─────────────────────────────────────────────────
        [ObservableProperty] private object? _vistaActual;

        // ── Info de usuario ──────────────────────────────────────────────
        public string NombreUsuario => _perfilService.PerfilActual?.NombreCompleto
                                       ?? servicioSesionActual.NombreUsuario;
        public string Iniciales     => _perfilService.PerfilActual?.Iniciales ?? "??";
        public string NombreRol     => _perfilService.PerfilActual?.NombreRol  ?? "";

        // ── Visibilidad de módulos ───────────────────────────────────────
        public bool VerPesajes     => SesionPermisos.TieneAlguno(Permiso.Pesajes_Ver,    Permiso.Pesajes_Crear,    Permiso.Pesajes_Modificar);
        public bool VerEmpleados   => SesionPermisos.TieneAlguno(Permiso.Empleados_Ver,  Permiso.Empleados_Crear,  Permiso.Empleados_Modificar);
        public bool VerUsuarios    => SesionPermisos.TieneAlguno(Permiso.Usuarios_Ver,   Permiso.Usuarios_Crear,   Permiso.Usuarios_Modificar);
        public bool VerProductos   => SesionPermisos.TieneAlguno(Permiso.Productos_Ver,  Permiso.Productos_Crear,  Permiso.Productos_Modificar);
        public bool VerProveedores => SesionPermisos.TieneAlguno(Permiso.Proveedores_Ver,Permiso.Proveedores_Crear,Permiso.Proveedores_Modificar);
        public bool VerReportes    => SesionPermisos.Tiene(Permiso.Reportes_Ver);

        // ── Eventos ──────────────────────────────────────────────────────
        public event EventHandler? CierreRequerido;

        public MainViewModel(IPerfilUsuarioService perfilService,
                             UniversalSearchViewModel searchVm)
        {
            _perfilService = perfilService;
            _searchVm      = searchVm;
            _searchVm.ResultSelected += OnResultadoBusquedaSeleccionado;

            _routes = new Dictionary<string, Func<object>>
            {
                // Usuarios
                [Routes.Usuarios]  = () => new ConstructionVM("Gestión de Usuarios",  "Usuarios"),
                [Routes.Empleados] = () => new ConstructionVM("Gestión de Empleados", "Usuarios"),
                [Routes.Roles]     = () => new ConstructionVM("Gestión de Roles",     "Usuarios"),
                [Routes.Bitacora]  = () => new ConstructionVM("Bitácora",             "Usuarios"),
                // Productos
                [Routes.Productos]            = () => new ProductosVM(),
                [Routes.Proveedores]          = () => new ProveedoresVM(),
                [Routes.Fabricantes]          = () => new FabricantesVM(),
                [Routes.Categorias]           = () => new CategoriasVM(),
                [Routes.ContactosProveedores] = () => new ConstructionVM("Contactos Proveedores",  "Productos"),
                [Routes.ContactosFabricantes] = () => new ConstructionVM("Contactos Fabricantes",  "Productos"),
                // Pesajes
                [Routes.Pesajes]       = () => new ConstructionVM("Movimientos y Entradas", "Pesajes"),
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

            // Disponer la vista actual (si es IDisposable)
            (VistaActual as IDisposable)?.Dispose();

            // Desuscribir del SearchVM y disponerlo
            _searchVm.ResultSelected -= OnResultadoBusquedaSeleccionado;
            (_searchVm as IDisposable)?.Dispose();
        }
    }

    // ── VMs marcadores (DataTemplate triggers) ───────────────────────────
    public class WelcomeVM     : ViewModelBase { }
    public class ProductosVM   : ViewModelBase { }
    public class ProveedoresVM : ViewModelBase { }
    public class FabricantesVM : ViewModelBase { }
    public class CategoriasVM  : ViewModelBase { }

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
