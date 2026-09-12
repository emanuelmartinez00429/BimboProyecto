using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos
{
    /// <summary>
    /// Modelos en memoria de la pantalla de Recepción de Materia Prima.
    /// La persistencia real vive en movimientos/movimiento_productos/entradas_producto.
    /// </summary>
    public static class PesajeCalc
    {
        public static int Round(double n) => (int)Math.Round(n, MidpointRounding.AwayFromZero);

        public static string HoraAhora() => DateTime.Now.ToString("hh:mm tt");
        public static string FechaHoy()  => DateTime.Now.ToString("dd/MM/yyyy");

        /// <summary>
        /// Bultos estimados de una pesada: al bruto se le quita la tara extra que se pesó
        /// junto con la carga (las tarimas) y el resto se divide por lo que pesa UN bulto
        /// completo — el producto más su propio empaque.
        /// <para/>
        /// <c>bultos = (bruto − tara_extra_de_esta_entrada) / (peso_teorico + tara_empaque)</c>
        /// <para/>
        /// Es un INDICADOR APROXIMADO, nunca un dato capturado: no se persiste. Devuelve
        /// <c>null</c> cuando no hay forma de calcularlo con sentido (bruto no positivo, sin
        /// peso teórico en el catálogo, o el bruto no alcanza a cubrir la tara extra) — la UI
        /// muestra "—" en vez de un número inventado.
        /// </summary>
        public static double? BultosTeoricos(
            double pesoBruto, double taraExtraEntrada,
            double pesoTeoricoUnitario, double taraEmpaqueUnitaria)
        {
            if (pesoBruto <= 0) return null;
            if (pesoTeoricoUnitario <= 0) return null;   // sin peso teórico no hay cómo calcular

            double pesoProducto = pesoBruto - taraExtraEntrada;
            if (pesoProducto <= 0) return null;

            double pesoPorBulto = pesoTeoricoUnitario + taraEmpaqueUnitaria;
            if (pesoPorBulto <= 0) return null;

            return pesoProducto / pesoPorBulto;
        }

        /// <summary>
        /// La estimación de bultos no es confiable porque no se pesó la tara extra de esa
        /// entrada: el peso de las tarimas se está contando como si fuera producto.
        /// </summary>
        public static bool BultosSonAproximados(double taraExtraEntrada) => taraExtraEntrada <= 0;

        /// <summary>
        /// Reparte un total en <paramref name="n"/> cuotas iguales redondeadas; el residuo del
        /// redondeo va a la última. Garantiza que la suma de las cuotas dé el total EXACTO, que
        /// es lo que hace literalmente cierto que "la tara extra total es la suma de las entradas".
        /// </summary>
        public static double[] RepartirTaraExtra(double total, int n, int decimales = 2)
        {
            if (n <= 0) return Array.Empty<double>();

            var cuotas = new double[n];
            double cuota = Math.Round(total / n, decimales, MidpointRounding.AwayFromZero);
            double acumulado = 0;

            for (int i = 0; i < n - 1; i++)
            {
                cuotas[i] = cuota;
                acumulado += cuota;
            }
            cuotas[n - 1] = Math.Round(total - acumulado, decimales, MidpointRounding.AwayFromZero);
            return cuotas;
        }

        /// <summary>
        /// Tara extra máxima que se puede repartir sin que ninguna pesada quede con neto ≤ 0
        /// (la BD lo rechaza con un CHECK). Como el reparto es uniforme, el techo lo marca la
        /// pesada de menor margen: <c>min(bruto − tara_individual) × cantidad de pesadas</c>.
        /// </summary>
        public static double TaraExtraMaximaRepartible(IEnumerable<(double Bruto, double TaraInd)> entradas)
        {
            var margenes = entradas.Select(e => e.Bruto - e.TaraInd).ToList();
            if (margenes.Count == 0) return 0;

            double menor = margenes.Min();
            if (menor <= 0) return 0;

            double maximo = menor * margenes.Count - 0.01;   // estrictamente menor, no igual
            return Math.Max(0, Math.Floor(maximo * 100) / 100);
        }
    }

    /// <summary>Item de proveedor para el combo del modal de camión (id real de la BD).</summary>
    public record ProveedorItem(int Id, string Nombre);

    public class EntradaPesaje
    {
        public int    Id         { get; set; }
        public int    NumeroFila { get; set; }
        public double Bruto      { get; set; }
        public double TaraInd   { get; set; }
        public double TaraExtra { get; set; }
        public double TaraTotal { get; set; }
        public double Neto      { get; set; }

        /// <summary>Indicador calculado a partir del peso. Nunca se persiste. Null = no calculable.</summary>
        public double? BultosTeoricos { get; set; }

        /// <summary>
        /// Bultos que capturó el operario a mano en el flujo VIEJO
        /// (<c>numero_bultos_recibido</c>). Null en las entradas nuevas, donde ya no se captura.
        /// </summary>
        public int? BultosCapturados { get; set; }

        /// <summary>Lo que muestra la grilla: el dato real si existe, si no la estimación.</summary>
        public string BultosTexto => BultosCapturados.HasValue
            ? BultosCapturados.Value.ToString("N0", CultureInfo.InvariantCulture)
            : BultosTeoricos?.ToString("N2", CultureInfo.InvariantCulture) ?? "—";

        /// <summary>La estimación no es confiable: no se pesó la tara extra de esta entrada.</summary>
        public bool BultosAproximados => !BultosCapturados.HasValue && TaraExtra <= 0;

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

        /// <summary>
        /// Regla del basurero de la fila. El servidor la vuelve a exigir
        /// (<c>cambiar_estado_producto_pesaje_tabla_bitacora</c>): acá solo se anticipa
        /// para que el botón se vea gris en vez de fallar al tocarlo.
        /// </summary>
        public bool PuedeQuitar => !TienePesajes;

        public string MotivoQuitar => TienePesajes
            ? "No se puede quitar: el producto ya tiene pesajes registrados"
            : "Quitar este producto de la carga";

        [ObservableProperty] private string _estado = "Abierto";

        public ObservableCollection<EntradaPesaje> Entradas { get; } = new();

        // ── Agregados ─────────────────────────────────────────────────────────
        public double PesoRecibido => Entradas.Sum(e => e.Neto);

        /// <summary>
        /// Tara extra registrada de este producto: la SUMA de lo pesado en cada entrada.
        /// Es la fuente de verdad — el total nunca se guarda por separado.
        /// </summary>
        public double TaraExtraRegistrada => Entradas.Sum(e => e.TaraExtra);

        /// <summary>Pesadas a las que todavía no se les cargó la tara extra.</summary>
        public int PesadasSinTaraExtra => Entradas.Count(e => e.TaraExtra <= 0);

        /// <summary>Hay pesadas sin tara extra: sus bultos estimados son aproximados.</summary>
        public bool FaltaTaraExtra => Entradas.Count > 0 && PesadasSinTaraExtra > 0;

        /// <summary>Bultos estimados recibidos: la suma de las estimaciones de cada pesada.</summary>
        public double BultosEstimados => Entradas.Sum(e => e.BultosTeoricos ?? 0);

        // Proporción del manifiesto, basada en PESO (no en los bultos, que ya no se capturan).
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
                ? (Brush)CapaUI.Services.Empresa.EmpresaThemeService.ObtenerBrushPrincipalActual()
                : (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));

        /// <summary>
        /// Recalcula TODO lo que deriva de <see cref="Entradas"/> tras un alta/baja de pesada.
        /// <para/>
        /// Notifica con <see cref="string.Empty"/> (= "todas las propiedades cambiaron") a
        /// propósito: la lista enumerada a mano se desincronizó una vez —faltaban
        /// <see cref="PuedeQuitar"/>/<see cref="TienePesajes"/>/<see cref="MotivoQuitar"/>,
        /// así que el basurero de la fila no se ponía gris al pesar un producto—. Una fila
        /// tiene ~15 bindings y esto corre en un clic humano, no en un bucle: el costo de
        /// refrescar de más es nulo y ninguna propiedad derivada se puede volver a olvidar.
        /// </summary>
        public void NotificarAgregados() => OnPropertyChanged(string.Empty);
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
        /// Cuántas recepciones abiertas comparten esta placa. Un camión que trae carga de
        /// dos proveedores son dos <c>movimientos</c> con la misma placa — uno por
        /// proveedor, cada uno con su manifiesto. Lo calcula el ViewModel al recargar.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlacaCompartida))]
        private int _recepcionesEnPlaca = 1;

        /// <summary>La placa la comparten dos o más recepciones: es un solo camión físico.</summary>
        public bool PlacaCompartida => RecepcionesEnPlaca > 1;

        /// <summary>
        /// Productos vivos de la recepción SEGÚN LA BASE. No se puede usar
        /// <c>Productos.Count</c> para esto: esa colección solo se llena para el camión
        /// seleccionado, así que en los demás daría 0 y el basurero se vería habilitado
        /// para recepciones que sí tienen carga.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeQuitar))]
        [NotifyPropertyChangedFor(nameof(MotivoQuitar))]
        private int _productosEnBase;

        /// <summary>
        /// Regla del basurero del camión: solo se quita una recepción vacía. A diferencia
        /// del producto, esta NO la exige el servidor —la RPC de anular recepción no mira
        /// los productos—, así que por ahora el cerrojo es solo de pantalla.
        /// </summary>
        public bool PuedeQuitar => ProductosEnBase == 0;

        public string MotivoQuitar => ProductosEnBase == 0
            ? "Quitar este camión"
            : "No se puede quitar: el camión ya tiene productos agregados";

        /// <summary>
        /// LEGADO — <c>movimientos.peso_tara_extra</c> del flujo anterior, donde la tara extra
        /// se pesaba una vez por camión y se prorrateaba por bultos declarados. Solo lectura:
        /// nunca se vuelve a escribir. Sirve para reconocer camiones cargados con el flujo viejo.
        /// La tara extra vigente vive en cada entrada (<c>ProductoCamion.TaraExtraRegistrada</c>).
        /// </summary>
        public double TaraExtraLegado { get; set; }

        /// <summary>El camión trae tara extra cargada con el esquema anterior.</summary>
        public bool EsLegado => TaraExtraLegado > 0;

        public ObservableCollection<ProductoCamion> Productos { get; } = new();

        /// <summary>Tara extra de todo el camión: la suma de la registrada en cada producto.</summary>
        public double TaraExtraRegistrada => Productos.Sum(p => p.TaraExtraRegistrada);

        /// <summary>Pesadas del camión entero (todos los productos) sin tara extra cargada.</summary>
        public int PesadasSinTaraExtra => Productos.Sum(p => p.PesadasSinTaraExtra);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalKgTexto))]
        private double _totalKg;

        public string TotalKgTexto => TotalKg > 0 ? $"{TotalKg:N0} kg" : "0 kg";

        [ObservableProperty] private bool _isSelected;

        /// <summary>Recalcula los agregados que dependen de las entradas de los productos.</summary>
        public void NotificarTotales()
        {
            OnPropertyChanged(nameof(TaraExtraRegistrada));
            OnPropertyChanged(nameof(PesadasSinTaraExtra));
            OnPropertyChanged(nameof(TotalKg));
            OnPropertyChanged(nameof(TotalKgTexto));
        }
    }

    public partial class GrupoCamionPesaje : ObservableObject
    {
        [ObservableProperty] private int _numero;
        [ObservableProperty] private string _placa = "";
        [ObservableProperty] private string _observaciones = "";
        [ObservableProperty] private string _estado = "Abierto";
        [ObservableProperty] private bool _isSelected;

        public ObservableCollection<CamionPesaje> Recepciones { get; } = new();

        public double TotalKg => Recepciones.Sum(r => r.TotalKg);
        public string TotalKgTexto => TotalKg > 0 ? $"{TotalKg:N0} kg" : "0 kg";

        public bool PuedeQuitar => Recepciones.Count > 0 && Recepciones.All(r => r.PuedeQuitar);
        public string MotivoQuitar => PuedeQuitar
            ? "Quitar camión y todas sus recepciones"
            : "No se puede quitar: el camión tiene productos registrados";

        public void NotificarTotales()
        {
            OnPropertyChanged(nameof(TotalKg));
            OnPropertyChanged(nameof(TotalKgTexto));
            OnPropertyChanged(nameof(PuedeQuitar));
            OnPropertyChanged(nameof(MotivoQuitar));
        }
    }
}
