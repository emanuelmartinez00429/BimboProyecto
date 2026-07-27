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

        public static double CalcTaraInd(int bultosDeclarados, double taraUnitaria)
            => Round(bultosDeclarados) * taraUnitaria;

        public static string HoraAhora() => DateTime.Now.ToString("hh:mm tt");
        public static string FechaHoy()  => DateTime.Now.ToString("dd/MM/yyyy");

        /// <summary>
        /// Reparte la tara extra TOTAL del camión (se pesa una sola vez: tarimas, forros,
        /// separadores) entre todos los bultos declarados de la carga. Así, cuando se
        /// termina de pesar todo, la suma de taras extra atribuidas equivale al total real.
        /// Devuelve 0 si no hay bultos declarados (evita división por cero).
        /// </summary>
        public static double TaraExtraPorBulto(double taraExtraTotal, int bultosDeclaradosCamion)
            => bultosDeclaradosCamion > 0 ? taraExtraTotal / bultosDeclaradosCamion : 0;

        /// <summary>
        /// Bultos teóricos: cuántos bultos representa el peso bruto que acaba de marcar la
        /// báscula. Cada bulto pesa: producto + su empaque + su parte de la tara extra.
        /// <para/>
        /// Devuelve <c>null</c> cuando el cálculo no es confiable (bruto no positivo, o
        /// falta el peso teórico del producto) — la UI muestra "—" en ese caso en vez de
        /// un número inventado.
        /// <para/>
        /// OJO: usa la tara de empaque POR BULTO, mientras que el trigger de BD la aplica
        /// PLANA al calcular el peso neto guardado. Inconsistencia conocida y documentada
        /// (ver Deuda Técnica); este indicador es informativo y no altera el neto.
        /// </summary>
        public static double? BultosDeclarados(
            double pesoBruto, double pesoTeoricoUnitario, double taraEmpaqueUnitaria, double taraExtraPorBulto)
        {
            if (pesoBruto <= 0) return null;
            if (pesoTeoricoUnitario <= 0) return null;   // sin peso teórico no hay cómo calcular

            double pesoPorBulto = pesoTeoricoUnitario + taraEmpaqueUnitaria + taraExtraPorBulto;
            if (pesoPorBulto <= 0) return null;

            return pesoBruto / pesoPorBulto;
        }
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
        public double TaraUnitaria   { get; set; }   // empaque de UN bulto
        public double PesoTeorico    { get; set; }   // peso unitario del producto, sin empaque
        public double PesoManifestado { get; set; }

        /// <summary>Bultos que declara el manifiesto (dato de papel, sin verificar).</summary>
        public int    BultosDeclarados { get; set; }

        public string Observaciones   { get; set; } = "";
        public string ProveedorNombre { get; set; } = "";

        /// <summary>True si ya tiene pesajes: no se puede quitar del camión.</summary>
        public bool TienePesajes => Entradas.Count > 0;

        [ObservableProperty] private string _estado = "Abierto";

        public ObservableCollection<EntradaPesaje> Entradas { get; } = new();

        // ── Agregados (idénticos a calcAgregadosProducto del diseño) ──────────
        public double PesoRecibido => Entradas.Sum(e => e.Neto);

        public double BultosRecibidos => PesoManifestado > 0
            ? PesoRecibido * BultosDeclarados / PesoManifestado
            : 0;

        public double BultosRestantes => Math.Max(0, BultosDeclarados - BultosRecibidos);

        public double PctRestante
        {
            get
            {
                if (BultosDeclarados <= 0) return 0;
                double pct = (BultosDeclarados - BultosRecibidos) / BultosDeclarados * 100;
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

        /// <summary>
        /// Tara extra TOTAL de la carga (tarimas, forros, separadores) — se pesa una
        /// sola vez para todo el camión, no por pesada.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TaraExtraPorBulto))]
        [NotifyPropertyChangedFor(nameof(FaltaTaraExtra))]
        private double _taraExtraTotal;

        public ObservableCollection<ProductoCamion> Productos { get; } = new();

        /// <summary>Suma de los bultos declarados de todos los productos de la carga.</summary>
        public int BultosDeclaradosTotal => Productos.Sum(p => p.BultosDeclarados);

        /// <summary>Parte de la tara extra que le toca a cada bulto de la carga.</summary>
        public double TaraExtraPorBulto =>
            PesajeCalc.TaraExtraPorBulto(TaraExtraTotal, BultosDeclaradosTotal);

        /// <summary>
        /// True si todavía no se registró la tara extra: los netos y los bultos teóricos
        /// pueden no ser correctos hasta que se ingrese.
        /// </summary>
        public bool FaltaTaraExtra => TaraExtraTotal <= 0;

        /// <summary>Recalcula lo que depende de los bultos declarados de los productos.</summary>
        public void NotificarTaraExtra()
        {
            OnPropertyChanged(nameof(BultosDeclaradosTotal));
            OnPropertyChanged(nameof(TaraExtraPorBulto));
            OnPropertyChanged(nameof(FaltaTaraExtra));
        }
    }
}
