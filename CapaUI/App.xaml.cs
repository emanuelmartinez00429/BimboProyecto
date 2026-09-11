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
using CapaUI.Formularios.Principal.Pantallas.Configuracion;
using CapaUI.Formularios.Principal.Pantallas.Empleados;
using CapaUI.Formularios.Principal.Pantallas.Fabricantes;
using CapaUI.Formularios.Principal.Pantallas.Pesaje;
using CapaUI.Formularios.Principal.Pantallas.Presentaciones;
using CapaUI.Formularios.Principal.Pantallas.Productos;
using CapaUI.Formularios.Principal.Pantallas.Proveedores;
using CapaUI.Formularios.Principal.Pantallas.Roles;
using CapaUI.Formularios.Principal.Pantallas.Reporteria;
using CapaUI.Formularios.Principal.Pantallas.Usuarios;
using CapaUI.Formularios.Principal.Pantallas.Notificaciones;
using CapaUI.Services.Picker;
using CapaUI.Services.Empresa;
using CapaUI.Core.Empresa;
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

        private static IServiceScope? _scopeSesion;

        public static T CrearVm<T>() where T : class =>
            ActivatorUtilities.CreateInstance<T>(_scopeSesion?.ServiceProvider ?? Services);

        public static T CrearVm<T>(params object[] parameters) where T : class =>
            ActivatorUtilities.CreateInstance<T>(_scopeSesion?.ServiceProvider ?? Services, parameters);

        private static LoginWindow? _loginActual;
        private static MainWindow?  _mainActual;

        /// <summary>
        /// Nivel mínimo del log, desde <c>LOG_LEVEL</c> (variable de entorno o App.config).
        /// Default <c>Warning</c>: en producción el archivo solo guarda lo que importa.
        /// <para/>
        /// Ponerlo en <c>Debug</c> habilita el cronometraje por llamada de
        /// <c>RepositorioBase</c> — es la forma de medir cuánto tarda realmente cada round
        /// trip a Supabase desde una red concreta, sin recompilar.
        /// </summary>
        private static Serilog.Events.LogEventLevel NivelDeLogConfigurado()
        {
            string? valor = Environment.GetEnvironmentVariable("LOG_LEVEL")
                ?? System.Configuration.ConfigurationManager.AppSettings["LOG_LEVEL"];

            return Enum.TryParse<Serilog.Events.LogEventLevel>(valor, ignoreCase: true, out var nivel)
                ? nivel
                : Serilog.Events.LogEventLevel.Warning;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BimboPesaje", "Logs");
            Directory.CreateDirectory(logFolder);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(NivelDeLogConfigurado())
                .WriteTo.File(
                    Path.Combine(logFolder, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Auditoría proactiva de aceleración de hardware DirectX y Render Capability Tiers
            CapaUI.Core.Diagnosticos.PipelineTelemetryService.AuditarCapacidadesHardware();

            _ = Services; // fuerza inicialización en el hilo UI (el getter ya crea el provider)

            DispatcherUnhandledException += (s, args) =>
            {
                if (args.Exception is OperationCanceledException or TaskCanceledException)
                {
                    args.Handled = true;
                    return;
                }
                Log.Fatal(args.Exception, "Excepción no controlada en el Dispatcher");
            };

            Services.GetRequiredService<EmpresaThemeService>().CargarCacheSinRed();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MostrarLogin();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddDataLayer();
            services.AddApplicationLayer();
            services.AddSingleton<IPickerService, PickerService>();
            services.AddSingleton<EmpresaThemeService>();
            services.AddSingleton<LogoEmpresaCache>();
            services.AddSingleton<IconoSidebarCache>();
            services.AddTransient<UniversalSearchViewModel>();
            services.AddTransient<ProductosViewModel>();
            services.AddTransient<PesajeViewModel>();
            services.AddTransient<ProveedoresViewModel>();
            services.AddTransient<FabricantesViewModel>();
            services.AddTransient<CategoriasViewModel>();
            services.AddTransient<PresentacionesViewModel>();
            services.AddTransient<ContactosFabricantesViewModel>();
            services.AddTransient<ContactosProveedoresViewModel>();
            services.AddTransient<UsuariosViewModel>();
            services.AddTransient<EmpleadosViewModel>();
            services.AddTransient<BitacoraViewModel>();
            services.AddTransient<RolesViewModel>();
            services.AddTransient<ReporteriaViewModel>();
            services.AddTransient<ConfiguracionEmpresaViewModel>();
            services.AddTransient<NotificacionesViewModel>();
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
            _scopeSesion = Services.CreateScope();
            _mainActual = _scopeSesion.ServiceProvider.GetRequiredService<MainWindow>();
            _mainActual.SesionCerrada += OnSesionCerrada;
            _mainActual.Show();
        }

        private static void OnSesionCerrada(object? s, EventArgs e)
        {
            _mainActual!.SesionCerrada -= OnSesionCerrada;
            _mainActual = null;
            _scopeSesion?.Dispose();
            _scopeSesion = null;
            // La limpieza de sesión ya se hizo en MainWindow.LimpiarRecursosAsync() via IUsuarioSesionService
            MostrarLogin();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CapaUI.Core.Diagnosticos.PipelineTelemetryService.Instance.Dispose();
            _scopeSesion?.Dispose();
            _scopeSesion = null;
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
