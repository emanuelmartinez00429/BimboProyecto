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
        private static readonly Brush _rojo   = (Brush)new BrushConverter().ConvertFromString("#FCA5A5")!;

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
            TxtHint.Text    = $"Reporte MOV-PROD-{producto.Id}";

            TxtPlaca.Text     = camion.Placa;
            TxtProveedor.Text = camion.Proveedor;
            TxtFechaHora.Text = $"{_fecha} {_hora}";
            TxtCodigo.Text    = producto.ProductoCodigo;
            TxtTaraInd.Text   = _taraInd.ToString("N2", CultureInfo.InvariantCulture);
            TxtManifestado.Text = producto.PesoManifestado.ToString("N0", CultureInfo.InvariantCulture) + " kg";
            TxtBultosDeclarados.Text = producto.BultosDeclarados.ToString("N0", CultureInfo.InvariantCulture);

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
            double taraTotal = _taraInd + taraExtra;
            // Sin Math.Max(0, ...) a propósito: hay que VER el negativo para entender el error.
            double neto      = NetoCalculado;
            double netoTotal = _pesoRecibidoPrevio + Math.Max(0, neto);
            double dif       = _producto.PesoManifestado - netoTotal;

            TxtTaraTotal.Text = taraTotal.ToString("N2", CultureInfo.InvariantCulture);
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

            TxtDif.Text = (dif < 0 ? "+" : "") + Math.Abs(dif).ToString("N1", CultureInfo.InvariantCulture);
            TxtDif.Foreground = dif < 0 ? _rojo : _blanco;
            TxtDifMsg.Text = dif < 0
                ? "Excedente sobre lo manifestado"
                : dif == 0 ? "Recepción completa" : "Aún falta por recibir";

            if (AvisoBultosAprox != null)
                AvisoBultosAprox.Visibility = PesajeCalc.BultosSonAproximados(taraExtra)
                    ? Visibility.Visible : Visibility.Collapsed;

            if (AvisoNetoInvalido != null)
                AvisoNetoInvalido.Visibility = Bruto > 0 && neto <= 0
                    ? Visibility.Visible : Visibility.Collapsed;

            if (BtnSeguir != null) BtnSeguir.IsEnabled = Valido;
        }

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
