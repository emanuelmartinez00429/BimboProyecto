using CapaAplicacion.Common;
using CapaAplicacion.Preferencias;
using CapaAplicacion.Preferencias.Dtos;
using CapaAplicacion.Preferencias.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;

namespace CapaUI.Services.Sesion;

/// <summary>
/// Timeout por inactividad de la sesión (Fase 4.1 del Plan de Seguridad).
/// <para/>
/// Contexto: terminales compartidas de planta. Un operario pesa, se aleja —o se va a
/// almorzar—, y la sesión queda abierta con sus permisos: cualquiera puede usar su
/// usuario. Este servicio cierra la sesión sola si no hay input (teclado ni mouse)
/// durante los minutos configurados y MainWindow vuelve al login. El usuario lo ajusta
/// desde «Mi Usuario ».
/// <para/>
/// El valor vive como preferencia personal (<c>usuario_preferencias</c>, clave
/// <c>timeout_inactividad</c>, ámbito global) con el mismo molde de <c>escala_ui</c>:
/// cada sesión arranca con el valor propio del usuario logueado y RLS garantiza que
/// nadie escriba la fila de otro.
/// </summary>
/// <remarks>
/// Mecánica: un listener global en <c>InputManager.PreProcessInput</c> marca la última
/// actividad (cualquier tecla o evento de mouse), y un <c>DispatcherTimer</c> de 60 s
/// comprueba contra ella. No reinicia el timer en cada movimiento (sería Stop/Start por
/// cada milímetro de mouse); el chequeo es barato y granular por minuto.
/// <para/>
/// El servicio es <b>scoped</b>: vive y muere con la sesión de MainWindow, así que el
/// estado no sobrevive a un_logout_
/// </para>
/// </summary>
public interface ISesionInactividadService
{
    /// <summary>Minutos de inactividad configurados para el usuario actual (valor en caché tras cargar).</summary>
    int Minutos { get; }

    /// <summary>Lee la preferencia del usuario y arranca la vigilancia. Se llama al subir MainWindow.</summary>
    Task<Result> CargarEIniciarAsync(CancellationToken ct = default);

    /// <summary>Guarda los minutos, re-aplica el timer y deja la preferencia persistida.</summary>
    Task<Result> GuardarMinutosAsync(int minutos, CancellationToken ct = default);

    /// <summary>Detiene la vigilancia; lifespan: MainWindow la llama al limpiar recursos del logout.</summary>
    void Detener();

    /// <summary>Se dispara una vez al cumplirse la inactividad; MainWindow cierra sesión.</summary>
    event Action? SesionExpirada;
}

public sealed class SesionInactividadService : ISesionInactividadService
{
    /// <summary>MINUTOS_TIMEOUT del plan (30): arranque sin preferencia guardada.</summary>
    public const int MinutosPorDefecto = 30;

    private const int MinutosMinimo = 1;
    private const int MinutosMaximo = 240;

    private readonly IUsuarioSesionService          _sesion;
    private readonly IPreferenciasUsuarioRepository _repo;

    private System.Windows.Threading.DispatcherTimer? _verificador;
    private int _minutos = MinutosPorDefecto;
    private DateTime _ultimoInput = DateTime.UtcNow;
    private bool _enlazado;

    public int Minutos => _minutos;

    public event Action? SesionExpirada;

    public SesionInactividadService(
        IUsuarioSesionService sesion,
        IPreferenciasUsuarioRepository repo)
    {
        _sesion = sesion;
        _repo   = repo;
    }

    public async Task<Result> CargarEIniciarAsync(CancellationToken ct = default)
    {
        var idUsuario = _sesion.SesionActual?.IdUsuario;
        if (idUsuario is null)
            return Result.Fail("No hay una sesión activa.");

        _minutos = MinutosPorDefecto;

        var resultado = await _repo.ObtenerTodasAsync(idUsuario.Value, ct);
        if (resultado.Success)
        {
            var fila = resultado.Value?
                .FirstOrDefault(p => p.Clave == ClavesPreferencia.TimeoutInactividad);
            if (fila is not null)
                _minutos = ValorPreferencia.Entero(fila.ValorJson, MinutosPorDefecto);
        }

        IniciarVigilancia();
        return Result.Ok();
    }

    public async Task<Result> GuardarMinutosAsync(int minutos, CancellationToken ct = default)
    {
        if (minutos < MinutosMinimo || minutos > MinutosMaximo)
            return Result.Fail(
                $"Los minutos deben estar entre {MinutosMinimo} y {MinutosMaximo}.");

        var idUsuario = _sesion.SesionActual?.IdUsuario;
        if (idUsuario is null)
            return Result.Fail("No hay una sesión activa.");

        var guardado = await _repo.GuardarAsync(
            idUsuario.Value,
            ClavesPreferencia.TimeoutInactividad,
            ClavesPreferencia.AmbitoGlobal,
            ValorPreferencia.DesdeNumero(minutos), ct);

        if (!guardado.Success) return guardado;

        _minutos = minutos;
        IniciarVigilancia(); // re-arma con el intervalo nuevo y el último input ya registrado
        return Result.Ok();
    }

    public void Detener()
    {
        System.Windows.Input.InputManager.Current.PreProcessInput -= OnPreProcessInput;
        _verificador?.Stop();
        _verificador = null;
    }

    // ── Vigilancia ───────────────────────────────────────────────────────

    private void IniciarVigilancia()
    {
        System.Windows.Input.InputManager.Current.PreProcessInput -= OnPreProcessInput;
        System.Windows.Input.InputManager.Current.PreProcessInput += OnPreProcessInput;

        _verificador?.Stop();
        _verificador = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60),
        };
        _verificador.Tick += OnVerificadorTick;
        _verificador.Start();
    }

    private void OnPreProcessInput(object? sender, System.Windows.Input.PreProcessInputEventArgs args)
    {
        var input = args.StagingItem?.Input;
        // Cualquier tecla y cualquier gesto de mouse cuenta como actividad; los inputs
        // "raw" (timer tick, render, etc.) no.
        if (input is System.Windows.Input.KeyEventArgs or System.Windows.Input.MouseEventArgs)
            _ultimoInput = DateTime.UtcNow;
    }

    private void OnVerificadorTick(object? sender, EventArgs e)
    {
        var inactivo = DateTime.UtcNow - _ultimoInput;
        if (inactivo < TimeSpan.FromMinutes(_minutos)) return;

        // Una sola vez: corta el verificador para que no martille si
        // MainWindow tarda en limpiar.
        _verificador?.Stop();
        SesionExpirada?.Invoke();
    }
}
