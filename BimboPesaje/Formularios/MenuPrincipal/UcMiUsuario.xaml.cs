using CapaServicios;
using ServicioConexión.Conexion;
using System.Windows;

namespace BimboPesaje.Formularios.MenuPrincipal
{
    public partial class UcMiUsuario : System.Windows.Controls.UserControl
    {
        public UcMiUsuario()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var perfil = ServicioPerfilUsuario.PerfilActual;
            if (perfil == null) return;

            BigAvatar.Text    = perfil.Iniciales;
            FullName.Text     = perfil.NombreCompleto;
            RoleTag.Text      = perfil.NombreRol;
            DataNombre.Text   = perfil.NombreCompleto;
            DataUsuario.Text  = perfil.NombreUsuario;
            DataCorreo.Text   = perfil.Correo;
            PwdSubtitle.Text  = $"Recibirás una confirmación al correo {perfil.Correo}.";
        }

        private void BtnCambiarPassword_Click(object sender, RoutedEventArgs e)
        {
            PanelPassword.Visibility = PanelPassword.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private async void BtnGuardarPassword_Click(object sender, RoutedEventArgs e)
        {
            string actual   = TxtPwdActual.Password.Trim();
            string nueva    = TxtPwdNueva.Password.Trim();
            string confirmar= TxtPwdConfirm.Password.Trim();

            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(nueva) || string.IsNullOrEmpty(confirmar))
            {
                System.Windows.MessageBox.Show("Completa los tres campos de contraseña.",
                    "Campos vacíos", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (nueva != confirmar)
            {
                System.Windows.MessageBox.Show("La nueva contraseña y la confirmación no coinciden.",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (nueva.Length < 6)
            {
                System.Windows.MessageBox.Show("La contraseña debe tener al menos 6 caracteres.",
                    "Contraseña débil", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnGuardarPassword.IsEnabled = false;

                var client = await ConexionSupabase.GetClientAsync();
                await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = nueva });

                TxtPwdActual.Clear();
                TxtPwdNueva.Clear();
                TxtPwdConfirm.Clear();
                PanelPassword.Visibility = Visibility.Collapsed;

                System.Windows.MessageBox.Show("Contraseña actualizada correctamente.",
                    "Éxito", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al cambiar la contraseña:\n{ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                BtnGuardarPassword.IsEnabled = true;
            }
        }
    }
}
