using System.Windows.Controls;

namespace CapaUI.Core.Controls
{
    /// <summary>
    /// ARCHIVO TEMPORAL DE DIAGNÓSTICO — borrar cuando se cierre el tema de la
    /// previsualización en el diseñador. No lo usa ninguna pantalla de la app.
    /// <para/>
    /// Todo lo que hace es <c>InitializeComponent()</c>: sin datos, sin DI, sin eventos.
    /// La lógica del diagnóstico está en el XAML — ver el comentario de arriba de todo
    /// de <c>DiagnosticoDisenador.xaml</c> para cómo leer las tres franjas.
    /// </summary>
    public partial class DiagnosticoDisenador : UserControl
    {
        public DiagnosticoDisenador() => InitializeComponent();
    }
}
