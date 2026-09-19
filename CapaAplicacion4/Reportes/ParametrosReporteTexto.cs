using System.Collections;
using System.Globalization;

namespace CapaAplicacion.Reportes;

/// <summary>Resumen textual de los parámetros empleados para generar un reporte.</summary>
public static class ParametrosReporteTexto
{
    public static string Crear(params (string Etiqueta, object? Valor)[] campos) =>
        string.Join("; ", campos.Select(c => $"{c.Etiqueta}: {Valor(c.Valor)}"));

    private static string Valor(object? valor) => valor switch
    {
        null => "Sin dato",
        string texto => texto,
        bool booleano => booleano ? "Sí" : "No",
        DateTime fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IEnumerable lista => string.Join(", ", lista.Cast<object?>().Select(Valor)),
        IFormattable numero => numero.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? "Sin dato"
    };
}
