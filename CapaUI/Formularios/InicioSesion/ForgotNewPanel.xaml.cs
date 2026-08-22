using System.Windows;
using System.Windows.Controls;
using CapaDominio.Reglas;
using WpfColor     = System.Windows.Media.Color;
using WpfBrush     = System.Windows.Media.SolidColorBrush;
using WpfEffect    = System.Windows.Media.Effects.DropShadowEffect;
using WpfBrushes   = System.Windows.Media.Brushes;
using WpfColorConv = System.Windows.Media.ColorConverter;
using CapaUI.Services.Empresa;

namespace CapaUI.Formularios.InicioSesion
{
    public partial class ForgotNewPanel : UserControl
    {
        private readonly LoginWindow _win;
        private readonly string _email;
        private bool _show1 = false, _show2 = false;

        private static readonly WpfBrush _successBrush  = new(WpfColor.FromRgb(0x10, 0xB9, 0x81));
        private static readonly WpfBrush _borderBrush   = new(WpfColor.FromRgb(0xD8, 0xDC, 0xE4));
        private static readonly WpfBrush _disabledBrush = new(WpfColor.FromRgb(0x9C, 0xA3, 0xAF));

        private static readonly string[] _strengthLabels = ["", "Muy débil", "Débil", "Aceptable", "Buena", "Fuerte"];
        private static readonly string[] _strengthColors  = ["#E5E7EB", "#B91C1C", "#DC2626", "#F59E0B", "#10B981", "#059669"];

        public ForgotNewPanel(LoginWindow win, string email)
        {
            _win   = win;
            _email = email;
            InitializeComponent();
        }

        private string NewPassword     => _show1 ? TxtNewVisible.Text     : TxtNew.Password;
        private string ConfirmPassword => _show2 ? TxtConfirmVisible.Text : TxtConfirm.Password;

        // ── Focus effects ──
        private void Pass_GotFocus(object sender, RoutedEventArgs e)
        {
            if (GetBorder(sender as FrameworkElement) is { } b)
            {
                b.BorderBrush = EmpresaThemeService.ObtenerBrushPrincipalActual();
                b.Effect = new WpfEffect { BlurRadius = 8, ShadowDepth = 0, Color = EmpresaThemeService.ObtenerColorPrincipalActual(), Opacity = 0.12 };
            }
        }

        private void Pass_LostFocus(object sender, RoutedEventArgs e)
        {
            if (GetBorder(sender as FrameworkElement) is { } b)
            {
                b.BorderBrush = _borderBrush;
                b.Effect = null;
            }
        }

        private static Border? GetBorder(FrameworkElement? el)
        {
            var p = el?.Parent as FrameworkElement;
            while (p != null) { if (p is Border b) return b; p = p.Parent as FrameworkElement; }
            return null;
        }

        // ── Password 1 ──
        private void TxtNew_Changed(object sender, RoutedEventArgs e)      { UpdateStrength(TxtNew.Password); Validate(); }
        private void TxtNewVisible_Changed(object sender, TextChangedEventArgs e) { UpdateStrength(TxtNewVisible.Text); Validate(); }

        // ── Password 2 ──
        private void TxtConfirm_Changed(object sender, RoutedEventArgs e)      => Validate();
        private void TxtConfirmVisible_Changed(object sender, TextChangedEventArgs e) => Validate();

        // ── Eye toggles ──
        private void BtnToggle1_Click(object sender, RoutedEventArgs e)
        {
            _show1 = !_show1;
            if (_show1) { TxtNewVisible.Text = TxtNew.Password; TxtNew.Visibility = Visibility.Collapsed; TxtNewVisible.Visibility = Visibility.Visible; TxtNewVisible.Focus(); }
            else         { TxtNew.Password = TxtNewVisible.Text; TxtNewVisible.Visibility = Visibility.Collapsed; TxtNew.Visibility = Visibility.Visible; TxtNew.Focus(); }
            Validate();
        }

