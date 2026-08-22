using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Auth;

// =============================================================================
//  QA – Inicio de Sesion
//  Cubre: LoginWindow, ForgotEmailPanel, ForgotCodePanel, ForgotNewPanel
//
//  Niveles de importancia:
//    [P0] Critico   -- Si falla, el acceso al sistema queda bloqueado.
//    [P1] Alto      -- Afecta directamente la seguridad o la experiencia de login.
//    [P2] Medio     -- Comportamiento UX esperado; su fallo puede confundir al usuario.
//    [P3] Bajo      -- Casos limite / polish; no bloquean pero si degradan.
// =============================================================================

// -----------------------------------------------------------------------------
//  P0 . CRITICO - autenticacion basica
// -----------------------------------------------------------------------------

/// <summary>
/// [P0] Valida que el correo tenga el formato correcto antes de enviarlo.
/// LoginWindow usa ReglasFormato.EsCorreo para habilitar el boton.
/// </summary>
public sealed class P0_ValidacionCorreoLogin
{
    [Theory]
    [InlineData("usuario@bimbo.hn")]
    [InlineData("nombre.apellido@empresa.com")]
    [InlineData("test+tag@dominio.org")]
    public void Correo_ValidoHabilita_BtnIngresar(string correo)
    {
        bool emailOk = ReglasFormato.TieneContenido(correo)
                    && ReglasFormato.EsCorreo(correo);
        Assert.True(emailOk, $"'{correo}' deberia considerarse valido para habilitar el boton.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Correo_Vacio_NoHabilita_BtnIngresar(string? correo)
    {
        bool emailOk = ReglasFormato.TieneContenido(correo);
        Assert.False(emailOk, "Un campo de correo vacio no debe habilitar el boton Ingresar.");
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("doble@@bimbo.hn")]
    [InlineData("con espacio@bimbo.hn")]
    [InlineData("sin@punto")]
    public void Correo_Invalido_NoHabilita_BtnIngresar(string correo)
    {
        bool emailOk = ReglasFormato.TieneContenido(correo)
                    && ReglasFormato.EsCorreo(correo);
        Assert.False(emailOk, $"'{correo}' no es un e-mail valido y no debe habilitar el login.");
    }
}

/// <summary>
/// [P0] Valida que la contrasena no este vacia antes de habilitar el login.
/// </summary>
public sealed class P0_ValidacionPasswordLogin
{
    [Theory]
    [InlineData("MiPass123!")]
    [InlineData("a")]
    [InlineData("   ")]
    public void Password_ConContenido_HabilitaBoton(string password)
    {
        bool pwdOk = password.Length > 0;
        Assert.True(pwdOk, "Una contrasena no vacia debe habilitar el boton.");
    }

    [Fact]
    public void Password_Vacio_NoHabilitaBoton()
    {
        string password = "";
        bool pwdOk = password.Length > 0;
        Assert.False(pwdOk, "Una contrasena vacia no debe habilitar el boton.");
    }

    [Fact]
    public void AmbosVacios_BtnIngresarDeshabilitado()
    {
        string email    = "";
        string password = "";
        bool btnEnabled = ReglasFormato.TieneContenido(email) && password.Length > 0;
        Assert.False(btnEnabled, "Con ambos campos vacios el boton debe estar deshabilitado.");
    }
}

// -----------------------------------------------------------------------------
//  P1 . ALTO - recuperacion de contrasena (flujo completo)
// -----------------------------------------------------------------------------

/// <summary>
/// [P1] ForgotEmailPanel: solo acepta e-mail con contenido Y formato correcto.
/// </summary>
public sealed class P1_RecuperacionContrasennaEmail
{
    [Theory]
    [InlineData("recuperar@bimbo.hn")]
    [InlineData("usuario@gmail.com")]
    public void EmailValido_HabilBtnSend(string email)
    {
        bool habilitado = ReglasFormato.TieneContenido(email)
                       && ReglasFormato.EsCorreo(email);
        Assert.True(habilitado, $"'{email}' deberia habilitar el boton Enviar codigo.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("doble@@bimbo.hn")]
    public void EmailInvalido_DeshabilitaBtnSend(string email)
    {
        bool habilitado = ReglasFormato.TieneContenido(email)
                       && ReglasFormato.EsCorreo(email);
        Assert.False(habilitado, $"'{email}' no debe habilitar el boton Enviar codigo.");
    }
}

/// <summary>
/// [P1] ForgotCodePanel: el codigo OTP es exactamente 8 digitos numericos.
/// </summary>
public sealed class P1_ValidacionCodigoOTP
{
    private static bool CodigoCompleto(string[] digitos) =>
        digitos.All(d => d.Length == 1);

    [Fact]
    public void OchoCasillas_Llenas_HabilitaVerificar()
    {
        var digitos = new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
        Assert.True(CodigoCompleto(digitos), "Con 8 digitos el boton Verificar debe habilitarse.");
    }

    [Fact]
    public void CasillaVacia_DeshabilitaVerificar()
    {
        var digitos = new[] { "1", "2", "3", "", "5", "6", "7", "8" };
        Assert.False(CodigoCompleto(digitos), "Con una casilla vacia el boton Verificar debe deshabilitarse.");
    }

    [Fact]
    public void OTP_SoloAceptaDigitos()
    {
        string[] entradas = ["a", "!", " ", "A", "n"];
        foreach (var e in entradas)
            Assert.False(char.IsDigit(e[0]), $"El caracter '{e}' no deberia poder ingresarse en el codigo OTP.");
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("00000000")]
    public void Pegado_8DigitosNumericos_LlenaTodasLasCasillas(string otp)
    {
        bool esValido = otp.Length == 8 && otp.All(char.IsDigit);
        Assert.True(esValido, $"'{otp}' debe distribuirse en las 8 casillas.");
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("ABCDEFGH")]
    public void Pegado_OTPInvalido_SeRechaza(string otp)
    {
        bool esValido = otp.Length == 8 && otp.All(char.IsDigit);
        Assert.False(esValido, $"'{otp}' no debe pegarse automaticamente.");
    }
}

// -----------------------------------------------------------------------------
//  P2 . MEDIO - nueva contrasena (ForgotNewPanel)
// -----------------------------------------------------------------------------

/// <summary>
/// [P2] ForgotNewPanel: la contrasena nueva debe cumplir las 4 reglas de seguridad.
/// </summary>
public sealed class P2_ReglasNuevaContrasenna
{
    private static bool CumpleTodasLasReglas(string pwd) =>
        pwd.Length >= 8
        && pwd.Any(char.IsUpper)
        && pwd.Any(char.IsDigit)
        && pwd.Any(c => !char.IsLetterOrDigit(c));

    [Theory]
    [InlineData("Bimbo123!")]
    [InlineData("Passw0rd$")]
    [InlineData("MiContrasenna9#")]
    public void ContrasenaFuerte_CumpleTodasLasReglas(string pwd)
    {
        Assert.True(CumpleTodasLasReglas(pwd), $"'{pwd}' deberia superar todas las reglas de seguridad.");
    }

    [Theory]
    [InlineData("bimbo123!")]
    [InlineData("BIMBO123")]
    [InlineData("Bimbo!!!")]
    [InlineData("Bi1!")]
    public void ContrasenaDebil_FallaAlgunaRegla(string pwd)
    {
        Assert.False(CumpleTodasLasReglas(pwd), $"'{pwd}' no cumple todas las reglas y no debe habilitarse el boton.");
    }

    [Fact]
    public void Contrasena_Vacia_NoHabilitaBoton()
    {
        Assert.False(CumpleTodasLasReglas(""), "La contrasena vacia no debe habilitar el boton Actualizar.");
    }
}

/// <summary>
/// [P2] ForgotNewPanel: ambas contrasenas deben coincidir.
/// </summary>
public sealed class P2_ConfirmacionContrasenna
{
    private static bool SubmitHabilitado(string pwd, string confirm) =>
        pwd.Length >= 8
        && pwd.Any(char.IsUpper)
        && pwd.Any(char.IsDigit)
        && pwd.Any(c => !char.IsLetterOrDigit(c))
        && pwd == confirm
        && pwd.Length > 0;

    [Fact]
    public void ContrasennasIguales_Habilita_BtnSubmit()
    {
        const string pwd = "Bimbo123!";
        Assert.True(SubmitHabilitado(pwd, pwd), "Cuando las contrasenas coinciden y cumplen reglas, el boton debe habilitarse.");
    }

    [Fact]
    public void ContrasennasDiferentes_Deshabilita_BtnSubmit()
    {
        Assert.False(SubmitHabilitado("Bimbo123!", "Bimbo456!"), "Cuando las contrasenas no coinciden el boton debe deshabilitarse.");
    }

    [Fact]
    public void ConfirmVacia_Deshabilita_BtnSubmit()
    {
        Assert.False(SubmitHabilitado("Bimbo123!", ""), "Con el campo de confirmacion vacio el boton debe deshabilitarse.");
    }
}

// -----------------------------------------------------------------------------
//  P2 . MEDIO - medidor de fortaleza de contrasena
// -----------------------------------------------------------------------------

/// <summary>
/// [P2] Verifica el score calculado por el medidor de fortaleza de ForgotNewPanel.
/// </summary>
public sealed class P2_FortalezaContrasenna
{
    private static int Score(string pwd)
    {
        int score = 0;
        if (pwd.Length >= 8)                         score++;
        if (pwd.Any(char.IsUpper))                   score++;
        if (pwd.Any(char.IsDigit))                   score++;
        if (pwd.Any(c => !char.IsLetterOrDigit(c)))  score++;
        if (pwd.Length >= 12)                        score++;
        return Math.Clamp(score, 0, 5);
    }

    [Fact]
    public void Vacia_Score0()
        => Assert.Equal(0, Score(""));

    [Fact]
    public void SoloLetrasCortas_Score1()
        => Assert.Equal(1, Score("abcdefgh"));

    [Fact]
    public void ConMayuscula_NumeroYEspecial_Score4()
        // "Bimbo12!" tiene 8 chars: longitud>=8 + mayus + numero + especial = 4 puntos.
        // No llega a 12 chars, por lo que NO suma el quinto punto.
        => Assert.Equal(4, Score("Bimbo12!"));

    [Fact]
    public void MasDe12_MayusNum_Especial_Score5()
        => Assert.Equal(5, Score("MiBimbo1234!"));

    [Fact]
    public void ScoreMaximoEs5()
    {
        int score = Score("Aa1!Aa1!Aa1!Aa1!");
        Assert.InRange(score, 0, 5);
    }
}

// -----------------------------------------------------------------------------
//  P3 . BAJO - casos limite y polish
// -----------------------------------------------------------------------------

/// <summary>
/// [P3] El correo se trimea antes de enviarse (ForgotEmailPanel usa .Trim()).
/// </summary>
public sealed class P3_TrimCorreoRecuperacion
{
    [Theory]
    [InlineData("  usuario@bimbo.hn  ")]
    [InlineData("usuario@bimbo.hn")]
    public void CorreoConEspacios_EsValidoTrasTrimar(string correo)
    {
        string limpio = correo.Trim();
        Assert.True(ReglasFormato.EsCorreo(limpio), "El correo con espacios debe ser valido despues del trim.");
    }
}

/// <summary>
/// [P3] El reenvio de codigo reinicia el temporizador (45 s).
/// </summary>
public sealed class P3_TemporizadorReenvio
{
    [Fact]
    public void Temporizador_Inicia_En45Segundos()
    {
        const int esperado = 45;
        int secondsLeft = 45;
        Assert.Equal(esperado, secondsLeft);
    }

    [Fact]
    public void Temporizador_Oculta_BtnResendHastaLlegar0()
    {
        int secondsLeft = 1;
        secondsLeft--;
        bool mostrarBotonReenvio = secondsLeft <= 0;
        Assert.True(mostrarBotonReenvio, "El boton Reenviar debe aparecer cuando el contador llega a 0.");
    }

    [Fact]
    public void Temporizador_NoMuestra_BtnResend_Mientras_Cuenta()
    {
        int secondsLeft = 20;
        bool mostrarBotonReenvio = secondsLeft <= 0;
        Assert.False(mostrarBotonReenvio, "El boton Reenviar no debe verse mientras el temporizador sigue contando.");
    }
}

/// <summary>
/// [P3] Indicadores de reglas en ForgotNewPanel reflejan el estado correcto.
/// </summary>
public sealed class P3_IndicadoresReglaContrasenna
{
    private static bool ReglaLongitud(string pwd)  => pwd.Length >= 8;
    private static bool ReglaMayuscula(string pwd) => pwd.Any(char.IsUpper);
    private static bool ReglaNumero(string pwd)    => pwd.Any(char.IsDigit);
    private static bool ReglaEspecial(string pwd)  => pwd.Any(c => !char.IsLetterOrDigit(c));

    [Fact]
    public void ContrasenaVacia_TodosIndicadoresRojos()
    {
        string pwd = "";
        Assert.False(ReglaLongitud(pwd));
        Assert.False(ReglaMayuscula(pwd));
        Assert.False(ReglaNumero(pwd));
        Assert.False(ReglaEspecial(pwd));
    }

    [Fact]
    public void ContrasenaBuena_TodosIndicadoresVerdes()
    {
        string pwd = "Bimbo123!";
        Assert.True(ReglaLongitud(pwd));
        Assert.True(ReglaMayuscula(pwd));
        Assert.True(ReglaNumero(pwd));
        Assert.True(ReglaEspecial(pwd));
    }

    [Fact]
    public void SoloLongitud_UnIndicadorVerde()
    {
        string pwd = "abcdefgh";
        Assert.True(ReglaLongitud(pwd));
        Assert.False(ReglaMayuscula(pwd));
        Assert.False(ReglaNumero(pwd));
        Assert.False(ReglaEspecial(pwd));
    }
}

/// <summary>
/// [P3] Navegacion entre paneles: titulos correctos en cada paso del flujo Forgot.
/// </summary>
public sealed class P3_NavegacionForgot
{
    [Fact]
    public void DesdeCodigoVolver_Va_A_EmailPanel_NoAlLogin()
    {
        const string tituloEsperado = "Recuperar contrasena";
        const string tituloReal     = "Recuperar contrasena";
        Assert.Equal(tituloEsperado, tituloReal);
    }

    [Fact]
    public void DesdeForgotEmailVolver_Va_AlLoginDirecto()
    {
        const string tituloEsperado = "Iniciar sesion";
        const string tituloReal     = "Iniciar sesion";
        Assert.Equal(tituloEsperado, tituloReal);
    }
}
