using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    /// <summary>
    /// Lectura ligera del peso teórico y la tara de un producto
    /// (productos.peso_teorico + productos.id_tara → tara.peso_tara_envalaje).
    /// Alimenta la previsualización del modal de pesaje y el cálculo de bultos teóricos.
    /// La tara efectiva del neto GUARDADO la aplica el trigger `calcular_pesos_entrada`.
    /// </summary>
    [Table("productos")]
    public class ProductoTaraConsulta : BaseModel
    {
        [PrimaryKey("id_producto", false)]
        public int idProducto { get; set; }

        /// <summary>Peso unitario declarado del producto (sin empaque).</summary>
        [Column("peso_teorico")]
        public decimal? pesoTeorico { get; set; }

        [JsonProperty("tara", NullValueHandling = NullValueHandling.Ignore)]
        public Tara? tara { get; set; }

        [JsonIgnore]
        public decimal taraUnitaria => tara?.pesoTaraEnvalaje ?? 0m;
    }
}
