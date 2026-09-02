using System.Reflection;
using CapaDominio.Reglas;
using Npgsql;
using Xunit;

namespace BimboProyecto.Tests.Dominio;

/// <summary>
/// Registro de auditoría para contrastar cada <see cref="ReglaCampo"/> declarada en el dominio
/// contra la especificación de negocio (CI offline) y contra el catálogo físico de Supabase PostgreSQL.
/// </summary>
public sealed record DefinicionAuditoriaRegla(
    string ClaseDominio,
    string NombreCampo,
    ReglaCampo Regla,
    string Tabla,
    string Columna,
    bool ObligatorioEsperado,
    int? LargoMaximoEsperado,
    int? LargoMinimoEsperado,
    FormatoCampo FormatoEsperado,
    bool EsTopeUI = false,
    bool EsColumnaBD = true
)
{
    public override string ToString() => $"{ClaseDominio}.{NombreCampo} -> {Tabla}.{Columna}";
}

/// <summary>
/// Suite de pruebas automatizadas de deriva de esquema, valores fijados offline y auditoría por reflexión (R5).
/// <para/>
/// <b>Test A (Deriva de Esquema contra PostgreSQL):</b> Se conecta a PostgreSQL mediante <c>Npgsql</c>
/// leyendo <c>BIMBO_POSTGRES_CONNECTION_STRING</c>. Si no existe la variable, omite limpiamente para CI offline.
/// Si existe, consulta <c>information_schema.columns</c> para verificar que las 34 reglas coincidan con la BD física,
/// que ningún <c>LargoMaximo</c> sea más permisivo que la columna en BD, y que las columnas <c>text</c> tengan tope de UI de 500.
/// <para/>
/// <b>Test B (Valores Fijados para Ejecución Offline / CI):</b> Pruebas deterministas con <see cref="TheoryAttribute"/>
/// que validan las 34 reglas de dominio en las 10 clases de negocio sin requerir conexión a BD.
/// <para/>
/// <b>Test C (Auditoría Exhaustiva por Reflexión):</b> Inspecciona por Reflection el ensamblado de <see cref="ReglasProducto"/>
/// para asegurar que el 100% de los campos <see cref="ReglaCampo"/> declarados en <c>CapaDominio.Reglas</c> estén cubiertos
/// en el catálogo de auditoría.
/// </summary>
public sealed class ReglasEntidadesTests
{
    /// <summary>
    /// Catálogo autoritativo de las 34 reglas de negocio mapeadas a su columna física o servicio.
    /// </summary>
    public static readonly IReadOnlyList<DefinicionAuditoriaRegla> CatalogoAuditoria = new DefinicionAuditoriaRegla[]
    {
        // ── 1. ReglasProducto (5 campos) ────────────────────────────────────
        new("ReglasProducto", nameof(ReglasProducto.Codigo), ReglasProducto.Codigo,
            "productos", "codigo_producto", ObligatorioEsperado: true, LargoMaximoEsperado: 50, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasProducto", nameof(ReglasProducto.Nombre), ReglasProducto.Nombre,
            "productos", "nombre_producto", ObligatorioEsperado: true, LargoMaximoEsperado: 200, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasProducto", nameof(ReglasProducto.Contenido), ReglasProducto.Contenido,
            "productos", "contenido", ObligatorioEsperado: false, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasProducto", nameof(ReglasProducto.PesoTeorico), ReglasProducto.PesoTeorico,
            "productos", "peso_teorico", ObligatorioEsperado: false, LargoMaximoEsperado: null, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Decimal),
        new("ReglasProducto", nameof(ReglasProducto.PrecioPorKg), ReglasProducto.PrecioPorKg,
            "productos", "precio_por_kg", ObligatorioEsperado: false, LargoMaximoEsperado: null, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Decimal),

        // ── 2. ReglasCategoria (2 campos) ───────────────────────────────────
        new("ReglasCategoria", nameof(ReglasCategoria.Nombre), ReglasCategoria.Nombre,
            "categoria", "nombre_categoria", ObligatorioEsperado: true, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasCategoria", nameof(ReglasCategoria.Descripcion), ReglasCategoria.Descripcion,
            "categoria", "descripcion_categoria", ObligatorioEsperado: false, LargoMaximoEsperado: 200, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),

        // ── 3. ReglasPresentacion (2 campos) ────────────────────────────────
        new("ReglasPresentacion", nameof(ReglasPresentacion.Nombre), ReglasPresentacion.Nombre,
            "presentacion_producto", "nombre_presentacion", ObligatorioEsperado: true, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasPresentacion", nameof(ReglasPresentacion.Descripcion), ReglasPresentacion.Descripcion,
            "presentacion_producto", "descripcion_presentacion", ObligatorioEsperado: false, LargoMaximoEsperado: 500, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno, EsTopeUI: false),

        // ── 4. ReglasFabricante (2 campos) ──────────────────────────────────
        new("ReglasFabricante", nameof(ReglasFabricante.Nombre), ReglasFabricante.Nombre,
            "fabricante", "nombre_fabricante", ObligatorioEsperado: true, LargoMaximoEsperado: 200, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasFabricante", nameof(ReglasFabricante.Descripcion), ReglasFabricante.Descripcion,
            "fabricante", "descripcion_fabricante", ObligatorioEsperado: false, LargoMaximoEsperado: 500, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno, EsTopeUI: false),

        // ── 5. ReglasProveedor (5 campos) ───────────────────────────────────
        new("ReglasProveedor", nameof(ReglasProveedor.Nombre), ReglasProveedor.Nombre,
            "proveedores", "nombre_proveedor", ObligatorioEsperado: true, LargoMaximoEsperado: 200, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasProveedor", nameof(ReglasProveedor.Rtn), ReglasProveedor.Rtn,
            "proveedores", "rtn_proveedor", ObligatorioEsperado: false, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Rtn),
        new("ReglasProveedor", nameof(ReglasProveedor.Telefono), ReglasProveedor.Telefono,
            "proveedores", "telefono_proveedor", ObligatorioEsperado: false, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Telefono),
        new("ReglasProveedor", nameof(ReglasProveedor.Correo), ReglasProveedor.Correo,
            "proveedores", "correo_proveedor", ObligatorioEsperado: false, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Correo),
        new("ReglasProveedor", nameof(ReglasProveedor.Direccion), ReglasProveedor.Direccion,
            "proveedores", "direccion_proveedor", ObligatorioEsperado: false, LargoMaximoEsperado: 500, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno, EsTopeUI: false),

        // ── 6. ReglasEmpleado (5 campos) ────────────────────────────────────
        new("ReglasEmpleado", nameof(ReglasEmpleado.Nombre), ReglasEmpleado.Nombre,
            "empleados", "nombre_empleado", ObligatorioEsperado: true, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasEmpleado", nameof(ReglasEmpleado.Apellido), ReglasEmpleado.Apellido,
            "empleados", "apellido_empleado", ObligatorioEsperado: true, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasEmpleado", nameof(ReglasEmpleado.Identidad), ReglasEmpleado.Identidad,
            "empleados", "numero_identidad", ObligatorioEsperado: true, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasEmpleado", nameof(ReglasEmpleado.Telefono), ReglasEmpleado.Telefono,
            "empleados", "telefono_empleado", ObligatorioEsperado: false, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Telefono),
        new("ReglasEmpleado", nameof(ReglasEmpleado.Correo), ReglasEmpleado.Correo,
            "empleados", "correo_empleado", ObligatorioEsperado: false, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Correo),

        // ── 7. ReglasUsuario (4 campos) ─────────────────────────────────────
        new("ReglasUsuario", nameof(ReglasUsuario.Empleado), ReglasUsuario.Empleado,
            "usuarios", "id_empleado", ObligatorioEsperado: true, LargoMaximoEsperado: null, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasUsuario", nameof(ReglasUsuario.Rol), ReglasUsuario.Rol,
            "usuarios", "id_rol", ObligatorioEsperado: true, LargoMaximoEsperado: null, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasUsuario", nameof(ReglasUsuario.Correo), ReglasUsuario.Correo,
            "usuarios", "alias_usuario", ObligatorioEsperado: true, LargoMaximoEsperado: 50, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Correo),
        new("ReglasUsuario", nameof(ReglasUsuario.Password), ReglasUsuario.Password,
            "auth.users", "encrypted_password", ObligatorioEsperado: true, LargoMaximoEsperado: 72, LargoMinimoEsperado: 6, FormatoEsperado: FormatoCampo.Ninguno, EsColumnaBD: false),

        // ── 8. ReglasRol (1 campo) ──────────────────────────────────────────
        new("ReglasRol", nameof(ReglasRol.Nombre), ReglasRol.Nombre,
            "roles", "nombre_rol", ObligatorioEsperado: true, LargoMaximoEsperado: 50, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),

        // ── 9. ReglasContacto (3 campos) ────────────────────────────────────
        new("ReglasContacto", nameof(ReglasContacto.Nombre), ReglasContacto.Nombre,
            "contactos_proveedor", "nombre_contacto", ObligatorioEsperado: true, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasContacto", nameof(ReglasContacto.Telefono), ReglasContacto.Telefono,
            "contactos_proveedor", "telefono_contacto", ObligatorioEsperado: false, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Telefono),
        new("ReglasContacto", nameof(ReglasContacto.Correo), ReglasContacto.Correo,
            "contactos_proveedor", "correo_contacto", ObligatorioEsperado: false, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Correo),

        // ── 10. ReglasEmpresa (5 campos) ────────────────────────────────────
        new("ReglasEmpresa", nameof(ReglasEmpresa.Nombre), ReglasEmpresa.Nombre,
            "empresa", "nombre_empresa", ObligatorioEsperado: true, LargoMaximoEsperado: 200, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno),
        new("ReglasEmpresa", nameof(ReglasEmpresa.Rtn), ReglasEmpresa.Rtn,
            "empresa", "rtn_empresa", ObligatorioEsperado: false, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Rtn),
        new("ReglasEmpresa", nameof(ReglasEmpresa.Telefono), ReglasEmpresa.Telefono,
            "empresa", "telefono_empresa", ObligatorioEsperado: false, LargoMaximoEsperado: 20, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Telefono),
        new("ReglasEmpresa", nameof(ReglasEmpresa.Correo), ReglasEmpresa.Correo,
            "empresa", "correo_empresa", ObligatorioEsperado: false, LargoMaximoEsperado: 100, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Correo),
        new("ReglasEmpresa", nameof(ReglasEmpresa.Direccion), ReglasEmpresa.Direccion,
            "empresa", "direccion_empresa", ObligatorioEsperado: false, LargoMaximoEsperado: 500, LargoMinimoEsperado: null, FormatoEsperado: FormatoCampo.Ninguno, EsTopeUI: false),
    };

