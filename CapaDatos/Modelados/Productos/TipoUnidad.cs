using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    /// <summary>
    /// Categoría de <see cref="UnidadMedida"/> (Masa, Volumen, Conteo, ...). Tabla
    /// propia y no una columna de texto libre: agregar una categoría nueva el día
    /// de mañana (ej. "Longitud") es una fila, no una migración.
    /// </summary>
    [Table("tipo_unidad")]
    public class TipoUnidad : BaseModel
    {
        [PrimaryKey("id_tipo_unidad")]
        public int idTipoUnidad { get; set; }

        [Column("nombre_tipo_unidad")]
        public string nombreTipoUnidad { get; set; } = "";
    }
}
