using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Common;
using CapaUI.Formularios.InicioSesion;
using Xunit;

namespace BimboProyecto.Tests.Auth;

// =============================================================================
//  Recuperacion de contrasena — los tres pasos
//
//  Antes esto no se podia probar de ninguna forma: los tres paneles llamaban a
//  ConexionSupabase.GetClientAsync() y a client.Auth DIRECTO desde el code-behind,
//  salteando CapaAplicacion y CapaDatos (P-058). Con el contrato
//  IRecuperacionPasswordService de por medio, el flujo se ejercita con dobles.
//
//  Niveles: [P0] critico  [P1] alto  [P2] medio
// =============================================================================

internal sealed class RecuperacionFalsa : IRecuperacionPasswordService
{
    public Result RespuestaEnviar    { get; set; } = Result.Ok();
    public Result RespuestaVerificar { get; set; } = Result.Ok();
    public Result RespuestaCambiar   { get; set; } = Result.Ok();
    public Exception? Explota        { get; set; }

    public List<string> EmailsEnviados   { get; } = new();
    public List<(string email, string codigo)> Verificaciones { get; } = new();
    public List<string> PasswordsFijadas { get; } = new();

    public Task<Result> EnviarCodigoAsync(string email, CancellationToken ct = default)
    {
        EmailsEnviados.Add(email);
        return Responder(RespuestaEnviar);
    }

    public Task<Result> VerificarCodigoAsync(string email, string codigo, CancellationToken ct = default)
    {
        Verificaciones.Add((email, codigo));
        return Responder(RespuestaVerificar);
    }

    public Task<Result> CambiarPasswordAsync(string nuevaPassword, CancellationToken ct = default)
    {
        PasswordsFijadas.Add(nuevaPassword);
        return Responder(RespuestaCambiar);
    }

    private Task<Result> Responder(Result r) =>
        Explota is not null ? Task.FromException<Result>(Explota) : Task.FromResult(r);
}

// ── [P1] Paso 1 · pedir el codigo ────────────────────────────────────────────

public sealed class P1_SolicitarCodigo
{
    [Theory]
    [InlineData("usuario@bimbo.hn", true)]
    [InlineData("nombre@empresa.com", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("no-es-un-correo", false)]
    [InlineData("falta@dominio", false)]
    public void PuedeEnviar_exige_contenido_y_formato_de_correo(string email, bool esperado)
    {
        var vm = new SolicitarCodigoViewModel(new RecuperacionFalsa()) { Email = email };

        Assert.Equal(esperado, vm.PuedeEnviar);
    }

    /// <summary>
    /// El vacío es la trampa: <c>ReglasFormato.EsCorreo</c> lo da por válido (convención
    /// de campos opcionales), así que si no se exige contenido aparte el botón se
    /// habilitaría con la caja vacía.
    /// </summary>
    [Fact]
    public void Con_la_caja_vacia_el_boton_queda_deshabilitado()
        => Assert.False(new SolicitarCodigoViewModel(new RecuperacionFalsa()) { Email = "" }.PuedeEnviar);

    [Fact]
    public async Task Enviar_exitoso_no_deja_error_y_libera_el_boton()
    {
        var vm = new SolicitarCodigoViewModel(new RecuperacionFalsa()) { Email = "u@bimbo.hn" };

        Assert.True(await vm.EnviarAsync());
        Assert.Equal("", vm.Error);
        Assert.False(vm.Ocupado);
        Assert.True(vm.PuedeEnviar);
    }

    [Fact]
    public async Task Si_el_envio_falla_se_muestra_el_mensaje_del_servicio()
    {
        var svc = new RecuperacionFalsa { RespuestaEnviar = Result.Fail("No se pudo enviar el código.") };
        var vm  = new SolicitarCodigoViewModel(svc) { Email = "u@bimbo.hn" };

        Assert.False(await vm.EnviarAsync());
        Assert.Equal("No se pudo enviar el código.", vm.Error);
        Assert.False(vm.Ocupado);
    }

    [Fact]
    public async Task El_correo_viaja_sin_espacios_de_sobra()
    {
        var svc = new RecuperacionFalsa();
        await new SolicitarCodigoViewModel(svc) { Email = "  u@bimbo.hn  " }.EnviarAsync();

        Assert.Equal("u@bimbo.hn", Assert.Single(svc.EmailsEnviados));
    }
}

// ── [P0] Paso 2 · verificar el codigo ────────────────────────────────────────

public sealed class P0_VerificarCodigo
{
    private static VerificarCodigoViewModel Nuevo(IRecuperacionPasswordService svc) =>
        new(svc, "usuario@bimbo.hn");

