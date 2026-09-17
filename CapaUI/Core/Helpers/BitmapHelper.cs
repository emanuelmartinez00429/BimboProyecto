using System;
using System.IO;
using System.Windows.Media.Imaging;
using Serilog;

namespace CapaUI.Core.Helpers
{
    /// <summary>
    /// Utilidades de procesamiento y decodificación de mapas de bits para WPF.
    /// Diseñado para desacoplar el renderizado del ciclo de vida del disco y del Dispatcher.
    /// </summary>
    public static class BitmapHelper
    {
        /// <summary>
        /// Carga una imagen en memoria con BitmapCacheOption.OnLoad y llama a .Freeze()
        /// mediante MemoryStream para liberar inmediatamente el descriptor de archivo en disco,
        /// evitando bloqueos Win32 IOException y permitiendo acceso multi-hilo seguro.
        /// </summary>
        /// <param name="rutaLocal">Ruta absoluta o relativa al archivo de imagen en disco.</param>
        /// <returns>BitmapImage congelado e inmutable, o null si la ruta es inválida o falla la lectura.</returns>
        public static BitmapImage? CargarBitmapCongelado(string? rutaLocal)
        {
            if (string.IsNullOrWhiteSpace(rutaLocal) || !File.Exists(rutaLocal))
                return null;

            try
            {
                using var fileStream = new FileStream(rutaLocal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Carga completa en RAM y suelta descriptor
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze(); // Desconecta del Dispatcher, haciéndolo inmutable y multi-hilo
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "BitmapHelper: error al decodificar bitmap congelado desde {Ruta}", rutaLocal);
                return null;
            }
        }
    }
}
