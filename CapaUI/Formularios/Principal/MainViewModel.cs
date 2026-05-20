using System;
using System.Windows.Input;
using CapaUI.Core.MVVM;
using CapaUI.Core.Permisos;
using CapaDominio;

namespace CapaUI.Formularios.Principal
{
    public class MainViewModel : ViewModelBase
    {
        // ── Vista actual ─────────────────────────────────────────────────
        private object? _vistaActual;
        public object? VistaActual
        {
            get => _vistaActual;
            set => Set(ref _vistaActual, value);
        }

        // ── Info de usuario ──────────────────────────────────────────────
        public string NombreUsuario => ServicioPerfilUsuario.PerfilActual?.NombreCompleto
                                       ?? SesionActual.NombreUsuario;
        public string Iniciales     => ServicioPerfilUsuario.PerfilActual?.Iniciales ?? "??";
        public string NombreRol     => ServicioPerfilUsuario.PerfilActual?.NombreRol  ?? "";

        // ── Visibilidad de módulos ───────────────────────────────────────
        public bool VerPesajes      => SesionPermisos.TieneAlguno(Permiso.Pesajes_Ver,    Permiso.Pesajes_Crear,    Permiso.Pesajes_Modificar);
        public bool VerEmpleados    => SesionPermisos.TieneAlguno(Permiso.Empleados_Ver,  Permiso.Empleados_Crear,  Permiso.Empleados_Modificar);
        public bool VerUsuarios     => SesionPermisos.TieneAlguno(Permiso.Usuarios_Ver,   Permiso.Usuarios_Crear,   Permiso.Usuarios_Modificar);
        public bool VerProductos    => SesionPermisos.TieneAlguno(Permiso.Productos_Ver,  Permiso.Productos_Crear,  Permiso.Productos_Modificar);
        public bool VerProveedores  => SesionPermisos.TieneAlguno(Permiso.Proveedores_Ver,Permiso.Proveedores_Crear,Permiso.Proveedores_Modificar);
        public bool VerReportes     => SesionPermisos.Tiene(Permiso.Reportes_Ver);

        // ── Comandos de navegación ───────────────────────────────────────
        public ICommand NavPesajesCommand              { get; }
        public ICommand NavEmpleadosCommand            { get; }
        public ICommand NavUsuariosCommand             { get; }
        public ICommand NavProductosCommand            { get; }
        public ICommand NavProveedoresCommand          { get; }
        public ICommand NavRolesCommand                { get; }
        public ICommand NavBitacoraCommand             { get; }
        public ICommand NavFabricantesCommand          { get; }
        public ICommand NavCategoriasCommand           { get; }
        public ICommand NavContactosProveedoresCommand { get; }
        public ICommand NavContactosFabricantesCommand { get; }
        public ICommand NavReportesCommand             { get; }
        public ICommand NavDashboardCommand            { get; }
        public ICommand NavCrearReportesCommand        { get; }
        public ICommand NavMiUsuarioCommand            { get; }
        public ICommand NavBienvenidaCommand           { get; }
        public ICommand CerrarSesionCommand            { get; }

        public event EventHandler? SesionCerrada;

        public MainViewModel()
        {
            // Módulo Pesajes
            NavPesajesCommand = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Movimientos y Entradas", "Pesajes"));

            // Módulo Usuarios
            NavUsuariosCommand  = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Gestión de Usuarios",  "Usuarios"));
            NavEmpleadosCommand = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Gestión de Empleados", "Usuarios"));
            NavRolesCommand     = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Gestión de Roles",     "Usuarios"));
            NavBitacoraCommand  = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Bitácora",             "Usuarios"));

            // Módulo Productos
            NavProductosCommand            = new RelayCommand(() =>
                VistaActual = new ProductosVM());
            NavProveedoresCommand          = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Gestión de Proveedores", "Productos"));
            NavFabricantesCommand          = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Gestión de Fabricantes", "Productos"));
            NavCategoriasCommand           = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Gestión de Categorías",  "Productos"));
            NavContactosProveedoresCommand = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Contactos Proveedores",  "Productos"));
            NavContactosFabricantesCommand = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Contactos Fabricantes",  "Productos"));

            // Módulo Reportería
            NavReportesCommand      = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Reportería",    "Reportería"));
            NavDashboardCommand     = new RelayCommand(() =>
                VistaActual = new Dashboard.DashboardVM());
            NavCrearReportesCommand = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Crear Reportes","Reportería"));
            NavMiUsuarioCommand = new RelayCommand(() =>
                VistaActual = new ConstructionVM("Mi Usuario",  ""));
            NavBienvenidaCommand = new RelayCommand(() =>
                VistaActual = new WelcomeVM());

            CerrarSesionCommand = new RelayCommand(async () => await CerrarSesionAsync());

            // Vista inicial
            VistaActual = new WelcomeVM();
        }

        private async System.Threading.Tasks.Task CerrarSesionAsync()
        {
            try
            {
                var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                await client.Auth.SignOut();
            }
            catch { }
            finally
            {
                SesionPermisos.Limpiar();
                ServicioPerfilUsuario.Limpiar();
                SesionCerrada?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // ── VMs de pantalla ──────────────────────────────────────────────────
    public class WelcomeVM : ViewModelBase { }

    public class ConstructionVM : ViewModelBase
    {
        public string NombreModulo { get; }
        public string ModuloPadre  { get; }
        public ConstructionVM(string nombre, string padre = "")
        { NombreModulo = nombre; ModuloPadre = padre; }
    }

    public class ProductosVM : ViewModelBase { }

    /// <summary>Mantenido por compatibilidad.</summary>
    public class PlaceholderVM : ViewModelBase
    {
        public string NombreModulo { get; }
        public PlaceholderVM(string nombre) => NombreModulo = nombre;
    }
}
