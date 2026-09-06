using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    [Table("entradas_producto")]
    public class EntradaProducto : BaseModel
    {
        [PrimaryKey("id_pesaje", false)]
        public int idPesaje { get; set; }

        [Column("id_mov_producto")]
        public int? idMovProducto { get; set; }

        [Column("id_producto")]
        public int idProducto { get; set; }

        [Column("peso_bruto")]
        public decimal pesoBruto { get; set; }

        [Column("peso_tara_extra")]
        public decimal? pesoTaraExtra { get; set; }

        // Solo lectura — calculados por trigger en BD
        [Column("peso_tara_individual")]
        public decimal? pesoTaraIndividual { get; set; }

        [Column("peso_tara_total")]
        public decimal? pesoTaraTotal { get; set; }

        [Column("peso_neto")]
        public decimal pesoNeto { get; set; }

        [Column("numero_bultos_recibido")]
        public int? numeroBultosRecibido { get; set; }

        [Column("fecha_entrada")]
        public DateOnly fechaEntrada { get; set; }

        [Column("hora_entrada")]
        public TimeOnly horaEntrada { get; set; }

        [Column("fecha_salida")]
        public DateOnly? fechaSalida { get; set; }

        [Column("hora_salida")]
        public TimeOnly? horaSalida { get; set; }

        [Column("id_usuario")]
        public int idUsuario { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        [Column("id_tara")]
        public int? idTara { get; set; }

        [Column("id_tarima")]
        public int? idTarima { get; set; }

        [Column("observaciones")]
        public string? observaciones { get; set; }
    }
}
