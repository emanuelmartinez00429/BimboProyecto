using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear usuario"  : "Editar usuario";

            // ── Empleados (solo al crear) ─────────────────────────────────
            if (_esNuevo)
            {
                RowEmpleado.Visibility  = Visibility.Visible;
                RowPassword.Visibility = Visibility.Visible;
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

        private static string GenerarEmail(string nombreCompleto)
        {
            var partes = nombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2)
                return Normalizar(nombreCompleto) + "@empresa.com";

            var nombre  = Normalizar(partes[0]);
            var apellido = Normalizar(partes[^1]);
            return $"{nombre}.{apellido}@empresa.com";
        }

        /// <summary>
        /// Quita acentos/diacríticos ("Muñoz" → "munoz") para emails auto-generados.
        /// FormD descompone cada letra acentuada en letra base + marca combinante;
        /// se filtran las marcas iterando sobre char (nunca bytes UTF-8) y se
        /// recompone con FormC. Ver nota de referencia en la bóveda:
        /// ".NET - Normalización Unicode (FormD-FormC) para quitar acentos".
        /// </summary>
        private static string Normalizar(string s)
        {
            var formD = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(formD.Length);
            foreach (var c in formD)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC)
                     .ToLowerInvariant().Trim();
        }

        // ── Acciones ─────────────────────────────────────────────────────

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            ComboBoxItem? empItem = null;
            if (_esNuevo)
            {
                if (CmbEmpleado.SelectedItem is not ComboBoxItem item)
                {
                    MostrarError("Seleccione un empleado.");
                    return;
                }
                empItem = item;

                if (TxtPassword.Password.Length < 6)
                {
                    MostrarError("La contraseña debe tener al menos 6 caracteres.");
                    return;
                }
            }

            if (CmbRolModal.SelectedItem is not ComboBoxItem rolItem)
            {
                MostrarError("Seleccione un rol.");
                return;
            }

            int idRol = (int)rolItem.Tag;
            BtnGuardar.IsEnabled = false;

            try
            {
                if (_esNuevo)
                {
                    int idEmp = (int)empItem.Tag;
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
                        MostrarError(r.Error);
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
                        MostrarError(r.Error);
                        return;
                    }
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                MostrarError("Error inesperado: " + ex.Message);
            }
            finally
            {
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
