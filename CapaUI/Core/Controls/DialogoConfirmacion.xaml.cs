using System.Windows;
using System.Windows.Input;

namespace CapaUI.Core.Controls;

/// <summary>
/// Diálogo de confirmación con el lenguaje visual del proyecto, para acciones
/// que el usuario no puede deshacer solo (inactivar un registro, por ejemplo).
/// </summary>
/// <remarks>
/// <para>
/// Reemplaza al <c>MessageBox.Show(..., YesNo)</c> nativo: ese diálogo gris de
/// Windows encima de un modal azul se lee como un error del sistema y no como
/// una pregunta de la aplicación. Además, "Sí/No" no dice qué se va a hacer —
/// acá el botón que confirma lleva el verbo de la acción ("Inactivar").
/// </para>
/// <para>
/// Es una <see cref="Window"/> y no un overlay dentro del modal a propósito:
/// los modales del proyecto son <c>UserControl</c> hospedados por la vista
/// padre, así que un overlay propio obligaría a que cada host lo soporte.
/// Como <c>Window</c> modal (<see cref="Window.ShowDialog"/>) funciona igual
/// desde cualquiera, sin que el host se entere.
/// </para>
/// <para>
/// El foco arranca en <c>Volver</c> y Escape cancela: ante la duda, ni Enter ni
/// Escape deben terminar ejecutando lo destructivo.
/// </para>
/// </remarks>
public partial class DialogoConfirmacion : Window
{
    private DialogoConfirmacion()
    {
        InitializeComponent();
        Loaded += (_, _) => BtnVolver.Focus();
    }

    /// <summary>
    /// Muestra la advertencia y devuelve <c>true</c> solo si el usuario confirmó.
    /// </summary>
    /// <param name="titulo">Encabezado corto, ej. "Inactivar categoría".</param>
    /// <param name="mensaje">Qué implica seguir adelante, en una o dos frases.</param>
    /// <param name="textoConfirmar">Verbo del botón de confirmación, ej. "Inactivar".</param>
    /// <param name="propietario">
    /// Ventana sobre la que se centra. Si es <c>null</c> se usa la activa.
    /// </param>
    public static bool Confirmar(string titulo, string mensaje,
                                 string textoConfirmar = "Confirmar",
                                 Window? propietario = null)
    {
        var dialogo = new DialogoConfirmacion
        {
            Owner = propietario ?? Application.Current?.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsActive),
        };

        dialogo.TxtTitulo.Text      = titulo;
        dialogo.TxtMensaje.Text     = mensaje;
        dialogo.BtnConfirmar.Content = textoConfirmar;

        // Sin Owner no hay CenterOwner posible: se cae a centrar en pantalla.
        if (dialogo.Owner is null)
            dialogo.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return dialogo.ShowDialog() == true;
    }

    /// <summary>
    /// Atajo para el caso más frecuente: pasar un registro existente a inactivo.
    /// Centraliza el texto para que los siete modales digan exactamente lo mismo.
    /// </summary>
    /// <param name="entidad">Nombre del tipo en minúscula, ej. "categoría".</param>
    /// <param name="nombreRegistro">Cómo se llama el registro que se está tocando.</param>
    public static bool ConfirmarInactivacion(string entidad, string nombreRegistro) =>
        Confirmar(
            titulo: $"Inactivar {entidad}",
            mensaje: $"«{nombreRegistro}» va a quedar inactivo y dejará de aparecer " +
                     $"en los listados y buscadores que filtran por activos.\n\n" +
                     $"Podés volver a activarlo más adelante desde este mismo formulario.",
            textoConfirmar: "Inactivar");

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnVolver_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        Close();
    }
}
