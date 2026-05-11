using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    [Table("proveedores")]
    public class Proveedores : BaseModel
    {
        [PrimaryKey("id_proveedor")]
        public int idProveedor { get; set; }

        [Column("nombre_proveedor")]
        public string nombreProveedor { get; set; } = "";

        [Column("rtn_proveedor")]
        public string? rtnProveedor { get; set; }

        [Column("telefono_proveedor")]
        public string? telefonoProveedor { get; set; }

        [Column("correo_proveedor")]
        public string? correoProveedor { get; set; }

        [Column("direccion_proveedor")]
        public string? direccionProveedor { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }
    }
}
