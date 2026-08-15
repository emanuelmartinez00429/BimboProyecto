using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Productos;

/// <summary>
/// Modelo completo para CRUD directo sobre la tabla presentacion_producto.
/// Hereda BaseModel para poder usar client.From&lt;PresentacionCrud&gt;().
///
/// Distinto de Presentacion (join-only, sin BaseModel — lo usa Productos.cs) y
/// PresentacionConsulta (lectura de catálogo para la lupa). Mismo trío que
/// Fabricante / FabricanteConsulta / FabricanteCrud.
/// </summary>
[Table("presentacion_producto")]
public class PresentacionCrud : BaseModel
{
    [PrimaryKey("id_presentacion")]
    public int idPresentacion { get; set; }

    /// <summary>NOT NULL y UNIQUE en la base (constraint agregada el 2026-08-15).</summary>
    [Column("nombre_presentacion")]
    public string nombrePresentacion { get; set; } = "";

    [Column("descripcion_presentacion")]
    public string? descripcionPresentacion { get; set; }

    [Column("id_estado")]
    public int idEstado { get; set; }

    // Los pone la base: created_at por DEFAULT, updated_at por el trigger
    // trg_presentacion_updated_at. Van con ignoreOnInsert/ignoreOnUpdate porque
    // si no el SDK las serializa (en null al crear) y pisaría ambos.
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? createdAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? updatedAt { get; set; }
}
