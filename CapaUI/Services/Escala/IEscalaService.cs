using System.Windows;
using CapaAplicacion.Common;

namespace CapaUI.Services.Escala;

/// <summary>
/// Escalado propio de la aplicación: un factor por usuario y por pantalla que agranda o
/// achica toda la interfaz, por encima de la escala que ya aplica Windows.
/// <para/>
/// Resuelve el caso de una terminal al 175 %: la app se ve enorme y el layout se degrada.
/// Con un factor de 0,8 el contenido vuelve a un tamaño usable en esa pantalla, sin tocar
/// la configuración de Windows ni afectar a las demás aplicaciones.
/// <para/>
/// Salvo <see cref="AplicarA"/>, que la llama cada ventana al nacer, ninguna operación
/// pide una <see cref="Window"/>: el servicio resuelve solo cuál es la ventana de
/// referencia. Así un ViewModel puede cambiar la escala sin tocar el árbol visual.
/// </summary>
public interface IEscalaService
{
    /// <summary>Factor aplicado en este momento. <c>1.0</c> mientras no se haya aplicado ninguno.</summary>
    double Factor { get; }

    /// <summary>
    /// <c>true</c> cuando hay un factor impuesto por <c>UI_ESCALA</c>. Mientras lo esté,
    /// <see cref="GuardarAsync"/> y <see cref="RestablecerAsync"/> se niegan, y la pantalla
    /// de preferencias tiene que decírselo al usuario en vez de dejarlo mover un control
    /// que no va a tener efecto.
    /// </summary>
    bool EstaForzado { get; }

    /// <summary>
    /// <c>true</c> cuando hay un modal abierto en la aplicación (sea un diálogo modal Win32/WPF
    /// o un overlay activo con <c>ModalOverlay</c> visible). Mientras lo esté, el cambio de escala
    /// en vivo se bloquea para no alterar el layout en medio de una transacción.
    /// </summary>
    bool HayModalAbierto { get; }

    /// <summary>Se dispara justo antes de aplicar un nuevo factor de escala sobre las ventanas.</summary>
    event Action<double>? EscalaCambiando;

    /// <summary>Se dispara tras haber aplicado un nuevo factor de escala sobre las ventanas.</summary>
    event Action<double>? EscalaCambiado;

    /// <summary>
    /// Carga los factores del usuario de la sesión desde la caché local, sin tocar la red.
    /// Se llama antes de construir la ventana principal, para que nazca con la escala
    /// puesta en vez de saltar a la vista. Mismo papel que
    /// <c>EmpresaThemeService.CargarCacheSinRed()</c>.
    /// </summary>
    void CargarCacheSinRed();

    /// <summary>
    /// Trae los factores desde Supabase y, si difieren de la caché, la reescribe.
    /// No reaplica la escala sobre las ventanas ya abiertas: la corrección se ve en el
    /// próximo inicio de sesión. Cambiar la escala de golpe bajo los pies del usuario,
    /// sin que él haya tocado nada, es peor que mostrarla un arranque más tarde.
    /// </summary>
    Task RefrescarDesdeBaseAsync(CancellationToken ct = default);

    /// <summary>
    /// Aplica a la ventana el factor que corresponda a la pantalla donde está abriendo.
    /// Se llama desde <c>OnSourceInitialized</c>, antes del primer pase de layout, para
    /// que no haya un reacomodo visible.
    /// </summary>
    void AplicarA(Window ventana);

    /// <summary>
    /// Aplica el factor a todas las ventanas abiertas <b>sin persistirlo</b>.
    /// <para/>
    /// Es lo que corre mientras el usuario toca los botones de la pantalla de
    /// preferencias: el cambio se ve de inmediato y la escritura a Supabase queda para
    /// cuando deje de tocar. Sin esta separación, ir de 1,0 a 0,75 serían cinco viajes
    /// a la red, uno por clic.
    /// </summary>
    void Aplicar(double factor);

    /// <summary>
    /// Persiste el factor para la pantalla actual y lo aplica.
    /// <para/>
    /// Las guardas previas al cambio —cerrar popups y submenús, bloquear con un modal
    /// abierto— son responsabilidad de quien llama: dependen del árbol visual y no de
    /// este servicio.
    /// </summary>
    Task<Result> GuardarAsync(double factor, CancellationToken ct = default);

    /// <summary>
    /// Borra la preferencia de esta pantalla y vuelve al factor de respaldo.
    /// <para/>
    /// Borrar no es lo mismo que guardar 1,0: sin fila, la pantalla hereda el ámbito
    /// <c>global</c> si existe, y recién después cae en el neutro.
    /// </summary>
    Task<Result> RestablecerAsync(CancellationToken ct = default);

    /// <summary>
    /// Escala de Windows en la pantalla actual (1.0 = 100 %), para sugerir un factor
    /// con <c>EscalaUi.Sugerido(...)</c>.
    /// </summary>
    double EscalaDeWindows();
}
