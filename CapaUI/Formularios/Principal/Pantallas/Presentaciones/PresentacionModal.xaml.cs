using CapaUI.Core.Controls;
using CapaAplicacion.Common;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Interfaces;
using System;
using System.Windows;

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
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO"     : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear presentación" : "Editar presentación";

            if (!_esNuevo && _presentacion != null)
            {
                TxtNombre.Text      = _presentacion.Nombre;
                TxtDescripcion.Text = _presentacion.Descripcion;

                bool activo = _presentacion.IdEstado == EstadoRegistro.Activo;
                RbActivo.IsChecked   = activo;
                RbInactivo.IsChecked = !activo;

                // La auditoría solo tiene sentido sobre un registro que ya existe.
                FilaAuditoria.Visibility = Visibility.Visible;
                TxtCreado.Text     = FormatearFecha(_presentacion.CreatedAt);
                TxtActualizado.Text = FormatearFecha(_presentacion.UpdatedAt);
            }

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtNombre.Focus();
        }

        private static string FormatearFecha(DateTime? fecha) =>
            fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy HH:mm") : "—";

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                return;
            }

            // Ver CategoriaModal: se avisa solo al pasar a inactivo algo que ya
            // existía y estaba activo.
            bool estabaActivo = !_esNuevo && _presentacion!.IdEstado == EstadoRegistro.Activo;
            if (estabaActivo && RbInactivo.IsChecked == true &&
                !DialogoConfirmacion.ConfirmarInactivacion("presentación", TxtNombre.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicacion se colgo.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
                var dto = new PresentacionDto
                {
                    Id          = _esNuevo ? 0 : _presentacion!.Id,
                    Nombre      = TxtNombre.Text.Trim(),
                    Descripcion = TxtDescripcion.Text.Trim(),
                    IdEstado    = RbActivo.IsChecked == true
                        ? EstadoRegistro.Activo
                        : EstadoRegistro.Inactivo,
                };

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto);
                    if (!r.Success) { MostrarError(r.Error); return; }
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto);
                    if (!r.Success) { MostrarError(r.Error); return; }
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
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }

        /// <summary>
        /// nombre_presentacion tiene UNIQUE en la base, así que repetir un nombre
        /// vuelve como un 23505 de Postgres envuelto en texto de PostgREST —
        /// ilegible para el usuario. Se traduce al único mensaje que le sirve y
        /// se le devuelve el foco al campo que tiene que corregir.
        /// </summary>
        private void MostrarError(string error)
        {
            if (EsNombreDuplicado(error))
            {
                MessageBox.Show("Ya existe una presentación con ese nombre.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                TxtNombre.SelectAll();
                return;
            }

            MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static bool EsNombreDuplicado(string error) =>
            error.Contains("23505", StringComparison.OrdinalIgnoreCase)
            || error.Contains("presentacion_producto_nombre_presentacion_key", StringComparison.OrdinalIgnoreCase)
            || error.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
