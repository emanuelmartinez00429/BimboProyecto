namespace CapaAplicacion.Productos.Dtos;

public class ProductoDto
{
    public int    Id             { get; init; }
    public string CodigoInterno  { get; init; } = string.Empty;
    public string Nombre         { get; init; } = string.Empty;
    public string Contenido      { get; init; } = string.Empty;
    public string Presentacion   { get; init; } = string.Empty;
    public string Fabricante     { get; init; } = string.Empty;
    public string Proveedor      { get; init; } = string.Empty;
    public string Categoria      { get; init; } = string.Empty;
    public string Pais           { get; init; } = string.Empty;
    public int    IdEstado       { get; init; }
    public int    IdFabricante   { get; init; }
    /// <summary>Proveedor del fabricante. Es el que acota la lupa de fabricantes
    /// al abrir el modal: sin este dato el selector arranca sin alcance y lista
    /// todos los fabricantes, no los del proveedor que muestra el formulario.</summary>
    public int?   IdProveedor    { get; init; }
    public int    IdCategoria    { get; init; }
    public int    IdPais         { get; init; }
    public int    IdPresentacion { get; init; }
    public decimal? PesoTeorico  { get; init; }
    public int?     IdTara       { get; init; }
    public string   Tara         { get; init; } = string.Empty;
    public decimal? PrecioPorKg  { get; init; }
    public DateTime? CreatedAt   { get; init; }
    public DateTime? UpdatedAt   { get; init; }
}
