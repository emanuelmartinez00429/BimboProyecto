using CapaAplicacion.Common;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Interfaces;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Presentaciones
{
    public partial class PresentacionModal : System.Windows.Controls.UserControl
    {
        private readonly IPresentacionRepository _repo;
        private readonly PresentacionDto?        _presentacion;
        private readonly bool                    _esNuevo;

        public event Action? Cerrado;
        public event Action? Guardado;

        public PresentacionModal(IPresentacionRepository repo, PresentacionDto? presentacion)
        {
            _repo         = repo;
            _presentacion = presentacion;
            _esNuevo      = presentacion == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO"      : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear presentación"  : "Editar presentación";

            try
            {
                var uri = new Uri("pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png");
                ModalLogo.Source = new BitmapImage(uri);
            }
            catch { }

            if (!_esNuevo && _presentacion != null)
            {
                TxtNombre.Text      = _presentacion.Nombre;
                TxtDescripcion.Text = _presentacion.Descripcion;

                bool activo          = _presentacion.IdEstado == EstadoRegistro.Activo;
                RbActivo.IsChecked   = activo;
                RbInactivo.IsChecked = !activo;
            }
            else
            {
                // Todo registro nuevo se crea Activo — el toggle a Inactivo
                // solo tiene sentido al editar (equivale a la baja lógica).
                RbActivo.IsChecked  = true;
                RbActivo.IsEnabled  = false;
                RbInactivo.IsEnabled = false;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private void TxtDescripcion_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Return)) return;
            e.Handled = true;
            _ = GuardarAsync();
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e) => await GuardarAsync();

        private async Task GuardarAsync()
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGuardar.IsEnabled = false;
            try
            {
                var dto = new PresentacionDto
                {
                    Id          = _esNuevo ? 0 : _presentacion!.Id,
                    Nombre      = TxtNombre.Text.Trim(),
                    Descripcion = TxtDescripcion.Text.Trim(),
                    IdEstado    = _esNuevo || RbActivo.IsChecked == true
                        ? EstadoRegistro.Activo
                        : EstadoRegistro.Inactivo,
                };

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto);
                    if (!r.Success) { MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto);
                    if (!r.Success) { MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error inesperado: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
