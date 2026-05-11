using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    [Table("tara")]
    public class Tara : BaseModel
    {
        [PrimaryKey("id_tara")]
        public int idTara { get; set; }

        [Column("peso_tara_envalaje")]
        public decimal pesoTaraEnvalaje { get; set; }

        [Column("descripcion_tara")]
        public string? descripcionTara { get; set; }
    }
}
