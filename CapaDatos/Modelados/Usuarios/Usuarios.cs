using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("usuarios")]
    public class Usuarios : BaseModel
    {
        [PrimaryKey("id_usuario")]
        public int idUsuario { get; set; }
        
        [Column("alias_usuario")]
        public string aliasUsuario { get; set; }
        
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


    }
}
