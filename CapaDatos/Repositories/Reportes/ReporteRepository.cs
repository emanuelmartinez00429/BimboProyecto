using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositories.Reportes;

public sealed class ReporteRepository : RepositorioBase, IReporteRepository
{
    public ReporteRepository(IConexionMonitor conexion) : base(conexion) { }

    public Task<Result<int>> RegistrarAsync(ReporteRegistroDto reporte, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_reporte"] = reporte.NombreReporte,
                ["p_tipo_reporte"] = reporte.TipoReporte,
                ["p_descripcion"] = reporte.Descripcion,
                ["p_fecha_desde"] = reporte.FechaDesde?.Date,
                ["p_fecha_hasta"] = reporte.FechaHasta?.Date,
                ["p_parametros_reporte"] = JObject.Parse(reporte.ParametrosJson),
                ["p_usuario_ingresando"] = reporte.UsuarioIngresando,
            };

            var response = await client.Rpc("ingresar_reporte_tabla_bitacora", parametros);

            string? json = response?.Content;
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("La función no devolvió el identificador del reporte.");

            int id;
            try { id = JToken.Parse(json).ToObject<int>(); }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                throw new InvalidOperationException("La función devolvió un identificador de reporte inválido.", ex);
            }

            return id > 0
                ? id
                : throw new InvalidOperationException("La función devolvió un identificador de reporte inválido.");
        }, "Registrar reporte");
}
