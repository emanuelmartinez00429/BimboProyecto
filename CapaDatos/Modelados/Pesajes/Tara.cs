using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using CapaDatos.Modelados.Productos;

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

        /// <summary>
        /// Antes implícito ("kg" por convención del nombre de columna). Ahora es
        /// un dato real, FK a <c>unidad_medida</c>.
        /// </summary>
        [Column("id_unidad")]
        public int idUnidad { get; set; }

        /// <summary>Navegación poblada solo en las consultas que la piden con join.</summary>
        public UnidadMedida? unidad_medida { get; set; }

        public string abreviatura_Unidad => unidad_medida?.abreviatura ?? "kg";
    }
}
