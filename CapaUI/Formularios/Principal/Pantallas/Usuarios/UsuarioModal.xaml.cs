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
        private readonly bool                  _esCreacionConEmpleado;
        private ValidadorFormulario            _validador = null!;
        private readonly int                   _preselectedIdEmpleado;
        private readonly string?               _preselectedNombre;
        private readonly string?               _preselectedCorreo;
        private readonly SolicitudIdempotente  _solicitud = new();

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
            IUsuarioRepository    usuarioRepo,
            IRolRepository        rolRepo,
            UsuarioVistaDto?      usuario)
        {
            _usuarioRepo    = usuarioRepo;
            _rolRepo        = rolRepo;
            _usuario        = usuario;
            _esNuevo        = usuario == null;
            InitializeComponent();
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
            _esCreacionConEmpleado   = true;
            _preselectedIdEmpleado   = idEmpleado;
            _preselectedNombre       = nombreEmpleado;
            _preselectedCorreo       = correoEmpleado;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Empleado y contraseña solo valen al crear, y el empleado ni siquiera
            // eso cuando el modal se abrió desde una ficha de empleado (ahí viene
            // preseleccionado y el combo está oculto). SoloSi expresa esa condición
            // sin sacar la regla de la declaración.
            _validador = ValidadorFormulario.Nuevo()
                .Combo(CmbEmpleado, "El empleado").Segun(ReglasUsuario.Empleado)
                    .SoloSi(() => _esNuevo && !_esCreacionConEmpleado)
                .Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)
                .Clave(TxtPassword, "La contraseña").Segun(ReglasUsuario.Password)
                    .SoloSi(() => _esNuevo)
                .Combo(CmbRolModal, "El rol").Segun(ReglasUsuario.Rol)
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear usuario"  : "Editar usuario";

            // ── Empleados (solo al crear) ─────────────────────────────────
            if (_esCreacionConEmpleado)
            {
                RowEmpleado.Visibility   = Visibility.Visible;
                RowPassword.Visibility   = Visibility.Visible;
                CmbEmpleado.Visibility   = Visibility.Collapsed;
                TxtEmpleadoNombre.Visibility = Visibility.Visible;
                TxtEmpleadoNombre.Text   = _preselectedNombre ?? "";
                TxtEmail.Text            = _preselectedCorreo ?? "";
                TxtEmail.IsReadOnly      = true;
            }
            else if (_esNuevo)
            {
                RowEmpleado.Visibility   = Visibility.Visible;
                RowPassword.Visibility   = Visibility.Visible;
                TxtEmpleadoNombre.Visibility = Visibility.Collapsed;
                CmbEmpleado.Visibility   = Visibility.Visible;
                var rEmp = await _usuarioRepo.ObtenerEmpleadosSinUsuarioAsync();
                if (rEmp.Success)
                {
                    CmbEmpleado.Items.Clear();
                    foreach (var emp in rEmp.Value!)
                        CmbEmpleado.Items.Add(new ComboBoxItem
                        {
                            Content = $"{emp.NombreEmpleado} {emp.ApellidoEmpleado}",
                            Tag = emp.IdEmpleado
                        });
                    if (CmbEmpleado.Items.Count > 0)
                        CmbEmpleado.SelectedIndex = 0;
                }
                else
                {
                    MostrarError($"No se pudieron cargar los empleados: {rEmp.Error}");
                    BtnGuardar.IsEnabled = false;
                }

                CmbEmpleado.SelectionChanged += CmbEmpleado_SelectionChanged;
            }
            else
            {
                RowEmpleado.Visibility = Visibility.Collapsed;
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
            }

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            CmbEmpleado.Focus();
        }

        // ── Auto-email al seleccionar empleado ───────────────────────────
        private void CmbEmpleado_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbEmpleado.SelectedItem is ComboBoxItem item && item.Tag is int)
            {
                var nombre = item.Content?.ToString() ?? "";
                TxtEmail.Text = GenerarEmail(nombre);
            }
        }

        /// <summary>
        /// Arma el correo institucional a partir del nombre del empleado
        /// ("José Muñoz" → "jose.munoz@empresa.com").
        /// </summary>
        /// <remarks>
        /// Usa <see cref="TextoBusqueda.Normalizar"/> (quita tildes y pasa a
        /// minúsculas). Antes había acá una copia privada de esa misma lógica;
        /// se borró para no tener dos normalizaciones que puedan divergir — esa
        /// función además está espejada a <c>public.sin_tildes()</c> de Postgres.
        /// El <c>.Trim()</c> que la copia local agregaba se hace acá, porque
        /// <c>Normalizar</c> no recorta por su cuenta.
        /// </remarks>
        private static string GenerarEmail(string nombreCompleto)
        {
            const string dominio = "@empresa.com";
            const int maxTotal = 50;
            int maxLocal = maxTotal - dominio.Length; // 38

            var partes = nombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string local;
            if (partes.Length < 2)
            {
                local = TextoBusqueda.Normalizar(nombreCompleto).Trim();
            }
            else
            {
                var nombre   = TextoBusqueda.Normalizar(partes[0]).Trim();
                var apellido = TextoBusqueda.Normalizar(partes[^1]).Trim();
                local = $"{nombre}.{apellido}";
            }

            if (local.Length > maxLocal)
                local = local[..maxLocal];

            return $"{local}{dominio}";
        }

        // ── Acciones ─────────────────────────────────────────────────────

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            if (!_validador.Validar()) return;

            // Ya validados arriba; acá solo se leen.
            var empItem = CmbEmpleado.SelectedItem as ComboBoxItem;
            int idRol   = (int)((ComboBoxItem)CmbRolModal.SelectedItem).Tag;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _usuario!.IdEstado == 1,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "usuario",
                    nombreRegistro: TxtEmail.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicacion se colgo.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";

            try
            {
                if (_esNuevo)
                {
                    int idEmp = _esCreacionConEmpleado
                        ? _preselectedIdEmpleado
                        : (int)empItem!.Tag;
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
                    if (idRol != _usuario.IdRol)
                    {
                        var rRol = await _usuarioRepo.AsignarRolAsync(_usuario.IdUsuario, idRol,
                            _solicitud.Obtener("asignar_rol_usuario", new { _usuario.IdUsuario, IdRol = idRol }));
                        if (!rRol.Success)
                        {
                            MostrarError(ErroresRepositorio.Traducir(rRol.Error));
                            return;
                        }
                        _solicitud.Confirmar();
                    }

                    int idEstado = RbActivo.IsChecked == true ? 1 : 2;
                    if (idEstado != _usuario.IdEstado)
                    {
                        var rEstado = await _usuarioRepo.CambiarEstadoAsync(_usuario.IdUsuario, idEstado,
                            _solicitud.Obtener("cambiar_estado_usuario", new { _usuario.IdUsuario, IdEstado = idEstado }));
                        if (!rEstado.Success)
                        {
                            MostrarError(ErroresRepositorio.Traducir(rEstado.Error));
                            return;
                        }
                        _solicitud.Confirmar();
                    }
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
