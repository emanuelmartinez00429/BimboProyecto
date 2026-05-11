using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    [Table("tarima")]
    public class Tarima : BaseModel
    {
        [PrimaryKey("id_tarima")]
        public int idTarima { get; set; }

        [Column("peso_tarima")]
        public decimal pesoTarima { get; set; }

        [Column("tipo_tarima")]
        public string? tipoTarima { get; set; }

        [Column("estado_fisico_tarima")]
        public string? estadoFisicoTarima { get; set; }

        [Column("descripcion_tarima")]
        public string? descripcionTarima { get; set; }

        public string displayTarima => $"{tipoTarima ?? "Tarima"} — {pesoTarima:F2} kg";
    }
}
