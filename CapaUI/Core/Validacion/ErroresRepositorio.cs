using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Core.Validacion;

/// <summary>
/// Traduce a español los errores que devuelve el repositorio antes de mostrarlos.
/// </summary>
/// <remarks>
/// <para>
/// Un choque de constraint única vuelve como el código <c>23505</c> de Postgres
/// envuelto en el texto de PostgREST: menciona el nombre de la constraint, el
/// esquema y la tabla. Para el usuario es ilegible, y hasta ahora solo
/// <c>PresentacionModal</c> lo traducía — los otros ocho modales volcaban ese
/// texto crudo en un <c>MessageBox</c>.
/// </para>
/// <para>
/// El reconocimiento del duplicado es genérico; lo único propio de cada entidad
/// es el mensaje y a qué campo hay que devolverle el foco.
/// </para>
/// </remarks>
public static class ErroresRepositorio
{
    /// <summary>
    /// Muestra el error ya traducido. Si es un duplicado, además devuelve el foco
    /// al campo en conflicto y le selecciona el contenido, para que corregirlo sea
    /// escribir encima.
    /// </summary>
    /// <param name="error">Texto tal como vino del repositorio.</param>
    /// <param name="mensajeDuplicado">
    /// Qué decir si resultó ser un choque de unicidad, ej. "Ya existe una
    /// categoría con ese nombre." Si es <c>null</c>, un duplicado se muestra como
    /// cualquier otro error.
    /// </param>
    /// <param name="campoEnConflicto">Campo al que volver. Opcional.</param>
    public static void Mostrar(string error, string? mensajeDuplicado = null,
                               TextBox? campoEnConflicto = null)
    {
        if (mensajeDuplicado is not null && EsDuplicado(error))
        {
            MessageBox.Show(mensajeDuplicado, "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);

            campoEnConflicto?.Focus();
            campoEnConflicto?.SelectAll();
            return;
        }

        MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// La misma traducción que <see cref="Mostrar"/> pero devolviendo el texto en
    /// vez de mostrarlo.
    /// </summary>
    /// <remarks>
    /// Para los modales que muestran el error <b>en línea</b> y no en un
    /// <c>MessageBox</c> — hoy <c>UsuarioModal</c>, que tiene su propio
    /// <c>TxtError</c> en el formulario. Sin esto tendrían que elegir entre
    /// conservar su presentación o recibir la traducción del duplicado; así se
    /// quedan con las dos.
    /// </remarks>
    public static string Traducir(string error, string? mensajeDuplicado = null) =>
        mensajeDuplicado is not null && EsDuplicado(error) ? mensajeDuplicado : error;

    /// <summary>
    /// Mensaje único para las excepciones no previstas. Estaba repetido palabra
    /// por palabra en nueve modales.
    /// </summary>
    public static void MostrarInesperado(Exception ex) =>
        MessageBox.Show(TextoInesperado(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Igual que <see cref="MostrarInesperado"/>, para mostrar en línea.</summary>
    public static string TextoInesperado(Exception ex) => "Error inesperado: " + ex.Message;

    /// <summary>
    /// ¿Es un choque de constraint única? Se miran las tres formas en que puede
    /// llegar (el código SQLSTATE, el texto de Postgres y el de PostgREST) porque
    /// cuál aparece depende de por dónde pasó el error.
    /// </summary>
    public static bool EsDuplicado(string error) =>
        error.Contains("23505", StringComparison.OrdinalIgnoreCase)
        || error.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
        || error.Contains("already exists", StringComparison.OrdinalIgnoreCase);
}
