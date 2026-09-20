using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CapaAplicacion.Common;
using CapaAplicacion.Preferencias;
using CapaAplicacion.Preferencias.Dtos;
using CapaAplicacion.Preferencias.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;

namespace CapaUI.Services.Escala;

/// <summary>
/// Implementa el escalado propio con un <see cref="ScaleTransform"/> en el
/// <c>LayoutTransform</c> del contenido de cada ventana.
/// <para/>
/// Es <c>LayoutTransform</c> y no <c>RenderTransform</c> ni <c>Viewbox</c> porque el
/// contenido tiene que <b>volver a medirse</b> con el tamaño nuevo: un transform de
/// render escala el bitmap ya dibujado y el texto sale borroso.
/// <para/>
/// Vive en <c>CapaUI</c> —y no en una capa inferior— por el mismo motivo que
/// <c>EmpresaThemeService</c>: toca <see cref="Application"/> y <see cref="Window"/>.
/// La lectura y escritura del valor baja por el contrato de <c>CapaAplicacion</c>.
/// </summary>
public sealed class EscalaService : IEscalaService
{
    private readonly IUsuarioSesionService          _sesion;
    private readonly IPreferenciasUsuarioRepository _repo;
    private readonly ICacheEscalaLocal              _cache;

    /// <summary>Factores por ámbito: la huella de cada pantalla, más <c>global</c> de respaldo.</summary>
    private Dictionary<string, double> _factores = [];

    /// <summary>
    /// Medidas declaradas de cada ventana, antes de escalarlas. Sin esto, aplicar dos
    /// veces seguidas multiplicaría el ancho dos veces.
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/> para no retener ventanas cerradas.
    /// </summary>
    private readonly ConditionalWeakTable<Window, MedidasDeclaradas> _medidas = [];

    /// <summary>
    /// Factor impuesto por <c>UI_ESCALA</c>, que gana sobre la preferencia guardada.
    /// <c>null</c> cuando no hay override. Ver <see cref="FactorForzado"/>.
    /// </summary>
    private double? _forzado;

    public double Factor { get; private set; } = EscalaUi.Normal;

    public bool EstaForzado => _forzado is not null;

    public EscalaService(
        IUsuarioSesionService sesion,
        IPreferenciasUsuarioRepository repo,
        ICacheEscalaLocal cache)
    {
        _sesion = sesion;
        _repo   = repo;
        _cache  = cache;
    }

    // ── Carga ────────────────────────────────────────────────────────────────

    public void CargarCacheSinRed()
    {
        _forzado = FactorForzado();
        if (_forzado is not null)
            Serilog.Log.Warning("[EscalaService] Factor forzado por UI_ESCALA: {Factor}", _forzado);

        var idUsuario = _sesion.SesionActual?.IdUsuario;
        if (idUsuario is null) return;

        _factores = new Dictionary<string, double>(_cache.Leer(idUsuario.Value));
    }

    /// <summary>
    /// Factor impuesto desde <c>UI_ESCALA</c> (variable de entorno o <c>App.config</c>),
    /// con el mismo mecanismo que <c>LOG_LEVEL</c>.
    /// <para/>
    /// Existe para poder ver la app a cualquier escala sin pasar por la pantalla de
    /// preferencias — durante el desarrollo, y después para que QA reproduzca un tamaño
    /// puntual sin tocar la cuenta del usuario. Es solo de lectura: no se persiste nada,
    /// y mientras esté activo <see cref="CambiarAsync"/> se niega a guardar, para que no
    /// convivan dos fuentes de verdad.
    /// </summary>
    private static double? FactorForzado()
    {
        var valor = Environment.GetEnvironmentVariable("UI_ESCALA")
                 ?? System.Configuration.ConfigurationManager.AppSettings["UI_ESCALA"];

        return double.TryParse(
            valor,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var factor)
            ? EscalaUi.Ajustar(factor)
            : null;
    }

