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

            if (editInitial != null)
            {
                TxtBruto.Text     = editInitial.Bruto.ToString(CultureInfo.InvariantCulture);
                TxtTaraExtra.Text = editInitial.TaraExtra.ToString(CultureInfo.InvariantCulture);
                TxtBultos.Text    = editInitial.Bultos.ToString(CultureInfo.InvariantCulture);
                TxtObs.Text       = editInitial.Observaciones;
            }

            Loaded += (_, __) => { TxtBruto.Focus(); Recalcular(this, null!); };
        }

        private double Bruto     => double.TryParse(TxtBruto.Text,     NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        private double TaraExtra => double.TryParse(TxtTaraExtra.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        private int    Bultos    => int.TryParse(TxtBultos.Text, out var v) ? v : 0;
        private bool   Valido    => Bruto > 0 && Bultos > 0;

        private void Recalcular(object sender, RoutedEventArgs e)
        {
            // InitializeComponent() setea Text="0" en TxtTaraExtra, lo que dispara este
            // TextChanged antes de que el constructor asigne _producto. Sin esta guarda,
            // esa primera invocación revienta con NullReferenceException.
            if (_producto is null) return;

            double taraTotal = _taraInd + TaraExtra;
            double neto      = Math.Max(0, Bruto - taraTotal);
            double netoTotal = _pesoRecibidoPrevio + neto;
            double dif       = _producto.PesoManifestado - netoTotal;

            TxtNeto.Text      = neto.ToString("N2", CultureInfo.InvariantCulture);
            TxtNetoTotal.Text = netoTotal.ToString("N2", CultureInfo.InvariantCulture);

            TxtDif.Text = (dif < 0 ? "+" : "") + Math.Abs(dif).ToString("N1", CultureInfo.InvariantCulture);
            TxtDif.Foreground = dif < 0 ? _rojo : _blanco;
            TxtDifMsg.Text = dif < 0
                ? "Excedente sobre lo manifestado"
                : dif == 0 ? "Recepción completa" : "Aún falta por recibir";

            if (BtnSeguir != null) BtnSeguir.IsEnabled = Valido;
        }

        private EntradaPesaje Snapshot()
        {
            double taraTotal = _taraInd + TaraExtra;
            double neto      = Math.Max(0, Bruto - taraTotal);
            return new EntradaPesaje
            {
                Bruto = Bruto, TaraInd = _taraInd, TaraExtra = TaraExtra, TaraTotal = taraTotal,
                Neto = neto, Bultos = Bultos, Fecha = _fecha, Hora = _hora,
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
