using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    /// <summary>
    /// Modelo ligero para leer el catálogo de fabricantes directamente.
    /// Hereda BaseModel para poder usar client.From&lt;FabricanteConsulta&gt;().
    ///
    /// No reemplaza a Fabricante (que se carga vía join en productos).
    /// Solo se usa en GetFabricantesInternal para poblar el combo de filtros.
    /// </summary>
    [Table("fabricante")]
    public class FabricanteConsulta : BaseModel
    {
        [PrimaryKey("id_fabricante")]
        public int idFabricante { get; set; }

        [Column("nombre_fabricante")]
        public string nombreFabricante { get; set; } = "";
    }
}