    public async Task RefrescarDesdeBaseAsync(CancellationToken ct = default)
    {
        var idUsuario = _sesion.SesionActual?.IdUsuario;
        if (idUsuario is null) return;

        var resultado = await _repo.ObtenerTodasAsync(idUsuario.Value, ct);
        if (!resultado.Success || resultado.Value is null)
        {
            // Sin red o con error: la caché local ya cubrió el arranque.
            Serilog.Log.Debug("[EscalaService] No se pudieron refrescar las preferencias: {Error}", resultado.Error);
            return;
        }

        var desdeBase = resultado.Value
            .Where(p => p.Clave == ClavesPreferencia.EscalaUi)
            .ToDictionary(
                p => p.Ambito,
                p => EscalaUi.Ajustar(ValorPreferencia.Numero(p.ValorJson, EscalaUi.Normal)));

        if (SonElMismoConjunto(_factores, desdeBase)) return;

        _factores = desdeBase;
        _cache.Guardar(idUsuario.Value, _factores);
    }

    // ── Aplicación ───────────────────────────────────────────────────────────

    public void AplicarA(Window ventana)
    {
        ArgumentNullException.ThrowIfNull(ventana);

        var factor = FactorPara(HuellaDe(ventana));
        Aplicar(ventana, factor);
    }

    public void Aplicar(double factor)
    {
        var ajustado = EscalaUi.Ajustar(factor);

        // A todas las ventanas abiertas. No se resuelve el factor por pantalla acá: el
        // usuario está mirando una y eligió un valor concreto; recalcular por monitor
        // haría que la ventana de al lado cambie sola a otro tamaño.
        foreach (Window abierta in Application.Current.Windows)
            Aplicar(abierta, ajustado);
    }

    public async Task<Result> GuardarAsync(double factor, CancellationToken ct = default)
    {
        if (_forzado is not null)
            return Result.Fail("Hay un factor de escala forzado por UI_ESCALA; quitalo para poder cambiarlo desde la app.");

        var idUsuario = _sesion.SesionActual?.IdUsuario;
        if (idUsuario is null) return Result.Fail("No hay una sesión activa.");

        var ventana = VentanaDeReferencia();
        if (ventana is null) return Result.Fail("No hay una ventana abierta sobre la que aplicar la escala.");

        var ajustado = EscalaUi.Ajustar(factor);
        var huella   = HuellaDe(ventana);

        var guardado = await _repo.GuardarAsync(
            idUsuario.Value,
            ClavesPreferencia.EscalaUi,
            huella,
            ValorPreferencia.DesdeNumero(ajustado),
            ct);

        if (!guardado.Success) return guardado;

        _factores[huella] = ajustado;
        _cache.Guardar(idUsuario.Value, _factores);
        Aplicar(ajustado);

        return Result.Ok();
    }

    public async Task<Result> RestablecerAsync(CancellationToken ct = default)
    {
        if (_forzado is not null)
            return Result.Fail("Hay un factor de escala forzado por UI_ESCALA; quitalo para poder cambiarlo desde la app.");

        var idUsuario = _sesion.SesionActual?.IdUsuario;
        if (idUsuario is null) return Result.Fail("No hay una sesión activa.");

        var ventana = VentanaDeReferencia();
        if (ventana is null) return Result.Fail("No hay una ventana abierta sobre la que aplicar la escala.");

        var huella = HuellaDe(ventana);

        var borrado = await _repo.EliminarAsync(idUsuario.Value, ClavesPreferencia.EscalaUi, huella, ct);
        if (!borrado.Success) return borrado;

        _factores.Remove(huella);
        _cache.Guardar(idUsuario.Value, _factores);

        // Sin fila propia, esta pantalla vuelve a heredar 'global' si existe, y recién
        // después cae en el neutro. Por eso se recalcula en vez de asignar 1,0.
        Aplicar(FactorPara(huella));

        return Result.Ok();
    }

    /// <summary>
    /// Ventana contra la que se resuelven la huella de pantalla y el DPI. Es la principal;
    /// si todavía no hay (arranque, o cerrando sesión), la primera abierta.
    /// </summary>
    private static Window? VentanaDeReferencia() =>
        Application.Current?.MainWindow
        ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault();

