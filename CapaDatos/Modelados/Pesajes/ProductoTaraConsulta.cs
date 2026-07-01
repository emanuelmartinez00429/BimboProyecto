using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    /// <summary>
    /// Lectura ligera de la tara de un producto (productos.id_tara → tara.peso_tara_envalaje),
    /// para la previsualización del modal de pesaje. La tara efectiva del neto la aplica el
    /// trigger `calcular_pesos_entrada` en la BD.
    /// </summary>
    [Table("productos")]
    public class ProductoTaraConsulta : BaseModel
    {
        [PrimaryKey("id_producto", false)]
        public int idProducto { get; set; }

        [JsonProperty("tara", NullValueHandling = NullValueHandling.Ignore)]
        public Tara? tara { get; set; }

        [JsonIgnore]
        public decimal taraUnitaria => tara?.pesoTaraEnvalaje ?? 0m;
    }
}
