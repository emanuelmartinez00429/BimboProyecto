using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public string nombreRol { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }
    }
}
