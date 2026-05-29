using System.IO;
using System.Windows;
using CapaAplicacion;
using CapaDatos;
using CapaUI.Formularios.InicioSesion;
using CapaUI.Formularios.Principal;
using CapaUI.Formularios.Principal.Pantallas.Productos;
using CapaUI.Services.Picker;
using CapaUI.ViewModels.Search;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CapaUI
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

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

            Services = ConfigureServices();
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
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();
            return services.BuildServiceProvider();
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
            MostrarLogin();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
