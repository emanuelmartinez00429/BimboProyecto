using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
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
        private ValidadorFormulario              _validador = null!;

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
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtNombre, "El nombre").Segun(ReglasPresentacion.Nombre)
                .Campo(TxtDescripcion, "La descripción").Segun(ReglasPresentacion.Descripcion)
                .ValidarAlSalirDelCampo();

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
            if (!_validador.Validar()) return;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _presentacion!.IdEstado == EstadoRegistro.Activo,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "presentación",
                    nombreRegistro: TxtNombre.Text.Trim()))
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

                bool exito;
                string error;

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto);
                    (exito, error) = (r.Success, r.Error);
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto);
                    (exito, error) = (r.Success, r.Error);
                }

                if (!exito)
                {
                    ErroresRepositorio.Mostrar(error,
                        "Ya existe una presentación con ese nombre.", TxtNombre);
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

        // El manejo del duplicado 23505 que vivía acá se generalizó a
        // ErroresRepositorio y ahora lo usan los nueve modales, no solo este.
    }
}
