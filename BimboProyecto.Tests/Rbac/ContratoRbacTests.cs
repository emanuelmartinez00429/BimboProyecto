using System.Text.RegularExpressions;
using CapaUI.Core.Permisos;
using Npgsql;
using Xunit;

namespace BimboProyecto.Tests.Rbac;

public sealed class ContratoRbacTests
{
    [Fact]
    public void CatalogoTipado_CubreCadaPermisoUnaSolaVez()
    {
        var errores = PermisoCatalogo.ValidarCobertura();
        Assert.True(errores.Count == 0, string.Join(Environment.NewLine, errores));
        Assert.Equal(Enum.GetValues<Permiso>().Length, PermisoCatalogo.TodasLasDefiniciones.Count);
    }

    [Fact]
    public void Migraciones_TienenNombreTimestampValido()
    {
        var migraciones = Path.Combine(EncontrarRaizRepositorio(), "supabase", "migrations");
        var historicasConFormatoLegado = new HashSet<string>(StringComparer.Ordinal)
        {
            // Aplicadas antes de la regla; no se renombran porque Supabase identifica migraciones por archivo/historial.
            "202608240001_rpc_pesajes_idempotentes.sql",
            "202608240002_mejorar_detalle_bitacora_ingreso_pesaje.sql",
            "202608240003_describir_estados_pesaje_en_bitacora.sql",
        };
        var invalidas = Directory.EnumerateFiles(migraciones, "*.sql")
            .Select(Path.GetFileName)
            .Where(nombre => nombre is null || (!historicasConFormatoLegado.Contains(nombre) && !Regex.IsMatch(nombre, @"^\d{14}_[a-z0-9_]+\.sql$", RegexOptions.CultureInvariant)))
            .ToArray();

        Assert.True(invalidas.Length == 0, "Migraciones inválidas: " + string.Join(", ", invalidas));
    }

    [Fact]
    public void CambiarEstadoUsuario_UsaModificarParaActivarYEliminarParaDesactivar()
    {
        var sql = File.ReadAllText(Path.Combine(EncontrarRaizRepositorio(), "supabase", "migrations", "20260902140000_hotfix_notificaciones_seguridad_rbac.sql"));
        const string patron = @"IF\s+p_id_estado\s*=\s*1\s+THEN\s+v_codigo_accion\s*:=\s*'USUARIOS_MODIFICAR';\s+ELSE\s+v_codigo_accion\s*:=\s*'USUARIOS_ELIMINAR';\s+END\s+IF";

        Assert.False(sql.Contains("'USUARIOS_ACTIVAR'", StringComparison.Ordinal));
        Assert.Matches(new Regex(patron, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), sql);
    }

    [Fact]
    public async Task IntegracionSupabase_ContratoRbacCoincideConAccionesRemotas_CuandoHayConexion()
    {
        var conexion = Environment.GetEnvironmentVariable("BIMBO_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(conexion)) return;

        await using var db = new NpgsqlConnection(conexion);
        await db.OpenAsync();

        var codigosRemotos = new HashSet<string>(StringComparer.Ordinal);
        await using (var comando = new NpgsqlCommand("select codigo_accion from public.acciones", db))
        await using (var lector = await comando.ExecuteReaderAsync())
            while (await lector.ReadAsync()) codigosRemotos.Add(lector.GetString(0));

        var requeridos = PermisoCatalogo.TodasLasDefiniciones.Select(d => d.CodigoAccion)
            .Concat(ExtraerCodigosReferenciados(EncontrarRaizRepositorio()))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Empty(requeridos.Except(codigosRemotos));
        Assert.DoesNotContain("USUARIOS_ACTIVAR", codigosRemotos);

        await using var duplicados = new NpgsqlCommand("select codigo_accion from public.acciones group by codigo_accion having count(*) <> 1", db);
        await using var lectorDuplicados = await duplicados.ExecuteReaderAsync();
        Assert.False(await lectorDuplicados.ReadAsync(), "public.acciones contiene codigo_accion duplicados.");
    }

    [Fact]
    public async Task IntegracionSupabase_CambiarEstadoUsuarioMantieneContratoRemoto_CuandoHayConexion()
    {
        var conexion = Environment.GetEnvironmentVariable("BIMBO_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(conexion)) return;

        await using var db = new NpgsqlConnection(conexion);
        await db.OpenAsync();
        const string consulta = """
            select pg_get_functiondef(p.oid)
            from pg_proc p join pg_namespace n on n.oid = p.pronamespace
            where n.nspname = 'public' and p.proname = 'cambiar_estado_usuario_seguro'
            """;
        await using var comando = new NpgsqlCommand(consulta, db);
        await using var lector = await comando.ExecuteReaderAsync();
        Assert.True(await lector.ReadAsync(), "No existe public.cambiar_estado_usuario_seguro en la base de integración.");
        var definicion = lector.GetString(0);
        Assert.False(await lector.ReadAsync(), "Hay más de una sobrecarga de cambiar_estado_usuario_seguro.");

        const string patron = @"IF\s+p_id_estado\s*=\s*1\s+THEN\s+v_codigo_accion\s*:=\s*'USUARIOS_MODIFICAR';\s+ELSE\s+v_codigo_accion\s*:=\s*'USUARIOS_ELIMINAR';\s+END\s+IF";
        Assert.False(definicion.Contains("'USUARIOS_ACTIVAR'", StringComparison.Ordinal));
        Assert.Matches(new Regex(patron, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), definicion);
    }

    private static IEnumerable<string> ExtraerCodigosReferenciados(string raiz)
    {
        const string patron = "(?:TienePermiso\\(\\s*\\\"|usuario_tiene_permiso_codigo\\(\\s*')(?<codigo>[A-Z]+(?:_[A-Z0-9]+)+)";
        var archivos = Directory.EnumerateFiles(Path.Combine(raiz, "CapaUI"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(raiz, "CapaDatos"), "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(raiz, "supabase", "migrations"), "*.sql", SearchOption.TopDirectoryOnly));
        return archivos.SelectMany(archivo => Regex.Matches(File.ReadAllText(archivo), patron).Select(m => m.Groups["codigo"].Value));
    }

    private static string EncontrarRaizRepositorio()
    {
        foreach (var inicio in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directorio = new DirectoryInfo(inicio); directorio is not null; directorio = directorio.Parent)
                if (File.Exists(Path.Combine(directorio.FullName, "BimboProyecto.sln"))) return directorio.FullName;
        throw new DirectoryNotFoundException("No se encontró BimboProyecto.sln para validar el contrato RBAC.");
    }
}
