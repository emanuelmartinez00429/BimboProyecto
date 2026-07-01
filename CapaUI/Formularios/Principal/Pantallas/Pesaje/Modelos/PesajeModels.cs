using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos
{
    /// <summary>
    /// Modelos en memoria de la pantalla de Recepción de Materia Prima (Fase 1).
    /// Equivalen a la semilla del diseño. La persistencia real
    /// (movimientos/movimiento_productos/entradas_producto) es Fase 2.
    /// </summary>
    public static class PesajeCalc
    {
        public static int Round(double n) => (int)Math.Round(n, MidpointRounding.AwayFromZero);

        public static double CalcTaraInd(int bultosTeoricos, double taraUnitaria)
            => Round(bultosTeoricos) * taraUnitaria;

        public static string HoraAhora() => DateTime.Now.ToString("hh:mm tt");
        public static string FechaHoy()  => DateTime.Now.ToString("dd/MM/yyyy");
    }

    /// <summary>Item de proveedor para el combo del modal de camión (id real de la BD).</summary>
    public record ProveedorItem(int Id, string Nombre);

    public class EntradaPesaje
    {
        public int    Id        { get; set; }
        public double Bruto     { get; set; }
        public double TaraInd   { get; set; }
        public double TaraExtra { get; set; }
        public double TaraTotal { get; set; }
        public double Neto      { get; set; }
        public int    Bultos    { get; set; }
        public string Fecha     { get; set; } = "";
        public string Hora      { get; set; } = "";
        public string Observaciones { get; set; } = "";

        // Para la vista "Todo el camión"
        public int    ProdId     { get; set; }
        public string ProdNombre { get; set; } = "";
    }

    public partial class ProductoCamion : ObservableObject
    {
        public int    Id            { get; set; }   // id_mov_producto
        public int    IdProducto    { get; set; }   // id_producto (para insertar pesajes)
        public string ProductoCodigo { get; set; } = "";
        public string ProductoNombre { get; set; } = "";
        public double TaraUnitaria   { get; set; }
        public double PesoManifestado { get; set; }
        public int    BultosTeoricos  { get; set; }
        public string Observaciones   { get; set; } = "";
        public string ProveedorNombre { get; set; } = "";

        [ObservableProperty] private string _estado = "Abierto";

        public ObservableCollection<EntradaPesaje> Entradas { get; } = new();

        // ── Agregados (idénticos a calcAgregadosProducto del diseño) ──────────
        public double PesoRecibido => Entradas.Sum(e => e.Neto);

        public double BultosRecibidos => PesoManifestado > 0
            ? PesoRecibido * BultosTeoricos / PesoManifestado
            : 0;

        public double BultosRestantes => Math.Max(0, BultosTeoricos - BultosRecibidos);

        public double PctRestante
        {
            get
            {
                if (BultosTeoricos <= 0) return 0;
                double pct = (BultosTeoricos - BultosRecibidos) / BultosTeoricos * 100;
                return Math.Max(0, Math.Min(100, pct));
            }
        }

        public double PctRecibido => Math.Max(0, Math.Min(100, 100 - PctRestante));

        /// <summary>Escala 0..1 para el ScaleTransform de la barra (transform-based, sin layout).</summary>
        public double EscalaRecibido => PctRecibido / 100.0;

        public string ProgresoTexto => PctRestante <= 0
            ? "Completo"
            : $"{Math.Round(PctRestante)}% restante";

        public Brush ProgresoColor => PctRestante <= 0
            ? Brushes.ForestGreen
            : PctRestante < 40
                ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"))
                : (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));

        /// <summary>Recalcula los agregados tras cambios en Entradas.</summary>
        public void NotificarAgregados()
        {
            OnPropertyChanged(nameof(PesoRecibido));
            OnPropertyChanged(nameof(BultosRecibidos));
            OnPropertyChanged(nameof(BultosRestantes));
            OnPropertyChanged(nameof(PctRestante));
            OnPropertyChanged(nameof(PctRecibido));
            OnPropertyChanged(nameof(EscalaRecibido));
            OnPropertyChanged(nameof(ProgresoTexto));
            OnPropertyChanged(nameof(ProgresoColor));
        }
    }

    public partial class CamionPesaje : ObservableObject
    {
        public int    Id              { get; set; }
        public string Placa           { get; set; } = "";
        public string Proveedor       { get; set; } = "";
        public int?   IdProveedor     { get; set; }
        public string FechaAsignacion { get; set; } = "";
        public string Observaciones   { get; set; } = "";

        [ObservableProperty] private string _estado = "Abierto";

        public ObservableCollection<ProductoCamion> Productos { get; } = new();
    }
}