    /// <summary>Corazón del servicio: deja la ventana en el factor pedido, de forma idempotente.</summary>
    private void Aplicar(Window ventana, double factor)
    {
        if (ventana.Content is not FrameworkElement raiz) return;

        var declaradas = _medidas.GetValue(ventana, MedidasDeclaradas.Capturar);
        var cambio     = !EscalaUi.SonIguales(Factor, factor);

        Factor = factor;

        raiz.LayoutTransform = EscalaUi.EsNormal(factor)
            ? Transform.Identity
            : new ScaleTransform(factor, factor);

        // NO se toca TextFormattingMode. Probado en pantalla el 2026-09-20: forzar `Ideal`
        // fuera del factor neutro deja el texto visiblemente borroso —los campos de
        // PesajeModal y los botones compartidos fueron los peores casos—, y `ClearType` no
        // lo rescata porque el contenido cuelga del overlay translúcido de los modales, y
        // WPF apaga ClearType sobre superficies con transparencia.
        //
        // La advertencia de Microsoft sobre `Display` + zoom ("the worst of all 2D
        // transforms for display mode text") habla de espaciado desparejo entre glifos, no
        // de borrosidad. En el rango 0,70–1,30 que usa la app, ese espaciado no se nota y
        // la nitidez sí. Se deja lo que declara el XAML de cada ventana.

        declaradas.AplicarEscaladas(ventana, factor);

        // Windows cachea el tamaño mínimo hasta que la ventana avisa que cambió su marco.
        // Sin esto, tras un cambio en vivo el mínimo viejo sigue vigente.
        if (cambio) ForzarRelecturaDelMarco(ventana);
    }

    // ── Pantalla ─────────────────────────────────────────────────────────────

    /// <summary>Huella de la pantalla donde está la ventana: el ámbito con el que se guarda el factor.</summary>
    private static string HuellaDe(Window ventana)
    {
        var (ancho, alto) = ResolucionDelMonitor(ventana);
        return ClavesPreferencia.HuellaPantalla(ancho, alto, VisualTreeHelper.GetDpi(ventana).DpiScaleX);
    }

    public double EscalaDeWindows()
    {
        var ventana = VentanaDeReferencia();
        return ventana is null ? 1.0 : VisualTreeHelper.GetDpi(ventana).DpiScaleX;
    }

    /// <summary>
    /// Factor para un ámbito: primero el de esa pantalla, si no el de respaldo
    /// <c>global</c>, si no el neutro. Una pantalla desconocida no deja la app sin escala.
    /// </summary>
    private double FactorPara(string huella)
    {
        if (_forzado is not null) return _forzado.Value;
        if (_factores.TryGetValue(huella, out var propio)) return EscalaUi.Ajustar(propio);
        if (_factores.TryGetValue(ClavesPreferencia.AmbitoGlobal, out var respaldo)) return EscalaUi.Ajustar(respaldo);
        return EscalaUi.Normal;
    }

    private static bool SonElMismoConjunto(
        IReadOnlyDictionary<string, double> a,
        IReadOnlyDictionary<string, double> b) =>
        a.Count == b.Count &&
        a.All(par => b.TryGetValue(par.Key, out var otro) && EscalaUi.SonIguales(par.Value, otro));

    // ── Interop ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolución física del monitor donde está la ventana. Es física y no en DIPs porque
    /// la huella debe leerse como la resolución que el usuario reconoce (1920x1080).
    /// Si la ventana todavía no tiene handle, cae al monitor primario.
    /// </summary>
    private static (int Ancho, int Alto) ResolucionDelMonitor(Window ventana)
    {
        var hwnd = new WindowInteropHelper(ventana).Handle;
        if (hwnd == IntPtr.Zero) return ResolucionPrimaria(ventana);

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info    = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfo(monitor, ref info)) return ResolucionPrimaria(ventana);