        private void BtnToggle2_Click(object sender, RoutedEventArgs e)
        {
            _show2 = !_show2;
            if (_show2) { TxtConfirmVisible.Text = TxtConfirm.Password; TxtConfirm.Visibility = Visibility.Collapsed; TxtConfirmVisible.Visibility = Visibility.Visible; TxtConfirmVisible.Focus(); }
            else         { TxtConfirm.Password = TxtConfirmVisible.Text; TxtConfirmVisible.Visibility = Visibility.Collapsed; TxtConfirm.Visibility = Visibility.Visible; TxtConfirm.Focus(); }
            Validate();
        }

        private void ChkShow_Changed(object sender, RoutedEventArgs e)
        {
            bool show = ChkShow.IsChecked == true;
            if (show != _show1) BtnToggle1_Click(sender, e);
            if (show != _show2) BtnToggle2_Click(sender, e);
        }

        // ── Strength meter ──
        private void UpdateStrength(string pwd)
        {
            if (string.IsNullOrEmpty(pwd)) { StrengthPanel.Visibility = Visibility.Collapsed; return; }
            StrengthPanel.Visibility = Visibility.Visible;

            int score = ReglasContrasena.CalcularScore(pwd);
            score = Math.Clamp(score, 0, 5);

            var bars  = new[] { Bar1, Bar2, Bar3, Bar4, Bar5 };
            var hex   = _strengthColors[score];
            var fill  = new WpfBrush((WpfColor)WpfColorConv.ConvertFromString(hex)!);
            var empty = new WpfBrush(WpfColor.FromRgb(0xE5, 0xE7, 0xEB));

            for (int i = 0; i < 5; i++)
                bars[i].Background = i < score ? fill : empty;

            StrengthLabel.Text       = score > 0 ? $"Seguridad: {_strengthLabels[score]}" : "";
            StrengthLabel.Foreground = fill;
        }

        // ── Rule indicators ──
        private void UpdateRules(string pwd)
        {
            SetRule(R1Icon, R1Text, ReglasContrasena.TieneLargoMinimo(pwd));
            SetRule(R2Icon, R2Text, ReglasContrasena.TieneMayuscula(pwd));
            SetRule(R3Icon, R3Text, ReglasContrasena.TieneNumero(pwd));
            SetRule(R4Icon, R4Text, ReglasContrasena.TieneSimbolo(pwd));
        }

        private void SetRule(System.Windows.Shapes.Ellipse icon, TextBlock label, bool pass)
        {
            icon.Fill   = pass ? _successBrush : WpfBrushes.Transparent;
            icon.Stroke = pass ? _successBrush : _disabledBrush;
            label.Foreground = pass ? _successBrush : _disabledBrush;
        }

        // ── Validation ──
        private void Validate()
        {
            string pwd     = NewPassword;
            string confirm = ConfirmPassword;

            UpdateRules(pwd);

            bool allRules = ReglasContrasena.CumpleTodasLasReglas(pwd);

            bool match = pwd == confirm;
            LblMismatch.Visibility = (!match && confirm.Length > 0)
                ? Visibility.Visible : Visibility.Collapsed;

            BtnSubmit.IsEnabled = allRules && match && pwd.Length > 0;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => _win.NavigateTo(new ForgotCodePanel(_win, _email), "Verificar código");

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            BtnSubmit.IsEnabled = false;
            BtnSubmit.Content   = "Actualizando…";
            ErrorContainer.Visibility = Visibility.Collapsed;

            try
            {
                var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = NewPassword });
                await client.Auth.SignOut();
                ShowSuccess();
            }
            catch (Exception)
            {
                LblError.Text = "No se pudo actualizar la contraseña. Inténtalo de nuevo.";
                ErrorContainer.Visibility = Visibility.Visible;
                BtnSubmit.IsEnabled = true;
                BtnSubmit.Content   = "Actualizar contraseña";
            }
        }

        private void ShowSuccess()
        {
            FormPanel.Visibility    = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            _win.NavigateTo(this, "Contraseña actualizada");
        }

        private void BtnGoToLogin_Click(object sender, RoutedEventArgs e)
            => _win.NavigarAlLogin();
    }
}
