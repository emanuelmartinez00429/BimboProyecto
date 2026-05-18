using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CapaUI.Core.MVVM;

namespace CapaUI.Formularios.Dashboard
{
    public enum MermaTone { Normal, Warning, Critical }

    // ══════════════════════════════════════════════════════════════════════
    //  ViewModel principal
    // ══════════════════════════════════════════════════════════════════════
    public class DashboardVM : ViewModelBase
    {
        // ── Fecha capitalizada ────────────────────────────────────────────
        public string DateLabel { get; }

        // ── KPI Inventario ────────────────────────────────────────────────
        public string ProdCount  { get; } = "1,247";
        public string ProvCount  { get; } = "84";
        public string MarcaCount { get; } = "23";

        // ── KPI Pesajes del día ───────────────────────────────────────────
        public string PesajeCount { get; } = "142";
        public string TotalNeto   { get; } = "38,420";
        public string PctMerma    { get; } = "3.2";

        // ── Período del toggle Hoy / Semana / Mes ─────────────────────────
        private string _periodo = "Hoy";
        public  string  Periodo
        {
            get => _periodo;
            set => Set(ref _periodo, value);
        }
        public ICommand SelectPeriodoCommand { get; }

        // ── Datos de gráfica y lista ──────────────────────────────────────
        public ObservableCollection<MermaItemVM> TopMerma { get; }
        public ObservableCollection<PesajeRowVM> Ultimos  { get; }

        public DashboardVM()
        {
            var cultura = new CultureInfo("es-MX");
            var raw     = DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", cultura);
            DateLabel   = cultura.TextInfo.ToTitleCase(raw);

            SelectPeriodoCommand = new RelayCommand(p => Periodo = p?.ToString() ?? "Hoy");

            TopMerma = new ObservableCollection<MermaItemVM>
            {
                new("Pan Blanco Grande", 5.8, 142),
                new("Tortillinas",       4.6, 118),
                new("Bimbollos",         3.9,  96),
                new("Pan Integral",      2.4,  61),
                new("Donas Glaseadas",   1.7,  44),
            };

            Ultimos = new ObservableCollection<PesajeRowVM>
            {
                new("P-2841", "Pan Blanco Grande", "1,240", "14:32", false),
                new("P-2840", "Tortillinas",         "880", "14:18", false),
                new("P-2839", "Bimbollos",           "620", "13:55", true),
                new("P-2838", "Pan Integral",      "1,100", "13:42", false),
                new("P-2837", "Donas Glaseadas",     "340", "13:20", false),
            };
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Item de barra de merma
    // ══════════════════════════════════════════════════════════════════════
    public class MermaItemVM
    {
        private const double MaxPct = 5.8;

        public string    Name       { get; }
        public double    Pct        { get; }
        public int       Kg         { get; }
        public MermaTone Tone       => Pct >= 4 ? MermaTone.Critical
                                     : Pct >= 3 ? MermaTone.Warning
                                                : MermaTone.Normal;
        public double    BarPercent => Pct / MaxPct * 100.0;
        public string    PctText    => $"{Pct}%";
        public string    KgText     => $"{Kg} kg";

        // Brush del gradiente según tono (listo para que el XAML bindee directo)
        public Brush BarBrush => Tone switch
        {
            MermaTone.Critical => new LinearGradientBrush(
                new GradientStopCollection(new[]
                {
                    new GradientStop(Color.FromRgb(0xDC, 0x26, 0x26), 0),
                    new GradientStop(Color.FromRgb(0xF8, 0x71, 0x71), 1),
                }),
                new Point(0, 0.5), new Point(1, 0.5)),
            MermaTone.Warning => new LinearGradientBrush(
                new GradientStopCollection(new[]
                {
                    new GradientStop(Color.FromRgb(0xF5, 0x9E, 0x0B), 0),
                    new GradientStop(Color.FromRgb(0xFB, 0xBF, 0x24), 1),
                }),
                new Point(0, 0.5), new Point(1, 0.5)),
            _ => new LinearGradientBrush(
                new GradientStopCollection(new[]
                {
                    new GradientStop(Color.FromRgb(0x1E, 0x3A, 0x8A), 0),
                    new GradientStop(Color.FromRgb(0x3B, 0x82, 0xF6), 1),
                }),
                new Point(0, 0.5), new Point(1, 0.5)),
        };

        public MermaItemVM(string name, double pct, int kg)
        { Name = name; Pct = pct; Kg = kg; }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Fila de pesaje reciente
    // ══════════════════════════════════════════════════════════════════════
    public class PesajeRowVM
    {
        public string     Code          { get; }
        public string     Producto      { get; }
        public string     Kg            { get; }
        public string     Hora          { get; }
        public bool       IsWarn        { get; }
        public Visibility WarnVisibility => IsWarn ? Visibility.Visible : Visibility.Collapsed;

        public PesajeRowVM(string code, string producto, string kg, string hora, bool isWarn)
        { Code = code; Producto = producto; Kg = kg; Hora = hora; IsWarn = isWarn; }
    }
}
