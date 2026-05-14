using CapaDatos.Repositorios;
using ServicioConexión.Conexion;

namespace CapaServicios
{
    /// <summary>
    /// Maneja la descarga y caché local del logo de empresa desde Supabase Storage.
    ///
    /// Flujo:
    ///   1. Lee empresa.logo_empresa (ruta dentro del bucket).
    ///   2. Compara con la ruta guardada en el archivo de metadatos local.
    ///   3. Si cambió (o no existe el caché), descarga y sobreescribe el archivo local.
    ///   4. Devuelve la ruta local lista para usarse en la UI.
    /// </summary>
    public static class ServicioLogo
    {
        private const string BucketNombre = "empresa-logos";

        // Carpeta: %AppData%\BimboPesaje\Assets\
        private static readonly string CarpetaLocal =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "BimboPesaje", "Assets");

        private static readonly string RutaLogoLocal = Path.Combine(CarpetaLocal, "logo.png");
        private static readonly string RutaMetaLocal = Path.Combine(CarpetaLocal, "logo_meta.txt");

        /// <summary>
        /// Descarga el logo si cambió desde la última vez y devuelve la ruta local del archivo.
        /// Retorna null si la empresa no tiene logo configurado.
        /// </summary>
        public static async Task<string?> ObtenerRutaLocalAsync()
        {
            try
            {
                var empresa = await RepositorioEmpresa.ObtenerAsync();
                string? rutaBucket = empresa?.LogoEmpresa;

                if (string.IsNullOrWhiteSpace(rutaBucket))
                    return null;

                Directory.CreateDirectory(CarpetaLocal);

                // Verificar si el logo local ya está actualizado
                string? metaGuardada = File.Exists(RutaMetaLocal)
                    ? await File.ReadAllTextAsync(RutaMetaLocal)
                    : null;

                bool necesitaDescargar = metaGuardada != rutaBucket
                                      || !File.Exists(RutaLogoLocal);

                if (necesitaDescargar)
                {
                    var client = await ConexionSupabase.GetClientAsync();
                    byte[] bytes = await client.Storage
                        .From(BucketNombre)
                        .Download(rutaBucket, null);

                    await File.WriteAllBytesAsync(RutaLogoLocal, bytes);
                    await File.WriteAllTextAsync(RutaMetaLocal, rutaBucket);
                }

                return RutaLogoLocal;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServicioLogo] No se pudo descargar el logo: {ex.Message}");
                // Si falla la descarga, devolver el logo local si existe
                return File.Exists(RutaLogoLocal) ? RutaLogoLocal : null;
            }
        }

        /// <summary>
        /// Sube un nuevo logo al bucket, elimina el anterior y actualiza la BD.
        /// Llamar desde el formulario de gestión de empresa al cambiar el logo.
        /// </summary>
        /// <param name="idEmpresa">ID del registro de empresa.</param>
        /// <param name="rutaArchivoLocal">Ruta del archivo a subir (seleccionado por el usuario).</param>
        public static async Task ActualizarLogoAsync(int idEmpresa, string rutaArchivoLocal)
        {
            string extension = Path.GetExtension(rutaArchivoLocal).ToLowerInvariant();
            string nuevaRutaBucket = $"logo_empresa_{idEmpresa}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";

            byte[] bytes = await File.ReadAllBytesAsync(rutaArchivoLocal);

            var client = await ConexionSupabase.GetClientAsync();

            // 1. Subir nuevo archivo al bucket
            await client.Storage
                .From(BucketNombre)
                .Upload(bytes, nuevaRutaBucket);

            // 2. Actualizar BD y obtener ruta anterior
            string? rutaAnterior = await RepositorioEmpresa.ActualizarLogoAsync(idEmpresa, nuevaRutaBucket);

            // 3. Eliminar archivo anterior del bucket (si existía)
            if (!string.IsNullOrWhiteSpace(rutaAnterior) && rutaAnterior != nuevaRutaBucket)
            {
                try
                {
                    await client.Storage
                        .From(BucketNombre)
                        .Remove(new List<string> { rutaAnterior });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServicioLogo] No se pudo eliminar logo anterior '{rutaAnterior}': {ex.Message}");
                }
            }

            // 4. Actualizar caché local
            await File.WriteAllBytesAsync(RutaLogoLocal, bytes);
            await File.WriteAllTextAsync(RutaMetaLocal, nuevaRutaBucket);
        }

        /// <summary>
        /// Invalida el caché local forzando re-descarga en el próximo inicio.
        /// </summary>
        public static void InvalidarCache()
        {
            if (File.Exists(RutaMetaLocal))
                File.Delete(RutaMetaLocal);
        }
    }
}
