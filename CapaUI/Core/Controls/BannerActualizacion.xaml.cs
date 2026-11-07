using System.Windows.Controls;

namespace CapaUI.Core.Controls
{
    /// <summary>
    /// Banner compartido por el login y la ventana principal. Solo bindea contra el
    /// <c>DataContext</c> (un <c>ActualizacionViewModel</c>); no toca estado ni abre
    /// diálogos, así se puede reutilizar en las dos ventanas sin duplicar lógica.
    /// </summary>
    public partial class BannerActualizacion : UserControl
    {
        public BannerActualizacion()
        {
            InitializeComponent();
        }
    }
}
