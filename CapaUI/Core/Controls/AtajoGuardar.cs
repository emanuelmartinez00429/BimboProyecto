using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapaUI.Core.Controls;

/// <summary>
/// Comportamiento adjunto: Ctrl+Enter dispara el botón "Guardar" del modal,
/// desde cualquier campo con foco (tunneling de <see cref="UIElement.PreviewKeyDown"/>
/// desde la raíz hasta el elemento enfocado, así que no importa en qué TextBox/
/// ComboBox/RadioButton esté el usuario).
/// </summary>
/// <remarks>
/// <para>
/// No duplica la lógica de guardado: en vez de llamar a un método, dispara el
/// evento <see cref="ButtonBase.Click"/> del botón real vía
/// <see cref="ButtonAutomationPeer"/> — es lo mismo que hace un clic físico, así
/// que la validación, el "Guardando…" y el disable mientras guarda (todo lo que
/// ya vive en <c>BtnGuardar_Click</c> de cada modal) se respeta sin tocarlo.
/// </para>
/// <para>
/// Solo <c>Ctrl+Enter</c>, nunca <c>Enter</c> solo: los modales de selección de
/// catálogo (<see cref="SelectorCatalogoModal"/>) y los combos filtrables ya usan
/// Enter para confirmar una fila/opción — atarlo acá también los pisaría.
/// </para>
/// </remarks>
public static class AtajoGuardar
{
    public static readonly DependencyProperty BotonProperty =
        DependencyProperty.RegisterAttached(
            "Boton", typeof(Button), typeof(AtajoGuardar),
            new PropertyMetadata(null, OnBotonChanged));

    public static void SetBoton(UIElement elemento, Button? valor) => elemento.SetValue(BotonProperty, valor);
    public static Button? GetBoton(UIElement elemento) => (Button?)elemento.GetValue(BotonProperty);

    private static void OnBotonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement raiz) return;

        raiz.PreviewKeyDown -= AlPresionarTecla;
        if (e.NewValue is Button) raiz.PreviewKeyDown += AlPresionarTecla;
    }

    private static void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        // Key.Enter y Key.Return: mismo patrón defensivo que ya usan ComboFiltro
        // y SuggestionSearchBox en este proyecto para la tecla Enter.
        if (e.Key is not (Key.Enter or Key.Return)) return;
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (sender is not UIElement raiz) return;

        var boton = GetBoton(raiz);
        if (boton is null || !boton.IsEnabled || !boton.IsVisible) return;

        e.Handled = true;
        ((IInvokeProvider)new ButtonAutomationPeer(boton)).Invoke();
    }
}
