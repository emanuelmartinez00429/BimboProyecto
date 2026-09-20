using Newtonsoft.Json.Linq;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CapaDatos.Modelados.Preferencias;

/// <summary>
/// Modelo de <c>public.usuario_preferencias</c>: preferencias personales por usuario.
/// <para/>
/// La PK es compuesta y <b>natural</b> (no hay secuencia detrás), así que los tres
/// campos van con <c>shouldInsert: true</c> — al revés que las tablas con
/// <c>id_xxx</c> serial, donde el valor lo genera la base.
/// </summary>
[Table("usuario_preferencias")]
public class UsuarioPreferencia : BaseModel
{
    [PrimaryKey("id_usuario", true)]
    public int idUsuario { get; set; }

    [PrimaryKey("clave", true)]
    public string clave { get; set; } = "";

    [PrimaryKey("ambito", true)]
    public string ambito { get; set; } = "global";

    /// <summary>
    /// Columna <c>jsonb</c>. Es <see cref="JToken"/> y no <see cref="string"/> porque
    /// una cadena de C# se serializaría como cadena JSON: el número <c>0.8</c> llegaría
    /// a la base como <c>"0.8"</c> (jsonb de tipo string) en vez de como número.
    /// </summary>
    [Column("valor")]
    public JToken valor { get; set; } = JValue.CreateNull();

    // Los pone la base: created_at por DEFAULT y updated_at por el trigger
    // trg_usuario_preferencias_updated_at. Van con ignoreOnInsert/ignoreOnUpdate para
    // que el cliente no los pise con la hora local del equipo.
    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? createdAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? updatedAt { get; set; }
}
