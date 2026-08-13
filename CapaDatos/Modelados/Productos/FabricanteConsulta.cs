using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    /// <summary>
    /// Modelo ligero para leer el catálogo de fabricantes directamente.
    /// Hereda BaseModel para poder usar client.From&lt;FabricanteConsulta&gt;().
    ///
    /// No reemplaza a Fabricante (que se carga vía join en productos).
    /// Se usa para poblar el combo de filtros y el selector de catálogo.
    /// </summary>
    [Table("fabricante")]
    public class FabricanteConsulta : BaseModel
    {
        [PrimaryKey("id_fabricante")]
        public int idFabricante { get; set; }

        [Column("nombre_fabricante")]
        public string nombreFabricante { get; set; } = "";

        [Column("descripcion_fabricante")]
        public string? descripcionFabricante { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        /// <summary>Padre del catálogo encadenado proveedor → fabricante.</summary>
        [Column("id_proveedor")]
        public int? idProveedor { get; set; }
    }
}
