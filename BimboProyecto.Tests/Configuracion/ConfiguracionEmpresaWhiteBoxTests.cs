using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaDominio.Reglas;
using CapaUI.Core.Permisos;
using CapaUI.Core.Validacion;
using CapaUI.Navigation;
using Xunit;

namespace BimboProyecto.Tests.Configuracion;

/// <summary>
/// Batería de pruebas de caja blanca e invariantes para la pantalla y el módulo de Configuración de Empresa.
/// Valida:
/// 1. Reglas de validación de negocio y formato del dominio (ReglasEmpresa y ReglasFormato).
/// 2. ChangeTracker tipado sobre EmpresaSnapshot (detección exacta de cambios, reversión circular, normalización).
/// 3. Invariantes de contratos DTO y persistencia de empresa.
/// 4. Invariantes de rutas de navegación y permisos RBAC requeridos.
/// 5. Invariantes de arquitectura de vista (Zero-Shader, Vector Freeze, eliminación de Cancelar, Realtime).
/// </summary>
public sealed class ConfiguracionEmpresaWhiteBoxTests
{
    // Snapshot réplica idéntico al de ConfiguracionEmpresaViewModel para verificación de ChangeTracker
    public sealed record EmpresaSnapshotTest(
        string NombreEmpresa,
        string RtnEmpresa,
        string DireccionEmpresa,
        string TelefonoEmpresa,
        string CorreoEmpresa,
        string DominioCorreo,
        string ColorEmpresa,
        string? RutaLogoSeleccionado,
        string? RutaIconoSidebarSeleccionado);

    private static EmpresaSnapshotTest CrearSnapshotBase() =>
        new(
            NombreEmpresa: "Bimbo de Honduras S.A.",
            RtnEmpresa: "08011999123456",
            DireccionEmpresa: "Parque Industrial Sula, Edificio 4, SPS",
            TelefonoEmpresa: "25501234",
            CorreoEmpresa: "contacto@bimbo.hn",
            DominioCorreo: "bimbo.hn",
            ColorEmpresa: "#1E3A8A",
            RutaLogoSeleccionado: null,
            RutaIconoSidebarSeleccionado: null);

    // =========================================================================
    // 1. REGLAS DE NEGOCIO Y FORMATO (DOMINIO)
    // =========================================================================

