using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Reglas;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.InicioSesion
{
    /// <summary>
    /// En qué terminó un intento de ingreso. La vista decide qué pintar con esto;
    /// el mensaje para el usuario ya viene en <see cref="LoginViewModel.Error"/>.
    /// </summary>
    public enum ResultadoIngreso
    {
        /// <summary>Credenciales válidas y sesión iniciada: se puede navegar.</summary>
        Exitoso,

        /// <summary>El proveedor de autenticación rechazó usuario o contraseña.</summary>
        CredencialesRechazadas,

        /// <summary>Autenticó bien, pero no se pudo armar la sesión (perfil, permisos).</summary>
        SesionFallida,

        /// <summary>Algo se rompió fuera de los caminos previstos (red, bug).</summary>
        ErrorInesperado,
    }

    /// <summary>
    /// La máquina de estados del inicio de sesión, sin una sola referencia a WPF.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe para que el flujo de autenticación —lo más sensible de la app— se pueda
    /// probar sin levantar una <c>Window</c>. Antes vivía entero en
    /// <c>LoginWindow.xaml.cs</c>, mezclado con animaciones y asignaciones de
    /// <c>Visibility</c>, así que no había forma de escribir un test de "credenciales
    /// rechazadas" o "la sesión falla" sin instanciar toda la infraestructura gráfica.
    /// Ver <c>P-058</c> en la deuda técnica.
    /// </para>
    /// <para>
    /// <b>Deliberadamente sin tipos de WPF.</b> El proyecto de pruebas apunta a
    /// <c>net8.0</c> (no <c>net8.0-windows</c>) y enlaza este archivo por
    /// <c>&lt;Compile Include="..\CapaUI\..."&gt;</c>: si acá entrara un
    /// <c>Visibility</c>, un <c>Brush</c> o un <c>Dispatcher</c>, el archivo dejaría
    /// de compilar en las pruebas. Esa restricción es la que mantiene honesta la
    /// separación.
    /// </para>
    /// <para>
    /// <b>Lo que NO está acá, a propósito:</b> el chrome de la ventana, los efectos de
    /// foco, los spinners, el bitmap del logo y el intercambio
    /// <c>PasswordBox</c>/<c>TextBox</c> del ojito. Todo eso es vista. En particular
    /// <c>PasswordBox</c> no se puede bindear —es una decisión de seguridad de WPF—,
    /// así que la contraseña la empuja el code-behind a <see cref="Password"/>.
    /// </para>
    /// </remarks>
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _autenticacion;
        private readonly IUsuarioSesionService _sesion;

        public LoginViewModel(IAuthService autenticacion, IUsuarioSesionService sesion)
        {
            _autenticacion = autenticacion;
            _sesion        = sesion;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeIngresar))]
        private string _email = "";

        /// <summary>
        /// Contraseña en texto plano. La escribe el code-behind desde el
        /// <c>PasswordBox</c>, que no admite binding. No agrega exposición respecto de
        /// lo que ya hacía antes: el flujo viejo también leía <c>TxtPassword.Password</c>
        /// a una variable local antes de llamar al servicio.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeIngresar))]
        private string _password = "";

        /// <summary>Hay un intento de ingreso en curso.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeIngresar))]
        private bool _ocupado;

        /// <summary>Mensaje para el usuario. Vacío cuando no hay error que mostrar.</summary>
        [ObservableProperty]
        private string _error = "";

        /// <summary>Regla del botón «Ingresar». La política vive en el dominio.</summary>
        public bool PuedeIngresar => !Ocupado && ReglasLogin.CredencialesCompletas(Email, Password);

        /// <summary>
        /// Un paso del progreso terminó bien (1 = credenciales, 2 = sesión). La vista lo
        /// usa para marcar el check; el ViewModel no sabe qué es un check.
        /// </summary>
        public event Action<int>? PasoCompletado;

        /// <summary>
        /// Autentica y arma la sesión.
        /// </summary>
        /// <param name="animarPaso">
        /// Cómo anima la vista el tramo de progreso <c>(desde, hasta)</c>. Se corre
        /// <b>en paralelo</b> con la llamada de red, no en serie: el paso dura
        /// <c>max(animación, red)</c> y no la suma de ambas. Ese entrelazado era del
        /// código viejo y se conserva tal cual. En las pruebas se pasa <c>null</c> y el
        /// flujo corre sin esperas artificiales.
        /// </param>
        /// <remarks>
        /// <see cref="Ocupado"/> queda en <c>true</c> cuando el resultado es
        /// <see cref="ResultadoIngreso.Exitoso"/>: ahí la app navega y la ventana muere,
        /// así que reactivar el botón sería un parpadeo inútil. En cualquier final que
        /// no sea exitoso vuelve a <c>false</c> para que se pueda reintentar.
        /// </remarks>
        public async Task<ResultadoIngreso> IngresarAsync(
            Func<int, int, Task>? animarPaso = null,
            CancellationToken ct = default)
        {
            Ocupado = true;
            Error   = "";

            Task Animar(int desde, int hasta) => animarPaso?.Invoke(desde, hasta) ?? Task.CompletedTask;

            try
            {
                // Paso 1 · verificar credenciales
                var animacion1 = Animar(0, 25);
                var autenticar = _autenticacion.LoginAsync(Email, Password, ct);
                await Task.WhenAll(animacion1, autenticar);

                // await y no .Result: sobre una tarea ya completa cuesta lo mismo, pero
                // deja pasar la excepción original en vez de envolverla en AggregateException.
                var credenciales = await autenticar;
                if (!credenciales.Success)
                {
                    Error   = credenciales.Error;
                    Ocupado = false;
                    return ResultadoIngreso.CredencialesRechazadas;
                }
                PasoCompletado?.Invoke(1);

                // Paso 2 · perfil, permisos y último acceso, en una sola llamada
                var animacion2 = Animar(25, 70);
                var iniciar    = _sesion.IniciarSesionAsync(credenciales.Value!.IdUsuario, ct);
                await Task.WhenAll(animacion2, iniciar);

                var sesion = await iniciar;
                if (!sesion.Success)
                {
                    Error   = sesion.Error;
                    Ocupado = false;
                    return ResultadoIngreso.SesionFallida;
                }
                PasoCompletado?.Invoke(2);

                return ResultadoIngreso.Exitoso;
            }
            catch (OperationCanceledException)
            {
                Ocupado = false;
                throw;
            }
            catch (Exception ex)
            {
                Error   = "Error: " + ex.Message;
                Ocupado = false;
                return ResultadoIngreso.ErrorInesperado;
            }
        }
    }
}
