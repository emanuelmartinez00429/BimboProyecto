using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
using CapaUI.Core.Seguridad;
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
        private readonly SolicitudIdempotente    _solicitud = new();
        private ChangeTracker<PresentacionSnapshot> _tracker = new(null);

        private sealed record PresentacionSnapshot(
            string Nombre,
            string Descripcion);

        public event Action? Cerrado;
        public event Action? Guardado;

        /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
        public PresentacionModal()
        {
            _repo = null!;
            InitializeComponent();
        }

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

                _tracker = new ChangeTracker<PresentacionSnapshot>(new PresentacionSnapshot(
                    (_presentacion.Nombre ?? string.Empty).Trim(),
                    (_presentacion.Descripcion ?? string.Empty).Trim()));
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

                // CreateAsync devuelve Result<int> y UpdateAsync Result: son tipos
                // distintos, así que no se pueden unificar en un ternario.
                bool exito;
                string error;

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto, _solicitud.Obtener("crear_presentacion", dto), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);
                }
                else
                {
                    var snapshotActual = new PresentacionSnapshot(
                        dto.Nombre,
                        dto.Descripcion);

                    bool datosCambiaron = _tracker.IsDirty(snapshotActual);
                    bool estadoCambio = _presentacion != null && dto.IdEstado != _presentacion.IdEstado;

                    if (!datosCambiaron && !estadoCambio)
                    {
                        Cerrado?.Invoke();
                        return;
                    }

                    exito = true;
                    error = string.Empty;

                    if (datosCambiaron)
                    {
                        var r = await _repo.UpdateAsync(dto, _solicitud.Obtener("actualizar_presentacion", dto), CancellationToken.None);
                        (exito, error) = (r.Success, r.Error);
                    }

                    // El estado es un comando aparte: actualizar_presentacion_seguro
                    // no lo toca. Solo se llama si realmente cambió.
                    if (exito && estadoCambio)
                    {
                        var rEstado = await _repo.CambiarEstadoAsync(
                            dto.Id,
                            dto.IdEstado,
                            _solicitud.Obtener("cambiar_estado_presentacion", new { dto.Id, dto.IdEstado }),
                            CancellationToken.None);
                        if (!rEstado.Success)
                        {
                            if (datosCambiaron)
                            {
                                // Transacción compensatoria / fallo parcial: los datos se guardaron pero falló el cambio de estado
                                _solicitud.Confirmar();
                                Guardado?.Invoke();
                                MessageBox.Show(
                                    $"Los datos de la presentación se actualizaron correctamente, pero no se pudo cambiar su estado: {rEstado.Error}\n\nPor favor, intente cambiar el estado nuevamente.",
                                    "Aviso de actualización parcial",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                return;
                            }

                            (exito, error) = (rEstado.Success, rEstado.Error);
                        }
                    }
                }

                if (!exito)
                {
                    ErroresRepositorio.Mostrar(error,
                        "Ya existe una presentación con ese nombre.", TxtNombre);
                    return;
                }
                _solicitud.Confirmar();

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
