using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    [Table("paises")]
   public class Paises : BaseModel
    {
        [PrimaryKey("id_pais")]
        public int idPais { get; set; }
        
        [Column("nombre_pais")]
        public string nombrePais { get; set; } = string.Empty;
        
        [Column("prefijo_pais")]
        public string prefijoPais { get; set; } = string.Empty;

        [Column("region")]
        public string region { get; set; } = string.Empty;
    }
}
