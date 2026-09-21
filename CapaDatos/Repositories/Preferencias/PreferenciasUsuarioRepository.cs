using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Preferencias.Dtos;
using CapaAplicacion.Preferencias.Interfaces;
using CapaDatos.Modelados.Preferencias;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;

using Op = Supabase.Postgrest.Constants.Operator;

namespace CapaDatos.Repositories.Preferencias;

/// <summary>
/// Preferencias personales del usuario contra <c>public.usuario_preferencias</c>.
/// <para/>
/// Es el único repositorio del sistema que escribe directo a una tabla en vez de pasar
/// por una RPC con idempotencia y auditoría RBAC. La decisión y su justificación están
/// en la migración <c>20260920000711_preferencias_usuario.sql</c>: el dato es del propio
/// usuario, no toca negocio, y RLS ya impide escribir la fila de otro.
/// </summary>
public sealed class PreferenciasUsuarioRepository : RepositorioBase, IPreferenciasUsuarioRepository
{
    /// <summary>Conflicto del upsert: la PK compuesta, en el orden en que la declara la tabla.</summary>
    private const string PkCompuesta = "id_usuario,clave,ambito";

    public PreferenciasUsuarioRepository(IConexionMonitor conexion) : base(conexion) { }

    public Task<Result<IReadOnlyList<PreferenciaDto>>> ObtenerTodasAsync(
        int idUsuario,
        CancellationToken ct = default) =>
        TryAsync<IReadOnlyList<PreferenciaDto>>(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();

            // El Where es redundante con RLS, que ya acota a las filas propias, pero
            // deja que PostgREST use idx_usuario_preferencias_usuario en vez de filtrar
            // fila por fila con la política.
            var respuesta = await client.From<UsuarioPreferencia>()
                .Where(x => x.idUsuario == idUsuario)
                .Get(ct);

            var filas = respuesta?.Models ?? [];

            return filas
                .Select(f => new PreferenciaDto(
                    f.clave,
                    f.ambito,
                    f.valor.ToString(Newtonsoft.Json.Formatting.None)))
                .ToList();
        }, "Cargar preferencias del usuario");

    public Task<Result> GuardarAsync(
        int idUsuario,
        string clave,
        string ambito,
        string valorJson,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            // Validar acá y no dejar que falle la base: los CHECK de formato devuelven
            // un error de Postgres crudo, ilegible para quien esté mirando la pantalla.
            ValidarClaveYAmbito(clave, ambito);

            var fila = new UsuarioPreferencia
            {
                idUsuario = idUsuario,
                clave     = clave,
                ambito    = ambito,
                valor     = ParsearValor(valorJson),
            };

            var client = await ConexionSupabase.GetClientAsync();

            await client.From<UsuarioPreferencia>().Upsert(
                fila,
                new QueryOptions
                {
                    OnConflict = PkCompuesta,
                    DuplicateResolution = QueryOptions.DuplicateResolutionType.MergeDuplicates,
                    // No hace falta que la base devuelva la fila escrita: el llamador ya
                    // tiene el valor y lo cachea local.
                    Returning = QueryOptions.ReturnType.Minimal,
                },
                ct);
        }, "Guardar preferencia del usuario");

    public Task<Result> EliminarAsync(
        int idUsuario,
        string clave,
        string ambito,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ValidarClaveYAmbito(clave, ambito);

            var client = await ConexionSupabase.GetClientAsync();

            await client.From<UsuarioPreferencia>()
                // .Filter() encadenados y NO `.Where(x => a && b && c)`: el traductor de
                // expression-trees de Supabase.Postgrest 4.0.3 genera el árbol
                // `((and.(a,b),c))` con 3+ términos — PGRST100 "failed to parse logic
                // tree". Encadenar Filters es el patrón del resto del repo y cada
                // término entra como AND separado en el query-string de PostgREST.
                .Filter("id_usuario", Op.Equals, idUsuario.ToString())
                .Filter("clave",      Op.Equals, clave)
                .Filter("ambito",     Op.Equals, ambito)
                .Delete(null, ct);
        }, "Eliminar preferencia del usuario");

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Espeja los <c>CHECK</c> de la tabla. Si estos cambian en una migración, este
    /// método tiene que cambiar con ellos.
    /// </summary>
    private static void ValidarClaveYAmbito(string clave, string ambito)
    {
        if (string.IsNullOrWhiteSpace(clave) ||
            !System.Text.RegularExpressions.Regex.IsMatch(clave, "^[a-z][a-z0-9_]{0,63}$"))
        {
            throw new ArgumentException(
                $"Clave de preferencia inválida: '{clave}'. Debe ser minúsculas, dígitos y guion bajo.",
                nameof(clave));
        }

        var ambitoValido =
            ambito == CapaAplicacion.Preferencias.ClavesPreferencia.AmbitoGlobal ||
            System.Text.RegularExpressions.Regex.IsMatch(
                ambito, @"^[0-9]{3,5}x[0-9]{3,5}@[0-9]+(\.[0-9]+)?$");

        if (!ambitoValido)
        {
            throw new ArgumentException(
                $"Ámbito de preferencia inválido: '{ambito}'. Debe ser 'global' o una huella de pantalla (1920x1080@1.75).",
                nameof(ambito));
        }
    }

    /// <summary>
    /// Convierte el JSON crudo del contrato en el <see cref="JToken"/> que espera la
    /// columna <c>jsonb</c>. Un JSON mal formado se corta acá y no viaja a la base.
    /// </summary>
    private static JToken ParsearValor(string valorJson)
    {
        if (string.IsNullOrWhiteSpace(valorJson))
            throw new ArgumentException("El valor de la preferencia no puede estar vacío.", nameof(valorJson));

        try
        {
            // DateParseHandling.None: sin esto, Newtonsoft convierte cualquier cadena
            // con pinta de fecha en DateTime y la reescribe en otro formato al guardar.
            using var lector = new Newtonsoft.Json.JsonTextReader(new StringReader(valorJson))
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
            };
            return JToken.ReadFrom(lector);
        }
        catch (Newtonsoft.Json.JsonReaderException ex)
        {
            throw new ArgumentException($"El valor de la preferencia no es JSON válido: {ex.Message}", nameof(valorJson));
        }
    }
}
