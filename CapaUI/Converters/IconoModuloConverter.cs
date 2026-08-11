using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CapaUI.Converters;

/// <summary>
/// Traduce la clave de ícono de un módulo a su <see cref="Geometry"/>.
///
/// Las geometrías se parsean una sola vez y quedan congeladas (<c>Freeze</c>),
/// así todas las tarjetas comparten la misma instancia inmutable en vez de
/// alojar una copia por elemento visual.
/// </summary>
[ValueConversion(typeof(string), typeof(Geometry))]
public sealed class IconoModuloConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, Geometry> Iconos = Construir();

    private static IReadOnlyDictionary<string, Geometry> Construir()
    {
        var origen = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gear"] = "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.6 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.6 1.65 1.65 0 0 0 10 3.09V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9c.2.6.75 1 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z",
            ["users"] = "M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2 M9 7a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M23 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75",
            ["box"] = "M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z M3.27 6.96 12 12.01l8.73-5.05 M12 22.08V12",
            ["scale"] = "M12 3v18 M5 21h14 M3 9l4-6 4 6a4 4 0 0 1-8 0z M13 9l4-6 4 6a4 4 0 0 1-8 0z",
            ["truck"] = "M1 3h15v13H1z M16 8h4l3 3v5h-7 M5.5 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M18.5 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4z",
            ["shield"] = "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z",
            ["modulo"] = "M3 3h7v7H3z M14 3h7v7h-7z M14 14h7v7h-7z M3 14h7v7H3z",
        };

        var mapa = new Dictionary<string, Geometry>(origen.Count, StringComparer.Ordinal);
        foreach (var (clave, datos) in origen)
        {
            var geometria = Geometry.Parse(datos);
            geometria.Freeze();
            mapa[clave] = geometria;
        }
        return mapa;
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string clave && Iconos.TryGetValue(clave, out var geometria)
            ? geometria
            : Iconos["modulo"];

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
