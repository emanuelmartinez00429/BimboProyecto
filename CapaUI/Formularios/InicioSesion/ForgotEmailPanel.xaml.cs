using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using WpfColor  = System.Windows.Media.Color;
using WpfBrush  = System.Windows.Media.SolidColorBrush;
using WpfEffect = System.Windows.Media.Effects.DropShadowEffect;

namespace CapaUI.Formularios.InicioSesion
{
    public partial class ForgotEmailPanel : UserControl
    {
        private readonly LoginWindow _win;
        private static readonly Regex _emailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static readonly WpfBrush _brandBrush  = new(WpfColor.FromRgb(0x1E, 0x3A, 0x8A));
        private static readonly WpfBrush _borderBrush = new(WpfColor.FromRgb(0xD8, 0xDC, 0xE4));

        public ForgotEmailPanel(LoginWindow win)
        {
            _win = win;
            InitializeComponent();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => _win.NavigarAlLogin();

        private void TxtEmail_Changed(object sender, TextChangedEventArgs e)
        {
            BtnSend.IsEnabled = _emailRegex.IsMatch(TxtEmail.Text.Trim());
            ErrorContainer.Visibility = Visibility.Collapsed;
        }

        private void Email_GotFocus(object sender, RoutedEventArgs e)
        {
            EmailBorder.BorderBrush = _brandBrush;
            EmailBorder.Effect = new WpfEffect
            {
                BlurRadius = 8, ShadowDepth = 0,
                Color = WpfColor.FromRgb(0x1E, 0x3A, 0x8A), Opacity = 0.12
            };
        }

        private void Email_LostFocus(object sender, RoutedEventArgs e)
        {
            EmailBorder.BorderBrush = _borderBrush;
            EmailBorder.Effect = null;
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string email = TxtEmail.Text.Trim();
            BtnSend.IsEnabled = false;
            BtnSend.Content = "Enviando…";

            try
            {
                var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
                await client.Auth.ResetPasswordForEmail(email);
                await Task.Delay(600);

                _win.NavigateTo(new ForgotCodePanel(_win, email), "Verificar código");
            }
            catch (Exception ex)
            {
                LblError.Text = ex.Message;
                ErrorContainer.Visibility = Visibility.Visible;
                BtnSend.IsEnabled = true;
                BtnSend.Content = "Enviar código";
            }
        }
    }
}
