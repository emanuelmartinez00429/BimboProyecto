using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados
{
    [Table("empresa")]
    public class Empresa : BaseModel
    {
        [PrimaryKey("id_empresa", false)]
        public int IdEmpresa { get; set; }

        [Column("nombre_empresa")]
        public string NombreEmpresa { get; set; } = string.Empty;

        [Column("rtn_empresa")]
        public string? RtnEmpresa { get; set; }

        [Column("direccion_empresa")]
        public string? DireccionEmpresa { get; set; }

        [Column("telefono_empresa")]
        public string? TelefonoEmpresa { get; set; }

        [Column("correo_empresa")]
        public string? CorreoEmpresa { get; set; }

        /// <summary>
        /// Ruta del logo dentro del bucket "empresa-logos" de Supabase Storage.
        /// Ejemplo: "logo_empresa_1.png"
        /// </summary>
        [Column("logo_empresa")]
        public string? LogoEmpresa { get; set; }

        [Column("dominio_correo")]
        public string? DominioCorreo { get; set; }
    }
}
