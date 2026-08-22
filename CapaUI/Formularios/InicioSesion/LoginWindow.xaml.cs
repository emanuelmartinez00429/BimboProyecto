using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Reglas;
using CapaUI.Core.Empresa;
using CapaUI.Services.Empresa;

namespace CapaUI.Formularios.InicioSesion
{
    public partial class LoginWindow : Window
    {
        public event EventHandler? LoginExitoso;

        private readonly IAuthService _authService;
        private readonly IUsuarioSesionService _sesionService;
        private readonly IEmpresaRepository _empresaRepository;
        private readonly LogoEmpresaCache _logoCache;
        private readonly EmpresaThemeService _themeService;
        private bool _pwdVisible = false;

        private static readonly SolidColorBrush _borderBrush  = new(Color.FromRgb(0xD8, 0xDC, 0xE4));
        private static readonly SolidColorBrush _successBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));
        private static readonly SolidColorBrush _primaryBrush = new(Color.FromRgb(0x1A, 0x1F, 0x2E));

        public LoginWindow(
            IAuthService authService,
            IUsuarioSesionService sesionService,
            IEmpresaRepository empresaRepository,
            LogoEmpresaCache logoCache,
            EmpresaThemeService themeService)
        {
            _authService    = authService;
            _sesionService  = sesionService;
            _empresaRepository = empresaRepository;
            _logoCache = logoCache;
            _themeService = themeService;
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Lo primero, antes de cualquier await: si ya hay un logo cacheado de una
            // corrida anterior, se pinta al instante. Así el usuario nunca ve el salto
            // "logo empacado → logo real" en el caso normal (que es casi siempre) —
            // ese salto era lo que se percibía como "está descargando la imagen".
            var rutaYaMostrada = _logoCache.ObtenerRutaCacheadaSinRed();
            if (rutaYaMostrada is not null) AplicarImagenLogo(rutaYaMostrada);

            try
            {
                var resultadoEmpresa = await _empresaRepository.ObtenerAsync();
                if (!resultadoEmpresa.Success)
                    throw new InvalidOperationException(resultadoEmpresa.Error);

                var empresa = resultadoEmpresa.Value;
                _themeService.Aplicar(empresa?.ColorEmpresa);
                if (!string.IsNullOrWhiteSpace(empresa?.DominioCorreo))
                    TxtEmail.GhostSuffix = empresa.DominioCorreo;
                else
                    TxtEmail.GhostSuffix = "@gmail.com";

                await AplicarLogoEmpresaAsync(empresa, rutaYaMostrada);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoginWindow] Error cargando dominio: {ex.Message}");
                TxtEmail.GhostSuffix = "@gmail.com";
            }
        }

        /// <summary>
        /// Resuelve el logo vigente contra <c>empresa.logo_empresa</c>. Si el archivo
        /// que ya se mostró (<paramref name="rutaYaMostrada"/>) sigue siendo el mismo,
        /// no hace nada — evita un reemplazo de imagen innecesario. Si es la primera
        /// vez en esta máquina o el logo cambió de verdad, ahí sí puede haber descarga
        /// real: se muestra el spinner chico solo mientras dura esa espera.
        /// </summary>
        private async Task AplicarLogoEmpresaAsync(EmpresaDto? empresa, string? rutaYaMostrada)
        {
            var rutaStorage = empresa?.LogoEmpresa;
            if (string.IsNullOrWhiteSpace(rutaStorage)) return;

            bool yaEsElVigente = rutaYaMostrada is not null &&
                string.Equals(Path.GetFileName(rutaYaMostrada), Path.GetFileName(rutaStorage),
                    StringComparison.OrdinalIgnoreCase);
            if (yaEsElVigente) return;

            IniciarLogoSpinner();
            try
            {
                var rutaLocal = await _logoCache.ObtenerRutaLocalAsync(rutaStorage);
                if (rutaLocal is not null) AplicarImagenLogo(rutaLocal);
            }
            finally
            {
                DetenerLogoSpinner();
            }
        }

        private void AplicarImagenLogo(string rutaLocal)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // carga y suelta el archivo
                bitmap.UriSource = new Uri(rutaLocal, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                LogoImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoginWindow] Error aplicando logo de empresa: {ex.Message}");
            }
        }

        /// <summary>
        /// Mismo patrón que el spinner grande del panel de "Iniciando sesión" (más
        /// abajo en este archivo): <c>BeginAnimation</c> desde code-behind, nunca un
        /// <c>Visibility</c> bindeado en XAML — ver la nota de por qué en el XAML.
        /// </summary>
        private void IniciarLogoSpinner()
        {
            LogoSpinner.Visibility = Visibility.Visible;
            var spinAnim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
                { RepeatBehavior = RepeatBehavior.Forever };
            LogoSpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, spinAnim);
        }

        private void DetenerLogoSpinner()
        {
            LogoSpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            LogoSpinner.Visibility = Visibility.Collapsed;
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
            border.BorderBrush = EmpresaThemeService.ObtenerBrushPrincipalActual();
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8, ShadowDepth = 0,
                Color = EmpresaThemeService.ObtenerColorPrincipalActual(), Opacity = 0.12
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
            BtnIngresar.IsEnabled = ReglasLogin.CredencialesCompletas(
                TxtEmail.Text,
                _pwdVisible ? TxtPasswordVisible.Text : TxtPassword.Password);
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
            string email    = TxtEmail.GetFullText();
            string password = _pwdVisible ? TxtPasswordVisible.Text : TxtPassword.Password;

            BtnIngresar.IsEnabled = false;
            OcultarError();

            // Mostrar loading
            LoginPanel.Visibility  = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            TitleBarText.Text       = "Iniciando sesión…";
            TxtProgress.Text  = "0%";
            ProgressBar.Width = 0;
            foreach (var (dot, label) in new[] { (S1Dot, S1Text), (S2Dot, S2Text), (S3Dot, S3Text), (S4Dot, S4Text) })
            {
                dot.Background   = Brushes.Transparent;
                dot.BorderBrush  = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
                label.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
                label.FontWeight = FontWeights.Normal;
                if (dot.Child is TextBlock check) check.Visibility = Visibility.Collapsed;
            }

            // Iniciar spinner
            var spinAnim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
                { RepeatBehavior = RepeatBehavior.Forever };
            SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spinAnim);

            try
            {
                // Step 1: Verificar credenciales.
                // La animación corre en paralelo con la llamada real (no en serie),
                // así el tiempo del paso es max(animación, red), no la suma de ambos.
                var animStep1 = AnimarStep(S1Dot, S1Text, 0, 25);
                var loginTask = _authService.LoginAsync(email, password);
                await System.Threading.Tasks.Task.WhenAll(animStep1, loginTask);
                var result = loginTask.Result;
                if (!result.Success)
                {
                    VolverAlLogin(result.Error);
                    return;
                }
                CompletarStep(S1Dot, S1Text);

                // Step 2: Establecer sesión + permisos + perfil (una sola llamada)
                var animStep2  = AnimarStep(S2Dot, S2Text, 25, 70);
                var sesionTask = _sesionService.IniciarSesionAsync(result.Value!.IdUsuario);
                await System.Threading.Tasks.Task.WhenAll(animStep2, sesionTask);
                var sesion = sesionTask.Result;
                if (!sesion.Success)
                {
                    VolverAlLogin(sesion.Error);
                    return;
                }
                CompletarStep(S2Dot, S2Text);

                // Diagnóstico del contrato Permiso(enum) ↔ acciones.nombre_accion (P-018)
                CapaUI.Core.Permisos.SesionPermisos.ValidarContraBD();

                // Step 3: Sincronizar módulos (sin llamada de red detrás, solo cosmético)
                await AnimarStep(S3Dot, S3Text, 70, 100);
                CompletarStep(S3Dot, S3Text);

                // Step 4: Preparar espacio de trabajo — se completa de verdad antes de navegar,
                // en vez del Task.Delay(300) plano que dejaba el check sin marcar.
                await System.Threading.Tasks.Task.Delay(120);
                CompletarStep(S4Dot, S4Text);
                await System.Threading.Tasks.Task.Delay(120);

                SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
                LoginExitoso?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                VolverAlLogin("Error: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task AnimarStep(Border dot, TextBlock label, int desde, int hasta)
        {
            dot.BorderBrush  = EmpresaThemeService.ObtenerBrushPrincipalActual();
            label.Foreground = _primaryBrush;
            label.FontWeight = FontWeights.SemiBold;

            for (int i = desde; i <= hasta; i++)
            {
                int pct = i;
                Dispatcher.Invoke(() => SetProgress(pct));
                await System.Threading.Tasks.Task.Delay(6);
            }
            await System.Threading.Tasks.Task.Delay(50);
        }

        private void CompletarStep(Border dot, TextBlock label)
        {
            dot.Background   = _successBrush;
            dot.BorderBrush  = _successBrush;
            label.Foreground = _successBrush;
            label.FontWeight = FontWeights.Normal;
            if (dot.Child is TextBlock check) check.Visibility = Visibility.Visible;
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
                if (dot.Child is TextBlock check) check.Visibility = Visibility.Collapsed;
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
