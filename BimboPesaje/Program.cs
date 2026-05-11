using BimboPesaje.Formularios.Usuarios;
using CapaServicios;
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

            using var login = new FrmLogin();
            if (login.ShowDialog() != DialogResult.OK)
                return;

            Application.Run(new MenuPrincipal());
        }
    }
}