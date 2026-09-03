namespace CapaAplicacion.Common.Cache;

/// <summary>
/// Etiquetas canónicas de la caché.
///
/// <para><b>Las etiquetas se comparan por igualdad exacta de cadena.</b> No hay
/// comodines, ni expresiones regulares, ni jerarquía por el separador <c>:</c>.
/// Purgar <see cref="CatalogosRaiz"/> NO alcanza a <c>"catalogos:paises"</c>: son
/// dos literales distintos y no se parecen en nada para el motor.</para>
///
/// <para>Por eso toda entrada de catálogo se registra con DOS etiquetas —la raíz
/// y la de su tabla— vía <see cref="DeCatalogo"/>. Así conviven la purga
/// granular (un evento de Realtime sobre una tabla) y la consolidada (una
/// reconexión de red invalida todos los catálogos de una).</para>
///
/// <para>Si alguien registrara solo la etiqueta específica, la purga por raíz
/// encontraría cero entradas y devolvería sin error: los catálogos viejos
/// seguirían en memoria durante horas, en silencio.</para>
/// </summary>
public static class TagsCache
{
    /// <summary>Etiqueta paraguas de todos los catálogos. Purga consolidada.</summary>
    public const string CatalogosRaiz = "catalogos";

    /// <summary>Estructura de módulos y acciones del RBAC. No incluye asignaciones por rol.</summary>
    public const string RbacDefiniciones = "rbac:definiciones";

    /// <summary>Etiqueta específica de un catálogo, derivada del nombre de su tabla.</summary>
    public static string DeTabla(string tabla) => $"{CatalogosRaiz}:{tabla}";

    /// <summary>
    /// Par de etiquetas con el que se registra toda entrada de catálogo: la raíz
    /// primero, la específica después. Es el único constructor de etiquetas que
    /// deben usar los decoradores — pasar un array armado a mano es exactamente
    /// como se rompe la purga consolidada.
    /// </summary>
    public static string[] DeCatalogo(string tabla) => [CatalogosRaiz, DeTabla(tabla)];

    // Nombres de tabla — deben coincidir con los de supabase_realtime, porque son
    // los mismos que consume InvalidadorCacheRealtime para mapear evento -> etiqueta.
    public const string TablaPaises         = "paises";
    public const string TablaUnidadMedida   = "unidad_medida";
    public const string TablaTara           = "tara";
    public const string TablaCategoria      = "categoria";
    public const string TablaFabricante     = "fabricante";
    public const string TablaPresentacion   = "presentacion_producto";
    public const string TablaProveedores    = "proveedores";
    public const string TablaProductos      = "productos";
}
