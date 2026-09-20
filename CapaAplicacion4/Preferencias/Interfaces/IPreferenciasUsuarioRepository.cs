using CapaAplicacion.Common;
using CapaAplicacion.Preferencias.Dtos;

namespace CapaAplicacion.Preferencias.Interfaces;

/// <summary>
/// Acceso a las preferencias personales del usuario (<c>public.usuario_preferencias</c>).
/// <para/>
/// A diferencia de las entidades de negocio, la escritura es un <c>upsert</c> directo a
/// la tabla y no pasa por una RPC con idempotencia y auditoría RBAC: el dato es del
/// propio usuario, no toca negocio, y auditar cada ajuste llenaría la bitácora de ruido.
/// Quién puede leer y escribir qué fila lo decide RLS en la base, no este contrato.
/// </summary>
public interface IPreferenciasUsuarioRepository
{
    /// <summary>
    /// Todas las preferencias del usuario en una sola consulta.
    /// <para/>
    /// Se traen juntas a propósito: son pocas filas y la app las necesita de una al
    /// iniciar sesión. Pedir una por clave serían N viajes para ahorrar bytes.
    /// </summary>
    Task<Result<IReadOnlyList<PreferenciaDto>>> ObtenerTodasAsync(
        int idUsuario,
        CancellationToken ct = default);

    /// <summary>
    /// Crea o actualiza una preferencia (<c>upsert</c> sobre la PK
    /// <c>(id_usuario, clave, ambito)</c>).
    /// </summary>
    /// <param name="valorJson">
    /// JSON crudo del valor. Construirlo con <see cref="ValorPreferencia"/>, no a mano:
    /// la cultura del equipo cambiaría el separador decimal.
    /// </param>
    Task<Result> GuardarAsync(
        int idUsuario,
        string clave,
        string ambito,
        string valorJson,
        CancellationToken ct = default);

    /// <summary>
    /// Borra una preferencia. Usado por "Restablecer": sin fila, la app vuelve a su
    /// valor por defecto, que no es lo mismo que guardar el default explícitamente.
    /// </summary>
    Task<Result> EliminarAsync(
        int idUsuario,
        string clave,
        string ambito,
        CancellationToken ct = default);
}
