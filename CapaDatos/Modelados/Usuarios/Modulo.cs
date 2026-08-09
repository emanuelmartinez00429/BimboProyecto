using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("modulos")]
    public class Modulo : BaseModel
    {
        [PrimaryKey("id_modulo")]
        public int idModulo { get; set; }

        [Column("nombre_modulo")]
        public string nombreModulo { get; set; } = string.Empty;

        [Column("descripcion_modulo")]
        public string? descripcionModulo { get; set; }

        [Column("created_at")]
        public DateTime? createdAt { get; set; }
    }
}
