using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Contactos;

[Table("contactos_fabricante")]
public class ContactoFabricanteModel : BaseModel
{
    [PrimaryKey("id_contacto_fabricante")]
    public int idContactoFabricante { get; set; }

    [Column("id_fabricante")]
    public int idFabricante { get; set; }

    [Column("nombre_contacto")]
    public string nombreContacto { get; set; } = "";

    [Column("telefono_contacto")]
    public string? telefonoContacto { get; set; }

    [Column("correo_contacto")]
    public string? correoContacto { get; set; }

    [Column("id_estado")]
    public int idEstado { get; set; }
}
