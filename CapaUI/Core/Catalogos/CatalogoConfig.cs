using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaUI.Core.Catalogos;

/// <summary>
/// Descriptor de un catálogo para el selector genérico. Es lo único que cambia
/// entre un campo y otro: "solo cambia la tabla".
/// </summary>
/// <param name="Clave">
/// Identidad para la caché. Incluye el alcance cuando el catálogo está acotado
/// (p. ej. <c>fabricantes:7</c>), para que dos alcances distintos no se pisen.
/// </param>
/// <param name="Cargar">
/// Firma uniforme para todos los catálogos. El selector la llama primero con
/// <c>page 1</c> y un tamaño igual al umbral de memoria, y decide su estrategia
/// según el <see cref="PagedResult{T}.Total"/> que vuelva.
/// </param>
/// <param name="TituloDescripcion">
/// Encabezado de la columna que muestra <c>FiltroItem.Descripcion</c> — ese
/// campo es genérico y cada catálogo lo usa para algo distinto (RTN en
/// Proveedores, código en Productos, abreviatura en Unidades…), así que el
/// título de la columna tiene que decir qué es realmente, no un "Descripción"
/// fijo que queda mal para la mitad de los catálogos.
/// </param>
/// <param name="DescripcionPrimero">
/// Muestra la columna Descripción antes que Nombre (p. ej. Productos: el
/// código es lo que la gente escanea/reconoce primero, el nombre confirma).
/// </param>
/// <param name="PermiteMultiple">
/// Habilita checkboxes independientes por fila en vez del círculo de
/// selección única — clickear varias filas las va sumando todas, y "Elegir"
/// las trae de una sola vez (mismo <see cref="Action{T}"/> Seleccionado,
/// invocado una vez por fila marcada).
/// </param>
/// <param name="EstaYaElegido">
/// Marca (atenuada, no seleccionable) las filas cuyo Id ya está elegido en
/// otro lado — p. ej. productos que ya están en la carga del proceso de
/// descarga. <c>null</c> ⇒ ninguna fila se atenúa.
/// </param>
public sealed record CatalogoConfig(
    string Clave,
    string Titulo,
    string Placeholder,
    Func<string, int, int, CancellationToken, Task<Result<PagedResult<FiltroItem>>>> Cargar,
    bool MostrarDescripcion  = true,
    string TituloDescripcion = "Descripción",
    bool MostrarEstado       = true,
    bool DescripcionPrimero  = false,
    bool PermiteMultiple     = false,
    Func<int?, bool>? EstaYaElegido = null);

/// <summary>
/// Factories de configuración — un miembro por catálogo. Dar de alta un campo
/// nuevo en cualquier formulario es agregar (o reusar) uno de estos.
/// </summary>
public static class Catalogos
{
    public static CatalogoConfig Presentaciones(ICatalogoRepository r) => new(
        "presentaciones", "Seleccionar presentación", "Buscar presentación...",
        (t, p, s, ct) => r.GetPresentacionesAsync(t, p, s, ct));

    public static CatalogoConfig Taras(ICatalogoRepository r) => new(
        "taras", "Seleccionar tara", "Buscar tara...",
        (t, p, s, ct) => r.GetTarasAsync(t, p, s, ct),
        MostrarEstado: false);   // tara no tiene columna de estado

    public static CatalogoConfig Categorias(ICatalogoRepository r) => new(
        "categorias", "Seleccionar categoría", "Buscar categoría...",
        (t, p, s, ct) => r.GetCategoriasAsync(t, p, s, ct));

    /// <summary>
    /// Unidades, opcionalmente acotadas a una categoría (masa, volumen, conteo).
    /// Mismo patrón de acotamiento que <see cref="Fabricantes"/>. Sin acotar es lo
    /// que usa el combo de contenido (puede ser masa o volumen); acotada a Masa es
    /// lo que va a usar el día que exista un editor de tara.
    /// </summary>
    public static CatalogoConfig Unidades(ICatalogoRepository r, int? idTipoUnidad = null) => new(
        idTipoUnidad is null ? "unidades" : $"unidades:{idTipoUnidad}",
        "Seleccionar unidad", "Buscar unidad...",
        (t, p, s, ct) => r.GetUnidadesAsync(t, p, s, idTipoUnidad, ct));

    public static CatalogoConfig Paises(ICatalogoRepository r) => new(
        "paises", "Seleccionar país", "Buscar país...",
        (t, p, s, ct) => r.GetPaisesAsync(t, p, s, ct),
        MostrarEstado: false);

    public static CatalogoConfig Proveedores(ICatalogoRepository r) => new(
        "proveedores", "Seleccionar proveedor", "Buscar proveedor...",
        (t, p, s, ct) => r.GetProveedoresAsync(t, p, s, ct),
        MostrarDescripcion: true, TituloDescripcion: "RTN");

    /// <summary>
    /// Productos, opcionalmente acotados a un proveedor — mismo patrón de
    /// encadenamiento que <see cref="Fabricantes"/>. El alcance viaja en el
    /// closure, no en la firma uniforme de <c>Cargar</c>.
    /// </summary>
    public static CatalogoConfig Productos(
        ICatalogoRepository r, int? idProveedor = null,
        bool permiteMultiple = false, Func<int?, bool>? estaYaElegido = null) => new(
        idProveedor is null ? "productos" : $"productos:{idProveedor}",
        "Seleccionar producto", "Buscar por ID, código o nombre...",
        (t, p, s, ct) => r.GetProductosAsync(t, p, s, idProveedor, ct),
        TituloDescripcion: "Código",
        DescripcionPrimero: true,
        PermiteMultiple: permiteMultiple,
        EstaYaElegido: estaYaElegido);

    /// <summary>
    /// Fabricantes, opcionalmente acotados a un proveedor. Único lugar donde vive
    /// la regla del encadenamiento: la vista y el modal llaman ambos acá, así que
    /// no puede divergir. El alcance viaja en el closure, no en la firma.
    /// </summary>
    public static CatalogoConfig Fabricantes(ICatalogoRepository r, int? idProveedor = null) => new(
        idProveedor is null ? "fabricantes" : $"fabricantes:{idProveedor}",
        "Seleccionar fabricante", "Buscar fabricante...",
        (t, p, s, ct) => r.GetFabricantesAsync(t, p, s, idProveedor, ct));
}