    public static TheoryData<DefinicionAuditoriaRegla> ObtenerDatosAuditoria()
    {
        var data = new TheoryData<DefinicionAuditoriaRegla>();
        foreach (var regla in CatalogoAuditoria)
        {
            data.Add(regla);
        }
        return data;
    }

    // ========================================================================
    // TEST A: Deriva de Esquema contra PostgreSQL (information_schema.columns)
    // ========================================================================

    [Fact]
    public async Task TestA_DerivaEsquemaPostgres_ReglasDominioAlineadasConBaseDeDatos_CuandoHayConexion()
    {
        var conexion = Environment.GetEnvironmentVariable("BIMBO_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(conexion)) return;

        await using var db = new NpgsqlConnection(conexion);
        await db.OpenAsync();

        const string consulta = """
            SELECT 
                lower(table_name) AS table_name,
                lower(column_name) AS column_name,
                lower(data_type) AS data_type,
                character_maximum_length,
                is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public';
            """;

        var columnasBd = new Dictionary<(string Tabla, string Columna), (string DataType, int? MaxLength, string IsNullable)>();

        await using (var comando = new NpgsqlCommand(consulta, db))
        await using (var lector = await comando.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync())
            {
                var tabla = lector.GetString(0);
                var columna = lector.GetString(1);
                var dataType = lector.GetString(2);
                int? maxLength = lector.IsDBNull(3) ? null : lector.GetInt32(3);
                var isNullable = lector.GetString(4);

                columnasBd[(tabla, columna)] = (dataType, maxLength, isNullable);
            }
        }

        Assert.NotEmpty(columnasBd);

        foreach (var def in CatalogoAuditoria.Where(d => d.EsColumnaBD))
        {
            var clave = (def.Tabla.ToLowerInvariant(), def.Columna.ToLowerInvariant());
            Assert.True(columnasBd.TryGetValue(clave, out var colBd),
                $"[Deriva] La columna '{def.Columna}' en la tabla '{def.Tabla}' mapeada por {def.ClaseDominio}.{def.NombreCampo} no existe en PostgreSQL 'public'.");

            if (colBd.DataType is "character varying" or "character" or "varchar")
            {
                if (colBd.MaxLength.HasValue && def.Regla.LargoMaximo.HasValue)
                {
                    Assert.True(def.Regla.LargoMaximo.Value <= colBd.MaxLength.Value,
                        $"[Deriva] La regla {def.ClaseDominio}.{def.NombreCampo} define LargoMaximo={def.Regla.LargoMaximo.Value}, lo cual es más permisivo que el tamaño físico de la columna '{def.Columna}' en BD ({colBd.MaxLength.Value}).");

                    Assert.Equal(def.LargoMaximoEsperado, def.Regla.LargoMaximo);
                }
            }
            else if (colBd.DataType is "text")
            {
                Assert.True(def.EsTopeUI,
                    $"[Deriva] La columna '{def.Columna}' en tabla '{def.Tabla}' es de tipo 'text'. La regla {def.ClaseDominio}.{def.NombreCampo} debe estar marcada como EsTopeUI=true.");

                Assert.Equal(500, def.Regla.LargoMaximo);
            }

            // Validación cruzada adicional para contactos_fabricante si aplica
            if (def.ClaseDominio == "ReglasContacto")
            {
                var claveFabricante = ("contactos_fabricante", def.Columna.ToLowerInvariant());
                if (columnasBd.TryGetValue(claveFabricante, out var colFab) && colFab.MaxLength.HasValue && def.Regla.LargoMaximo.HasValue)
                {
                    Assert.True(def.Regla.LargoMaximo.Value <= colFab.MaxLength.Value,
                        $"[Deriva] ReglasContacto.{def.NombreCampo} excede la longitud física en contactos_fabricante.{def.Columna}.");
                }
            }
        }
    }

