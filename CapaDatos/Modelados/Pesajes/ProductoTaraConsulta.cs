using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    /// <summary>
    /// Lectura ligera del peso teórico y la tara de un producto
    /// (productos.peso_teorico + productos.peso_tara). Alimenta la previsualización del
    /// modal de pesaje y el cálculo de bultos teóricos. La tara efectiva del neto GUARDADO
    /// la aplica el trigger `calcular_pesos_entrada`.
    /// </summary>
    [Table("productos")]
    public class ProductoTaraConsulta : BaseModel
    {
        [PrimaryKey("id_producto", false)]
        public int idProducto { get; set; }

        /// <summary>Peso unitario declarado del producto (sin empaque).</summary>
        [Column("peso_teorico")]
        public decimal? pesoTeorico { get; set; }

        /// <summary>Antes venía de un join a un catálogo compartido (id_tara →
        /// tara.peso_tara_envalaje) — ahora es un número propio de este producto.</summary>
        [Column("peso_tara")]
        public decimal? pesoTara { get; set; }

        public decimal taraUnitaria => pesoTara ?? 0m;
    }
}
