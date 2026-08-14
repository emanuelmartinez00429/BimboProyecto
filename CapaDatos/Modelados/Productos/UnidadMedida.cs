using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos
{
    /// <summary>
    /// Catálogo de unidades de medida. Cada fila pertenece a una categoría
    /// (<see cref="TipoUnidad"/>: Masa, Volumen, Conteo) — eso es lo que permite
    /// que un combo pida solo las unidades de masa (ej. para tara) sin listar
    /// mililitros ni litros.
    /// </summary>
    [Table("unidad_medida")]
    public class UnidadMedida : BaseModel
    {
        [PrimaryKey("id_unidad")]
        public int idUnidad { get; set; }

        [Column("nombre_unidad")]
        public string nombreUnidad { get; set; } = "";

        [Column("abreviatura")]
        public string? abreviatura { get; set; }

        [Column("id_tipo_unidad")]
        public int idTipoUnidad { get; set; }

        /// <summary>
        /// A la unidad base de su categoría. Sin uso todavía — queda listo para
        /// cuando haga falta convertir entre unidades de la misma categoría.
        /// </summary>
        [Column("factor_conversion")]
        public decimal? factorConversion { get; set; }

        [Column("id_estado")]
        public int idEstado { get; set; }

        /// <summary>Navegación poblada solo en las consultas que la piden con join.</summary>
        public TipoUnidad? tipo_unidad { get; set; }
    }
}
