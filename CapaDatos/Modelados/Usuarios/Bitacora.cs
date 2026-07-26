using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Usuarios
{
    [Table("bitacora")]
    public class Bitacora : BaseModel
    {
        [PrimaryKey("id_bitacora")]
        public int idBitacora { get; set; }

        [Column("id_usuario")]
        public int idUsuario { get; set; }

        [Column("id_accion")]
        public int? idAccion { get; set; }

        [Column("id_modulo")]
        public int? idModulo { get; set; }

        [Column("estado_anterior")]
        public string estadoAnterior { get; set; } = string.Empty;

        [Column("estado_actual")]
        public string estadoActual { get; set; } = string.Empty;

        [Column("campo_afectado")]
        public string campoAfectado { get; set; } = string.Empty;

        [Column("campo_extra")]
        public string? campoExtra { get; set; }

        [Column("tabla_afectada")]
        public string? tablaAfectada { get; set; }

        [Column("id_registro_afectado")]
        public int? idRegistroAfectado { get; set; }

        [Column("fecha_hora")]
        public DateTime? fechaHora { get; set; }

        // Embeds — postgrest-csharp los resuelve por FK + nombre, sin [Reference].
        // Nullable: id_accion/id_modulo son nullable en BD y el Select sin embeds
        // (ej. el de conteo) los deja en null.
        public Usuarios? usuarios { get; set; }
        public Accion?   acciones { get; set; }
        public Modulo?   modulos  { get; set; }
    }
}
