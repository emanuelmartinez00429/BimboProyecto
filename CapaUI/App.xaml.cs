using System.IO;
using System.Windows;
using CapaAplicacion;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos;
using CapaUI.Core.Permisos;
using CapaUI.Formularios.InicioSesion;
using CapaUI.Formularios.Principal;
using CapaUI.Formularios.Principal.Pantallas.Bitacora;
using CapaUI.Formularios.Principal.Pantallas.Categorias;
using CapaUI.Formularios.Principal.Pantallas.ContactosFabricantes;
using CapaUI.Formularios.Principal.Pantallas.ContactosProveedores;
using CapaUI.Formularios.Principal.Pantallas.Empleados;
using CapaUI.Formularios.Principal.Pantallas.Fabricantes;
using CapaUI.Formularios.Principal.Pantallas.Pesaje;
using CapaUI.Formularios.Principal.Pantallas.Productos;
using CapaUI.Formularios.Principal.Pantallas.Proveedores;
using CapaUI.Formularios.Principal.Pantallas.Roles;
using CapaUI.Formularios.Principal.Pantallas.Usuarios;
using CapaUI.Services.Picker;
using CapaUI.ViewModels.Search;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CapaUI
{
    public partial class App : Application
    {
        // C13: lazy-init para que App.Services funcione incluso cuando
        // BimboPesaje (WinForms host) es el ejecutable de entrada y
        // CapaUI.App.OnStartup nunca se invoca.
        private static IServiceProvider? _services;
        public static IServiceProvider Services => _services ??= ConfigureServices();

        private static LoginWindow? _loginActual;
        private static MainWindow?  _mainActual;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BimboPesaje", "Logs");
            Directory.CreateDirectory(logFolder);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.File(
                    Path.Combine(logFolder, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _ = Services; // fuerza inicialización en el hilo UI (el getter ya crea el provider)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MostrarLogin();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddDataLayer();
            services.AddApplicationLayer();
            services.AddSingleton<IPickerService, PickerService>();
            services.AddTransient<UniversalSearchViewModel>();
            services.AddTransient<ProductosViewModel>();
            services.AddTransient<PesajeViewModel>();
            services.AddTransient<ProveedoresViewModel>();
            services.AddTransient<FabricantesViewModel>();
            services.AddTransient<CategoriasViewModel>();
            services.AddTransient<ContactosFabricantesViewModel>();
            services.AddTransient<ContactosProveedoresViewModel>();
            services.AddTransient<UsuariosViewModel>();
            services.AddTransient<EmpleadosViewModel>();
            services.AddTransient<BitacoraViewModel>();
            services.AddTransient<RolesViewModel>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();

            var provider = services.BuildServiceProvider();

            // Conectar SesionPermisos (estático) con la fuente de verdad (IUsuarioSesionService)
            SesionPermisos.Configurar(provider.GetRequiredService<IUsuarioSesionService>());

            return provider;
        }

        public static void MostrarLogin()
        {
            _loginActual = Services.GetRequiredService<LoginWindow>();
            _loginActual.LoginExitoso += OnLoginExitoso;
            _loginActual.Show();
        }

        private static void OnLoginExitoso(object? s, EventArgs e)
        {
            _loginActual!.LoginExitoso -= OnLoginExitoso;
            _loginActual.Close();
            _loginActual = null;
            MostrarPrincipal();
        }

        private static void MostrarPrincipal()
        {
            _mainActual = Services.GetRequiredService<MainWindow>();
            _mainActual.SesionCerrada += OnSesionCerrada;
            _mainActual.Show();
        }

        private static void OnSesionCerrada(object? s, EventArgs e)
        {
            _mainActual!.SesionCerrada -= OnSesionCerrada;
            _mainActual = null;
            // La limpieza de sesión ya se hizo en MainWindow.LimpiarRecursosAsync() via IUsuarioSesionService
            MostrarLogin();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
