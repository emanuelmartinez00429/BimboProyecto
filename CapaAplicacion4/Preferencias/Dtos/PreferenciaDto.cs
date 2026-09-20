using System.Globalization;
using System.Text.Json;

namespace CapaAplicacion.Preferencias.Dtos;

/// <summary>
/// Una fila de <c>public.usuario_preferencias</c>.
/// <para/>
/// <see cref="ValorJson"/> es el JSON crudo de la columna <c>jsonb</c>, no un valor ya
/// tipado: la tabla es clave/valor justamente para que sumar una preferencia no
/// necesite migración ni un DTO nuevo. Para leerlo y escribirlo está
/// <see cref="ValorPreferencia"/>, que encapsula el parseo con cultura invariante.
/// </summary>
public sealed record PreferenciaDto(string Clave, string Ambito, string ValorJson);

/// <summary>
/// Lectura y escritura del JSON de una preferencia.
/// <para/>
/// Todo pasa por acá para que el parseo numérico no dependa de la cultura del equipo:
/// JSON usa punto decimal siempre, y con la cultura en español un
/// <c>double.Parse("0.8")</c> sin <see cref="CultureInfo.InvariantCulture"/> devuelve 8.
/// </summary>
public static class ValorPreferencia
{
    // ── Lectura ──────────────────────────────────────────────────────────────

    /// <summary>Número, o <paramref name="alterno"/> si el JSON no es un número válido.</summary>
    public static double Numero(string? valorJson, double alterno)
    {
        if (string.IsNullOrWhiteSpace(valorJson)) return alterno;

        try
        {
            using var doc = JsonDocument.Parse(valorJson);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Number => doc.RootElement.GetDouble(),
                // Tolerancia deliberada: una versión anterior pudo guardar el número
                // como cadena. Se acepta al leer, nunca se escribe así.
                JsonValueKind.String when double.TryParse(
                    doc.RootElement.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var n) => n,
                _ => alterno,
            };
        }
        catch (JsonException)
        {
            return alterno;
        }
    }

    /// <summary>Entero, o <paramref name="alterno"/> si el JSON no es un entero válido.</summary>
    public static int Entero(string? valorJson, int alterno)
    {
        var n = Numero(valorJson, double.NaN);
        return double.IsNaN(n) ? alterno : (int)Math.Round(n);
    }

    /// <summary>Texto, o <paramref name="alterno"/> si el JSON no es una cadena.</summary>
    public static string Texto(string? valorJson, string alterno)
    {
        if (string.IsNullOrWhiteSpace(valorJson)) return alterno;

        try
        {
            using var doc = JsonDocument.Parse(valorJson);
            return doc.RootElement.ValueKind == JsonValueKind.String
                ? doc.RootElement.GetString() ?? alterno
                : alterno;
        }
        catch (JsonException)
        {
            return alterno;
        }
    }

    /// <summary>Booleano, o <paramref name="alterno"/> si el JSON no es <c>true</c>/<c>false</c>.</summary>
    public static bool Booleano(string? valorJson, bool alterno)
    {
        if (string.IsNullOrWhiteSpace(valorJson)) return alterno;

        try
        {
            using var doc = JsonDocument.Parse(valorJson);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.True  => true,
                JsonValueKind.False => false,
                _ => alterno,
            };
        }
        catch (JsonException)
        {
            return alterno;
        }
    }

    // ── Escritura ────────────────────────────────────────────────────────────

    /// <summary>JSON de un número, con punto decimal, para guardar en la columna <c>jsonb</c>.</summary>
    public static string DesdeNumero(double valor) =>
        valor.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>JSON de una cadena, con el escapado que corresponda.</summary>
    public static string DesdeTexto(string valor) =>
        JsonSerializer.Serialize(valor);

    /// <summary>JSON de un booleano.</summary>
    public static string DesdeBooleano(bool valor) => valor ? "true" : "false";
}
