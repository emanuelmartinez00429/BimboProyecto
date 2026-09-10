using System.Windows;
using System.Windows.Controls;
using WpfColor  = System.Windows.Media.Color;
using WpfBrush  = System.Windows.Media.SolidColorBrush;
using WpfEffect = System.Windows.Media.Effects.DropShadowEffect;
using CapaDominio.Reglas;
using CapaUI.Services.Empresa;

namespace CapaUI.Formularios.InicioSesion
{
    public partial class ForgotEmailPanel : UserControl
    {
        private readonly LoginWindow _win;
        private readonly SolicitarCodigoViewModel _vm;

        private static readonly WpfBrush _borderBrush = new(WpfColor.FromRgb(0xD8, 0xDC, 0xE4));

        public ForgotEmailPanel(LoginWindow win)
        {
            _win = win;
            _vm  = new SolicitarCodigoViewModel(win.Recuperacion);
            InitializeComponent();
            TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => _win.NavigarAlLogin();

        private void TxtEmail_Changed(object sender, TextChangedEventArgs e)
        {
            // ReglasFormato.EsCorreo trata el vacío como válido (es la convención de
            // los campos opcionales), así que acá hace falta exigir contenido
            // aparte: con la caja vacía el botón tiene que quedar deshabilitado.
            _vm.Email = TxtEmail.Text;
            BtnSend.IsEnabled = _vm.PuedeEnviar;
            ErrorContainer.Visibility = Visibility.Collapsed;
        }

        private void Email_GotFocus(object sender, RoutedEventArgs e)
        {
            EmailBorder.BorderBrush = EmpresaThemeService.ObtenerBrushPrincipalActual();
            EmailBorder.Effect = new WpfEffect
            {
                BlurRadius = 8, ShadowDepth = 0,
                Color = EmpresaThemeService.ObtenerColorPrincipalActual(), Opacity = 0.12
            };
        }

        private void Email_LostFocus(object sender, RoutedEventArgs e)
        {
            EmailBorder.BorderBrush = _borderBrush;
            EmailBorder.Effect = null;
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            _vm.Email = TxtEmail.Text;
            BtnSend.IsEnabled = false;
            BtnSend.Content = "Enviando…";

            if (await _vm.EnviarAsync())
            {
                // La pausa no es cosmética: el correo tarda, y saltar al paso siguiente
                // al instante hace que el usuario mire una caja vacía pensando que falló.
                await Task.Delay(600);
                _win.NavigateTo(new ForgotCodePanel(_win, _vm.Email.Trim()), "Verificar código");
                return;
            }

            LblError.Text = _vm.Error;
            ErrorContainer.Visibility = Visibility.Visible;
            BtnSend.IsEnabled = true;
            BtnSend.Content = "Enviar código";
        }
    }
}