        return (info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top);
    }

    private static (int Ancho, int Alto) ResolucionPrimaria(Window ventana)
    {
        var dpi = VisualTreeHelper.GetDpi(ventana);
        return ((int)Math.Round(SystemParameters.PrimaryScreenWidth  * dpi.DpiScaleX),
                (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpi.DpiScaleY));
    }

    private static void ForzarRelecturaDelMarco(Window ventana)
    {
        var hwnd = new WindowInteropHelper(ventana).Handle;
        if (hwnd == IntPtr.Zero) return;

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    private const int MONITOR_DEFAULTTONEAREST = 2;

    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOMOVE     = 0x0002;
    private const uint SWP_NOZORDER   = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // ── Medidas de la ventana ────────────────────────────────────────────────

    /// <summary>
    /// Las medidas que la ventana declara en XAML, guardadas antes del primer escalado.
    /// <para/>
    /// El <c>LayoutTransform</c> escala el <b>contenido</b>, no el marco: una ventana con
    /// ancho fijo se queda del mismo tamaño mientras su contenido se achica (aire de
    /// sobra) o se agranda (recorte). Por eso las medidas explícitas se escalan con él.
    /// </summary>
    private sealed class MedidasDeclaradas
    {
        private double Ancho     { get; init; }
        private double Alto      { get; init; }
        private double AnchoMin  { get; init; }
        private double AltoMin   { get; init; }
        private double AnchoMax  { get; init; }
        private double AltoMax   { get; init; }

        /// <summary>Alto de la barra de título declarado en el <c>WindowChrome</c>, o <c>NaN</c> si la ventana no usa uno.</summary>
        private double AltoCaption { get; init; }

        public static MedidasDeclaradas Capturar(Window v) => new()
        {
            Ancho    = v.Width,
            Alto     = v.Height,
            AnchoMin = v.MinWidth,
            AltoMin  = v.MinHeight,
            AnchoMax = v.MaxWidth,
            AltoMax  = v.MaxHeight,
            AltoCaption = System.Windows.Shell.WindowChrome.GetWindowChrome(v)?.CaptionHeight ?? double.NaN,
        };

        public void AplicarEscaladas(Window v, double factor)
        {
            // Width/Height pueden venir en NaN (tamaño automático) y Max* en infinito:
            // en esos casos no hay nada que escalar.
            if (!double.IsNaN(Ancho)) v.Width  = Ancho * factor;
            if (!double.IsNaN(Alto))  v.Height = Alto  * factor;

            v.MinWidth  = AnchoMin * factor;
            v.MinHeight = AltoMin  * factor;

            if (!double.IsInfinity(AnchoMax)) v.MaxWidth  = AnchoMax * factor;
            if (!double.IsInfinity(AltoMax))  v.MaxHeight = AltoMax  * factor;

            AjustarBarraDeTitulo(v, factor);
        }

        /// <summary>
        /// <c>WindowChrome.CaptionHeight</c> se expresa en coordenadas de la ventana y
        /// <b>no</b> lo alcanza el <c>LayoutTransform</c> del contenido. En
        /// <c>MainWindow</c> vale 56 y coincide exactamente con la fila de la barra
        /// superior: a 0,8× esa barra pasa a medir 44,8 px visuales, pero Windows sigue
        /// tratando la franja 0–56 como área de título, y los 11,2 px de diferencia
        /// quedan sobre el sidebar y el contenido — hacer clic ahí arrastraría la ventana
        /// en vez de interactuar con lo que se ve.
        /// <para/>
        /// <c>ResizeBorderThickness</c> queda sin escalar a propósito: es una zona de
        /// agarre para el mouse, no parte del diseño, igual que el desfase fijo de los
        /// tooltips en <c>Core/ToolTipPlacement.cs</c>.
        /// </summary>
        private void AjustarBarraDeTitulo(Window v, double factor)
        {
            if (double.IsNaN(AltoCaption)) return;

            var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(v);
            if (chrome is null || chrome.IsFrozen) return;

            chrome.CaptionHeight = AltoCaption * factor;
        }
    }
}
