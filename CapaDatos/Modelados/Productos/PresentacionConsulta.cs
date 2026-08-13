using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    /// <summary>
    /// Modelo ligero para leer el catálogo de presentaciones directamente.
    /// Hereda BaseModel para poder usar client.From&lt;PresentacionConsulta&gt;().
    ///
    /// No reemplaza a Presentacion (join-only dentro de productos); mismo
    /// criterio que FabricanteConsulta vs. Fabricante.
    /// </summary>
    [Table("presentacion_producto")]
    public class PresentacionConsulta : BaseModel
    {
        [PrimaryKey("id_presentacion")]
        public int idPresentacion { get; set; }

        [Column("nombre_presentacion")]
        public string nombrePresentacion { get; set; } = "";

        [Column("descripcion_presentacion")]
        public string? descripcionPresentacion { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }
    }
}
