using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using UserControl = System.Windows.Controls.UserControl;

namespace BimboPesaje.Formularios.InicioSesion
{
    public partial class UcLoginShell : UserControl
    {
        public event EventHandler? MinimizeRequested;
        public event EventHandler? CloseRequested;
        public event EventHandler? LoginSucceeded;
        public event EventHandler? DragMoveRequested;

        public UcLoginShell()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                CargarLogoLocal();
                NavigateTo(new UcLoginForm(this), "Iniciar sesión");
            };
        }

        /// <summary>
        /// Intenta cargar el logo desde el caché local (%AppData%\BimboPesaje\Assets\logo.png).
        /// Si no existe, deja el logo embebido que ya está en el XAML.
        /// </summary>
        private void CargarLogoLocal()
        {
            try
            {
                string rutaLocal = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BimboPesaje", "Assets", "logo.png");

                if (!System.IO.File.Exists(rutaLocal)) return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource      = new Uri(rutaLocal, UriKind.Absolute);
                bitmap.CacheOption    = BitmapCacheOption.OnLoad; // libera el file lock
                bitmap.CreateOptions  = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                bitmap.Freeze();

                LogoImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UcLoginShell] No se pudo cargar logo local: {ex.Message}");
            }
        }

        public void NavigateTo(UserControl view, string title)
        {
            TitleBarText.Text = title;
            MainContent.Content = view;
        }

        public void NotifyLoginSuccess() => LoginSucceeded?.Invoke(this, EventArgs.Empty);

        private void ChromeBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMoveRequested?.Invoke(this, EventArgs.Empty);

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => MinimizeRequested?.Invoke(this, EventArgs.Empty);

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
