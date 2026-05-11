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
            Task.Run(async () =>
            {
                try
                {
                    var client = await ConexionSupabase.GetClientAsync();
                    await GestorRealtime.IniciarAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al conectar: " + ex.Message, "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0); // Si falla, cierra el sistema
                }
            }).GetAwaiter().GetResult(); // Bloquea hasta que termine


            Application.Run(new MenuPrincipal());
        }
    }
}