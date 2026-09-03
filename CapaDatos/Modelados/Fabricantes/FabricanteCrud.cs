using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Fabricantes;

/// <summary>
/// Modelo completo para CRUD directo sobre la tabla fabricante.
/// Hereda BaseModel para poder usar client.From&lt;FabricanteCrud&gt;().
///
/// Distinto de Fabricante (join-only, sin BaseModel) y
/// FabricanteConsulta (lectura de catálogo para combos).
/// </summary>
[Table("fabricante")]
public class FabricanteCrud : BaseModel
{
    [PrimaryKey("id_fabricante")]
    public int idFabricante { get; set; }

    [Column("nombre_fabricante")]
    public string nombreFabricante { get; set; } = "";

    [Column("descripcion_fabricante")]
    public string? descripcionFabricante { get; set; }

    [Column("id_proveedor")]
    public int? idProveedor { get; set; }

    [Column("id_pais")]
    public int? idPais { get; set; }

    [Column("id_estado")]
    public int idEstado { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? createdAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? updatedAt { get; set; }
}
