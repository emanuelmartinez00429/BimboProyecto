using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDatos.Modelados.Usuarios
{
    internal class usuarioVista : BaseModel
    {
        [PrimaryKey("id_usuario")]
        public int idUsuario { get; set; }

        [Column("alias_usuarios")]
        public string correoUsuario { get; set; }

        [Column("id_empleado")]
        public int idEmpleado { get; set; }

        [Column("id_rol")]
        public int idRol { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        [Column("uuid_usuario")]
        public string? uuidUsuario { get; set; }

        [Column("ultimo_acceso")]
        public DateTime ultimoAcceso { get; set; }

        public Roles roles { get; set; }

        public string nombreRol => roles?.nombreRol ?? "Sin rol";
    }
}
