using System;
using System.Globalization;
using System.Windows.Controls;
using CapaAplicacion.Perfil;
using Microsoft.Extensions.DependencyInjection;

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

            var perfil = App.Services.GetRequiredService<IPerfilUsuarioService>();
            var nombre = perfil.PerfilActual?.NombreCompleto ?? "";
            RunNombre.Text = nombre.Split(' ')[0];
        }
    }
}
