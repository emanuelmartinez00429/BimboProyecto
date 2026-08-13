using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    /// <summary>
    /// Catálogo de unidades de medida. No tiene columna de estado.
    /// </summary>
    [Table("unidad_medida")]
    public class UnidadMedida : BaseModel
    {
        [PrimaryKey("id_unidad")]
        public int idUnidad { get; set; }

        [Column("nombre_unidad")]
        public string nombreUnidad { get; set; } = "";

        [Column("abreviatura")]
        public string? abreviatura { get; set; }
    }
}
