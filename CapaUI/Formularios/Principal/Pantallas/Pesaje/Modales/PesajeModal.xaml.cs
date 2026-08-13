using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public partial class PesajeModal : UserControl
    {
        public event Action? Cerrado;
        public event Action<EntradaPesaje>? GuardarYSeguir;
        public event Action<EntradaPesaje?>? CerrarCamion;

        private readonly ProductoCamion _producto;
        private readonly EntradaPesaje? _editInitial;
        private readonly double _taraInd;
        private readonly double _pesoRecibidoPrevio;
        private readonly string _fecha;
        private readonly string _hora;

        private static readonly Brush _blanco = Brushes.White;
        private static readonly Brush _rojo   = (Brush)new BrushConverter().ConvertFromString("#FCA5A5")!;

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
        private bool Valido => Bruto > 0 && TaraExtraEntrada >= 0 && NetoCalculado > 0;

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
            TxtNeto.Text      = neto.ToString("N2", CultureInfo.InvariantCulture);
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

        private void Seguir_Click(object sender, RoutedEventArgs e)
        {
            if (Valido) GuardarYSeguir?.Invoke(Snapshot());
        }

        private void CerrarCamion_Click(object sender, RoutedEventArgs e)
            => CerrarCamion?.Invoke(Valido ? Snapshot() : null);

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
