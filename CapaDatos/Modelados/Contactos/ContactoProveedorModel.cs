using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Contactos;

[Table("contactos_proveedor")]
public class ContactoProveedorModel : BaseModel
{
    [PrimaryKey("id_contacto_proveedor")]
    public int idContactoProveedor { get; set; }

    [Column("id_proveedor")]
    public int idProveedor { get; set; }

    [Column("nombre_contacto")]
    public string nombreContacto { get; set; } = "";

    [Column("telefono_contacto")]
    public string? telefonoContacto { get; set; }

    [Column("correo_contacto")]
    public string? correoContacto { get; set; }

    [Column("id_estado")]
    public int idEstado { get; set; }
}
