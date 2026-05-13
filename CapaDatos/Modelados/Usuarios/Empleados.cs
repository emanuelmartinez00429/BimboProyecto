using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("empleados")]
    public class Empleados : BaseModel
    {
        [PrimaryKey("id_empleado")]
        public int idEmpleado { get; set; }

        [Column("nombre_empleado")]
        public string nombreEmpleado { get; set; }
        
        [Column("apellido_empleado")]
        public string apellidoEmpleado { get; set; }

        [Column("correo_empleado")]
        public string correoEmpleado { get; set; }
        
        [Column("telefono_empleado")]
        public string telefonoEmpleado { get; set; }
        
        [Column("numero_identidad")]
        public string numeroIdentidad { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        
    }
}
