using CapaDatos.Modelados;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios
{
    public static class RepositorioEmpresa
    {
        /// <summary>
        /// Obtiene el primer registro de empresa (siempre hay uno).
        /// </summary>
        public static async Task<Empresa?> ObtenerAsync()
        {
            var client = await ConexionSupabase.GetClientAsync();
            using var ctsObtener = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
            var result = await client.From<Empresa>().Limit(1).Get(ctsObtener.Token);
            return result?.Models?.FirstOrDefault();
        }

        /// <summary>
        /// Actualiza la ruta del logo en la BD y elimina el archivo anterior
        /// del bucket antes de que el caller suba el nuevo.
        /// Devuelve la ruta anterior (para que el caller la borre del Storage).
        /// </summary>
        public static async Task<string?> ActualizarLogoAsync(int idEmpresa, string nuevaRuta)
        {
            var client = await ConexionSupabase.GetClientAsync();

            // Leer ruta actual para devolvérsela al llamador
            var actual = await ObtenerAsync();
            string? rutaAnterior = actual?.LogoEmpresa;

            using var ctsActualizar = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
            await client.From<Empresa>()
                        .Where(e => e.IdEmpresa == idEmpresa)
                        .Set(e => e.LogoEmpresa!, nuevaRuta)
                        .Update(null, ctsActualizar.Token);

            return rutaAnterior;
        }
    }
}
