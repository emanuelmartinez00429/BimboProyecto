using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaUI.Core.Permisos;
using CapaDatos.Repositorios.Usuario;
using CapaServicios;

namespace CapaUI.Formularios.InicioSesion
{
    public partial class LoginWindow : Window
    {
        public event EventHandler? LoginExitoso;

        private bool _pwdVisible = false;

        private static readonly SolidColorBrush _brandBrush   = new(Color.FromRgb(0x1E, 0x3A, 0x8A));
        private static readonly SolidColorBrush _borderBrush  = new(Color.FromRgb(0xD8, 0xDC, 0xE4));
        private static readonly SolidColorBrush _successBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));
        private static readonly SolidColorBrush _primaryBrush = new(Color.FromRgb(0x1A, 0x1F, 0x2E));

        public LoginWindow()
        {
            InitializeComponent();
        }

        // ── Chrome ───────────────────────────────────────────────────────────
        private void ChromeBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        // ── Focus effects ─────────────────────────────────────────────────────
        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            var border = GetParentBorder(sender as FrameworkElement);
            if (border == null) return;
            border.BorderBrush = _brandBrush;
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8, ShadowDepth = 0,
                Color = Color.FromRgb(0x1E, 0x3A, 0x8A), Opacity = 0.12
            };
        }

        private void Input_LostFocus(object sender, RoutedEventArgs e)
        {
            var border = GetParentBorder(sender as FrameworkElement);
            if (border == null) return;
            border.BorderBrush = _borderBrush;
            border.Effect = null;
        }

        private static Border? GetParentBorder(FrameworkElement? el)
        {
            var p = el?.Parent as FrameworkElement;
            while (p != null) { if (p is Border b) return b; p = p.Parent as FrameworkElement; }
            return null;
        }

        // ── Campos ───────────────────────────────────────────────────────────
        private void Fields_Changed(object sender, RoutedEventArgs e)
        {
            var emailOk = !string.IsNullOrWhiteSpace(TxtEmail.Text);
            var pwdOk = _pwdVisible
                ? !string.IsNullOrWhiteSpace(TxtPasswordVisible.Text)
                : TxtPassword.Password.Length > 0;
            BtnIngresar.IsEnabled = emailOk && pwdOk;
        }

        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && BtnIngresar.IsEnabled)
                _ = IngresarAsync();
        }

        // ── Toggle password ───────────────────────────────────────────────────
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

        // ── Login ─────────────────────────────────────────────────────────────
        private void BtnIngresar_Click(object sender, RoutedEventArgs e) => _ = IngresarAsync();

        private async System.Threading.Tasks.Task IngresarAsync()
        {
            string email    = TxtEmail.Text.Trim();
            string password = _pwdVisible ? TxtPasswordVisible.Text : TxtPassword.Password;

            BtnIngresar.IsEnabled = false;
            OcultarError();

            // Mostrar loading
            LoginPanel.Visibility  = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            TitleBarText.Text       = "Iniciando sesión…";

            // Iniciar spinner
            var spinAnim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
                { RepeatBehavior = RepeatBehavior.Forever };
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spinAnim);

            try
            {
                // Step 1: Verificar credenciales
                await AnimarStep(S1Dot, S1Text, 0, 25);

                var client  = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                var session = await client.Auth.SignInWithPassword(email, password);

                if (session?.User == null)
                {
                    VolverAlLogin("Credenciales incorrectas.");
                    return;
                }

                CompletarStep(S1Dot, S1Text);

                // Step 2: Cargar perfil
                await AnimarStep(S2Dot, S2Text, 25, 55);

                var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id!);
                if (usuario == null)
                {
                    await client.Auth.SignOut();
                    VolverAlLogin("Usuario no registrado en el sistema.");
                    return;
                }

                SesionActual.IdUsuario         = usuario.idUsuario;
                SesionActual.NombreUsuario      = email;
                servicioSesionActual.IdUsuario  = usuario.idUsuario;

                CompletarStep(S2Dot, S2Text);

                // Step 3: Sincronizar módulos / permisos
                await AnimarStep(S3Dot, S3Text, 55, 80);
                await SesionPermisos.CargarAsync(usuario.idRol);
                CompletarStep(S3Dot, S3Text);

                // Step 4: Preparar espacio
                await AnimarStep(S4Dot, S4Text, 80, 100);
                await ServicioPerfilUsuario.CargarAsync();
                CompletarStep(S4Dot, S4Text);

                await System.Threading.Tasks.Task.Delay(300);
                LoginExitoso?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                VolverAlLogin("Error: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task AnimarStep(Border dot, TextBlock label, int desde, int hasta)
        {
            dot.BorderBrush  = _brandBrush;
            label.Foreground = _primaryBrush;
            label.FontWeight = FontWeights.SemiBold;

            for (int i = desde; i <= hasta; i++)
            {
                int pct = i;
                Dispatcher.Invoke(() => SetProgress(pct));
                await System.Threading.Tasks.Task.Delay(18);
            }
            await System.Threading.Tasks.Task.Delay(180);
        }

        private void CompletarStep(Border dot, TextBlock label)
        {
            dot.Background   = _successBrush;
            dot.BorderBrush  = _successBrush;
            label.Foreground = _successBrush;
            label.FontWeight = FontWeights.Normal;
        }

        private void SetProgress(int pct)
        {
            TxtProgress.Text = $"{pct}%";
            ProgressBar.Width = ProgressTrack.ActualWidth * pct / 100.0;
        }

        private void VolverAlLogin(string error)
        {
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            LoadingPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility   = Visibility.Visible;
            TitleBarText.Text       = "Iniciar sesión";
            BtnIngresar.IsEnabled   = true;
            MostrarError(error);
            // Reset loading UI
            TxtProgress.Text  = "0%";
            ProgressBar.Width = 0;
            foreach (var (dot, label) in new[] { (S1Dot, S1Text), (S2Dot, S2Text), (S3Dot, S3Text), (S4Dot, S4Text) })
            {
                dot.Background   = Brushes.Transparent;
                dot.BorderBrush  = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
                label.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
                label.FontWeight = FontWeights.Normal;
            }
        }

        private void MostrarError(string msg)
        {
            LblError.Text             = msg;
            ErrorContainer.Visibility = Visibility.Visible;
        }

        private void OcultarError() => ErrorContainer.Visibility = Visibility.Collapsed;

        private void BtnForgot_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(new ForgotEmailPanel(this), "Recuperar contraseña");
        }

        // ── Navegación entre vistas de recuperación ────────────────────────────
        public void NavigateTo(System.Windows.Controls.UserControl view, string title)
        {
            TitleBarText.Text       = title;
            LoginPanel.Visibility   = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ForgotContent.Content   = view;
            ForgotContent.Visibility = Visibility.Visible;
        }

        public void NavigarAlLogin()
        {
            ForgotContent.Visibility = Visibility.Collapsed;
            ForgotContent.Content    = null;
            LoginPanel.Visibility    = Visibility.Visible;
            TitleBarText.Text        = "Iniciar sesión";
        }
    }
}
