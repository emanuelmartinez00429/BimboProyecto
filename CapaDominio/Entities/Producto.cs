namespace CapaDominio.Entities;

public class Producto
{
    public int    Id            { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string Nombre        { get; set; } = string.Empty;
    public string Contenido     { get; set; } = string.Empty;
    public string Presentacion  { get; set; } = string.Empty;
    public string Fabricante    { get; set; } = string.Empty;
    public string Categoria     { get; set; } = string.Empty;
    public string Pais          { get; set; } = string.Empty;
    public int    IdEstado      { get; set; }
}
