using System;
using System.Globalization;
using System.Windows.Controls;
using CapaDominio;

namespace CapaUI.Formularios.Principal.Pantallas
{
    public partial class WelcomeScreen : UserControl
    {
        public WelcomeScreen()
        {
            InitializeComponent();
            Loaded += (_, _) => Init();
        }

        private void Init()
        {
            TxtFecha.Text = DateTime.Now
                .ToString("dddd, d 'de' MMMM 'de' yyyy", new CultureInfo("es-MX"))
                .ToUpper();

            var nombre = ServicioPerfilUsuario.PerfilActual?.NombreCompleto ?? "";
            RunNombre.Text = nombre.Split(' ')[0];
        }
    }
}
