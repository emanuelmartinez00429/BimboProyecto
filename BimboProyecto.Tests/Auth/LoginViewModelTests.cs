using CapaAplicacion.Auth.Dtos;
using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Entities;
using CapaUI.Formularios.InicioSesion;
using Xunit;

namespace BimboProyecto.Tests.Auth;

// =============================================================================
//  Orquestacion del inicio de sesion — LoginViewModel
//
//  Antes esto no se podia probar: el flujo entero vivia en LoginWindow.xaml.cs
//  mezclado con animaciones y asignaciones de Visibility, asi que un test de
//  "credenciales rechazadas" exigia instanciar una Window real (P-058).
//
//  Al extraer el ViewModel SIN tipos de WPF, el proyecto de pruebas —que apunta
//  a net8.0 y no a net8.0-windows— puede enlazarlo y ejercitarlo con dobles.
//
//  Niveles: [P0] critico  [P1] alto  [P2] medio
// =============================================================================

// ── Dobles ───────────────────────────────────────────────────────────────────

internal sealed class AutenticacionFalsa : IAuthService
{
    public Result<LoginResultDto> Respuesta { get; init; } =
        Result<LoginResultDto>.Ok(new LoginResultDto { IdUsuario = 7, Email = "u@bimbo.hn", IdRol = 2 });

    public Exception? Explota { get; init; }

    public int     Llamadas       { get; private set; }
    public string? EmailRecibido  { get; private set; }
    public string? ClaveRecibida  { get; private set; }

    public Task<Result<LoginResultDto>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        Llamadas++;
        EmailRecibido = email;
        ClaveRecibida = password;
        if (Explota is not null) return Task.FromException<Result<LoginResultDto>>(Explota);
        return Task.FromResult(Respuesta);
    }
}

internal sealed class SesionFalsa : IUsuarioSesionService
{
    private static readonly UsuarioSesion _vacia = new(Array.Empty<ModuloPermisos>());

    public Result<UsuarioSesion> Respuesta { get; init; } = Result<UsuarioSesion>.Ok(_vacia);

    public int  Llamadas          { get; private set; }
    public int? IdUsuarioRecibido { get; private set; }

    public Task<Result<UsuarioSesion>> IniciarSesionAsync(int idUsuario, CancellationToken ct = default)
    {
        Llamadas++;
        IdUsuarioRecibido = idUsuario;
        return Task.FromResult(Respuesta);
    }

    public UsuarioSesion? SesionActual => null;
    public bool Autenticado            => false;
    public void CerrarSesion()                     { }
    public bool TienePermiso(string nombreAccion)  => false;
}

internal sealed class PreferenciasFalsas : IPreferenciasInicioSesionService
{
    public string? UltimoUsuario { get; set; }
    public int GuardarLlamadas { get; private set; }
    public string? UltimoGuardado { get; private set; }

    public PreferenciasFalsas(string? usuarioInicial = null) => UltimoUsuario = usuarioInicial;

    public string? ObtenerUltimoUsuario() => UltimoUsuario;

    public void GuardarUltimoUsuario(string? email)
    {
        GuardarLlamadas++;
        UltimoGuardado = email;
        UltimoUsuario = email;
    }
}

internal static class Dado
{
    /// <summary>ViewModel con credenciales cargadas y los dobles que se le pasen.</summary>
    public static LoginViewModel UnLogin(
        IAuthService auth,
        IUsuarioSesionService sesion,
        IPreferenciasInicioSesionService? preferencias = null) =>
        new(auth, sesion, preferencias ?? new PreferenciasFalsas())
        {
            Email = "usuario@bimbo.hn",
            Password = "MiPass123!"
        };
}

// ── [P0] El camino feliz ─────────────────────────────────────────────────────

public sealed class P0_IngresoExitoso
{
    [Fact]
    public async Task Credenciales_validas_devuelven_Exitoso()
    {
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa());

        Assert.Equal(ResultadoIngreso.Exitoso, await vm.IngresarAsync());
        Assert.Equal("", vm.Error);
    }

    [Fact]
    public async Task La_sesion_se_inicia_con_el_IdUsuario_que_devolvio_la_autenticacion()
    {
        var auth = new AutenticacionFalsa
        {
            Respuesta = Result<LoginResultDto>.Ok(new LoginResultDto { IdUsuario = 4321 }),
        };
        var sesion = new SesionFalsa();

        await Dado.UnLogin(auth, sesion).IngresarAsync();

        Assert.Equal(1, sesion.Llamadas);
        Assert.Equal(4321, sesion.IdUsuarioRecibido);
    }

    /// <summary>
    /// Al salir exitoso la app navega y la ventana muere, asi que reactivar el boton
    /// seria un parpadeo inutil. Es intencional, no un descuido.
    /// </summary>
    [Fact]
    public async Task Tras_un_ingreso_exitoso_el_ViewModel_queda_ocupado()
    {
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa());

        await vm.IngresarAsync();

        Assert.True(vm.Ocupado);
        Assert.False(vm.PuedeIngresar);
    }
}

// ── [P0] Los caminos de error ────────────────────────────────────────────────

