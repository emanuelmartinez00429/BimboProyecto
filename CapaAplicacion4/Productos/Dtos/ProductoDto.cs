namespace CapaAplicacion.Productos.Dtos;

public class ProductoDto
{
    public int    Id             { get; init; }
    public string CodigoInterno  { get; init; } = string.Empty;
    public string Nombre         { get; init; } = string.Empty;
    /// <summary>Numérico puro (antes texto libre tipo "20 kg" — la unidad ahora es
    /// <see cref="Unidad"/>, un campo estructurado aparte).</summary>
    public decimal? Contenido    { get; init; }
    public string Presentacion   { get; init; } = string.Empty;
    public string Fabricante     { get; init; } = string.Empty;
    public string Proveedor      { get; init; } = string.Empty;
    public string Categoria      { get; init; } = string.Empty;
    public string Pais           { get; init; } = string.Empty;
    public int    IdEstado       { get; init; }
    /// <summary>Nullable porque la columna lo es en la base — hay productos sin
    /// fabricante/presentación/categoría/país cargados.</summary>
    public int?   IdFabricante   { get; init; }
    /// <summary>Proveedor del fabricante. Es el que acota la lupa de fabricantes
    /// al abrir el modal: sin este dato el selector arranca sin alcance y lista
    /// todos los fabricantes, no los del proveedor que muestra el formulario.</summary>
    public int?   IdProveedor    { get; init; }
    public int?   IdCategoria    { get; init; }
    public int?   IdPais         { get; init; }
    public int?   IdPresentacion { get; init; }
    public decimal? PesoTeorico  { get; init; }
    /// <summary>Peso de la tara en kg. Antes era <c>IdTara</c>, una FK a un catálogo
    /// compartido (tabla <c>tara</c>) — ahora es un número propio de este producto.</summary>
    public decimal? PesoTara     { get; init; }
    /// <summary>Unidad de <see cref="Contenido"/> (FK a unidad_medida). Antes vivía
    /// solo como sufijo de texto dentro de Contenido, mezclado con el número; ahora
    /// Contenido es puramente numérico y esta es la unidad estructurada aparte.</summary>
    public int?     IdUnidad     { get; init; }
    public string   Unidad       { get; init; } = string.Empty;
    public decimal? PrecioPorKg  { get; init; }
    public DateTime? CreatedAt   { get; init; }
    public DateTime? UpdatedAt   { get; init; }
}
