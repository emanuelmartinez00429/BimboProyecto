using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>Lo que el modal devuelve: el total a repartir y sobre qué pesadas.</summary>
    public record ResultadoTaraExtra(double Total, IReadOnlyList<EntradaPesaje> Entradas);

    /// <summary>
    /// Carga una tara extra YA PESADA (tarimas, forros, separadores) y la reparte en partes
    /// iguales entre las pesadas registradas de un producto o de todo el camión.
    /// <para/>
    /// El valor que se ingresa es siempre el NUEVO TOTAL del alcance, no un delta: por eso
    /// existe «Usar lo ya registrado», que precarga la suma actual para sumarle encima. Un
    /// modo "sumar vs reemplazar" se prestaría a cargar dos veces la misma tarima.
    /// </summary>
    public partial class TaraExtraTotalModal : UserControl
    {
        /// <summary>Fila de la lista de pesadas parciales.</summary>
        public sealed class Parcial
        {
            public double Kg { get; init; }
            public string Texto => $"{Kg.ToString("N2", CultureInfo.InvariantCulture)} kg";
        }

        public event Action? Cerrado;
        public event Action<ResultadoTaraExtra>? Confirmado;

        private readonly CamionPesaje _camion;
        private readonly ProductoCamion? _producto;
        private readonly ObservableCollection<Parcial> _parciales = new();
        private bool _cargando = true;

        /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
        public TaraExtraTotalModal()
        {
            _camion = null!;
            InitializeComponent();
        }

        public TaraExtraTotalModal(CamionPesaje camion, ProductoCamion? producto)
        {
            InitializeComponent();
            _camion   = camion;
            _producto = producto;

            LstParciales.ItemsSource = _parciales;
            _parciales.CollectionChanged += (_, __) => ActualizarUI();

            // Sin producto seleccionado el único alcance posible es el camión entero.
            if (_producto is null)
            {
                RbAlcanceProducto.IsEnabled = false;
                RbAlcanceCamion.IsChecked   = true;
            }

            _cargando = false;
            ActualizarUI();
            Loaded += (_, __) => TxtTotal.Focus();
        }

        // ── Alcance ──────────────────────────────────────────────────────────

        private bool AlcanceEsProducto => RbAlcanceProducto.IsChecked == true && _producto is not null;

        /// <summary>Pesadas sobre las que se va a repartir, según el alcance elegido.</summary>
        private List<EntradaPesaje> EntradasAlcance => AlcanceEsProducto
            ? _producto!.Entradas.ToList()
            : _camion.Productos.SelectMany(p => p.Entradas).ToList();

        private double TaraYaRegistrada => EntradasAlcance.Sum(e => e.TaraExtra);

        // ── Lectura del total ────────────────────────────────────────────────

        private double LeerTotal()
        {
            if (RbModoParciales.IsChecked == true) return _parciales.Sum(p => p.Kg);
            return double.TryParse(TxtTotal.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        private static double LeerKg(string texto) =>
            double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        // ── Handlers ─────────────────────────────────────────────────────────

        private void Alcance_Changed(object sender, RoutedEventArgs e) { if (!_cargando) ActualizarUI(); }
        private void Campo_Changed(object sender, TextChangedEventArgs e) { if (!_cargando) ActualizarUI(); }

        private void Modo_Changed(object sender, RoutedEventArgs e)
        {
            if (_cargando) return;
            bool parciales = RbModoParciales.IsChecked == true;
            PanelTotal.Visibility     = parciales ? Visibility.Collapsed : Visibility.Visible;
            PanelParciales.Visibility = parciales ? Visibility.Visible   : Visibility.Collapsed;
            ActualizarUI();
        }

        private void UsarRegistrado_Click(object sender, RoutedEventArgs e)
        {
            TxtTotal.Text = TaraYaRegistrada.ToString("N2", CultureInfo.InvariantCulture);
            TxtTotal.Focus();
            TxtTotal.CaretIndex = TxtTotal.Text.Length;
        }

        private void AgregarParcial_Click(object sender, RoutedEventArgs e) => AgregarParcial();

        private void Parcial_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { AgregarParcial(); e.Handled = true; }
        }

        private void AgregarParcial()
        {
            double kg = LeerKg(TxtParcial.Text);
            if (kg <= 0) { MostrarError("Ingresá un peso mayor que cero."); return; }

            _parciales.Add(new Parcial { Kg = kg });
            TxtParcial.Clear();
            TxtParcial.Focus();
        }

        private void QuitarParcial_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Parcial p }) _parciales.Remove(p);
        }

        private void Aplicar_Click(object sender, RoutedEventArgs e)
        {
            var entradas = EntradasAlcance;
            if (entradas.Count == 0)
            {
                MostrarError("Registrá al menos una pesada antes de cargar la tara extra.");
                return;
            }

            double total = LeerTotal();
            if (total <= 0) { MostrarError("Ingresá el peso de la tara extra."); return; }

            Confirmado?.Invoke(new ResultadoTaraExtra(total, entradas));
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        // ── UI ───────────────────────────────────────────────────────────────

        private void ActualizarUI()
        {
            if (_cargando) return;

            TxtTitulo.Text = AlcanceEsProducto
                ? _producto!.ProductoNombre
                : $"Camión {_camion.Placa}";

            var entradas = EntradasAlcance;
            int n        = entradas.Count;
            double total = LeerTotal();
            double ya    = TaraYaRegistrada;

            TxtSinParciales.Visibility = _parciales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            TxtPesadas.Text      = n.ToString("N0", CultureInfo.InvariantCulture);
            TxtYaRegistrada.Text = $"{ya.ToString("N2", CultureInfo.InvariantCulture)} kg";
            TxtARepartir.Text    = $"{total.ToString("N2", CultureInfo.InvariantCulture)} kg";
            TxtPromedio.Text     = n > 0
                ? $"{(total / n).ToString("N2", CultureInfo.InvariantCulture)} kg"
                : "—";

            // Ya hay tara cargada: aplicar reemplaza el reparto existente, no lo suma.
            int conTara = entradas.Count(x => x.TaraExtra > 0);
            AvisoReemplazo.Visibility = conTara > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (conTara > 0)
                TxtAvisoReemplazo.Text =
                    $"{conTara} de {n} pesada(s) ya tienen tara extra cargada ({ya.ToString("N2", CultureInfo.InvariantCulture)} kg en total). " +
                    "Al aplicar se reemplaza el reparto actual — usá «Usar lo ya registrado» si querés sumarle encima.";

            ValidarYActualizarBoton(entradas, total, n);

            TxtPie.Text = n == 0
                ? "Sin pesadas registradas"
                : $"{n} pesada(s) · {(n > 0 ? (total / n).ToString("N2", CultureInfo.InvariantCulture) : "0")} kg c/u  ·  Ctrl+Enter para aplicar";
        }

        /// <summary>
        /// Pre-vuelo del CHECK de la BD: si alguna pesada quedaría con neto ≤ 0 el reparto no
        /// se puede hacer. Se avisa acá y se deshabilita el botón, para no descubrirlo con un
        /// error crudo de Postgrest a mitad de camino.
        /// </summary>
        private void ValidarYActualizarBoton(List<EntradaPesaje> entradas, double total, int n)
        {
            if (n == 0)
            {
                MostrarError("Registrá al menos una pesada antes de cargar la tara extra.");
                BtnAplicar.IsEnabled = false;
                return;
            }
            if (total <= 0)
            {
                LimpiarError();
                BtnAplicar.IsEnabled = false;
                return;
            }

            var cuotas = PesajeCalc.RepartirTaraExtra(total, n);
            for (int i = 0; i < n; i++)
            {
                if (entradas[i].Bruto - entradas[i].TaraInd - cuotas[i] > 0) continue;

                double max = PesajeCalc.TaraExtraMaximaRepartible(
                    entradas.Select(x => (x.Bruto, x.TaraInd)));
                MostrarError(
                    $"Con {total.ToString("N2", CultureInfo.InvariantCulture)} kg una pesada quedaría con peso neto " +
                    $"cero o negativo. Máximo repartible: {max.ToString("N2", CultureInfo.InvariantCulture)} kg.");
                BtnAplicar.IsEnabled = false;
                return;
            }

            LimpiarError();
            BtnAplicar.IsEnabled = true;
        }

        private void MostrarError(string mensaje)
        {
            TxtAvisoError.Text     = mensaje;
            AvisoError.Visibility  = Visibility.Visible;
        }

        private void LimpiarError() => AvisoError.Visibility = Visibility.Collapsed;
    }
}
