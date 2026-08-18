using System.Windows;

namespace CapaUI.Core.Validacion;

/// <summary>
/// Aviso de cambio de estado activo/inactivo, con el texto centralizado para que
/// los siete modales digan exactamente lo mismo.
/// </summary>
/// <remarks>
/// <para>
/// Usa el <see cref="MessageBox"/> nativo a propósito. Hubo antes un diálogo
/// propio con el lenguaje visual del proyecto y se descartó: al ser una ventana
/// aparte se dibujaba encima del modal tapándolo a medias, y no se entendía sobre
/// qué registro estaba preguntando. El diálogo del sistema se posiciona y se
/// dimensiona solo, y el usuario ya sabe leerlo.
/// </para>
/// <para>
/// Avisa en <b>los dos sentidos</b>. Inactivar esconde el registro de los
/// listados que filtran por activos; activarlo lo devuelve a circulación, y en un
/// catálogo compartido eso también es un efecto que conviene confirmar.
/// </para>
/// </remarks>
public static class ConfirmacionEstado
{
    /// <summary>
    /// Pregunta si corresponde. Devuelve <c>true</c> cuando se puede seguir con el
    /// guardado — incluso si no hubo nada que preguntar.
    /// </summary>
    /// <param name="esNuevo">
    /// En un alta no se pregunta: crear algo directamente inactivo es una decisión
    /// explícita del usuario, no una consecuencia inesperada. Avisar ahí sería
    /// ruido, y el ruido enseña a ignorar el aviso.
    /// </param>
    /// <param name="estabaActivo">
    /// Estado original ya resuelto a <c>bool</c>. Se recibe calculado y no el DTO
    /// porque cada módulo guarda el estado distinto: <c>bool</c> en Categorías,
    /// <c>int == 1</c> en Empleados/Productos/Usuarios y el enum
    /// <c>EstadoRegistro</c> en Fabricantes/Presentaciones/Proveedores.
    /// </param>
    /// <param name="quedaActivo">Estado elegido en el formulario.</param>
    /// <param name="entidad">Tipo en minúscula y singular, ej. "categoría".</param>
    /// <param name="nombreRegistro">Cómo se llama el registro que se está editando.</param>
    public static bool Confirmar(bool esNuevo, bool estabaActivo, bool quedaActivo,
                                 string entidad, string nombreRegistro)
    {
        if (esNuevo || estabaActivo == quedaActivo) return true;

        var (titulo, mensaje) = quedaActivo
            ? ($"Activar {entidad}",
               $"«{nombreRegistro}» va a volver a estar activo y aparecerá de nuevo " +
               $"en los listados y buscadores.\n\n¿Confirmás el cambio?")
            : ($"Inactivar {entidad}",
               $"«{nombreRegistro}» va a quedar inactivo y dejará de aparecer en los " +
               $"listados y buscadores que filtran por activos.\n\n" +
               $"Podés volver a activarlo más adelante desde este mismo formulario.\n\n" +
               $"¿Confirmás el cambio?");

        return MessageBox.Show(mensaje, titulo,
            MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
        // Default en "No": si alguien manda un Enter de más, no se ejecuta el cambio.
    }
}
