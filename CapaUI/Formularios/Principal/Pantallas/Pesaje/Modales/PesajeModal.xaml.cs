using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public partial class PesajeModal : UserControl
    {
        public event Action? Cerrado;

        /// <summary>
        /// Guarda la pesada. Devuelve <c>Task&lt;bool&gt;</c> y no <c>Action</c> a propósito:
        /// el modal necesita ESPERAR el guardado para bloquearse mientras corre y saber si
        /// salió bien antes de limpiarse para la siguiente pesada. Con <c>Action</c> el
        /// handler quedaba como <c>async void</c> y una excepción ahí tumbaba la aplicación.
        /// </summary>
        public event Func<EntradaPesaje, Task<bool>>? GuardarYSeguir;

        private readonly ProductoCamion _producto;
        private readonly double _taraInd;

        // Mutables: tras guardar, el modal se prepara para la pesada siguiente en vez de
        // cerrarse, así que la entrada en edición, el acumulado previo y la marca de tiempo
        // dejan de ser los del constructor.
        private EntradaPesaje? _editInitial;
        private double _pesoRecibidoPrevio;
        private string _fecha;
        private string _hora;

        /// <summary>Guarda de reentrada: sin esto, dos clics seguidos insertaban dos pesajes.</summary>
        private bool _guardando;
        private ValidadorFormulario? _validador;

        /// <summary>
        /// Entrada que se está corrigiendo, o <c>null</c> si lo próximo es una pesada nueva.
        /// <para/>
        /// La expone el modal y no la captura quien lo abre porque cambia durante su vida: al
        /// guardar una corrección, el modal queda listo para una pesada nueva. Leer el valor
        /// capturado al abrirlo haría que el segundo guardado intentara anular una entrada
        /// que ya fue anulada.
        /// </summary>
        public EntradaPesaje? EntradaEnEdicion => _editInitial;

        private static readonly Brush _blanco = Brushes.White;
        private static readonly Brush _rojo   = Hex("#FCA5A5");
        private static readonly Brush _verde  = Hex("#4ADE80");
        private static readonly Brush _blancoSuave = Hex("#CCFFFFFF");

        /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
        public PesajeModal()
        {
            _producto = null!;
            _fecha    = null!;
            _hora     = null!;
            InitializeComponent();
        }

        public PesajeModal(CamionPesaje camion, ProductoCamion producto, EntradaPesaje? editInitial)
        {
            InitializeComponent();
            _producto    = producto;
            _editInitial = editInitial;
            // La tara individual es PLANA (tara.peso_tara_envalaje), igual que el trigger de BD;
            // no se multiplica por bultos.
            _taraInd     = producto.TaraUnitaria;
            _pesoRecibidoPrevio = producto.Entradas.Where(e => e != editInitial).Sum(e => e.Neto);
            _fecha = editInitial?.Fecha ?? PesajeCalc.FechaHoy();
            _hora  = editInitial?.Hora  ?? PesajeCalc.HoraAhora();

            TxtEyebrow.Text = editInitial != null ? "EDICIÓN · PESAJE" : "REGISTRAR PESAJE";
            TxtTitle.Text   = producto.ProductoNombre;
            TxtHint.Text    = $"Reporte MOV-PROD-{producto.Id}  ·  Ctrl+Enter para guardar";

            TxtPlaca.Text     = camion.Placa;
            TxtProveedor.Text = camion.Proveedor;
            TxtFechaHora.Text = $"{_fecha} {_hora}";
            TxtCodigo.Text    = producto.ProductoCodigo;
            TxtTaraInd.Text   = _taraInd.ToString("N2", CultureInfo.InvariantCulture);
            TxtManifestado.Text = Kg(producto.PesoManifestado);

            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtObs, "Las observaciones").Segun(ReglasEntradaPesaje.Observaciones)
                .ValidarAlSalirDelCampo();

            if (editInitial != null)
            {
                TxtBruto.Text           = editInitial.Bruto.ToString(CultureInfo.InvariantCulture);
                // Se precarga la tara extra que ya tenía, para que editar el bruto no la borre.
                TxtTaraExtraEntrada.Text = editInitial.TaraExtra.ToString(CultureInfo.InvariantCulture);
                TxtObs.Text             = editInitial.Observaciones;
            }

            Loaded += (_, __) => { TxtBruto.Focus(); Recalcular(this, null!); };
        }

        private double Bruto => double.TryParse(TxtBruto.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        /// <summary>Tara extra PESADA en esta pesada (tarimas/forros que vinieron con ella).</summary>
        private double TaraExtraEntrada =>
            double.TryParse(TxtTaraExtraEntrada.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private double NetoCalculado => Bruto - _taraInd - TaraExtraEntrada;

        /// <summary>
        /// Además del bruto, exige que el neto sea positivo: la BD tiene un CHECK sobre
        /// <c>peso_neto</c> y el rechazo llegaría como excepción cruda de Postgrest.
        /// </summary>
        private bool Valido => !_guardando && Bruto > 0 && TaraExtraEntrada >= 0 && NetoCalculado > 0;

        private void Recalcular(object sender, RoutedEventArgs e)
        {
            // Los TextChanged pueden dispararse durante InitializeComponent(), antes de
            // que el constructor asigne _producto. Sin esta guarda revienta con NRE.
            if (_producto is null) return;

            double taraExtra = TaraExtraEntrada;
            // Sin Math.Max(0, ...) a propósito: hay que VER el negativo para entender el error.
            double neto      = NetoCalculado;
            double netoTotal = _pesoRecibidoPrevio + Math.Max(0, neto);
            double dif       = _producto.PesoManifestado - netoTotal;

            // Sin bruto todavía no hay nada que mostrar: "—" (igual que Bultos estimados).
            // Con bruto cargado, sí se muestra el negativo si corresponde (ver comentario arriba).
            TxtNeto.Text      = Bruto > 0
                ? neto.ToString("N2", CultureInfo.InvariantCulture)
                : "—";
            TxtNetoTotal.Text = netoTotal.ToString("N2", CultureInfo.InvariantCulture);

            // Bultos estimados: cuántos bultos representa el peso de producto de esta pesada.
            // Devuelve null (→ "—") si falta el peso teórico del producto o el bruto no alcanza.
            double? estimados = PesajeCalc.BultosTeoricos(
                Bruto, taraExtra, _producto.PesoTeorico, _producto.TaraUnitaria);
            TxtBultosEstimados.Text = estimados.HasValue
                ? estimados.Value.ToString("N2", CultureInfo.InvariantCulture)
                : "—";

            // Se muestra sin decimales, así que "completa" es lo que se leería como 0.
            bool completa = Math.Abs(dif) < 0.5;
            TxtDif.Text = (dif < 0 && !completa ? "+" : "") + Kg(Math.Abs(dif));
            TxtDif.Foreground = dif < 0 && !completa ? _rojo : _blanco;
            TxtDifMsg.Text = completa ? "Recepción completa"
                : dif < 0 ? "Excedente sobre lo manifestado" : "Aún falta por recibir";

            ActualizarPanel(neto, taraExtra, netoTotal);

            if (BtnSeguir != null) BtnSeguir.IsEnabled = Valido;
        }

        /// <summary>
        /// Tarjetas de acumulados, barra de avance, gráfico y avisos del panel lateral.
        /// <para/>
        /// La pesada que se está corrigiendo se saca de las "previas": si no, contaría dos
        /// veces (la versión guardada y la que se teclea) en el gráfico, el promedio y el
        /// contador. En el gráfico, "Ahora" ocupa su lugar en vez de ir al final.
        /// </summary>
        private void ActualizarPanel(double neto, double taraExtra, double netoTotal)
        {
            var previas = _producto.Entradas.Where(e => e != _editInitial).ToList();
            var netosPrevios = previas.Select(e => e.Neto).ToList();
            bool pesadaValida = Bruto > 0 && neto > 0;
            double manifestado = _producto.PesoManifestado;

            int conteo = previas.Count(e => e.Neto > 0) + (pesadaValida ? 1 : 0);
            TxtConteoPesajes.Text = conteo == 1 ? "1 pesaje" : $"{conteo} pesajes";

            TxtNetoAcum.Text = Kg(netoTotal);
            TxtNetoAcum.Foreground = manifestado > 0 && netoTotal > manifestado ? _rojo : _verde;
            // Tara individual PLANA por pesada (igual que el trigger), no bultos × tara.
            TxtTaraAcum.Text = Kg(previas.Sum(e => e.TaraInd) + (pesadaValida ? _taraInd : 0));
            TxtTaraExtraAcum.Text = Kg(previas.Sum(e => e.TaraExtra) + (pesadaValida ? taraExtra : 0));

            double avance = manifestado > 0 ? Math.Clamp(netoTotal / manifestado, 0, 1) : 0;
            TxtAvance.Text = $"{avance * 100:0}%";
            ColAvance.Width = new GridLength(avance, GridUnitType.Star);
            ColResto.Width  = new GridLength(1 - avance, GridUnitType.Star);

            int indiceAhora = _editInitial is null ? previas.Count : Math.Max(0, _producto.Entradas.IndexOf(_editInitial));
            Grafico.Actualizar(netosPrevios, indiceAhora, pesadaValida ? neto : null);
            TxtGraficoResumen.Text = $"{conteo} {(conteo == 1 ? "pesada" : "pesadas")} · máx {Grafico.EscalaY.ToString("0.##", CultureInfo.InvariantCulture)}";
            TxtPromedio.Text = netosPrevios.Count > 0 ? $"promedio {Kg(netosPrevios.Average())} kg" : "";

            var alertas = ReglasPanelPesaje.EvaluarAlertas(Bruto, neto, taraExtra, _pesoRecibidoPrevio, manifestado, netosPrevios);
            PanelAlertas.Children.Clear();
            foreach (var a in alertas.Take(MaxAlertasVisibles))
                PanelAlertas.Children.Add(CrearAlerta(a));
            int ocultas = alertas.Count - MaxAlertasVisibles;
            TxtAlertasExtra.Text = ocultas == 1 ? "+1 aviso más" : $"+{ocultas} avisos más";
            TxtAlertasExtra.Visibility = ocultas > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private const int MaxAlertasVisibles = 2;

        private static Border CrearAlerta(AlertaPesaje a)
        {
            var (fondo, borde, titulo) = a.Nivel switch
            {
                NivelAlertaPesaje.Critica     => ("#33DC2626", "#80EF4444", "#FECACA"),
                NivelAlertaPesaje.Advertencia => ("#33D97706", "#80F59E0B", "#FDE68A"),
                _                             => ("#2610B981", "#8034D399", "#86EFAC"),
            };
            bool ok = a.Nivel is NivelAlertaPesaje.Info or NivelAlertaPesaje.Ok;

            FrameworkElement icono = ok
                ? new System.Windows.Shapes.Path
                  {
                      Data = Geometry.Parse("M20,6 L9,17 l-5,-5"), Stroke = Hex(titulo), StrokeThickness = 2.2,
                      Width = 11, Height = 11, Stretch = Stretch.Uniform, Margin = new Thickness(0, 3, 8, 0),
                      VerticalAlignment = VerticalAlignment.Top,
                  }
                : new TextBlock
                  {
                      Text = "!", FontFamily = new FontFamily("Segoe UI"), FontSize = 13, FontWeight = FontWeights.Black,
                      Foreground = Hex(titulo), Margin = new Thickness(2, -1, 10, 0), VerticalAlignment = VerticalAlignment.Top,
                  };

            var texto = new StackPanel();
            texto.Children.Add(new TextBlock
            {
                Text = a.Titulo, FontFamily = new FontFamily("Segoe UI"), FontSize = 11.5,
                FontWeight = FontWeights.SemiBold, Foreground = Hex(titulo),
            });
            texto.Children.Add(new TextBlock
            {
                Text = a.Mensaje, FontFamily = new FontFamily("Segoe UI"), FontSize = 11,
                Foreground = _blancoSuave, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0),
            });

            var fila = new DockPanel();
            DockPanel.SetDock(icono, Dock.Left);
            fila.Children.Add(icono);
            fila.Children.Add(texto);

            return new Border
            {
                Background = Hex(fondo), BorderBrush = Hex(borde), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 0, 0, 6), Child = fila,
            };
        }

        private static Brush Hex(string color)
        {
            var b = (Brush)new BrushConverter().ConvertFromString(color)!;
            b.Freeze();
            return b;
        }

        private static string Kg(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

        private EntradaPesaje Snapshot()
        {
            double taraExtra = TaraExtraEntrada;
            double taraTotal = _taraInd + taraExtra;
            return new EntradaPesaje
            {
                Bruto = Bruto, TaraInd = _taraInd, TaraExtra = taraExtra, TaraTotal = taraTotal,
                Neto = NetoCalculado, Fecha = _fecha, Hora = _hora,
                BultosTeoricos = PesajeCalc.BultosTeoricos(
                    Bruto, taraExtra, _producto.PesoTeorico, _producto.TaraUnitaria),
                Observaciones = TxtObs.Text?.Trim() ?? "",
            };
        }

        /// <summary>
        /// Bloquea el modal mientras el pesaje viaja al servidor: da la señal visual que
        /// faltaba y, sobre todo, impide que un segundo clic registre la pesada dos veces.
        /// </summary>
        private void AplicarEstadoGuardando(bool guardando)
        {
            _guardando = guardando;

            TxtGuardando.Visibility  = guardando ? Visibility.Visible : Visibility.Collapsed;
            BtnVolver.IsEnabled      = !guardando;
            TxtBruto.IsEnabled       = !guardando;
            TxtTaraExtraEntrada.IsEnabled = !guardando;
            TxtObs.IsEnabled         = !guardando;

            // BtnSeguir.IsEnabled sale siempre de Valido (que ya contempla _guardando), para
            // que no haya dos fuentes de verdad sobre si el botón se puede tocar.
            Recalcular(this, null!);
        }

        /// <summary>
        /// Deja el modal listo para la pesada siguiente en vez de cerrarlo: eso es lo que
        /// "Seguir pesando" promete, y antes cerraba el modal obligando a reabrirlo desde
        /// "Pesar" para cada tarima.
        /// </summary>
        private void PrepararSiguientePesada()
        {
            // Se relee de la colección del producto en vez de sumar el neto que calculó este
            // modal: para cuando llegamos acá el ViewModel ya insertó la pesada con los pesos
            // que devolvió la BD, y esos son los que mandan.
            _pesoRecibidoPrevio = _producto.Entradas.Sum(e => e.Neto);

            // Una edición ya guardada deja de serlo: lo próximo que se registre es una pesada
            // nueva, no otra corrección de la misma entrada.
            _editInitial = null;

            // Fecha y hora se vuelven a tomar: si no, la segunda pesada se guardaría con la
            // marca de tiempo de la primera.
            _fecha = PesajeCalc.FechaHoy();
            _hora  = PesajeCalc.HoraAhora();
            TxtFechaHora.Text = $"{_fecha} {_hora}";
            TxtEyebrow.Text   = "REGISTRAR PESAJE";

            TxtBruto.Clear();
            TxtTaraExtraEntrada.Clear();
            TxtObs.Clear();
            _validador?.Limpiar();

            Recalcular(this, null!);
            TxtBruto.Focus();
        }

        private async void Seguir_Click(object sender, RoutedEventArgs e)
        {
            if (_guardando || !Valido || GuardarYSeguir is null) return;
            if (_validador is not null && !_validador.Validar()) return;

            var snapshot = Snapshot();
            AplicarEstadoGuardando(true);
            try
            {
                if (await GuardarYSeguir(snapshot))
                    PrepararSiguientePesada();
                // Si falló, el VM ya avisó por Toast y los datos siguen escritos: el operador
                // corrige y reintenta sin volver a teclear todo.
            }
            catch (Exception ex)
            {
                // Este método es async void porque WPF lo exige: una excepción que se escape
                // acá no la puede atrapar nadie y tumba la aplicación con la pesada a medias.
                Serilog.Log.Error(ex, "PesajeModal: falló el guardado del pesaje");
            }
            finally
            {
                AplicarEstadoGuardando(false);
            }
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
