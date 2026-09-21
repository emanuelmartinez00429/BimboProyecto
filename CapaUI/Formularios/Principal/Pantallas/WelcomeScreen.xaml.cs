using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
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

            // Se muestra el ALIAS COMPLETO (hasta dos nombres) del perfil fresco —
            // el mismo valor que usa la tarjeta del sidebar; sin recortar al
            // primer nombre. Si no hay alias, es el nombre real completo.
            var perfil = App.Services.GetRequiredService<IPerfilUsuarioService>();
            RunNombre.Text = perfil.PerfilActual?.NombreCompleto ?? "";
        }
    }
}
