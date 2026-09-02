using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("roles")]
    public class Roles : BaseModel
    {
        [PrimaryKey("id_rol")]
        public int idRol { get; set; }

        [Column("nombre_rol")]
        public string nombreRol { get; set; } = string.Empty;

        [Column("id_estado")]
        public int idEstado { get; set; } = 1;

        [Column("es_sistema")]
        public bool esSistema { get; set; }

        [Column("created_at")]
        public DateTime? createdAt { get; set; }

        [Column("updated_at")]
        public DateTime? updatedAt { get; set; }
    }
}