    [Fact(DisplayName = "ReglasEmpresa: Nombre es obligatorio y no puede superar 200 caracteres")]
    public void ReglasEmpresa_Nombre_ObligatorioYLongitudMaxima()
    {
        Assert.True(ReglasEmpresa.Nombre.Obligatorio);
        Assert.Equal(200, ReglasEmpresa.Nombre.LargoMaximo);

        Assert.False(ReglasFormato.TieneContenido(""));
        Assert.False(ReglasFormato.TieneContenido("   "));
        Assert.False(ReglasFormato.TieneContenido(null));
        Assert.True(ReglasFormato.TieneContenido("Bimbo"));

        Assert.True(ReglasFormato.NoExcedeLargo(new string('a', 200), 200));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('a', 201), 200));
    }

    [Theory(DisplayName = "ReglasEmpresa: RTN hondureño exige exactamente 14 dígitos cuando se proporciona")]
    [InlineData("08011999123456", true)]
    [InlineData("0801-1999-123456", true)]
    [InlineData("0801 1999 123456", true)]
    [InlineData(null, true)]       // Opcional
    [InlineData("", true)]         // Opcional
    [InlineData("   ", true)]      // Opcional
    [InlineData("0801199912345", false)]    // 13 dígitos
    [InlineData("080119991234567", false)]   // 15 dígitos
    [InlineData("0801199912345A", false)]    // Carácter no numérico
    public void ReglasEmpresa_Rtn_ValidacionFormato(string? rtn, bool validoEsperado)
    {
        var resultado = ReglasFormato.EsRtn(rtn);
        Assert.Equal(validoEsperado, resultado);
    }

    [Fact(DisplayName = "ReglasEmpresa: RTN respeta límite máximo de 20 caracteres de almacenamiento")]
    public void ReglasEmpresa_Rtn_LongitudMaxima()
    {
        Assert.Equal(20, ReglasEmpresa.Rtn.LargoMaximo);
        Assert.True(ReglasFormato.NoExcedeLargo("0801-1999-123456", 20));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('1', 21), 20));
    }

    [Theory(DisplayName = "ReglasEmpresa: Teléfono admite entre 8 y 15 dígitos con prefijo internacional")]
    [InlineData("25501234", true)]           // 8 dígitos (formato hondureño)
    [InlineData("+50425501234", true)]       // Con prefijo internacional
    [InlineData("+504 2550-1234", true)]     // Con espacios y guiones
    [InlineData(null, true)]                 // Opcional
    [InlineData("", true)]                   // Opcional
    [InlineData("1234567", false)]           // 7 dígitos (menos de 8)
    [InlineData("1234567890123456", false)]  // 16 dígitos (más de 15)
    [InlineData("2550-ABCD", false)]         // Letras no permitidas
    public void ReglasEmpresa_Telefono_ValidacionFormato(string? telefono, bool validoEsperado)
    {
        var resultado = ReglasFormato.EsTelefono(telefono);
        Assert.Equal(validoEsperado, resultado);
    }

    [Fact(DisplayName = "ReglasEmpresa: Teléfono respeta tope de 20 caracteres")]
    public void ReglasEmpresa_Telefono_LongitudMaxima()
    {
        Assert.Equal(20, ReglasEmpresa.Telefono.LargoMaximo);
        Assert.True(ReglasFormato.NoExcedeLargo("+504 2550-1234", 20));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('9', 21), 20));
    }

    [Theory(DisplayName = "ReglasEmpresa: Correo valida formato estándar algo@algo.algo")]
    [InlineData("contacto@bimbo.hn", true)]
    [InlineData("admin@empresa.com", true)]
    [InlineData("soporte.tecnico@dominio.org", true)]
    [InlineData(null, true)]                 // Opcional
    [InlineData("", true)]                   // Opcional
    [InlineData("invalido", false)]          // Sin arroba ni dominio
    [InlineData("sin@dominio", false)]       // Sin TLD
    [InlineData("@nodominio.com", false)]    // Sin usuario
    [InlineData("usuario @bimbo.com", false)]// Con espacio
    public void ReglasEmpresa_Correo_ValidacionFormato(string? correo, bool validoEsperado)
    {
        var resultado = ReglasFormato.EsCorreo(correo);
        Assert.Equal(validoEsperado, resultado);
    }

    [Fact(DisplayName = "ReglasEmpresa: Correo respeta longitud máxima de 100 caracteres")]
    public void ReglasEmpresa_Correo_LongitudMaxima()
    {
        Assert.Equal(100, ReglasEmpresa.Correo.LargoMaximo);
        Assert.True(ReglasFormato.NoExcedeLargo("contacto@bimbo.hn", 100));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('a', 92) + "@bimbo.hn", 100));
    }

    [Fact(DisplayName = "ReglasEmpresa: Dirección respeta tope de 500 caracteres")]
    public void ReglasEmpresa_Direccion_LongitudMaxima()
    {
        Assert.Equal(500, ReglasEmpresa.Direccion.LargoMaximo);
        Assert.True(ReglasFormato.NoExcedeLargo(new string('x', 500), 500));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('x', 501), 500));
    }

    [Fact(DisplayName = "Dominio de correo respeta tope de 100 caracteres")]
    public void ReglasEmpresa_DominioCorreo_LongitudMaxima()
    {
        Assert.True(ReglasFormato.NoExcedeLargo("bimbo.hn", 100));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('d', 101), 100));
    }

    // =========================================================================
    // 2. DIRTY TRACKING TIPADO CON CHANGETRACKER<T>
    // =========================================================================

    [Fact(DisplayName = "ChangeTracker: Instancia inicial es limpia (IsDirty == false)")]
    public void ChangeTracker_EstadoInicial_NoEsDirty()
    {
        var baseSnapshot = CrearSnapshotBase();
        var tracker = ChangeTracker.Create(baseSnapshot);

        Assert.True(tracker.HasInitialSnapshot);
        Assert.Equal(baseSnapshot, tracker.InitialSnapshot);

        var actual = CrearSnapshotBase();
        Assert.False(tracker.IsDirty(actual));
    }

    [Fact(DisplayName = "ChangeTracker: Detecta alteración en cada uno de los 9 campos individuales")]
    public void ChangeTracker_DetectaCambiosEnCadaCampo()
    {
        var baseSnapshot = CrearSnapshotBase();
        var tracker = ChangeTracker.Create(baseSnapshot);

        // 1. Nombre
        Assert.True(tracker.IsDirty(baseSnapshot with { NombreEmpresa = "Grupo Bimbo de Honduras" }));
        // 2. RTN
        Assert.True(tracker.IsDirty(baseSnapshot with { RtnEmpresa = "05011999654321" }));
        // 3. Dirección
        Assert.True(tracker.IsDirty(baseSnapshot with { DireccionEmpresa = "Col. El Palmar, Tegucigalpa" }));
        // 4. Teléfono
        Assert.True(tracker.IsDirty(baseSnapshot with { TelefonoEmpresa = "22345678" }));
        // 5. Correo
        Assert.True(tracker.IsDirty(baseSnapshot with { CorreoEmpresa = "info@bimbo.hn" }));
        // 6. Dominio
        Assert.True(tracker.IsDirty(baseSnapshot with { DominioCorreo = "grupobimbo.hn" }));
        // 7. Color
        Assert.True(tracker.IsDirty(baseSnapshot with { ColorEmpresa = "#7C3AED" }));
        // 8. RutaLogoSeleccionado
        Assert.True(tracker.IsDirty(baseSnapshot with { RutaLogoSeleccionado = @"C:\logos\nuevo_logo.png" }));
        // 9. RutaIconoSidebarSeleccionado
        Assert.True(tracker.IsDirty(baseSnapshot with { RutaIconoSidebarSeleccionado = @"C:\logos\nuevo_icono.png" }));
    }

    [Fact(DisplayName = "ChangeTracker: Reversión circular devuelve IsDirty a false")]
    public void ChangeTracker_ReversionCircular_VuelveAClean()
    {
        var baseSnapshot = CrearSnapshotBase();
        var tracker = ChangeTracker.Create(baseSnapshot);

        // Usuario edita el teléfono
        var editado = baseSnapshot with { TelefonoEmpresa = "99998888" };
        Assert.True(tracker.IsDirty(editado), "Debe marcarse sucio tras edición");

        // Usuario se arrepiente y vuelve a escribir el teléfono original
        var revertido = editado with { TelefonoEmpresa = baseSnapshot.TelefonoEmpresa };
        Assert.False(tracker.IsDirty(revertido), "Debe volver a estado limpio sin falsos positivos");
    }

    [Fact(DisplayName = "ChangeTracker: Normalización case-insensitive para códigos de color")]
    public void ChangeTracker_NormalizacionColorHex()
    {
        // En ConfiguracionEmpresaViewModel, CrearSnapshotActual normaliza ColorEmpresa con ToUpperInvariant
        var inicial = CrearSnapshotBase() with { ColorEmpresa = "#1E3A8A".ToUpperInvariant() };
        var tracker = ChangeTracker.Create(inicial);

        // Simulando que el usuario escribió "#1e3a8a" en minúsculas, pero el VM lo normaliza:
        var actualNormalizado = inicial with { ColorEmpresa = "#1e3a8a".Trim().ToUpperInvariant() };
        Assert.False(tracker.IsDirty(actualNormalizado), "Colores en distinto casing deben normalizar al mismo valor");
    }

    [Fact(DisplayName = "ChangeTracker: Sin snapshot inicial siempre reporta IsDirty true")]
    public void ChangeTracker_SinSnapshotInicial_ReportaDirty()
    {
        var tracker = new ChangeTracker<EmpresaSnapshotTest>(null);
        Assert.False(tracker.HasInitialSnapshot);
        Assert.True(tracker.IsDirty(CrearSnapshotBase()));
    }

    // =========================================================================
    // 3. INVARIANTES DE NAVEGACIÓN Y PERMISOS RBAC
    // =========================================================================

    [Fact(DisplayName = "Rutas: Routes.Configuracion es 'configuracion'")]
    public void Rutas_ConstanteConfiguracion_TieneValorCorrecto()
    {
        Assert.Equal("configuracion", Routes.Configuracion);
    }

    [Fact(DisplayName = "RBAC: Permiso.ModificarConfiguracion está registrado en el catálogo")]
    public void Permisos_ModificarConfiguracion_TieneDefinicionValida()
    {
        var existe = PermisoCatalogo.IntentarObtenerDefinicion(Permiso.ModificarConfiguracion, out var def);
        Assert.True(existe);
        Assert.Equal("Modificar Configuración", def.NombreVisible);
        Assert.Equal("CONFIGURACION_MODIFICAR", def.CodigoAccion);
    }

    // =========================================================================
    // 4. INVARIANTES DE VISTA Y ARQUITECTURA (ZERO-SHADER Y VECTOR FREEZE)
    // =========================================================================

    [Fact(DisplayName = "Vista XAML: Cero DropShadowEffect (Zero-Shader Layout)")]
    public void VistaXaml_ZeroShader_NoContieneDropShadowEffect()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml");

        Assert.True(File.Exists(xamlPath), $"El archivo XAML no fue encontrado en: {xamlPath}");

        var contenido = File.ReadAllText(xamlPath);
        Assert.DoesNotContain("<DropShadowEffect", contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Vista XAML: Iconos vectoriales congelados con po:Freeze='True'")]
    public void VistaXaml_VectoresCongelados_UsaPoFreeze()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml");

        Assert.True(File.Exists(xamlPath), $"El archivo XAML no fue encontrado en: {xamlPath}");

        var contenido = File.ReadAllText(xamlPath);
        Assert.Contains("po:Freeze=\"True\"", contenido);
    }

    [Fact(DisplayName = "Vista XAML: No existe botón Cancelar y se usa BotonGuardarInstitucional")]
    public void VistaXaml_EstructuraBotones_SoloGuardarInstitucional()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml");

        Assert.True(File.Exists(xamlPath), $"El archivo XAML no fue encontrado en: {xamlPath}");

        var contenido = File.ReadAllText(xamlPath);

        // No debe existir botón cancelar en la vista de pantalla
        Assert.DoesNotContain("BtnCancelar", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content=\"Cancelar\"", contenido, StringComparison.OrdinalIgnoreCase);

        // Debe usar el estilo del botón verde institucional
        Assert.Contains("BotonGuardarInstitucional", contenido);
        Assert.Contains("Guardar cambios", contenido);
    }

    [Fact(DisplayName = "Vista XAML: Banner de Realtime y Banner de Dominio presentes")]
    public void VistaXaml_BannersInformativos_Presentes()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml");

        Assert.True(File.Exists(xamlPath), $"El archivo XAML no fue encontrado en: {xamlPath}");

        var contenido = File.ReadAllText(xamlPath);

        // Banner Realtime
        Assert.Contains("HayNotificacionRealtime", contenido);
        Assert.Contains("DescartarNotificacionRealtimeCommand", contenido);

        // Advertencia de cambio de dominio
        Assert.Contains("no podrán ingresar al sistema", contenido);
    }

    [Fact(DisplayName = "Vista XAML: Mergea Styles.xaml según convención de diseño ADR-028")]
    public void VistaXaml_MergedDictionaries_ContieneStylesXaml()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml");

        Assert.True(File.Exists(xamlPath), $"El archivo XAML no fue encontrado en: {xamlPath}");

        var contenido = File.ReadAllText(xamlPath);
        Assert.Contains("Styles.xaml", contenido);
        Assert.Contains("ResourceDictionary.MergedDictionaries", contenido);
    }

    [Fact(DisplayName = "Vista XAML: Fondo del UserControl estandarizado (#EAF1F8)")]
    public void VistaXaml_Background_Estandarizado()
    {
        var xamlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml");

        Assert.True(File.Exists(xamlPath), $"El archivo XAML no fue encontrado en: {xamlPath}");

        var contenido = File.ReadAllText(xamlPath);
        Assert.Contains("Background=\"#EAF1F8\"", contenido);
    }

    [Fact(DisplayName = "RealtimeService: _pkColumns contiene mapeo para la tabla 'empresa'")]
    public void RealtimeService_PkColumns_ContieneEmpresa()
    {
        var csPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaDatos", "Realtime", "RealtimeService.cs");

        Assert.True(File.Exists(csPath), $"El archivo RealtimeService.cs no fue encontrado en: {csPath}");

        var contenido = File.ReadAllText(csPath);
        Assert.Contains("[\"empresa\"]", contenido);
        Assert.Contains("\"id_empresa\"", contenido);
    }

    [Fact(DisplayName = "ViewModel C#: No contiene comando huérfano CancelarCommand")]
    public void ViewModel_SinComandoHuerfanoCancelar()
    {
        var vmPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaViewModel.cs");

        Assert.True(File.Exists(vmPath), $"El archivo ConfiguracionEmpresaViewModel.cs no fue encontrado en: {vmPath}");

        var contenido = File.ReadAllText(vmPath);
        Assert.DoesNotContain("private void Cancelar()", contenido);
        Assert.DoesNotContain("SolicitarCierre", contenido);
    }

    [Fact(DisplayName = "ViewModel C#: Valida formato de color hexadecimal en DatosValidos")]
    public void ViewModel_ValidaFormatoColorHexadecimal()
    {
        var vmPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaViewModel.cs");

        Assert.True(File.Exists(vmPath), $"El archivo ConfiguracionEmpresaViewModel.cs no fue encontrado en: {vmPath}");

        var contenido = File.ReadAllText(vmPath);
        Assert.Contains("EmpresaThemeService.EsColorValido(ColorEmpresa)", contenido);
        Assert.Contains("El color debe tener formato hexadecimal válido (#RRGGBB).", contenido);
    }

    [Fact(DisplayName = "Migración SQL: 20260917000000 publica tabla empresa en supabase_realtime")]
    public void Migracion_PublicarEmpresaEnRealtime_ExisteYEsCorrecta()
    {
        var sqlPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "supabase", "migrations", "20260917000000_publicar_empresa_en_realtime.sql");

        Assert.True(File.Exists(sqlPath), $"El archivo de migración SQL no fue encontrado en: {sqlPath}");

        var contenido = File.ReadAllText(sqlPath);
        Assert.Contains("ALTER PUBLICATION supabase_realtime ADD TABLE public.empresa;", contenido);
    }

    [Fact(DisplayName = "Vista CodeBehind: No ejecuta carga en modo diseño y no contiene lógica de negocio")]
    public void VistaCodeBehind_NoEjecutaCargaEnModoDiseno()
    {
        var csPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Formularios", "Principal", "Pantallas", "Configuracion", "ConfiguracionEmpresaView.xaml.cs");

        Assert.True(File.Exists(csPath), $"El archivo ConfiguracionEmpresaView.xaml.cs no fue encontrado en: {csPath}");

        var contenido = File.ReadAllText(csPath);
        Assert.Contains("DesignerProperties.GetIsInDesignMode(this)", contenido);
        Assert.Contains("OpenFileDialog", contenido);
        Assert.DoesNotContain("IEmpresaRepository", contenido);
        Assert.DoesNotContain("Npgsql", contenido);
    }

    [Theory(DisplayName = "NormalizarHex: Convierte códigos de 6 u 7 caracteres a formato #RRGGBB mayúsculas")]
    [InlineData("#1E3A8A", "#1E3A8A")]
    [InlineData("#1e3a8a", "#1E3A8A")]
    [InlineData("1E3A8A", "#1E3A8A")]
    [InlineData("1e3a8a", "#1E3A8A")]
    [InlineData("   #7c3aed   ", "#7C3AED")]
    [InlineData("   7C3AED   ", "#7C3AED")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizarHex_FormateoConsistente(string? entrada, string esperado)
    {
        // Ejercita la regla real de CapaDominio — la misma que usa
        // ConfiguracionEmpresaViewModel.NormalizarHex por delegación — en vez de una
        // copia local que podría desincronizarse silenciosamente del código de producción.
        var resultado = ReglasFormato.NormalizarColorHex(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Fact(DisplayName = "Realtime WAL: Simulación de payload valida coincidencia case-insensitive e ignora ausencia de '#'")]
    public void RealtimeWal_SimulacionPayload_CoincideCaseInsensitive()
    {
        // Reimplementa solo el ensamblado de los 9 campos (igual a
        // ConfiguracionEmpresaViewModel.ValoresCoincidenConActual), pero delegando la
        // comparación de cada campo en las reglas reales de CapaDominio en vez de una
        // copia local — así un cambio de comportamiento en ReglasFormato se refleja acá.
        static bool SimularValoresCoinciden(
            IReadOnlyDictionary<string, string?> valores,
            EmpresaSnapshotTest actual,
            string? logoActual,
            string? iconoActual)
        {
            bool Coincide(string clave, string? valorActual) =>
                !valores.TryGetValue(clave, out var valorRecibido) ||
                ReglasFormato.ValoresRealtimeCoinciden(valorRecibido, valorActual);

            bool CoincideColor(string clave, string? valorActual) =>
                !valores.TryGetValue(clave, out var valorRecibido) ||
                ReglasFormato.ColoresRealtimeCoinciden(valorRecibido, valorActual);

            return Coincide("nombre_empresa", actual.NombreEmpresa)
                && Coincide("rtn_empresa", actual.RtnEmpresa)
                && Coincide("direccion_empresa", actual.DireccionEmpresa)
                && Coincide("telefono_empresa", actual.TelefonoEmpresa)
                && Coincide("correo_empresa", actual.CorreoEmpresa)
                && Coincide("dominio_correo", actual.DominioCorreo)
                && CoincideColor("color_empresa", actual.ColorEmpresa)
                && Coincide("logo_empresa", logoActual)
                && Coincide("icono_sidebar", iconoActual);
        }

        var baseSnap = CrearSnapshotBase(); // ColorEmpresa = "#1E3A8A"

        // 1. Payload idéntico en minúsculas y sin '#'
        var payloadMinuscula = new Dictionary<string, string?>
        {
            ["nombre_empresa"] = "Bimbo de Honduras S.A.",
            ["rtn_empresa"] = "08011999123456",
            ["direccion_empresa"] = "Parque Industrial Sula, Edificio 4, SPS",
            ["telefono_empresa"] = "25501234",
            ["correo_empresa"] = "contacto@bimbo.hn",
            ["dominio_correo"] = "bimbo.hn",
            ["color_empresa"] = "1e3a8a", // Minúscula y sin '#'
            ["logo_empresa"] = null,
            ["icono_sidebar"] = null
        };
        Assert.True(SimularValoresCoinciden(payloadMinuscula, baseSnap, null, null));

        // 2. Payload con cambio real de color desde otra terminal
        var payloadCambioColor = new Dictionary<string, string?>
        {
            ["color_empresa"] = "#7C3AED"
        };
        Assert.False(SimularValoresCoinciden(payloadCambioColor, baseSnap, null, null));

        // 3. Payload con cambio real de nombre desde otra terminal
        var payloadCambioNombre = new Dictionary<string, string?>
        {
            ["nombre_empresa"] = "Bimbo Bakeries S.A."
        };
        Assert.False(SimularValoresCoinciden(payloadCambioNombre, baseSnap, null, null));
    }

    [Fact(DisplayName = "EmpresaThemeService C#: Invariante de validación de color hexadecimal")]
    public void EmpresaThemeService_InvarianteValidacionColor()
    {
        var themePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CapaUI", "Services", "Empresa", "EmpresaThemeService.cs");

        Assert.True(File.Exists(themePath), $"El archivo EmpresaThemeService.cs no fue encontrado en: {themePath}");

        var contenido = File.ReadAllText(themePath);
        Assert.Contains("public static bool EsColorValido(string? valor)", contenido);
        Assert.Contains("sin_color", contenido);
        Assert.Contains("NumberStyles.HexNumber", contenido);
    }

    // =========================================================================
    // 5. INVARIANTES DE CONTRATOS Y DTOs
    // =========================================================================

    [Fact(DisplayName = "EmpresaDto: Posee todas las propiedades de identidad y configuración")]
    public void EmpresaDto_PropiedadesCompletas()
    {
        var dto = new EmpresaDto
        {
            IdEmpresa = 1,
            NombreEmpresa = "Bimbo de Honduras S.A.",
            RtnEmpresa = "08011999123456",
            DireccionEmpresa = "SPS",
            TelefonoEmpresa = "25501234",
            CorreoEmpresa = "info@bimbo.hn",
            LogoEmpresa = "logo_1_2026.png",
            IconoSidebar = "sidebar_1_2026.png",
            DominioCorreo = "bimbo.hn",
            ColorEmpresa = "#1E3A8A"
        };

        Assert.Equal(1, dto.IdEmpresa);
        Assert.Equal("Bimbo de Honduras S.A.", dto.NombreEmpresa);
        Assert.Equal("logo_1_2026.png", dto.LogoEmpresa);
        Assert.Equal("sidebar_1_2026.png", dto.IconoSidebar);
        Assert.Equal("#1E3A8A", dto.ColorEmpresa);
    }

    [Fact(DisplayName = "EmpresaGuardadaDto: Encapsula resultado y lista inmutable de advertencias")]
    public void EmpresaGuardadaDto_EstructuraCorrecta()
    {
        var empresa = new EmpresaDto { IdEmpresa = 1, NombreEmpresa = "Bimbo" };
        var advertencias = new[] { "No se pudo eliminar el logo anterior" };

        var guardada = new EmpresaGuardadaDto
        {
            Empresa = empresa,
            Advertencias = advertencias
        };

        Assert.Same(empresa, guardada.Empresa);
        Assert.Single(guardada.Advertencias);
        Assert.Equal("No se pudo eliminar el logo anterior", guardada.Advertencias[0]);
    }
}
