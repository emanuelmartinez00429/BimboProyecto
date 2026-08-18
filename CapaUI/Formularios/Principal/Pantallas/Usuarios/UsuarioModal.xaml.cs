using CapaUI.Core.Controls;
using CapaUI.Core.Validacion;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;

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

        public event Action? Cerrado;
        public event Action? Guardado;

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
                .Combo(CmbEmpleado, "El empleado").Obligatorio()
                    .SoloSi(() => _esNuevo && !_esCreacionConEmpleado)
                .Clave(TxtPassword, "La contraseña").LargoMinimo(6)
                    .SoloSi(() => _esNuevo)
                .Combo(CmbRolModal, "El rol").Obligatorio()
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
            var partes = nombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2)
                return TextoBusqueda.Normalizar(nombreCompleto).Trim() + "@empresa.com";

            var nombre   = TextoBusqueda.Normalizar(partes[0]).Trim();
            var apellido = TextoBusqueda.Normalizar(partes[^1]).Trim();
            return $"{nombre}.{apellido}@empresa.com";
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

                    var r = await _usuarioRepo.CrearAsync(dto);
                    if (!r.Success)
                    {
                        // Se traduce antes de mostrarlo: este modal presenta el
                        // error en linea (TxtError) y no por MessageBox, asi que
                        // usa Traducir en vez de Mostrar.
                        MostrarError(ErroresRepositorio.Traducir(
                            r.Error, "Ya existe un usuario con ese correo."));
                        return;
                    }
                }
                else if (_usuario != null)
                {
                    var dto = new ActualizarUsuarioDto
                    {
                        IdUsuario = _usuario.IdUsuario,
                        IdRol     = idRol,
                        IdEstado  = RbActivo.IsChecked == true ? 1 : 2,
                    };

                    var r = await _usuarioRepo.ActualizarAsync(dto);
                    if (!r.Success)
                    {
                        MostrarError(ErroresRepositorio.Traducir(r.Error));
                        return;
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
