using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Permisos;
using CapaUI.Core.Seguridad;

namespace CapaUI.Formularios.Principal.Pantallas.Usuarios
{
    public partial class UsuarioModal : System.Windows.Controls.UserControl
    {
        private readonly IUsuarioRepository    _usuarioRepo;
        private readonly IRolRepository        _rolRepo;
        private readonly UsuarioVistaDto?      _usuario;
        private readonly bool                  _esNuevo;
        private ValidadorFormulario            _validador = null!;
        private readonly int                   _preselectedIdEmpleado;
        private readonly string?               _preselectedNombre;
        private readonly string?               _preselectedCorreo;
        private readonly SolicitudIdempotente  _solicitud = new();
        private ChangeTracker<UsuarioSnapshot> _tracker = new(null);
        private PasswordVisibilityController?  _passwordVisibility;

        private sealed record UsuarioSnapshot(int IdRol, int IdEstado);

        public event Action? Cerrado;
        public event Action? Guardado;

        /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
        public UsuarioModal()
        {
            _usuarioRepo = null!;
            _rolRepo     = null!;
            InitializeComponent();
        }

        public UsuarioModal(
            IUsuarioRepository usuarioRepo,
            IRolRepository     rolRepo,
            UsuarioVistaDto    usuario)
        {
            _usuarioRepo = usuarioRepo ?? throw new ArgumentNullException(nameof(usuarioRepo));
            _rolRepo     = rolRepo ?? throw new ArgumentNullException(nameof(rolRepo));
            _usuario     = usuario ?? throw new ArgumentNullException(nameof(usuario));
            _esNuevo     = false;
            InitializeComponent();
            InitializePasswordVisibility();
            Loaded += OnLoaded;
        }

        public UsuarioModal(
            IUsuarioRepository usuarioRepo,
            IRolRepository     rolRepo,
            int                idEmpleado,
            string             nombreEmpleado,
            string             correoEmpleado)
        {
            _usuarioRepo             = usuarioRepo;
            _rolRepo                 = rolRepo;
            _usuario                 = null;
            _esNuevo                 = true;
            _preselectedIdEmpleado   = idEmpleado;
            _preselectedNombre       = nombreEmpleado;
            _preselectedCorreo       = correoEmpleado;
            InitializeComponent();
            InitializePasswordVisibility();
            Loaded += OnLoaded;
        }

        private void InitializePasswordVisibility()
        {
            _passwordVisibility = new PasswordVisibilityController(TxtPassword, TxtPasswordVisible);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)
                .Clave(TxtPassword, "La contraseña").Segun(ReglasUsuario.Password)
                    .SoloSi(() => _esNuevo)
                .Combo(CmbRolModal, "El rol").Segun(ReglasUsuario.Rol)
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear usuario"  : "Editar usuario";

            // ── Empleados (solo al crear con empleado preseleccionado) ────
            if (_esNuevo)
            {
                RowEmpleado.Visibility       = Visibility.Visible;
                RowPassword.Visibility       = Visibility.Visible;
                TxtEmpleadoNombre.Text       = _preselectedNombre ?? "";
                TxtEmail.Text                = _preselectedCorreo ?? "";
                TxtEmail.IsReadOnly          = true;
            }
            else
            {
                RowEmpleado.Visibility       = Visibility.Collapsed;
                RowPassword.Visibility       = Visibility.Collapsed;
            }

            // ── Roles ─────────────────────────────────────────────────────
            var rRol = await _rolRepo.ObtenerTodosAsync();
            if (rRol.Success)
            {
                CmbRolModal.Items.Clear();
                foreach (var rol in rRol.Value!)
                    CmbRolModal.Items.Add(new ComboBoxItem
                    {
                        Content = rol.NombreRol,
                        Tag     = rol.IdRol
                    });
            }
            else
            {
                MostrarError($"No se pudieron cargar los roles: {rRol.Error}");
                BtnGuardar.IsEnabled = false;
            }

