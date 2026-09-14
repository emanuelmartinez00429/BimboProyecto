using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    [Table("productos")]
    public class ProductosInsertar : BaseModel
    {
        [PrimaryKey("id_producto")]
        public int idProducto { get; set; }
        [Column("codigo_producto")]
        public string codigoProducto { get; set; } = string.Empty;
        [Column("nombre_producto")]
        public string nombreProducto { get; set; } = string.Empty;
        [Column("id_presentacion")]
        public int? idPresentacion { get; set; }
        [Column("id_fabricante")]
        public int? idFabricante { get; set; }
        [Column("id_estado")]
        public int idEstado { get; set; }
        [Column("peso_tara")]
        public decimal? pesoTara { get; set; }
        [Column("id_categoria")]
        public int? idCategoria { get; set; }
        [Column("contenido")]
        public decimal? contenidoProducto { get; set; }

        [Column("id_pais")]
        public int? idPais { get; set; }
        [Column("peso_teorico")]
        public decimal? pesoTeorico { get; set; }

    }
}
