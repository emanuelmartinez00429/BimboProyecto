using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    [Table("movimientos")]
    public class Movimiento : BaseModel
    {
        [PrimaryKey("id_movimiento", false)]
        public int idMovimiento { get; set; }

        [Column("id_proveedor")]
        public int idProveedor { get; set; }

        [Column("placa_vehiculo")]
        public string? placaVehiculo { get; set; }

        [Column("fecha_asignacion")]
        public DateOnly fechaAsignacion { get; set; }

        [Column("id_usuario")]
        public int idUsuario { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        [Column("observaciones")]
        public string? observaciones { get; set; }

        /// <summary>
        /// LEGADO — tara extra total del camión en el flujo anterior, que la app prorrateaba
        /// entre los bultos declarados al insertar cada entrada.
        /// <para/>
        /// Ya NO se escribe: la tara extra se pesa y se guarda por entrada
        /// (<c>entradas_producto.peso_tara_extra</c>), y el total es la suma de esas filas.
        /// Se conserva de solo lectura para no perder el dato de los camiones viejos.
        /// </summary>
        [Column("peso_tara_extra")]
        public decimal pesoTaraExtra { get; set; }

        // Navigation — se carga con Select("*, proveedores(*)")
        // JsonProperty mapea la clave JSON de PostgREST y NullValueHandling.Ignore
        // evita que se incluya en el INSERT cuando es null
        [JsonProperty("proveedores", NullValueHandling = NullValueHandling.Ignore)]
        public Proveedores? proveedor { get; set; }

        // Excluida del INSERT — calculada en memoria
        [JsonIgnore]
        public string nombreProveedor => proveedor?.nombreProveedor ?? "";
    }
}