            // ── Modo edición ──────────────────────────────────────────────
            if (!_esNuevo && _usuario != null)
            {
                RowEstado.Visibility = Visibility.Visible;
                CmbRolModal.IsEnabled = SesionPermisos.Tiene(Permiso.AsignarRolUsuario);
                RbActivo.IsEnabled = SesionPermisos.Tiene(Permiso.EliminarUsuario);
                RbInactivo.IsEnabled = SesionPermisos.Tiene(Permiso.EliminarUsuario);

                TxtEmail.Text = _usuario.CorreoUsuario;

                foreach (ComboBoxItem item in CmbRolModal.Items)
                    if (item.Tag is int rid && rid == _usuario.IdRol)
                    {
                        CmbRolModal.SelectedItem = item;
                        break;
                    }

                RbActivo.IsChecked   = _usuario.IdEstado == 1;
                RbInactivo.IsChecked = _usuario.IdEstado != 1;

                _tracker = new ChangeTracker<UsuarioSnapshot>(new UsuarioSnapshot(
                    _usuario.IdRol,
                    _usuario.IdEstado));
            }

            // Foco al abrir: en la contraseña al crear, en el rol al editar.
            if (_esNuevo)
                TxtPassword.Focus();
            else
                CmbRolModal.Focus();
        }

        // ── Acciones ─────────────────────────────────────────────────────

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
            => _passwordVisibility?.Toggle();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            if (!_validador.Validar()) return;

            int idRol = (int)((ComboBoxItem)CmbRolModal.SelectedItem).Tag;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _usuario!.IdEstado == 1,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "usuario",
                    nombreRegistro: TxtEmail.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicación se colgó.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";

            try
            {
                if (_esNuevo)
                {
                    int idEmp    = _preselectedIdEmpleado;
                    string email = TxtEmail.Text.Trim();

                    var dto = new CrearUsuarioDto
                    {
                        IdEmpleado = idEmp,
                        Email      = email,
                        Password   = TxtPassword.Password,
                        IdRol      = idRol,
                    };

                    var r = await _usuarioRepo.CrearAsync(dto, _solicitud.Obtener("crear_usuario", dto));
                    if (!r.Success)
                    {
                        // Se traduce antes de mostrarlo: este modal presenta el
                        // error en linea (TxtError) y no por MessageBox, asi que
                        // usa Traducir en vez de Mostrar.
                        MostrarError(ErroresRepositorio.Traducir(
                            r.Error, "Ya existe un usuario con ese correo."));
                        return;
                    }
                    _solicitud.Confirmar();
                }
                else if (_usuario != null)
                {
                    int idEstado = RbActivo.IsChecked == true ? 1 : 2;
                    var snapshotActual = new UsuarioSnapshot(idRol, idEstado);
                    bool cambioRol = idRol != _usuario.IdRol;
                    bool cambioEstado = idEstado != _usuario.IdEstado;

                    if (!_tracker.IsDirty(snapshotActual))
                    {
                        Cerrado?.Invoke();
                        return;
                    }

                    if (cambioRol)
                    {
                        var rRol = await _usuarioRepo.AsignarRolAsync(_usuario.IdUsuario, idRol,
                            _solicitud.Obtener("asignar_rol_usuario", new { _usuario.IdUsuario, IdRol = idRol }));
                        if (!rRol.Success)
                        {
                            MostrarError(ErroresRepositorio.Traducir(rRol.Error));
                            return;
                        }
                    }

                    if (cambioEstado)
                    {
                        var rEstado = await _usuarioRepo.CambiarEstadoAsync(_usuario.IdUsuario, idEstado,
                            _solicitud.Obtener("cambiar_estado_usuario", new { _usuario.IdUsuario, IdEstado = idEstado }));
                        if (!rEstado.Success)
                        {
                            if (cambioRol)
                            {
                                // El rol se consolidó exitosamente en la llamada previa.
                                // Manejo honesto de fallo parcial sin rollback destructivo (AP-03 / P-061).
                                _solicitud.Confirmar();
                                MessageBox.Show(
                                    $"El rol del usuario se actualizó correctamente, pero no se pudo cambiar el estado:\n{ErroresRepositorio.Traducir(rEstado.Error)}",
                                    "Aviso de Estado", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Guardado?.Invoke();
                                return;
                            }

                            MostrarError(ErroresRepositorio.Traducir(rEstado.Error));
                            return;
                        }
                    }

                    _solicitud.Confirmar();
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                MostrarError(ErroresRepositorio.TextoInesperado(ex));
            }
            finally
            {
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }

        // ── Errores ──────────────────────────────────────────────────────

        private void MostrarError(string msg)
        {
            TxtError.Text       = msg;
            TxtError.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            TxtError.Visibility = Visibility.Collapsed;
            TxtError.Text       = "";
        }
    }
}
