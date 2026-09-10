using System;
using System.ComponentModel;
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

            // En el diseñador de VS el Loaded igual se dispara, pero Init() habla con
            // App.Services (arma el contenedor de DI entero) y con la cultura es-MX:
            // nada de eso es del árbol visual y solo sirve para tumbar el lienzo.
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

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
