using CapaAplicacion.Auth.Interfaces;
using CapaDominio.Reglas;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Formularios.InicioSesion
{
    /// <summary>
    /// Base de los tres pasos de la recuperación de contraseña: el estado que todos
    /// comparten (ocupado, error) y la forma de correr una operación del servicio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los tres van juntos en un archivo porque son <b>un solo flujo</b> —pedir código,
    /// verificarlo, cambiar la contraseña— y leerlos separados esconde la secuencia.
    /// </para>
    /// <para>
    /// <b>Sin un solo tipo de WPF</b>, igual que <see cref="LoginViewModel"/>: el
    /// proyecto de pruebas apunta a <c>net8.0</c> y enlaza este archivo, así que si
    /// alguien mete acá un <c>Visibility</c> las pruebas dejan de compilar. Ver P-058.
    /// </para>
    /// </remarks>
    public abstract partial class PasoRecuperacionViewModel : ObservableObject
    {
        protected readonly IRecuperacionPasswordService Servicio;

        protected PasoRecuperacionViewModel(IRecuperacionPasswordService servicio) => Servicio = servicio;

        /// <summary>Hay una llamada en curso. Mientras tanto no se puede reintentar.</summary>
        [ObservableProperty]
        private bool _ocupado;

        /// <summary>Mensaje para el usuario. Vacío cuando no hay nada que mostrar.</summary>
        [ObservableProperty]
        private string _error = "";

        // El hook que genera [ObservableProperty] para Ocupado existe SOLO acá, en la
        // clase que declara el campo. Se reexpone virtual para que cada paso avise que
        // su propia regla de "puedo continuar" cambió — si no, el botón se congela.
        partial void OnOcupadoChanged(bool value) => AlCambiarOcupado();

        /// <summary>Avisar acá qué propiedad calculada depende de <see cref="Ocupado"/>.</summary>
        protected virtual void AlCambiarOcupado() { }

        /// <summary>
        /// Corre una operación del servicio marcando <see cref="Ocupado"/> y dejando el
        /// mensaje en <see cref="Error"/> si falla. Devuelve si salió bien.
        /// </summary>
        /// <remarks>
        /// El servicio no lanza por fallos esperados (devuelve <c>Result</c>), pero se
        /// atrapa igual: un bug o un corte de red en medio del SDK no puede dejar el
        /// panel colgado en «Enviando…» para siempre.
        /// </remarks>
        protected async Task<bool> EjecutarAsync(Func<CancellationToken, Task<CapaAplicacion.Common.Result>> operacion,
                                                 CancellationToken ct)
        {
            Ocupado = true;
            Error   = "";
            try
            {
                var r = await operacion(ct);
                if (r.Success) return true;
                Error = r.Error;
                return false;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                return false;
            }
            finally
            {
                Ocupado = false;
            }
        }
    }

    /// <summary>Paso 1 · pedir el código al correo.</summary>
    public partial class SolicitarCodigoViewModel : PasoRecuperacionViewModel
    {
        public SolicitarCodigoViewModel(IRecuperacionPasswordService servicio) : base(servicio) { }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeEnviar))]
        private string _email = "";

        /// <summary>
        /// <c>ReglasFormato.EsCorreo</c> trata el vacío como válido —es la convención de
        /// los campos opcionales—, así que hay que exigir contenido aparte.
        /// </summary>
        public bool PuedeEnviar =>
            !Ocupado && ReglasFormato.TieneContenido(Email) && ReglasFormato.EsCorreo(Email);

        protected override void AlCambiarOcupado() => OnPropertyChanged(nameof(PuedeEnviar));

        public Task<bool> EnviarAsync(CancellationToken ct = default) =>
            EjecutarAsync(t => Servicio.EnviarCodigoAsync(Email.Trim(), t), ct);
    }

    /// <summary>Paso 2 · verificar el código de un solo uso.</summary>
    public partial class VerificarCodigoViewModel : PasoRecuperacionViewModel
    {
        public VerificarCodigoViewModel(IRecuperacionPasswordService servicio, string email) : base(servicio)
            => Email = email;

        /// <summary>El correo al que se envió el código. Viene del paso anterior.</summary>
        public string Email { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeVerificar))]
        private string _codigo = "";

        /// <summary>La longitud y el formato del OTP son un contrato con el proveedor.</summary>
        public bool PuedeVerificar => !Ocupado && ReglasLogin.OtpValido(Codigo);

        protected override void AlCambiarOcupado() => OnPropertyChanged(nameof(PuedeVerificar));

        public Task<bool> VerificarAsync(CancellationToken ct = default) =>
            EjecutarAsync(t => Servicio.VerificarCodigoAsync(Email, Codigo, t), ct);

        public Task<bool> ReenviarAsync(CancellationToken ct = default) =>
            EjecutarAsync(t => Servicio.EnviarCodigoAsync(Email, t), ct);
    }

    /// <summary>Paso 3 · fijar la contraseña nueva.</summary>
    public partial class NuevaPasswordViewModel : PasoRecuperacionViewModel
    {
        public NuevaPasswordViewModel(IRecuperacionPasswordService servicio) : base(servicio) { }

        /// <summary>
        /// La escribe el code-behind desde el <c>PasswordBox</c>, que no admite binding
        /// por decisión de seguridad de WPF.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
        [NotifyPropertyChangedFor(nameof(TieneLargoMinimo))]
        [NotifyPropertyChangedFor(nameof(TieneMayuscula))]
        [NotifyPropertyChangedFor(nameof(TieneNumero))]
        [NotifyPropertyChangedFor(nameof(TieneSimbolo))]
        [NotifyPropertyChangedFor(nameof(Coinciden))]
        private string _nuevaPassword = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
        [NotifyPropertyChangedFor(nameof(Coinciden))]
        private string _confirmacion = "";

        // Los cuatro checks de la lista de requisitos. La política vive en el dominio;
        // acá solo se expone para que la vista pinte cada tilde.
        public bool TieneLargoMinimo => ReglasContrasena.TieneLargoMinimo(NuevaPassword);
        public bool TieneMayuscula   => ReglasContrasena.TieneMayuscula(NuevaPassword);
        public bool TieneNumero      => ReglasContrasena.TieneNumero(NuevaPassword);
        public bool TieneSimbolo     => ReglasContrasena.TieneSimbolo(NuevaPassword);

        /// <summary>Las dos cajas coinciden. Con la confirmación vacía no es un error todavía.</summary>
        public bool Coinciden => NuevaPassword == Confirmacion;

        /// <summary>Mostrar el aviso de «no coinciden» recién cuando el usuario escribió algo.</summary>
        public bool MostrarNoCoinciden => !Coinciden && Confirmacion.Length > 0;

        public bool PuedeGuardar =>
            !Ocupado && NuevaPassword.Length > 0 && Coinciden &&
            ReglasContrasena.CumpleTodasLasReglas(NuevaPassword);

        protected override void AlCambiarOcupado() => OnPropertyChanged(nameof(PuedeGuardar));

        partial void OnConfirmacionChanged(string value) => OnPropertyChanged(nameof(MostrarNoCoinciden));
        partial void OnNuevaPasswordChanged(string value) => OnPropertyChanged(nameof(MostrarNoCoinciden));

        public Task<bool> GuardarAsync(CancellationToken ct = default) =>
            EjecutarAsync(t => Servicio.CambiarPasswordAsync(NuevaPassword, t), ct);
    }
}
