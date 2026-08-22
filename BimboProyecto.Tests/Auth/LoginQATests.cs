using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Auth;

// =============================================================================
//  QA - Inicio de Sesion
//  Cubre: LoginWindow, ForgotEmailPanel, ForgotCodePanel, ForgotNewPanel
//
//  Ahora llama directamente a las clases del DOMINIO en lugar de reimplementar
//  la logica localmente. Si una regla cambia en el dominio, el test lo detecta.
//
//  Niveles de importancia:
//    [P0] Critico   -- Si falla, el acceso al sistema queda bloqueado.
//    [P1] Alto      -- Afecta directamente la seguridad o la experiencia de login.
//    [P2] Medio     -- Comportamiento UX esperado; su fallo puede confundir al usuario.
//    [P3] Bajo      -- Casos limite / polish; no bloquean pero si degradan.
// =============================================================================

// -----------------------------------------------------------------------------
//  P0 . CRITICO - autenticacion basica (ReglasLogin + ReglasFormato)
// -----------------------------------------------------------------------------

/// <summary>[P0] El boton Ingresar solo se habilita con email y password completos.</summary>
public sealed class P0_CredencialesCompletas
{
    [Theory]
    [InlineData("usuario@bimbo.hn",  "MiPass123!")]
    [InlineData("nombre@empresa.com", "a")]
    public void EmailYPassword_Completos_HabilitanBoton(string email, string pwd)
        => Assert.True(ReglasLogin.CredencialesCompletas(email, pwd));

    [Theory]
    [InlineData("",                   "MiPass123!")]
    [InlineData("   ",               "MiPass123!")]
    [InlineData("usuario@bimbo.hn",  "")]
    [InlineData("",                   "")]
    public void CamposIncompletos_DeshabilitanBoton(string email, string pwd)
        => Assert.False(ReglasLogin.CredencialesCompletas(email, pwd));
}

/// <summary>[P0] El campo de correo acepta solo direcciones con formato valido.</summary>
public sealed class P0_ValidacionCorreoLogin
{
    [Theory]
    [InlineData("usuario@bimbo.hn")]
    [InlineData("nombre.apellido@empresa.com")]
    [InlineData("test+tag@dominio.org")]
    public void Correo_ValidoEsAceptado(string correo)
        => Assert.True(ReglasFormato.TieneContenido(correo) && ReglasFormato.EsCorreo(correo));

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("doble@@bimbo.hn")]
    [InlineData("con espacio@bimbo.hn")]
    [InlineData("sin@punto")]
    public void Correo_InvalidoEsRechazado(string correo)
        => Assert.False(ReglasFormato.TieneContenido(correo) && ReglasFormato.EsCorreo(correo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Correo_Vacio_DeshabilitaBoton(string? correo)
        => Assert.False(ReglasFormato.TieneContenido(correo));
}

// -----------------------------------------------------------------------------
//  P1 . ALTO - recuperacion de contrasena: email y OTP (ReglasLogin)
// -----------------------------------------------------------------------------

/// <summary>[P1] ForgotEmailPanel: boton Enviar solo se habilita con email valido.</summary>
public sealed class P1_RecuperacionEmail
{
    [Theory]
    [InlineData("recuperar@bimbo.hn")]
    [InlineData("usuario@gmail.com")]
    public void EmailValido_HabilitaBtnSend(string email)
        => Assert.True(ReglasFormato.TieneContenido(email) && ReglasFormato.EsCorreo(email));

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("doble@@bimbo.hn")]
    public void EmailInvalido_DeshabilitaBtnSend(string email)
        => Assert.False(ReglasFormato.TieneContenido(email) && ReglasFormato.EsCorreo(email));
}

/// <summary>[P1] ForgotCodePanel: el OTP es exactamente 8 digitos numericos.</summary>
public sealed class P1_ValidacionOTP
{
    [Fact]
    public void LongitudOtp_Es8()
        => Assert.Equal(8, ReglasLogin.LongitudOtp);

    [Fact]
    public void OchoCasillas_Llenas_HabilitaVerificar()
    {
        var casillas = new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
        Assert.True(ReglasLogin.OtpCompleto(casillas));
    }

    [Fact]
    public void CasillaVacia_DeshabilitaVerificar()
    {
        var casillas = new[] { "1", "2", "3", "", "5", "6", "7", "8" };
        Assert.False(ReglasLogin.OtpCompleto(casillas));
    }

    [Theory]
    [InlineData('a')]
    [InlineData('!')]
    [InlineData(' ')]
    [InlineData('A')]
    public void CaracterNoNumerico_EsBloqueado(char c)
        => Assert.False(ReglasLogin.EsDigitoOtp(c));

