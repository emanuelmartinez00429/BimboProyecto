using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CapaDominio.Reglas;
using TextBox        = System.Windows.Controls.TextBox;
using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
using WpfDataFormats = System.Windows.DataFormats;


namespace CapaUI.Formularios.InicioSesion
{

    public partial class ForgotCodePanel : UserControl
    {
        private readonly LoginWindow _win;
        private readonly VerificarCodigoViewModel _vm;
        private readonly string _email;
        private TextBox[] _digits = null!;
        private DispatcherTimer _timer = null!;
        private int _secondsLeft = 45;

        public ForgotCodePanel(LoginWindow win, string email)
        {
            _win   = win;
            _email = email;
            _vm    = new VerificarCodigoViewModel(win.Recuperacion, email);
            InitializeComponent();

            SubtitleBlock.Inlines.Clear();
            SubtitleBlock.Inlines.Add(new System.Windows.Documents.Run("Enviamos un código de 8 dígitos a "));
            SubtitleBlock.Inlines.Add(new System.Windows.Documents.Bold(
                new System.Windows.Documents.Run(email)));
            SubtitleBlock.Inlines.Add(new System.Windows.Documents.Run(". Ingresa el código a continuación."));

            Loaded += (_, _) =>
            {
                _digits = [D1, D2, D3, D4, D5, D6, D7, D8];
                StartCountdown();
                D1.Focus();
            };
            Unloaded += (_, _) => _timer?.Stop();
        }

        private void StartCountdown()
        {
            _secondsLeft = 45;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) =>
            {
                _secondsLeft--;
                if (_secondsLeft <= 0)
                {
                    _timer.Stop();
                    TxtCountdown.Visibility = Visibility.Collapsed;
                    BtnResend.Visibility    = Visibility.Visible;
                }
                else
                {
                    TxtCountdown.Text = $"Reenviar en 0:{_secondsLeft:D2}";
                }
            };
            _timer.Start();
        }

        private string GetCode() => string.Concat(_digits.Select(d => d.Text));

        private void CheckComplete()
        {
            if (_digits == null) return;
            BtnVerify.IsEnabled = ReglasLogin.OtpCompleto(_digits.Select(d => d.Text));
            ErrorContainer.Visibility = Visibility.Collapsed;
        }

        private void Digit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !ReglasLogin.EsDigitoOtp(e.Text[0]);
        }

        private void Digit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_digits == null) return;
            var box = (TextBox)sender;
            int idx = Convert.ToInt32(box.Tag);
            if (box.Text.Length == 1 && idx < 7)
                _digits[idx + 1].Focus();
            CheckComplete();
        }

        private void Digit_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_digits == null) return;
            var box = (TextBox)sender;
            int idx = Convert.ToInt32(box.Tag);

            if (e.Key == Key.Back && box.Text.Length == 0 && idx > 0)
            {
                _digits[idx - 1].Focus();
                _digits[idx - 1].SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Left && idx > 0)
            {
                _digits[idx - 1].Focus(); e.Handled = true;
            }
            else if (e.Key == Key.Right && idx < 7)
            {
                _digits[idx + 1].Focus(); e.Handled = true;
            }
        }

        private void Digit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && BtnVerify.IsEnabled)
                _ = VerifyAsync();
        }

        private void Digit_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = ((string)e.DataObject.GetData(typeof(string))).Trim();
                if (ReglasLogin.OtpValido(text))
                {
                    for (int i = 0; i < ReglasLogin.LongitudOtp; i++)
                        _digits[i].Text = text[i].ToString();
                    _digits[ReglasLogin.LongitudOtp - 1].Focus();
                    CheckComplete();
                    e.CancelCommand();
                    return;
                }
            }
            if (!e.DataObject.GetDataPresent(WpfDataFormats.UnicodeText))
                e.CancelCommand();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _win.NavigateTo(new ForgotEmailPanel(_win), "Recuperar contraseña");
        }

        private async void BtnResend_Click(object sender, RoutedEventArgs e)
        {
            BtnResend.Visibility    = Visibility.Collapsed;
            TxtCountdown.Visibility = Visibility.Visible;
            ErrorContainer.Visibility = Visibility.Collapsed;
            if (await _vm.ReenviarAsync())
            {
                StartCountdown();
                return;
            }

            // #9: Informar al usuario si el reenvío falló
            TxtCountdown.Visibility = Visibility.Collapsed;
            BtnResend.Visibility    = Visibility.Visible;
            LblError.Text = _vm.Error;
            ErrorContainer.Visibility = Visibility.Visible;
        }

        private void BtnVerify_Click(object sender, RoutedEventArgs e) => _ = VerifyAsync();

        private async Task VerifyAsync()
        {
            BtnVerify.IsEnabled = false;
            BtnVerify.Content   = "Verificando…";
            ErrorContainer.Visibility = Visibility.Collapsed;

            _vm.Codigo = GetCode();

            if (await _vm.VerificarAsync())
            {
                _timer?.Stop();
                _win.NavigateTo(new ForgotNewPanel(_win, _email), "Nueva contraseña");
                return;
            }

            LblError.Text = _vm.Error;
            ErrorContainer.Visibility = Visibility.Visible;
            BtnVerify.IsEnabled = true;
            BtnVerify.Content   = "Verificar código";
            foreach (var d in _digits) d.Text = "";
            D1.Focus();
        }
    }
}
