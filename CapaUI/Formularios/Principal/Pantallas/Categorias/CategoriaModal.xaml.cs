using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaUI.Core.Controls;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Categorias
{
    public partial class CategoriaModal : System.Windows.Controls.UserControl
    {
        private readonly ICategoriaRepository _repo;
        private readonly CategoriaDto?        _categoria;
        private readonly bool                 _esNuevo;

        public event Action? Cerrado;
        public event Action? Guardado;

        public CategoriaModal(ICategoriaRepository repo, CategoriaDto? categoria)
        {
            _repo      = repo;
            _categoria = categoria;
            _esNuevo   = categoria == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear categoría"  : "Editar categoría";

            if (!_esNuevo && _categoria != null)
            {
                TxtNombre.Text      = _categoria.Nombre;
                TxtDescripcion.Text = _categoria.Descripcion;

                RbActivo.IsChecked   = _categoria.EstadoCategoria;
                RbInactivo.IsChecked = !_categoria.EstadoCategoria;
            }

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtNombre.Focus();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Inactivar esconde el registro de los listados que filtran por
            // activos, así que se avisa antes — pero solo cuando es un cambio
            // real sobre algo que ya existía y estaba activo. Crear algo
            // directamente inactivo es una decisión explícita, no una sorpresa.
            bool estabaActivo = !_esNuevo && _categoria!.EstadoCategoria;
            if (estabaActivo && RbInactivo.IsChecked == true &&
                !DialogoConfirmacion.ConfirmarInactivacion("categoría", TxtNombre.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicacion se colgo.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
                var dto = new CategoriaDto
                {
                    Id              = _esNuevo ? 0 : _categoria!.Id,
                    Nombre          = TxtNombre.Text.Trim(),
                    Descripcion     = TxtDescripcion.Text.Trim(),
                    EstadoCategoria = RbActivo.IsChecked == true,
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
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
