using Supabase.Postgrest.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    [Table("presentacion_producto")]
    public class Presentacion : BaseModel
    {
        [PrimaryKey("id_presentacion")]
        public int idPresentacion { get; set; }
        [Column("nombre_presentacion")]
        public string nombrePresentacion { get; set; }
        [Column("descripcion_presentacion")]
        public string descripcionPresentacion { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }
    }
}
