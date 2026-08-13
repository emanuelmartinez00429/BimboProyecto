using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using CapaDatos.Modelados.Pesajes;

namespace CapaDatos.Modelados.Productos
{
    [Table("fabricante")]
    public class Fabricante
    {
        [PrimaryKey("id_fabricante")]
        public int idFabricante { get; set; }

        [Column("nombre_fabricante")]
        public string nombreFabricante { get; set; }

        [Column("id_proveedor")]
        public int? idProveedor { get; set; }

        public Proveedores? Proveedores { get; set; }
    }
}
