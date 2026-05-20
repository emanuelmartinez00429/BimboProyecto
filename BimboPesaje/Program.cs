using BimboPesaje.Formularios.InicioSesion;
using BimboPesaje.Formularios.MenuPrincipal;
using CapaDominio;
using ServicioConexión.Conexion;

namespace BimboPesaje
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                Task.Run(async () =>
                {
                    await ConexionSupabase.GetClientAsync();
                    await GestorRealtime.IniciarAsync();
                    GestorNotificaciones.Iniciar();
                    // Descargar/actualizar logo de empresa en caché local
                    await ServicioLogo.ObtenerRutaLocalAsync();
                }, cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(
                    "No se pudo conectar a Supabase en 15 segundos.\nVerifique su conexión a internet e intente de nuevo.",
                    "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al iniciar la aplicación:\n{ex.Message}",
                    "Error de inicio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var login = new FrmInicioSesion();
            if (login.ShowDialog() != DialogResult.OK)
                return;

            // Cargar perfil completo del usuario para el menú principal
            Task.Run(async () => await ServicioPerfilUsuario.CargarAsync()).GetAwaiter().GetResult();

            Application.Run(new FrmMenuPrincipal());
        }
    }
}