    // ========================================================================
    // TEST B: Valores Fijados para Ejecución Offline / CI ([Theory])
    // ========================================================================

    [Theory]
    [MemberData(nameof(ObtenerDatosAuditoria))]
    public void TestB_ValoresFijados_CumplenReglasNegocioOffline(DefinicionAuditoriaRegla def)
    {
        Assert.NotNull(def.Regla);
        Assert.Equal(def.ObligatorioEsperado, def.Regla.Obligatorio);
        Assert.Equal(def.LargoMaximoEsperado, def.Regla.LargoMaximo);
        Assert.Equal(def.LargoMinimoEsperado, def.Regla.LargoMinimo);
        Assert.Equal(def.FormatoEsperado, def.Regla.Formato);
    }

    [Fact]
    public void TestB_ReglasProducto_TieneTodosSusCamposAlineados()
    {
        Assert.True(ReglasProducto.Codigo.Obligatorio);
        Assert.Equal(50, ReglasProducto.Codigo.LargoMaximo);

        Assert.True(ReglasProducto.Nombre.Obligatorio);
        Assert.Equal(200, ReglasProducto.Nombre.LargoMaximo);

        Assert.False(ReglasProducto.Contenido.Obligatorio);
        Assert.Equal(100, ReglasProducto.Contenido.LargoMaximo);

        Assert.Equal(FormatoCampo.Decimal, ReglasProducto.PesoTeorico.Formato);
        Assert.Equal(FormatoCampo.Decimal, ReglasProducto.PrecioPorKg.Formato);
    }

