using System.Windows;

namespace CapaUI.Core.Controls
{
    /// <summary>
    /// Texto de marcador ("placeholder") de un <c>TextBox</c>, dibujado por la propia
    /// plantilla del estilo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Antes cada pantalla superponía su propio <c>TextBlock</c> gris sobre la caja y le
    /// acertaba el margen a ojo: <c>24</c> en el viejo <c>CamionModal</c>, <c>12</c> en los modales
    /// de tabla. Ninguno de esos números coincidía con dónde arranca el texto de verdad,
    /// que es <c>BorderThickness + Padding</c> del estilo — 11px sin foco y 12px con foco
    /// en <c>MInput</c>/<c>CeldaInput</c>. Resultado: el marcador quedaba corrido respecto
    /// del cursor, y distinto en cada pantalla.
    /// </para>
    /// <para>
    /// Poniéndolo dentro de la plantilla, el marcador usa <b>el mismo</b>
    /// <c>{TemplateBinding Padding}</c> que el <c>PART_ContentHost</c>, así que cae
    /// exactamente sobre el cursor por construcción — en cualquier estado y sin que cada
    /// consumidor tenga que adivinar nada. Uso:
    /// </para>
    /// <code>
    /// &lt;TextBox Style="{StaticResource CeldaInput}"
    ///          controls:Placeholder.Texto="Sin observaciones"/&gt;
    /// </code>
    /// </remarks>
    public static class Placeholder
    {
        public static readonly DependencyProperty TextoProperty =
            DependencyProperty.RegisterAttached(
                "Texto", typeof(string), typeof(Placeholder),
                new FrameworkPropertyMetadata(string.Empty));

        public static void SetTexto(DependencyObject elemento, string valor) =>
            elemento.SetValue(TextoProperty, valor);

        public static string GetTexto(DependencyObject elemento) =>
            (string)elemento.GetValue(TextoProperty);
    }
}
