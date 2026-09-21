using CapaAplicacion.Conexion;
using Xunit;

namespace BimboProyecto.Tests.Preferencias;

/// <summary>
/// Guarda blanca del filtro de borrado de preferencias (PGRST100).
/// <para/>
/// El traductor de expression-trees de Supabase.Postgrest 4.0.3 genera el árbol
/// <c>((and.(a,b),c))</c> cuando un <c>.Where(x => ...)</c> combina 3+ términos con
/// <c>&amp;&amp;</c> — PostgREST lo rechaza con "failed to parse logic tree". El
/// repositorio debe usar <c>.Filter()</c> encadenados (cada término entra como AND
/// separado del query-string), que es el patrón del resto del repo. Este test lee el
/// FUENTE de la misma forma que lo hace UsuariosWhiteBoxTests: si alguien reintroduce
/// el <c>&amp;&amp;</c>, el test lo detecta antes de que descubra en producción.
/// </summary>
public sealed class EliminarPreferenciaTests
{
    private const string RaizRepo = @"..\..\..\..\";
    private static string CodigoRepositorioPreferencias() =>
        File.ReadAllText(Path.Combine(
            RaizRepo, "CapaDatos", "Repositories", "Preferencias", "PreferenciasUsuarioRepository.cs"));

    [Fact]
    public void EliminarPreferencia_no_construye_el_filtro_con_expression_tree()
    {
        var codigo = CodigoRepositorioPreferencias();

        // PGRST100: `and.` mal formado con 3+ términos en `.Where(x => a && b && c)`.
        // (un Where de condición ÚNICA es legítimo — ObtenerTodas la usa; lo prohibido
        // es el árbol combinado con &&)
        Assert.DoesNotContain("x.clave == clave && x.ambito", codigo);

        // El DELETE va por Filter encadenados — cada término un AND del query-string.
        Assert.Contains(".Filter(\"id_usuario\"", codigo);
        Assert.Contains(".Filter(\"clave\"", codigo);
        Assert.Contains(".Filter(\"ambito\"", codigo);
    }

    [Fact]
    public void Las_operaciones_de_preferencias_no_arman_arboles_logicos_en_string()
    {
        // Los árboles lógicos hechos a mano en string ("and.", "or.") rompen
        // PostgREST igual que el expression-tree: la convención es la API del SDK.
        var codigo = CodigoRepositorioPreferencias();

        Assert.DoesNotContain("\"and.", codigo);
        Assert.DoesNotContain("\"or.", codigo);
    }

    [Fact]
    public async Task EliminarConClaveInvalida_FallaAntesDelViajeABase()
    {
        // La validación de forma (regex de clave/ámbito) corre ANTES de construir el
        // query: una clave inválida no alcanza a Supabase y el mensaje llega legible
        // por el contexto de TryAsync, sin un PostgrestException crudo detrás.
        var repo = new CapaDatos.Repositories.Preferencias.PreferenciasUsuarioRepository(
            new MonitorConexionDoble());

        var resultado = await repo.EliminarAsync(13, "Clave Con Espacios", "global");

        Assert.False(resultado.Success);
        Assert.Contains("Clave de preferencia inválida", resultado.Error ?? "");
    }

    /// <summary>
    /// Doble de <see cref="IConexionMonitor"/>: la validación de forma no consulta red,
    /// y el guard de SinConexion del <c>TryAsync</c> nunca habría de dispararse —
    /// el test exige que el rechazo llegue ANTES del viaje a la base.
    /// </summary>
    private sealed class MonitorConexionDoble : IConexionMonitor
    {
        public EstadoConexion Estado => EstadoConexion.Conectado;
        public event EventHandler<EstadoConexion>? EstadoCambiado { add { } remove { } }
        public event EventHandler? Reconectado { add { } remove { } }
        public void Iniciar() { }
        public void Detener() { }
    }
}