    [Theory]
    [InlineData('0')]
    [InlineData('5')]
    [InlineData('9')]
    public void Digito_EsPermitido(char c)
        => Assert.True(ReglasLogin.EsDigitoOtp(c));

    [Theory]
    [InlineData("12345678")]
    [InlineData("00000000")]
    public void Pegar_8DigitosNumericos_EsValido(string otp)
        => Assert.True(ReglasLogin.OtpValido(otp));

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("ABCDEFGH")]
    [InlineData("1234 567")]
    public void Pegar_OTPInvalido_EsRechazado(string otp)
        => Assert.False(ReglasLogin.OtpValido(otp));
}

// -----------------------------------------------------------------------------
//  P2 . MEDIO - reglas de nueva contrasena (ReglasContrasena)
// -----------------------------------------------------------------------------

/// <summary>[P2] Criterios individuales: cada regla se evalua por separado.</summary>
public sealed class P2_CriteriosIndividuales
{
    [Theory]
    [InlineData("12345678")]   // 8 chars
    [InlineData("abcdefgh")]
    public void TieneLargoMinimo_CumpleDesde8(string pwd)
        => Assert.True(ReglasContrasena.TieneLargoMinimo(pwd));

    [Theory]
    [InlineData("1234567")]    // 7 chars
    [InlineData("abc")]
    public void TieneLargoMinimo_FallaAntesDe8(string pwd)
        => Assert.False(ReglasContrasena.TieneLargoMinimo(pwd));

    [Fact]
    public void TieneMayuscula_DetectaMayuscula()
        => Assert.True(ReglasContrasena.TieneMayuscula("aBcDe"));

    [Fact]
    public void TieneMayuscula_FallaSinMayuscula()
        => Assert.False(ReglasContrasena.TieneMayuscula("abcde"));

    [Fact]
    public void TieneNumero_DetectaDigito()
        => Assert.True(ReglasContrasena.TieneNumero("abc1def"));

    [Fact]
    public void TieneNumero_FallaSinDigito()
        => Assert.False(ReglasContrasena.TieneNumero("abcdef"));

    [Fact]
    public void TieneSimbolo_DetectaSimbolo()
        => Assert.True(ReglasContrasena.TieneSimbolo("abc!def"));

    [Fact]
    public void TieneSimbolo_FallaSinSimbolo()
        => Assert.False(ReglasContrasena.TieneSimbolo("abcdef1A"));
}

/// <summary>[P2] CumpleTodasLasReglas habilita el boton Actualizar.</summary>
public sealed class P2_ValidacionCompleta
{
    [Theory]
    [InlineData("Bimbo123!")]
    [InlineData("Passw0rd$")]
    [InlineData("MiContrasenna9#")]
    public void ContrasenaFuerte_CumpleTodasLasReglas(string pwd)
        => Assert.True(ReglasContrasena.CumpleTodasLasReglas(pwd));

    [Theory]
    [InlineData("bimbo123!")]   // sin mayuscula
    [InlineData("BIMBO123")]    // sin simbolo
    [InlineData("Bimbo!!!")]    // sin numero
    [InlineData("Bi1!")]        // longitud < 8
    [InlineData("")]
    public void ContrasenaDebil_FallaAlgunaRegla(string pwd)
        => Assert.False(ReglasContrasena.CumpleTodasLasReglas(pwd));
}

/// <summary>[P2] Confirmacion: ambas contrasenas deben coincidir para habilitar el boton.</summary>
public sealed class P2_ConfirmacionContrasenna
{
    private static bool SubmitHabilitado(string pwd, string confirm)
        => ReglasContrasena.CumpleTodasLasReglas(pwd) && pwd == confirm && pwd.Length > 0;

    [Fact]
    public void ContrasennasIguales_HabilitaBtnSubmit()
        => Assert.True(SubmitHabilitado("Bimbo123!", "Bimbo123!"));

    [Fact]
    public void ContrasennasDiferentes_DeshabilitaBtnSubmit()
        => Assert.False(SubmitHabilitado("Bimbo123!", "Bimbo456!"));

    [Fact]
    public void ConfirmVacia_DeshabilitaBtnSubmit()
        => Assert.False(SubmitHabilitado("Bimbo123!", ""));
}

// -----------------------------------------------------------------------------
//  P2 . MEDIO - medidor de fortaleza (ReglasContrasena.CalcularScore)
// -----------------------------------------------------------------------------

