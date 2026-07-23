using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("acciones_roles")]
    public class AccionRol : BaseModel
    {
        [PrimaryKey("id_accion_rol")]
        public int idAccionRol { get; set; }

        [Column("id_accion")]
        public int idAccion { get; set; }

        [Column("id_rol")]
        public int idRol { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }
    }
}