public sealed class P0_IngresoRechazado
{
    [Fact]
    public async Task Si_la_autenticacion_falla_no_se_intenta_iniciar_sesion()
    {
        var auth   = new AutenticacionFalsa { Respuesta = Result<LoginResultDto>.Fail("Usuario o contraseña incorrectos") };
        var sesion = new SesionFalsa();
        var vm     = Dado.UnLogin(auth, sesion);

        Assert.Equal(ResultadoIngreso.CredencialesRechazadas, await vm.IngresarAsync());
        Assert.Equal("Usuario o contraseña incorrectos", vm.Error);
        Assert.Equal(0, sesion.Llamadas);
    }

    [Fact]
    public async Task Si_la_sesion_falla_se_reporta_como_SesionFallida()
    {
        var sesion = new SesionFalsa { Respuesta = Result<UsuarioSesion>.Fail("El usuario está inactivo") };
        var vm     = Dado.UnLogin(new AutenticacionFalsa(), sesion);

        Assert.Equal(ResultadoIngreso.SesionFallida, await vm.IngresarAsync());
        Assert.Equal("El usuario está inactivo", vm.Error);
    }

    [Fact]
    public async Task Una_excepcion_inesperada_no_se_escapa_y_se_reporta()
    {
        var auth = new AutenticacionFalsa { Explota = new HttpRequestException("sin red") };
        var vm   = Dado.UnLogin(auth, new SesionFalsa());

        Assert.Equal(ResultadoIngreso.ErrorInesperado, await vm.IngresarAsync());
        Assert.Contains("sin red", vm.Error);
    }

    /// <summary>Cualquier final que no sea exitoso tiene que dejar reintentar.</summary>
    [Theory]
    [InlineData("credenciales")]
    [InlineData("sesion")]
    [InlineData("excepcion")]
    public async Task Cualquier_fallo_libera_el_boton_para_reintentar(string caso)
    {
        var auth = caso == "excepcion"
            ? new AutenticacionFalsa { Explota = new InvalidOperationException("x") }
            : caso == "credenciales"
                ? new AutenticacionFalsa { Respuesta = Result<LoginResultDto>.Fail("no") }
                : new AutenticacionFalsa();
        var sesion = caso == "sesion"
            ? new SesionFalsa { Respuesta = Result<UsuarioSesion>.Fail("no") }
            : new SesionFalsa();

        var vm = Dado.UnLogin(auth, sesion);
        await vm.IngresarAsync();

        Assert.False(vm.Ocupado);
        Assert.True(vm.PuedeIngresar);
    }
}

// ── [P1] La regla del boton Ingresar ─────────────────────────────────────────

public sealed class P1_ReglaDelBoton
{
    private static LoginViewModel Nuevo() => new(new AutenticacionFalsa(), new SesionFalsa());

    [Theory]
    [InlineData("usuario@bimbo.hn", "MiPass123!", true)]
    [InlineData("nombre@empresa.com", "a", true)]
    [InlineData("", "MiPass123!", false)]
    [InlineData("   ", "MiPass123!", false)]
    [InlineData("usuario@bimbo.hn", "", false)]
    [InlineData("", "", false)]
    public void PuedeIngresar_delega_la_politica_en_el_dominio(string email, string clave, bool esperado)
    {
        var vm = Nuevo();
        vm.Email = email;
        vm.Password = clave;

        Assert.Equal(esperado, vm.PuedeIngresar);
    }

    [Fact]
    public void Con_un_ingreso_en_curso_el_boton_queda_deshabilitado()
    {
        var vm = Nuevo();
        vm.Email = "usuario@bimbo.hn";
        vm.Password = "MiPass123!";
        Assert.True(vm.PuedeIngresar);

        vm.Ocupado = true;

        Assert.False(vm.PuedeIngresar);
    }

    /// <summary>La vista se entera del cambio: si no notifica, el botón se congela.</summary>
    [Theory]
    [InlineData(nameof(LoginViewModel.Email))]
    [InlineData(nameof(LoginViewModel.Password))]
    [InlineData(nameof(LoginViewModel.Ocupado))]
    public void Cambiar_un_campo_notifica_PuedeIngresar(string propiedad)
    {
        var vm = Nuevo();
        var avisos = new List<string?>();
        vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        switch (propiedad)
        {
            case nameof(LoginViewModel.Email):    vm.Email = "a@b.c";  break;
            case nameof(LoginViewModel.Password): vm.Password = "x";   break;
            default:                              vm.Ocupado = true;   break;
        }

        Assert.Contains(nameof(LoginViewModel.PuedeIngresar), avisos);
    }
}

// ── [P1] El progreso que consume la vista ────────────────────────────────────

public sealed class P1_ProgresoDeLosPasos
{
    [Fact]
    public async Task El_camino_feliz_completa_los_dos_pasos_en_orden()
    {
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa());
        var pasos = new List<int>();
        vm.PasoCompletado += pasos.Add;

        await vm.IngresarAsync();

