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

        // Navigation — se carga con Select("*, proveedores(*)")
        public Proveedores? proveedor { get; set; }

        public string nombreProveedor => proveedor?.nombreProveedor ?? "";
    }
}
