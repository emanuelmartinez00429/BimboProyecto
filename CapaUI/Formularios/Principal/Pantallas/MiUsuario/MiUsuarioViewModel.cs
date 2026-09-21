using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Perfil;
using CapaAplicacion.Preferencias;
using CapaAplicacion.Preferencias.Interfaces;
using CapaAplicacion.Preferencias.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Reglas;
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
    private readonly IPreferenciasUsuarioRepository _preferencias;
    private readonly IPerfilUsuarioService _perfil;
    private readonly IUsuarioSesionService _sesion;
    private readonly ICambioPropiaPasswordService _cambioPassword;
    private readonly Debouncer _guardado = new(RetardoGuardadoMs);

    private bool _disposed;

    // ── Perfil ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApodoPreview))]
    private string _nombreCompleto;

    public string Iniciales      { get; }
    public string NombreRol      { get; }
    public string Email          { get; }

    // ── Perfil: apodo personal ───────────────────────────────────────────────
    // El saludo «Bienvenido, X» usa el apodo si el usuario definió uno. Es una
    // preferencia personal (usuario_preferencias, clave 'apodo'): no toca el dato
    // real del empleado. Vacío = sin apodo, el saludo vuelve al nombre real.

    /// <summary>Valor guardado en la base; referencia para saber si hubo cambios.</summary>
    private string _apodoGuardado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApodoPreview))]
    [NotifyPropertyChangedFor(nameof(HayCambioApodo))]
    private string _apodoEditable = string.Empty;

    /// <summary>Lo que mostraría el saludo del menú principal con la edición actual.</summary>
    public string ApodoPreview =>
        string.IsNullOrWhiteSpace(ApodoEditable) ? NombreCompleto : ApodoEditable.Trim();

    public bool HayCambioApodo =>
        ApodoEditable.Trim() != _apodoGuardado;

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

    // ── Seguridad: cambio de la propia contraseña ──────────────────────────
    // El cableado real (verificar la actual contra Supabase, generar OTP, etc.)
    // queda para el paso siguiente: esta sección solo sostiene el estado visual
    // y aplica las reglas del dominio para pintar el medidor y la checklist.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreFortaleza))]
    [NotifyPropertyChangedFor(nameof(PuedeEnviarCambio))]
    private string _contrasenaNueva = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayCoincidencia))]
    [NotifyPropertyChangedFor(nameof(PuedeEnviarCambio))]
    private string _contrasenaConfirmar = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeEnviarCambio))]
    private string _contrasenaActual = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAviso))]
    private string? _avisoInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayExito))]
    private string? _exito;

    /// <summary>Checklist R1..R4 de la vista: las 4 reglas del dominio evaluadas.</summary>
    public bool[] RequisitosOk => new[]
    {
        ReglasContrasena.TieneLargoMinimo(ContrasenaNueva),
        ReglasContrasena.TieneMayuscula(ContrasenaNueva),
        ReglasContrasena.TieneNumero(ContrasenaNueva),
        ReglasContrasena.TieneSimbolo(ContrasenaNueva),
    };

    /// <summary>Score de fortaleza 0..5 del dominio, para el medidor de barras.</summary>
    public int ScoreFortaleza => ReglasContrasena.CalcularScore(ContrasenaNueva);

    /// <summary><c>true</c> mientras las contraseñas escritas coinciden (solo enrojece cuando el usuario ya escribió la confirmación).</summary>
    public bool HayCoincidencia => string.IsNullOrEmpty(ContrasenaConfirmar) || ContrasenaNueva == ContrasenaConfirmar;

    public bool HayAviso => !string.IsNullOrWhiteSpace(AvisoInfo);
    public bool HayExito => !string.IsNullOrWhiteSpace(Exito);

    /// <summary>Forma completa: la actual está escrita, la nueva cumple TODAS las reglas del dominio y la confirmación coincide.</summary>
    public bool PuedeEnviarCambio =>
        !CambiandoPassword &&
        ContrasenaActual.Length > 0 &&
        ReglasContrasena.CumpleTodasLasReglas(ContrasenaNueva) &&
        ContrasenaNueva == ContrasenaConfirmar;

    /// <summary>Alimenta el estado del formulario desde el code-behind (PasswordChanged).</summary>
    public void EstablecerActual(string pwd)      { ContrasenaActual = pwd ?? string.Empty; Exito = null; }
    public void EstablecerNueva(string pwd)       { ContrasenaNueva = pwd ?? string.Empty; Exito = null; }
    public void EstablecerConfirmar(string pwd)   { ContrasenaConfirmar = pwd ?? string.Empty; Exito = null; }

    public MiUsuarioViewModel(IUsuarioSesionService sesion, IEscalaService escala,
                              IPreferenciasUsuarioRepository preferencias,
                              IPerfilUsuarioService perfil,
                              ICambioPropiaPasswordService cambioPassword)
    {
        _escala       = escala;
        _preferencias = preferencias;
        _perfil       = perfil;
        _sesion       = sesion;
        _cambioPassword = cambioPassword;

        var actual = sesion.SesionActual;
        var guardado = perfil.PerfilActual?.Apodo;

        _nombreCompleto = actual?.NombreCompleto ?? "—";
        _apodoGuardado  = guardado ?? string.Empty;
        _apodoEditable  = guardado ?? string.Empty;
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

    /// <summary>
    /// Guarda el apodo personal. Vacío restablece: borra la preferencia y el saludo
    /// vuelve al nombre completo real. Tras escribir, refresca el perfil para que la
    /// próxima vista del menú principal salude con el apodo nuevo.
    /// </summary>
    [RelayCommand]
    private async Task GuardarApodoAsync(CancellationToken ct)
    {
        ApodoMensaje = null;

        var idUsuario = IdSesion();
        if (idUsuario is null)
        {
            ApodoMensaje = "No hay una sesión activa; volvé a iniciar sesión para editar tu apodo.";
            return;
        }

        var apodo = ApodoEditable.Trim();

        // Validación de forma: una preferencia es texto cortito, no una biografía.
        if (apodo.Length > 64)
        {
            ApodoMensaje = "El apodo puede tener hasta 64 caracteres.";
            return;
        }

        if (apodo.Length > 0 && apodo == _apodoGuardado)
        {
            ApodoMensaje = "Ese apodo ya está guardado.";
            return;
        }

        var guardado =
            apodo.Length == 0
                ? await _preferencias.EliminarAsync(idUsuario.Value, ClavesPreferencia.Apodo,
                    ClavesPreferencia.AmbitoGlobal, ct)
                : await _preferencias.GuardarAsync(idUsuario.Value, ClavesPreferencia.Apodo,
                    ClavesPreferencia.AmbitoGlobal, ValorPreferencia.DesdeTexto(apodo), ct);

        if (!guardado.Success)
        {
            ApodoMensaje = guardado.Error;
            return;
        }

        _apodoGuardado = apodo;

        // Refresca el perfil singleton: el saludo del menú principal lee
        // PerfilActual.NombreCompleto, que con apodo ya lo pisa.
        await _perfil.CargarAsync(idUsuario.Value);
        if (_perfil.PerfilActual is { } fresco)
            NombreCompleto = fresco.NombreCompleto;

        ApodoMensaje = apodo.Length == 0
            ? "Apodo borrado: el saludo vuelve a usar tu nombre real."
            : "Apodo guardado.";
    }

    /// <summary>Id del usuario logueado, o <c>null</c> sin sesión (apodo no editable).</summary>
    private int? IdSesion() => _sesion.SesionActual?.IdUsuario;

    // ── Perfil: mensajes del editor de apodo ─────────────────────────────────
    // Mensajes propios de la tarjeta Perfil (no reusan los de Seguridad para que
    // un banner de "contraseña guardada" no aparezca en la tarjeta equivocada).

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayApodoMensaje))]
    private string? _apodoMensaje;

    public bool HayApodoMensaje => !string.IsNullOrWhiteSpace(ApodoMensaje);

    // ── Seguridad: cambio de la propia contraseña (cableado real) ─────────────
    // La prueba de dueño es la re-autenticación con la contraseña actual (mismo
    // método del login). El servicio usa la sesión re-validada para fijar la nueva
    // contraseña y NO cierra la sesión (a diferencia de la recuperación por OTP,
    // que mata su sesión de recuperación al terminar).

    /// <summary>Ocupado mientras corre el cambio; bloquea doble clic y reenvíos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeEnviarCambio))]
    private bool _cambiandoPassword;

    [RelayCommand]
    private async Task CambiarContrasenaAsync(CancellationToken ct)
    {
        if (!PuedeEnviarCambio || CambiandoPassword) return;

        CambiandoPassword = true;
        Error = null;
        AvisoInfo = null;
        Exito = null;

        try
        {
            var correo = _sesion.SesionActual?.Email ?? string.Empty;

            var resultado = await _cambioPassword.CambiarAsync(
                correo, ContrasenaActual, ContrasenaNueva, ct);

            if (ct.IsCancellationRequested) return;

            if (!resultado.Success)
            {
                AvisoInfo = resultado.Error;
                return;
            }

            Exito = "Contraseña actualizada. La necesitarás la próxima vez que inicies sesión.";

            // Limpia los campos con la misma vía que usa la vista para alimentarlos
            // (los PasswordBox son one-way hacia el VM; no se pueden vaciar desde acá).
            ContrasenaActual = string.Empty;
            ContrasenaNueva = string.Empty;
            ContrasenaConfirmar = string.Empty;
            OnPropertyChanged(nameof(RequisitosOk));
            OnPropertyChanged(nameof(ScoreFortaleza));
        }
        finally
        {
            CambiandoPassword = false;
        }
    }

    [RelayCommand]
    private void LimpiarFormulario()
    {
        ContrasenaActual = string.Empty;
        ContrasenaNueva = string.Empty;
        ContrasenaConfirmar = string.Empty;
        Error = null;
        AvisoInfo = null;
        Exito = null;
    }

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