    [Fact]
    public void TestB_ReglasCategoria_TieneDescripcionAlineadaA200()
    {
        Assert.True(ReglasCategoria.Nombre.Obligatorio);
        Assert.Equal(100, ReglasCategoria.Nombre.LargoMaximo);

        Assert.False(ReglasCategoria.Descripcion.Obligatorio);
        Assert.Equal(200, ReglasCategoria.Descripcion.LargoMaximo);
    }

    [Fact]
    public void TestB_ReglasPresentacion_TieneTopeUIDe500EnDescripcion()
    {
        Assert.True(ReglasPresentacion.Nombre.Obligatorio);
        Assert.Equal(100, ReglasPresentacion.Nombre.LargoMaximo);

        Assert.False(ReglasPresentacion.Descripcion.Obligatorio);
        Assert.Equal(500, ReglasPresentacion.Descripcion.LargoMaximo);
    }

    [Fact]
    public void TestB_ReglasFabricante_TieneTopeUIDe500EnDescripcion()
    {
        Assert.True(ReglasFabricante.Nombre.Obligatorio);
        Assert.Equal(200, ReglasFabricante.Nombre.LargoMaximo);

        Assert.False(ReglasFabricante.Descripcion.Obligatorio);
        Assert.Equal(500, ReglasFabricante.Descripcion.LargoMaximo);
    }

