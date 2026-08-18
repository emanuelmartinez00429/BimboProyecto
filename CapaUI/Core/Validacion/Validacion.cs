using System.Windows;

namespace CapaUI.Core.Validacion;

/// <summary>
/// Estado de error de un campo, como propiedad adjunta, para que los estilos
/// puedan reaccionar sin que el validador tenga que conocerlos.
/// </summary>
/// <remarks>
/// Son dos propiedades y no una a propósito: <see cref="ErrorProperty"/> lleva el
/// texto (lo consume el renglón de error y el ToolTip) y
/// <see cref="TieneErrorProperty"/> es el booleano que disparan los
/// <c>Trigger</c> de XAML. Un <c>Trigger</c> no puede preguntar "¿este string
/// está vacío?" sin un converter, así que el booleano se mantiene sincronizado
/// desde acá y el XAML queda limpio.
/// </remarks>
public static class Validacion
{
    public static readonly DependencyProperty ErrorProperty =
        DependencyProperty.RegisterAttached(
            "Error", typeof(string), typeof(Validacion),
            new PropertyMetadata(null, OnErrorChanged));

    public static void SetError(DependencyObject elemento, string? valor) =>
        elemento.SetValue(ErrorProperty, valor);

    public static string? GetError(DependencyObject elemento) =>
        (string?)elemento.GetValue(ErrorProperty);

    public static readonly DependencyProperty TieneErrorProperty =
        DependencyProperty.RegisterAttached(
            "TieneError", typeof(bool), typeof(Validacion),
            new PropertyMetadata(false));

    public static void SetTieneError(DependencyObject elemento, bool valor) =>
        elemento.SetValue(TieneErrorProperty, valor);

    public static bool GetTieneError(DependencyObject elemento) =>
        (bool)elemento.GetValue(TieneErrorProperty);

    private static void OnErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        SetTieneError(d, !string.IsNullOrWhiteSpace((string?)e.NewValue));
}
