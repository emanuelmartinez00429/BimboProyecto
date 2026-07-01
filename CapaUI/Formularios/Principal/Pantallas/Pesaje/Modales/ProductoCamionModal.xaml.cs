using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Productos.Dtos;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    public record ProductoResult(int IdProducto, string Codigo, string Nombre, double TaraUnitaria,
                                 double PesoManifestado, int BultosTeoricos, string Observaciones);

    public partial class ProductoCamionModal : UserControl
    {
        public event Action? Cerrado;
        public event Action<ProductoResult>? Guardado;

        private readonly bool _edit;
        private readonly CamionPesaje _camion;
        private int _idProducto;
        private string _codigo = "";
        private string _nombre = "";
        private double _tara;
        private bool _elegido;

        public ProductoCamionModal(string mode, ProductoCamion? initial, CamionPesaje camion)
        {
            InitializeComponent();
            _edit   = mode == "edit";
            _camion = camion;

            TxtEyebrow.Text = _edit ? "EDICIÓN · PRODUCTO" : "NUEVO · PRODUCTO";
            TxtTitle.Text   = _edit ? "Editar producto del camión" : "Agregar producto al camión";
            TxtGuardar.Text = _edit ? "Guardar cambios" : "Agregar";
            TxtHint.Text    = _edit ? "El producto en sí no se puede cambiar" : $"Se agregará al camión {camion.Placa}";

            if (_edit && initial != null)
            {
                _elegido    = true;
                _idProducto = initial.IdProducto;
                _codigo     = initial.ProductoCodigo;
                _nombre     = initial.ProductoNombre;
                _tara       = initial.TaraUnitaria;
                BtnSeleccionar.Visibility = Visibility.Collapsed;
                ProdLocked.Visibility     = Visibility.Visible;
                TxtLockedCod.Text = _codigo;
                TxtLockedNom.Text = _nombre;
                TxtPeso.Text   = initial.PesoManifestado.ToString(CultureInfo.InvariantCulture);
                TxtBultos.Text = initial.BultosTeoricos.ToString(CultureInfo.InvariantCulture);
                TxtObs.Text    = initial.Observaciones;
            }

            Loaded += (_, __) => Validar(this, null!);
        }

        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
        {
            var yaAgregados = _camion.Productos.Select(p => p.ProductoCodigo);
            var picker = new SeleccionarProductoModal(_camion.IdProveedor, _camion.Proveedor, yaAgregados);
            picker.Cerrado += CerrarPicker;
            picker.Seleccionado += dto =>
            {
                _elegido    = true;
                _idProducto = dto.Id;
                _codigo     = dto.CodigoInterno;
                _nombre     = dto.Nombre;
                _tara       = 0; // la tara efectiva la aplica el trigger de BD al pesar
                TxtSeleccion.Text = $"{_codigo}  ·  {_nombre}";
                CerrarPicker();
                Validar(this, null!);
            };
            PickerHost.Content = picker;
            PickerOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarPicker()
        {
            PickerOverlay.Visibility = Visibility.Collapsed;
            PickerHost.Content = null;
        }

        private void Validar(object sender, RoutedEventArgs e)
        {
            bool ok = _elegido
                      && double.TryParse(TxtPeso.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var peso) && peso > 0
                      && int.TryParse(TxtBultos.Text, out var bultos) && bultos > 0;
            if (BtnGuardar != null) BtnGuardar.IsEnabled = ok;
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtPeso.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var peso)) return;
            if (!int.TryParse(TxtBultos.Text, out var bultos)) return;
            Guardado?.Invoke(new ProductoResult(_idProducto, _codigo, _nombre, _tara, peso, bultos, TxtObs.Text?.Trim() ?? ""));
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
    }
}
