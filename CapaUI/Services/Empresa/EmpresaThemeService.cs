using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace CapaUI.Services.Empresa;

public sealed class EmpresaThemeService
{
    public const string ColorPredeterminado = "#1E3A8A";

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BimboPesaje",
        "TemaEmpresa",
        "color.txt");

    public string ColorActual { get; private set; } = ColorPredeterminado;

    public void CargarCacheSinRed()
    {
        try
        {
            if (File.Exists(CachePath))
                Aplicar(File.ReadAllText(CachePath), persistir: false);
            else
                Aplicar(null, persistir: false);
        }
        catch
        {
            Aplicar(null, persistir: false);
        }
    }

    public void Aplicar(string? valor, bool persistir = true)
    {
        var primary = Parsear(valor) ?? Parsear(ColorPredeterminado)!.Value;
        ColorActual = $"#{primary.R:X2}{primary.G:X2}{primary.B:X2}";

        var resources = Application.Current.Resources;
        Establecer(resources, "EmpresaPrimary", primary);
        Establecer(resources, "EmpresaPrimaryDark", Mezclar(primary, Colors.Black, 0.18));
        Establecer(resources, "EmpresaPrimaryDarker", Mezclar(primary, Colors.Black, 0.32));
        Establecer(resources, "EmpresaPrimaryBright", Mezclar(primary, Colors.White, 0.10));
        Establecer(resources, "EmpresaPrimaryLight", Mezclar(primary, Colors.White, 0.25));
        Establecer(resources, "EmpresaPrimaryLighter", Mezclar(primary, Colors.White, 0.42));

        if (!persistir) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, ColorActual);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "No se pudo persistir el color de empresa");
        }
    }

    public static bool EsColorValido(string? valor) => Parsear(valor).HasValue;

    public SolidColorBrush ObtenerBrushPrincipal()
    {
        if (Application.Current.Resources["EmpresaPrimaryBrush"] is SolidColorBrush brush)
            return brush;
        return new SolidColorBrush(Parsear(ColorPredeterminado)!.Value);
    }

    public static Color ObtenerColorPrincipalActual() =>
        Application.Current.Resources["EmpresaPrimaryColor"] is Color color
            ? color
            : Parsear(ColorPredeterminado)!.Value;

    public static SolidColorBrush ObtenerBrushPrincipalActual()
    {
        if (Application.Current.Resources["EmpresaPrimaryBrush"] is SolidColorBrush brush)
            return brush;
        return new SolidColorBrush(ObtenerColorPrincipalActual());
    }

    private static void Establecer(ResourceDictionary resources, string nombre, Color color)
    {
        resources[$"{nombre}Color"] = color;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[$"{nombre}Brush"] = brush;
    }

    private static Color? Parsear(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) ||
            valor.Equals("sin_color", StringComparison.OrdinalIgnoreCase))
            return null;

        var texto = valor.Trim();
        if (texto.Length == 6) texto = "#" + texto;
        if (texto.Length != 7 || texto[0] != '#') return null;

        return byte.TryParse(texto.AsSpan(1, 2), NumberStyles.HexNumber, null, out var r) &&
               byte.TryParse(texto.AsSpan(3, 2), NumberStyles.HexNumber, null, out var g) &&
               byte.TryParse(texto.AsSpan(5, 2), NumberStyles.HexNumber, null, out var b)
            ? Color.FromRgb(r, g, b)
            : null;
    }

    private static Color Mezclar(Color origen, Color destino, double proporcion)
    {
        static byte Canal(byte a, byte b, double p) =>
            (byte)Math.Clamp(Math.Round(a + ((b - a) * p)), 0, 255);

        return Color.FromRgb(
            Canal(origen.R, destino.R, proporcion),
            Canal(origen.G, destino.G, proporcion),
            Canal(origen.B, destino.B, proporcion));
    }
}
