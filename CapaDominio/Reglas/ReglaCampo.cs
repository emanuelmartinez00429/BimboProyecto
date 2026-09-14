namespace CapaDominio.Reglas;

/// <summary>
/// Formatos que el negocio reconoce para un campo de texto.
/// </summary>
/// <remarks>
/// El dominio declara <b>qué</b> formato tiene que cumplir el dato; cómo se
/// convierte el texto que tipeó una persona es otra cosa. Por eso
/// <see cref="Decimal"/> y <see cref="Entero"/> están acá como declaración,
/// pero el parseo en sí vive en la capa de presentación: depende del
/// <c>CultureInfo</c> del usuario (si escribe "1,5" o "1.5"), y eso no es
/// conocimiento de negocio.
/// </remarks>
public enum FormatoCampo
{
    Ninguno,
    Correo,
    Rtn,
    Telefono,
    Decimal,
    Entero,
}

/// <summary>
/// Qué exige el negocio de un campo: si es obligatorio, sus límites de largo y
/// qué formato tiene que cumplir.
/// </summary>
/// <remarks>
/// <para>
/// Esto es un <b>dato</b>, no un comportamiento: describe la regla sin
/// ejecutarla y sin saber nada de pantallas. Que el nombre de un producto sea
/// obligatorio y tope en 150 caracteres es una decisión del negocio y del
/// esquema — antes estaba escrita dentro de cada modal, o sea que la capa de UI
/// era la que decidía.
/// </para>
/// <para>
/// Quien lo consume decide qué hacer con el incumplimiento: la UI pinta el campo
/// y avisa, un repositorio podría rechazar antes de viajar a la red.
/// </para>
/// </remarks>
public sealed record ReglaCampo(
    bool         Obligatorio = false,
    int?         LargoMaximo = null,
    int?         LargoMinimo = null,
    FormatoCampo Formato     = FormatoCampo.Ninguno,
    decimal?     Minimo      = null,
    decimal?     Maximo      = null,
    int?         Decimales   = null);