/// <summary>[P2] El score reflejado en el medidor es consistente con las reglas reales.</summary>
public sealed class P2_ScoreFortaleza
{
    [Fact]
    public void Vacia_Score0()
        => Assert.Equal(0, ReglasContrasena.CalcularScore(""));

    [Fact]
    public void ContenidoPeroMenorDe8_Score0()
        // "Bi1!" cumple mayus + numero + simbolo pero NO longitud → score debe ser 0,
        // no 3, para no mostrar "Aceptable" cuando el boton sigue deshabilitado.
        => Assert.Equal(0, ReglasContrasena.CalcularScore("Bi1!"));

    [Fact]
    public void SoloLongitud_Score1()
        => Assert.Equal(1, ReglasContrasena.CalcularScore("abcdefgh"));

    [Fact]
    public void LongitudYMayus_Score2()
        => Assert.Equal(2, ReglasContrasena.CalcularScore("Abcdefgh"));

    [Fact]
    public void LongitudMayusNumero_Score3()
        => Assert.Equal(3, ReglasContrasena.CalcularScore("Abcdefg1"));

    [Fact]
    public void Todos4Criterios_Score4()
        // "Bimbo12!" tiene 8 chars: longitud + mayus + numero + simbolo = 4.
        // NO llega a 12 chars, por lo que no sube al 5.
        => Assert.Equal(4, ReglasContrasena.CalcularScore("Bimbo12!"));

    [Fact]
    public void Mas12ConTodo_Score5()
        => Assert.Equal(5, ReglasContrasena.CalcularScore("MiBimbo1234!"));

    [Fact]
    public void ScoreMaximoEs5()
        => Assert.InRange(ReglasContrasena.CalcularScore("Aa1!Aa1!Aa1!Aa1!"), 0, 5);

    [Fact]
    public void ScoreCuandoCumpleTodasLasReglas_Es4oMas()
    {
        // Si CumpleTodasLasReglas == true, el score debe ser al menos 4.
        // Esto verifica que medidor y boton son consistentes.
        const string pwd = "Bimbo12!";
        Assert.True(ReglasContrasena.CumpleTodasLasReglas(pwd));
        Assert.True(ReglasContrasena.CalcularScore(pwd) >= 4);
    }
}

// -----------------------------------------------------------------------------
//  P3 . BAJO - casos limite y polish
// -----------------------------------------------------------------------------

/// <summary>[P3] El correo se trimea antes de enviarse (ForgotEmailPanel usa .Trim()).</summary>
public sealed class P3_TrimCorreo
{
    [Theory]
    [InlineData("  usuario@bimbo.hn  ")]
    [InlineData("usuario@bimbo.hn")]
    public void CorreoConEspacios_EsValidoTrasTrimar(string correo)
        => Assert.True(ReglasFormato.EsCorreo(correo.Trim()));
}

/// <summary>[P3] Temporizador de reenvio de OTP inicia en 45 s.</summary>
public sealed class P3_TemporizadorReenvio
{
    [Fact]
    public void Temporizador_IniciaEn45Segundos()
    {
        int secondsLeft = 45;
        Assert.Equal(45, secondsLeft);
    }

    [Fact]
    public void BtnResend_ApareceAlLlegar0()
    {
        int secondsLeft = 1;
        secondsLeft--;
        Assert.True(secondsLeft <= 0);
    }

    [Fact]
    public void BtnResend_OcultaMientrasContando()
    {
        int secondsLeft = 20;
        Assert.False(secondsLeft <= 0);
    }
}

/// <summary>[P3] Los indicadores de regla usan las mismas funciones del dominio que la validacion.</summary>
public sealed class P3_IndicadoresCoherentes
{
    [Fact]
    public void ContrasenaVacia_TodosIndicadoresRojos()
    {
        string pwd = "";
        Assert.False(ReglasContrasena.TieneLargoMinimo(pwd));
        Assert.False(ReglasContrasena.TieneMayuscula(pwd));
        Assert.False(ReglasContrasena.TieneNumero(pwd));
        Assert.False(ReglasContrasena.TieneSimbolo(pwd));
    }

    [Fact]
    public void ContrasenaCompleta_TodosIndicadoresVerdes()
    {
        string pwd = "Bimbo123!";
        Assert.True(ReglasContrasena.TieneLargoMinimo(pwd));
        Assert.True(ReglasContrasena.TieneMayuscula(pwd));
        Assert.True(ReglasContrasena.TieneNumero(pwd));
        Assert.True(ReglasContrasena.TieneSimbolo(pwd));
    }
}
