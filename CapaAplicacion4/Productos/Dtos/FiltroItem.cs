namespace CapaAplicacion.Productos.Dtos;

public class FiltroItem
{
    public int?   Id     { get; init; }
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Texto secundario del catálogo (descripción, abreviatura, peso…). Opcional.</summary>
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Estado del registro. Null en los catálogos que no tienen columna de estado.</summary>
    public bool? Activo { get; init; }

    /// <summary>
    /// Id del registro padre, para catálogos encadenados (fabricante → id_proveedor).
    /// Permite acotar un catálogo hijo en memoria sin consultar de nuevo.
    /// </summary>
    public int? IdPadre { get; init; }

    public override string ToString() => Nombre;
}
