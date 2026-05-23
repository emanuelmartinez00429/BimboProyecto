using System.Windows;
using CapaAplicacion;
using CapaDatos;
using CapaUI.Formularios.InicioSesion;
using CapaUI.Formularios.Principal;
using CapaUI.Formularios.Principal.Pantallas.Productos;
using CapaUI.Services.Picker;
using CapaUI.ViewModels.Search;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
            var login = Services.GetRequiredService<LoginWindow>();
            login.LoginExitoso += (_, _) =>
            {
                login.Close();
                MostrarPrincipal();
            };
            login.Show();
        }

        private static void MostrarPrincipal()
        {
            var main = Services.GetRequiredService<MainWindow>();
            main.SesionCerrada += (_, _) =>
            {
                main.Close();
                MostrarLogin();
            };
            main.Show();
        }
    }
}
