using CapaDatos.Repositorios.Usuario;
using CapaServicios;
using ServicioConexión.Conexion;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfColor      = System.Windows.Media.Color;
using WpfBrush      = System.Windows.Media.SolidColorBrush;
using WpfEffect     = System.Windows.Media.Effects.DropShadowEffect;
using UserControl   = System.Windows.Controls.UserControl;
using KeyEventArgs  = System.Windows.Input.KeyEventArgs;

namespace BimboPesaje.Formularios.InicioSesion
{
    public partial class UcLoginForm : UserControl
    {
        private readonly UcLoginShell _shell;
        private bool _pwdVisible = false;

        private static readonly WpfBrush _brandBrush  = new(WpfColor.FromRgb(0x1E, 0x3A, 0x8A));
        private static readonly WpfBrush _borderBrush = new(WpfColor.FromRgb(0xD8, 0xDC, 0xE4));

        public UcLoginForm(UcLoginShell shell)
        {
            _shell = shell;
            InitializeComponent();
        }

        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            var border = GetParentBorder(sender as FrameworkElement);
            if (border == null) return;
            border.BorderBrush = _brandBrush;
            border.Effect = new WpfEffect
            {
                BlurRadius = 8, ShadowDepth = 0,
                Color = WpfColor.FromRgb(0x1E, 0x3A, 0x8A), Opacity = 0.12
            };
        }

        private void Input_LostFocus(object sender, RoutedEventArgs e)
        {
            var border = GetParentBorder(sender as FrameworkElement);
            if (border == null) return;
            border.BorderBrush = _borderBrush;
            border.Effect = null;
        }

        private static System.Windows.Controls.Border? GetParentBorder(FrameworkElement? el)
        {
            var parent = el?.Parent as FrameworkElement;
            while (parent != null)
            {
                if (parent is System.Windows.Controls.Border b) return b;
                parent = parent.Parent as FrameworkElement;
            }
            return null;
        }

        private void Fields_Changed(object sender, RoutedEventArgs e)
        {
            var emailOk = !string.IsNullOrWhiteSpace(TxtEmail.Text);
            var pwdOk   = _pwdVisible
                ? !string.IsNullOrWhiteSpace(TxtPasswordVisible.Text)
                : TxtPassword.Password.Length > 0;
            BtnIngresar.IsEnabled = emailOk && pwdOk;
        }

        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && BtnIngresar.IsEnabled)
                _ = DoLoginAsync();
        }

        private void BtnTogglePwd_Click(object sender, RoutedEventArgs e)
        {
            _pwdVisible = !_pwdVisible;
            if (_pwdVisible)
            {
                TxtPasswordVisible.Text       = TxtPassword.Password;
                TxtPassword.Visibility        = Visibility.Collapsed;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPasswordVisible.Focus();
            }
            else
            {
                TxtPassword.Password          = TxtPasswordVisible.Text;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility        = Visibility.Visible;
                TxtPassword.Focus();
            }
            Fields_Changed(sender, e);
        }

        private void BtnForgot_Click(object sender, RoutedEventArgs e)
            => _shell.NavigateTo(new UcForgotEmailForm(_shell), "Recuperar contraseña");

        private void BtnIngresar_Click(object sender, RoutedEventArgs e) => _ = DoLoginAsync();

        private async Task DoLoginAsync()
        {
            string email    = TxtEmail.Text.Trim();
            string password = _pwdVisible ? TxtPasswordVisible.Text : TxtPassword.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowError("Ingresa usuario y contraseña.");
                return;
            }

            BtnIngresar.IsEnabled = false;
            HideError();

            var loading = new UcLoadingForm(_shell);
            _shell.NavigateTo(loading, "Cargando…");

            try
            {
                var client  = await ConexionSupabase.GetClientAsync();
                var session = await client.Auth.SignInWithPassword(email, password);

                if (session?.User == null)
                {
                    BackToLogin("Credenciales incorrectas.");
                    return;
                }

                var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id!);
                if (usuario == null)
                {
                    await client.Auth.SignOut();
                    BackToLogin("El usuario no está registrado en el sistema.");
                    return;
                }

                SesionActual.IdUsuario     = usuario.idUsuario;
                SesionActual.NombreUsuario = email;

                await loading.CompleteAsync();
                _shell.NotifyLoginSuccess();
            }
            catch (Exception ex)
            {
                BackToLogin("Error: " + ex.Message);
            }
        }

        private void BackToLogin(string error)
        {
            _shell.NavigateTo(this, "Iniciar sesión");
            BtnIngresar.IsEnabled = true;
            ShowError(error);
        }

        private void ShowError(string msg)
        {
            LblError.Text = msg;
            ErrorContainer.Visibility = Visibility.Visible;
        }

        private void HideError() => ErrorContainer.Visibility = Visibility.Collapsed;
    }
}
