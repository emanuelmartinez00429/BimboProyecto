using System.Windows;
using CapaUI.Formularios.InicioSesion;
using CapaUI.Formularios.Principal;

namespace CapaUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MostrarLogin();
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