        Assert.Equal(new[] { 1, 2 }, pasos);
    }

    [Fact]
    public async Task Si_fallan_las_credenciales_no_se_completa_ningun_paso()
    {
        var auth = new AutenticacionFalsa { Respuesta = Result<LoginResultDto>.Fail("no") };
        var vm   = Dado.UnLogin(auth, new SesionFalsa());
        var pasos = new List<int>();
        vm.PasoCompletado += pasos.Add;

        await vm.IngresarAsync();

        Assert.Empty(pasos);
    }

    [Fact]
    public async Task Si_falla_la_sesion_solo_se_completa_el_primer_paso()
    {
        var sesion = new SesionFalsa { Respuesta = Result<UsuarioSesion>.Fail("no") };
        var vm     = Dado.UnLogin(new AutenticacionFalsa(), sesion);
        var pasos  = new List<int>();
        vm.PasoCompletado += pasos.Add;

        await vm.IngresarAsync();

        Assert.Equal(new[] { 1 }, pasos);
    }

    /// <summary>
    /// La animación corre en paralelo con la red, con los mismos tramos de siempre:
    /// 0→25 para credenciales y 25→70 para la sesión.
    /// </summary>
    [Fact]
    public async Task La_animacion_recibe_los_tramos_de_progreso_esperados()
    {
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa());
        var tramos = new List<(int desde, int hasta)>();

        await vm.IngresarAsync((desde, hasta) =>
        {
            tramos.Add((desde, hasta));
            return Task.CompletedTask;
        });

        Assert.Equal(new[] { (0, 25), (25, 70) }, tramos);
    }

    [Fact]
    public async Task Sin_animacion_el_flujo_igual_completa()
    {
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa());

        Assert.Equal(ResultadoIngreso.Exitoso, await vm.IngresarAsync(animarPaso: null));
    }
}

// ── [P2] Lo que viaja al servicio ────────────────────────────────────────────

public sealed class P2_CredencialesQueViajan
{
    [Fact]
    public async Task Se_autentica_con_el_email_y_la_clave_del_ViewModel()
    {
        var auth = new AutenticacionFalsa();
        var vm   = new LoginViewModel(auth, new SesionFalsa())
        {
            Email    = "fernando@bimbo.hn",
            Password = "Cl4ve-Larga!",
        };

        await vm.IngresarAsync();

        Assert.Equal(1, auth.Llamadas);
        Assert.Equal("fernando@bimbo.hn", auth.EmailRecibido);
        Assert.Equal("Cl4ve-Larga!", auth.ClaveRecibida);
    }

    [Fact]
    public async Task Un_reintento_tras_un_fallo_limpia_el_error_anterior()
    {
        var vm = Dado.UnLogin(new AutenticacionFalsa { Respuesta = Result<LoginResultDto>.Fail("primero") },
                              new SesionFalsa());
        await vm.IngresarAsync();
        Assert.Equal("primero", vm.Error);

        // Segundo intento, ahora con un servicio que responde bien.
        var vm2 = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa());
        await vm2.IngresarAsync();

        Assert.Equal("", vm2.Error);
    }
}

// ── [P1] Persistencia de preferencias del login ──────────────────────────────

public sealed class P1_PreferenciasRecordarUsuario
{
    [Fact]
    public async Task Login_exitoso_con_RecordarUsuario_activo_guarda_el_correo()
    {
        var prefs = new PreferenciasFalsas();
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa(), prefs);
        vm.Email = "operario@bimbo.hn";
        vm.RecordarUsuario = true;

        var resultado = await vm.IngresarAsync();

        Assert.Equal(ResultadoIngreso.Exitoso, resultado);
        Assert.Equal(1, prefs.GuardarLlamadas);
        Assert.Equal("operario@bimbo.hn", prefs.UltimoGuardado);
    }

    [Fact]
    public async Task Login_exitoso_con_RecordarUsuario_destildado_limpia_el_correo_guardado()
    {
        var prefs = new PreferenciasFalsas("anterior@bimbo.hn");
        var vm = Dado.UnLogin(new AutenticacionFalsa(), new SesionFalsa(), prefs);
        vm.Email = "operario@bimbo.hn";
        vm.RecordarUsuario = false;

        var resultado = await vm.IngresarAsync();

        Assert.Equal(ResultadoIngreso.Exitoso, resultado);
        Assert.Equal(1, prefs.GuardarLlamadas);
        Assert.Null(prefs.UltimoGuardado);
    }

    [Fact]
    public void Constructor_con_usuario_previo_precarga_email_y_activa_checkbox()
    {
        var prefs = new PreferenciasFalsas("recordado@bimbo.hn");
        var vm = new LoginViewModel(new AutenticacionFalsa(), new SesionFalsa(), prefs);

        Assert.Equal("recordado@bimbo.hn", vm.Email);
        Assert.True(vm.RecordarUsuario);
    }

    [Fact]
    public void Constructor_sin_usuario_previo_inicia_vacio_y_con_checkbox_destildado()
    {
        var prefs = new PreferenciasFalsas(null);
        var vm = new LoginViewModel(new AutenticacionFalsa(), new SesionFalsa(), prefs);

        Assert.Equal("", vm.Email);
        Assert.False(vm.RecordarUsuario);
    }
}

