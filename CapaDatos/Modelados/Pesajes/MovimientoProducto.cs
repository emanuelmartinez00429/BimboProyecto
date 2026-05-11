using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using ProdModel = CapaDatos.Modelados.Productos.Productos;

namespace CapaDatos.Modelados.Pesajes
{
    [Table("movimiento_productos")]
    public class MovimientoProducto : BaseModel
    {
        [PrimaryKey("id_mov_producto", false)]
        public int idMovProducto { get; set; }

        [Column("id_movimiento")]
        public int idMovimiento { get; set; }

        [Column("id_producto")]
        public int idProducto { get; set; }

        [Column("peso_manifestado")]
        public decimal pesoManifestado { get; set; }

        [Column("bultos_teoricos")]
        public int bultosTeóricos { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        [Column("observaciones")]
        public string? observaciones { get; set; }

        // Navigation — se carga con Select("*, productos(*)")
        [JsonProperty("productos", NullValueHandling = NullValueHandling.Ignore)]
        public ProdModel? producto { get; set; }

        [JsonIgnore]
        public string nombreProducto => producto?.nombreProducto ?? "";

        [JsonIgnore]
        public string codigoProducto => producto?.codigoProducto ?? "";
    }
}
