using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    [Table("categoria")]
    public class Categoria : BaseModel
    {
        [PrimaryKey("id_categoria")]
        public int idCategoria { get; set; }
        [Column("nombre_categoria")]
        public string nombreCategoria { get; set; } = string.Empty;
        [Column("descripcion_categoria")]
        public string descripcionCategoria { get; set; } = string.Empty;

        [Column("estado_categoria")]
        public bool estadoCategoria { get; set; }
    }
}
