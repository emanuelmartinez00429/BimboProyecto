using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
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
        private ValidadorFormulario           _validador = null!;

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
            // Las reglas se declaran una sola vez acá y sirven para los dos
            // momentos: al salir de cada campo y al guardar.
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtNombre, "El nombre").Segun(ReglasCategoria.Nombre)
                .Campo(TxtDescripcion, "La descripción").Segun(ReglasCategoria.Descripcion)
                .ValidarAlSalirDelCampo();

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
            if (!_validador.Validar()) return;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _categoria!.EstadoCategoria,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "categoría",
                    nombreRegistro: TxtNombre.Text.Trim()))
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

                // CreateAsync devuelve Result<int> y UpdateAsync Result: son tipos
                // distintos, así que no se pueden unificar en un ternario.
                bool exito;
                string error;

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto, Guid.NewGuid(), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto, Guid.NewGuid(), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);
                }

                if (!exito)
                {
                    ErroresRepositorio.Mostrar(error,
                        "Ya existe una categoría con ese nombre.", TxtNombre);
                    return;
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                ErroresRepositorio.MostrarInesperado(ex);
            }
            finally
            {
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