    [Fact]
    public void TestB_ReglasProveedor_TieneTopesDefensivosYFormatos()
    {
        Assert.True(ReglasProveedor.Nombre.Obligatorio);
        Assert.Equal(200, ReglasProveedor.Nombre.LargoMaximo);

        Assert.Equal(20, ReglasProveedor.Rtn.LargoMaximo);
        Assert.Equal(FormatoCampo.Rtn, ReglasProveedor.Rtn.Formato);

        Assert.Equal(20, ReglasProveedor.Telefono.LargoMaximo);
        Assert.Equal(FormatoCampo.Telefono, ReglasProveedor.Telefono.Formato);

        Assert.Equal(100, ReglasProveedor.Correo.LargoMaximo);
        Assert.Equal(FormatoCampo.Correo, ReglasProveedor.Correo.Formato);

        Assert.Equal(500, ReglasProveedor.Direccion.LargoMaximo);
    }

    [Fact]
    public void TestB_ReglasEmpleado_TieneIdentidadObligatoriaYTopes()
    {
        Assert.True(ReglasEmpleado.Nombre.Obligatorio);
        Assert.Equal(100, ReglasEmpleado.Nombre.LargoMaximo);

        Assert.True(ReglasEmpleado.Apellido.Obligatorio);
        Assert.Equal(100, ReglasEmpleado.Apellido.LargoMaximo);

        Assert.True(ReglasEmpleado.Identidad.Obligatorio);
        Assert.Equal(20, ReglasEmpleado.Identidad.LargoMaximo);

        Assert.Equal(20, ReglasEmpleado.Telefono.LargoMaximo);
        Assert.Equal(FormatoCampo.Telefono, ReglasEmpleado.Telefono.Formato);

        Assert.Equal(100, ReglasEmpleado.Correo.LargoMaximo);
        Assert.Equal(FormatoCampo.Correo, ReglasEmpleado.Correo.Formato);
    }

    [Fact]
    public void TestB_ReglasUsuario_TieneTope50EnCorreoYBcrypt72EnPassword()
    {
        Assert.True(ReglasUsuario.Empleado.Obligatorio);
        Assert.True(ReglasUsuario.Rol.Obligatorio);

        Assert.True(ReglasUsuario.Correo.Obligatorio);
        Assert.Equal(50, ReglasUsuario.Correo.LargoMaximo);
        Assert.Equal(FormatoCampo.Correo, ReglasUsuario.Correo.Formato);

        Assert.True(ReglasUsuario.Password.Obligatorio);
        Assert.Equal(6, ReglasUsuario.Password.LargoMinimo);
        Assert.Equal(72, ReglasUsuario.Password.LargoMaximo);
    }

