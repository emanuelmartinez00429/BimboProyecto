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
public sealed record CatalogoConfig(
    string Clave,
    string Titulo,
    string Placeholder,
    Func<string, int, int, CancellationToken, Task<Result<PagedResult<FiltroItem>>>> Cargar,
    bool MostrarDescripcion = true,
    bool MostrarEstado      = true);

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
        MostrarDescripcion: true);

    public static CatalogoConfig Productos(ICatalogoRepository r) => new(
        "productos", "Seleccionar producto", "Buscar por ID, código o nombre...",
        (t, p, s, ct) => r.GetProductosAsync(t, p, s, ct));

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
