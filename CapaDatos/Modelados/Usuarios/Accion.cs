using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("acciones")]
    public class Accion : BaseModel
    {
        [PrimaryKey("id_accion")]
        public int idAccion { get; set; }

        [Column("nombre_accion")]
        public string nombreAccion { get; set; } = string.Empty;

        [Column("codigo_accion")]
        public string codigoAccion { get; set; } = string.Empty;

        [Column("id_modulo")]
        public int idModulo { get; set; }

        [Column("descripcion_accion")]
        public string? descripcionAccion { get; set; }

        [Column("created_at")]
        public DateTime? createdAt { get; set; }
    }
}