    [Theory]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]   // corto
    [InlineData("123456789", false)] // largo
    [InlineData("1234567a", false)]  // no numerico
    [InlineData("", false)]
    public void PuedeVerificar_respeta_el_contrato_del_OTP(string codigo, bool esperado)
    {
        var vm = Nuevo(new RecuperacionFalsa());
        vm.Codigo = codigo;

        Assert.Equal(esperado, vm.PuedeVerificar);
    }

    [Fact]
    public async Task Se_verifica_contra_el_correo_del_paso_anterior()
    {
        var svc = new RecuperacionFalsa();
        var vm  = Nuevo(svc);
        vm.Codigo = "12345678";

        Assert.True(await vm.VerificarAsync());
        Assert.Equal(("usuario@bimbo.hn", "12345678"), Assert.Single(svc.Verificaciones));
    }

    [Fact]
    public async Task Un_codigo_rechazado_deja_el_mensaje_y_permite_reintentar()
    {
        var svc = new RecuperacionFalsa
        {
            RespuestaVerificar = Result.Fail("Código incorrecto o expirado. Inténtalo de nuevo."),
        };
        var vm = Nuevo(svc);
        vm.Codigo = "00000000";

        Assert.False(await vm.VerificarAsync());
        Assert.Contains("expirado", vm.Error);
        Assert.False(vm.Ocupado);
    }

    [Fact]
    public async Task Reenviar_usa_el_mismo_correo_y_no_pide_codigo()
    {
        var svc = new RecuperacionFalsa();

        Assert.True(await Nuevo(svc).ReenviarAsync());
        Assert.Equal("usuario@bimbo.hn", Assert.Single(svc.EmailsEnviados));
        Assert.Empty(svc.Verificaciones);
    }

    [Fact]
    public async Task Cuenta_deshabilitada_muestra_mensaje_de_error_y_no_permite_avanzar()
    {
        var svc = new RecuperacionFalsa
        {
            RespuestaVerificar = Result.Fail("Tu cuenta está deshabilitada. Contacta al administrador."),
        };
        var vm = Nuevo(svc);
        vm.Codigo = "12345678";

        Assert.False(await vm.VerificarAsync());
        Assert.Equal("Tu cuenta está deshabilitada. Contacta al administrador.", vm.Error);
        Assert.False(vm.Ocupado);
    }
}

// ── [P0] Paso 3 · fijar la contrasena nueva ──────────────────────────────────

public sealed class P0_NuevaPassword
{
    private static NuevaPasswordViewModel Nuevo() => new(new RecuperacionFalsa());

    [Theory]
    [InlineData("Segura123!", true,  true,  true,  true)]
    [InlineData("segura123!", true,  false, true,  true)]   // sin mayuscula
    [InlineData("SeguraAbc!", true,  true,  false, true)]   // sin numero
    [InlineData("Segura1234", true,  true,  true,  false)]  // sin simbolo
    [InlineData("Seg1!",      false, true,  true,  true)]   // corta
    public void Los_cuatro_requisitos_se_exponen_para_la_lista(
        string pwd, bool largo, bool mayus, bool num, bool simbolo)
    {
        var vm = Nuevo();
        vm.NuevaPassword = pwd;

        Assert.Equal(largo,   vm.TieneLargoMinimo);
        Assert.Equal(mayus,   vm.TieneMayuscula);
        Assert.Equal(num,     vm.TieneNumero);
        Assert.Equal(simbolo, vm.TieneSimbolo);
    }

    [Fact]
    public void Con_la_confirmacion_vacia_todavia_no_se_avisa_que_no_coinciden()
    {
        var vm = Nuevo();
        vm.NuevaPassword = "Segura123!";
        vm.Confirmacion  = "";

        Assert.False(vm.MostrarNoCoinciden);
        Assert.False(vm.PuedeGuardar);
    }

