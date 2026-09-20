namespace CapaAplicacion.Preferencias.Interfaces;

/// <summary>
/// Copia local del factor de escala por ámbito, para que la app arranque con la escala
/// correcta <b>sin esperar a la red</b> — el mismo papel que cumple el color cacheado en
/// <c>EmpresaThemeService.CargarCacheSinRed()</c>.
/// <para/>
/// Es aceleración, no verdad: si está vieja, la consulta a Supabase la pisa. Por eso
/// ninguna operación lanza — un archivo corrupto o un disco sin permisos degradan a
/// "sin preferencia", nunca tumban el arranque.
/// <para/>
/// El archivo se nombra por <b>id de usuario</b> y no por cuenta de Windows: en las
/// terminales de planta varios operarios comparten la misma sesión de Windows, así que
/// <c>DataProtectionScope.CurrentUser</c> no los separaría. Tampoco va cifrado — un
/// factor de escala no es un secreto, y quien pueda editar ese archivo ya tiene acceso
/// al equipo. El aislamiento real lo da RLS del lado del servidor.
/// </summary>
public interface ICacheEscalaLocal
{
    /// <summary>
    /// Factores guardados para ese usuario, indexados por ámbito. Diccionario vacío si
    /// no hay archivo o si no se pudo leer.
    /// </summary>
    IReadOnlyDictionary<string, double> Leer(int idUsuario);

    /// <summary>Reemplaza la copia local. Silencioso ante cualquier fallo de disco.</summary>
    void Guardar(int idUsuario, IReadOnlyDictionary<string, double> factoresPorAmbito);
}
