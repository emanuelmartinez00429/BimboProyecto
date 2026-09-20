using CapaAplicacion.Preferencias;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Services.Escala;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal.Pantallas.MiUsuario;

/// <summary>
/// Pantalla «Mi Usuario»: datos del perfil y preferencias personales.
/// <para/>
/// La única preferencia editable por ahora es la escala de la interfaz. Se aplica
/// <b>en vivo</b> —el usuario ve el tamaño mientras lo elige, no después de guardar— y
/// se persiste con retardo: sin eso, ir de 1,0 a 0,75 serían cinco escrituras a Supabase,
/// una por clic.
/// <para/>
/// No hay <c>ChangeTracker</c> ni botón Guardar, a diferencia de
/// <c>ConfiguracionEmpresaViewModel</c>: no hay nada pendiente de confirmar, porque cada
/// cambio ya está aplicado y en camino a guardarse. Ese molde vuelve cuando aparezca una
/// preferencia que no se aplique en vivo (densidad de tablas, filas por página).
/// </summary>
public sealed partial class MiUsuarioViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Cuánto se espera desde el último clic antes de escribir a Supabase. 600 ms es
    /// más de lo que tarda alguien en encadenar clics en los botones y menos de lo que
    /// tarda en cambiar de pantalla.
    /// </summary>
    private const int RetardoGuardadoMs = 600;

    private readonly IEscalaService _escala;
    private readonly Debouncer _guardado = new(RetardoGuardadoMs);

    private bool _disposed;

    // ── Perfil (solo lectura) ────────────────────────────────────────────────

    public string NombreCompleto { get; }
    public string Iniciales      { get; }
    public string NombreRol      { get; }
    public string Email          { get; }

    // ── Apariencia ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PorcentajeEscala))]
    [NotifyPropertyChangedFor(nameof(PuedeAumentar))]
    [NotifyPropertyChangedFor(nameof(PuedeDisminuir))]
    [NotifyPropertyChangedFor(nameof(EsSugerido))]
    private double _factorEscala;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PorcentajeSugerido))]
    [NotifyPropertyChangedFor(nameof(EsSugerido))]
    private double _factorSugerido;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayError))]
    private string? _error;

    /// <summary>Escala de Windows detectada, para explicar de dónde sale la sugerencia.</summary>
    public string PorcentajeWindows { get; }

    /// <summary><c>true</c> si <c>UI_ESCALA</c> está imponiendo un factor: el control se bloquea.</summary>
    public bool EstaForzado { get; }

    public string PorcentajeEscala    => Porcentaje(FactorEscala);
    public string PorcentajeSugerido  => Porcentaje(FactorSugerido);

    public bool PuedeAumentar  => !EstaForzado && !_escala.HayModalAbierto && FactorEscala < EscalaUi.Maximo - 0.001;
    public bool PuedeDisminuir => !EstaForzado && !_escala.HayModalAbierto && FactorEscala > EscalaUi.Minimo + 0.001;
    public bool EsSugerido     => EscalaUi.SonIguales(FactorEscala, FactorSugerido);
    public bool HayError       => !string.IsNullOrWhiteSpace(Error);

    public MiUsuarioViewModel(IUsuarioSesionService sesion, IEscalaService escala)
    {
        _escala = escala;

        var actual = sesion.SesionActual;
        NombreCompleto = actual?.NombreCompleto ?? "—";
        Iniciales      = actual?.Iniciales      ?? "—";
        NombreRol      = actual?.NombreRol      ?? "—";
        Email          = actual?.Email          ?? "—";

        EstaForzado       = escala.EstaForzado;
        FactorEscala      = escala.Factor;
        FactorSugerido    = EscalaUi.Sugerido(escala.EscalaDeWindows());
        PorcentajeWindows = Porcentaje(escala.EscalaDeWindows());

        _escala.EscalaCambiado += OnEscalaExternaCambiado;
    }

    private void OnEscalaExternaCambiado(double factor)
    {
        if (!EscalaUi.SonIguales(factor, FactorEscala))
            FactorEscala = factor;
    }

    // ── Comandos ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Aumentar()  => CambiarA(FactorEscala + EscalaUi.Paso);

    [RelayCommand]
    private void Disminuir() => CambiarA(FactorEscala - EscalaUi.Paso);

    [RelayCommand]
    private void UsarSugerido() => CambiarA(FactorSugerido);

    [RelayCommand]
    private async Task RestablecerAsync()
    {
        if (EstaForzado) return;

        if (_escala.HayModalAbierto)
        {
            Error = "No se puede cambiar la escala mientras haya un modal o diálogo abierto.";
            return;
        }

        _guardado.Cancelar();
        Error = null;

        var resultado = await _escala.RestablecerAsync();
        if (!resultado.Success)
        {
            Error = resultado.Error;
            return;
        }

        // El servicio ya recalculó el factor heredado; la pantalla se sincroniza con él.
        FactorEscala = _escala.Factor;
    }

    /// <summary>
    /// Aplica el factor al instante y programa la escritura. Si el usuario sigue
    /// tocando, el retardo se reinicia y solo se guarda el valor final.
    /// </summary>
    private void CambiarA(double factor)
    {
        if (EstaForzado || _disposed) return;

        if (_escala.HayModalAbierto)
        {
            Error = "No se puede cambiar la escala mientras haya un modal o diálogo abierto.";
            return;
        }

        var ajustado = EscalaUi.Ajustar(factor);
        if (EscalaUi.SonIguales(ajustado, FactorEscala)) return;

        FactorEscala = ajustado;
        Error = null;

        _escala.Aplicar(ajustado);

        _ = _guardado.EjecutarAsync(async ct =>
        {
            var resultado = await _escala.GuardarAsync(ajustado, ct);

            // El debounce cancela la tarea anterior con su token: un guardado que quedó
            // obsoleto no debe pisar la pantalla con su error.
            if (ct.IsCancellationRequested || _disposed) return;
            if (!resultado.Success) Error = resultado.Error;
        });
    }

    private static string Porcentaje(double factor) =>
        (factor * 100).ToString("0") + " %";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _escala.EscalaCambiado -= OnEscalaExternaCambiado;
        _guardado.Dispose();
    }
}
