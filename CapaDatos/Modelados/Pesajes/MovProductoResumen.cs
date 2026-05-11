using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Pesajes
{
    /// <summary>
    /// Modelo de solo lectura mapeado a la vista v_mov_productos_resumen.
    /// Contiene los indicadores calculados en tiempo real: bultos restantes, % peso restante, diferencias.
    /// </summary>
    [Table("v_mov_productos_resumen")]
    public class MovProductoResumen : BaseModel
    {
        [PrimaryKey("id_mov_producto", false)]
        public int idMovProducto { get; set; }

        [Column("id_movimiento")]
        public int idMovimiento { get; set; }

        [Column("placa_vehiculo")]
        public string? placaVehiculo { get; set; }

        [Column("nombre_proveedor")]
        public string nombreProveedor { get; set; } = "";

        [Column("nombre_producto")]
        public string nombreProducto { get; set; } = "";

        [Column("codigo_producto")]
        public string codigoProducto { get; set; } = "";

        [Column("peso_manifestado")]
        public decimal pesoManifestado { get; set; }

        [Column("bultos_teoricos")]
        public int bultosTeóricos { get; set; }

        [Column("peso_recibido")]
        public decimal pesoRecibido { get; set; }

        [Column("bultos_recibidos")]
        public decimal bultosRecibidos { get; set; }

        // Indicadores en tiempo real
        [Column("bultos_restantes")]
        public decimal bultosRestantes { get; set; }

        [Column("pct_peso_restante")]
        public decimal pctPesoRestante { get; set; }

        // Diferencias al cierre
        [Column("diferencia_kg")]
        public decimal diferenciaKg { get; set; }

        [Column("diferencia_pct")]
        public decimal diferenciaPct { get; set; }

        [Column("diferencia_usd")]
        public decimal diferenciaUsd { get; set; }

        [Column("total_entradas")]
        public int totalEntradas { get; set; }

        [Column("estado")]
        public string estado { get; set; } = "";

        public bool estaAbierto => estado == "Abierto";
    }
}