    [Fact]
    public void TestB_ReglasRol_TieneTope50EnNombre()
    {
        Assert.True(ReglasRol.Nombre.Obligatorio);
        Assert.Equal(50, ReglasRol.Nombre.LargoMaximo);
    }

    [Fact]
    public void TestB_ReglasContacto_TieneTopesEnTelefonoYCorreo()
    {
        Assert.True(ReglasContacto.Nombre.Obligatorio);
        Assert.Equal(100, ReglasContacto.Nombre.LargoMaximo);

        Assert.Equal(20, ReglasContacto.Telefono.LargoMaximo);
        Assert.Equal(FormatoCampo.Telefono, ReglasContacto.Telefono.Formato);

        Assert.Equal(100, ReglasContacto.Correo.LargoMaximo);
        Assert.Equal(FormatoCampo.Correo, ReglasContacto.Correo.Formato);
    }

    [Fact]
    public void TestB_ReglasEmpresa_TieneTopesEnTodosLosCampos()
    {
        Assert.True(ReglasEmpresa.Nombre.Obligatorio);
        Assert.Equal(200, ReglasEmpresa.Nombre.LargoMaximo);

        Assert.Equal(20, ReglasEmpresa.Rtn.LargoMaximo);
        Assert.Equal(FormatoCampo.Rtn, ReglasEmpresa.Rtn.Formato);

        Assert.Equal(20, ReglasEmpresa.Telefono.LargoMaximo);
        Assert.Equal(FormatoCampo.Telefono, ReglasEmpresa.Telefono.Formato);

        Assert.Equal(100, ReglasEmpresa.Correo.LargoMaximo);
        Assert.Equal(FormatoCampo.Correo, ReglasEmpresa.Correo.Formato);

        Assert.Equal(500, ReglasEmpresa.Direccion.LargoMaximo);
    }

    // ========================================================================
    // TEST C: Auditoría Exhaustiva por Reflexión
    // ========================================================================

    [Fact]
    public void TestC_AuditoriaExhaustivaPorReflexion_CubreElCienPorCientoDeReglasDeclaradas()
    {
        var assemblyDominio = typeof(ReglasProducto).Assembly;

        var tiposReglas = assemblyDominio.GetTypes()
            .Where(t => t.Namespace == "CapaDominio.Reglas" && t.IsClass)
            .ToList();

        var camposReflexion = new List<(string Clase, string Campo, ReglaCampo Regla)>();

        foreach (var tipo in tiposReglas)
        {
            var campos = tipo.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(ReglaCampo));

            foreach (var campo in campos)
            {
                var valor = (ReglaCampo)campo.GetValue(null)!;
                camposReflexion.Add((tipo.Name, campo.Name, valor));
            }
        }

        // Exactamente 34 reglas declaradas y 34 en el catálogo
        Assert.Equal(34, camposReflexion.Count);
        Assert.Equal(34, CatalogoAuditoria.Count);

        var mapeoCatalogo = CatalogoAuditoria.ToDictionary(
            d => $"{d.ClaseDominio}.{d.NombreCampo}",
            d => d,
            StringComparer.Ordinal
        );

        foreach (var (clase, campo, regla) in camposReflexion)
        {
            var clave = $"{clase}.{campo}";
            Assert.True(mapeoCatalogo.TryGetValue(clave, out var def),
                $"[Reflexión] El campo '{clave}' de tipo ReglaCampo no está registrado en el catálogo de auditoría.");

            Assert.Same(regla, def.Regla);
            Assert.Equal(def.ObligatorioEsperado, regla.Obligatorio);
            Assert.Equal(def.LargoMaximoEsperado, regla.LargoMaximo);
            Assert.Equal(def.LargoMinimoEsperado, regla.LargoMinimo);
            Assert.Equal(def.FormatoEsperado, regla.Formato);
        }
    }
}