    [Fact]
    public void Si_difieren_y_ya_escribio_algo_se_avisa()
    {
        var vm = Nuevo();
        vm.NuevaPassword = "Segura123!";
        vm.Confirmacion  = "Otra456!";

        Assert.True(vm.MostrarNoCoinciden);
        Assert.False(vm.PuedeGuardar);
    }

    [Fact]
    public void Solo_se_puede_guardar_con_todas_las_reglas_y_las_dos_cajas_iguales()
    {
        var vm = Nuevo();
        vm.NuevaPassword = "Segura123!";
        vm.Confirmacion  = "Segura123!";

        Assert.True(vm.PuedeGuardar);
        Assert.False(vm.MostrarNoCoinciden);
    }

    [Fact]
    public void Una_contrasena_debil_no_habilita_guardar_aunque_coincidan()
    {
        var vm = Nuevo();
        vm.NuevaPassword = "abc";
        vm.Confirmacion  = "abc";

        Assert.True(vm.Coinciden);
        Assert.False(vm.PuedeGuardar);
    }

    [Fact]
    public async Task Se_fija_exactamente_la_contrasena_del_ViewModel()
    {
        var svc = new RecuperacionFalsa();
        var vm  = new NuevaPasswordViewModel(svc);
        vm.NuevaPassword = "Segura123!";
        vm.Confirmacion  = "Segura123!";

        Assert.True(await vm.GuardarAsync());
        Assert.Equal("Segura123!", Assert.Single(svc.PasswordsFijadas));
    }

    [Fact]
    public async Task Si_el_cambio_falla_se_puede_reintentar()
    {
        var svc = new RecuperacionFalsa { RespuestaCambiar = Result.Fail("No se pudo actualizar la contraseña.") };
        var vm  = new NuevaPasswordViewModel(svc);
        vm.NuevaPassword = "Segura123!";
        vm.Confirmacion  = "Segura123!";

        Assert.False(await vm.GuardarAsync());
        Assert.Equal("No se pudo actualizar la contraseña.", vm.Error);
        Assert.True(vm.PuedeGuardar);
    }
}

// ── [P1] Lo que comparten los tres pasos ─────────────────────────────────────

public sealed class P1_ComportamientoComunDeLosPasos
{
    /// <summary>
    /// El servicio no lanza por fallos esperados, pero si algo se rompe adentro del SDK
    /// el panel no puede quedarse colgado en «Enviando…» para siempre.
    /// </summary>
    [Fact]
    public async Task Una_excepcion_del_servicio_no_deja_el_paso_ocupado()
    {
        var svc = new RecuperacionFalsa { Explota = new HttpRequestException("sin red") };
        var vm  = new SolicitarCodigoViewModel(svc) { Email = "u@bimbo.hn" };

        Assert.False(await vm.EnviarAsync());
        Assert.Contains("sin red", vm.Error);
        Assert.False(vm.Ocupado);
    }

    [Fact]
    public async Task Un_reintento_limpia_el_error_del_intento_anterior()
    {
        var svc = new RecuperacionFalsa { RespuestaEnviar = Result.Fail("primero") };
        var vm  = new SolicitarCodigoViewModel(svc) { Email = "u@bimbo.hn" };

        await vm.EnviarAsync();
        Assert.Equal("primero", vm.Error);

        svc.RespuestaEnviar = Result.Ok();
        Assert.True(await vm.EnviarAsync());
        Assert.Equal("", vm.Error);
    }

    /// <summary>Si no notifica, el botón se congela mientras corre la llamada.</summary>
    [Fact]
    public void Cambiar_Ocupado_notifica_la_regla_de_cada_paso()
    {
        var vm = new SolicitarCodigoViewModel(new RecuperacionFalsa()) { Email = "u@bimbo.hn" };
        var avisos = new List<string?>();
        vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        vm.Ocupado = true;

        Assert.Contains(nameof(SolicitarCodigoViewModel.PuedeEnviar), avisos);
        Assert.False(vm.PuedeEnviar);
    }
}
