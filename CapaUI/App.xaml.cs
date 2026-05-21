using System.Windows;
using CapaAplicacion;
using CapaDatos;
using CapaUI.Formularios.InicioSesion;
using CapaUI.Formularios.Principal;
using CapaUI.Formularios.Principal.Pantallas.Productos;
using CapaUI.Services.Navigation;
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
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<UniversalSearchViewModel>();
            services.AddTransient<ProductosViewModel>();
            return services.BuildServiceProvider();
        }

        public static void MostrarLogin()
        {
            var login = new LoginWindow();
            login.LoginExitoso += (_, _) =>
            {
                login.Close();
                MostrarPrincipal();
            };
            login.Show();
        }

        private static void MostrarPrincipal()
        {
            var main = new MainWindow();
            main.SesionCerrada += (_, _) =>
            {
                main.Close();
                MostrarLogin();
            };
            main.Show();
        }
    }
}
