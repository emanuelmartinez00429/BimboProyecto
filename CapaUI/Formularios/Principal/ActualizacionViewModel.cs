using System;
using System.Threading.Tasks;
using CapaUI.Services.Actualizacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal
{
    /// <summary>
    /// Estado del banner de actualización. Es un singleton de proceso: la actualización
    /// es de la máquina, no del usuario logueado, y la misma instancia alimenta el banner
    /// del login y el de <see cref="MainWindow"/>.
    /// <para/>
    /// No usa tipos de WPF a propósito: así se puede enlazar en BimboProyecto.Tests
    /// (net10.0) y probar la máquina de estados con un fake de <see cref="IActualizacionService"/>.
    /// </summary>
    public partial class ActualizacionViewModel : ObservableObject
    {
        private readonly IActualizacionService _servicio;
        private bool _iniciado;

        public ActualizacionViewModel(IActualizacionService servicio)
        {
            _servicio = servicio;
        }

        /// <summary>
        /// Se dispara cuando el usuario pide reiniciar. La confirmación y el apagado son
        /// responsabilidad de la vista; el ViewModel solo avisa.
        /// </summary>
        public event EventHandler? ReinicioSolicitado;

        public string VersionTexto => $"v{_servicio.VersionActual}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mensaje))]
        private bool _bannerVisible;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mensaje))]
        private string _versionDisponible = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mensaje))]
        private bool _descargando;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mensaje))]
        private int _progreso;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mensaje))]
        [NotifyCanExecuteChangedFor(nameof(ReiniciarAhoraCommand))]
        private bool _listaParaAplicar;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Mensaje))]
        private bool _aplicando;

        public string Mensaje => Aplicando
            ? "Reiniciando para aplicar la actualización…"
            : Descargando
                ? $"Descargando actualización {VersionDisponible}… {Progreso}%"
                : $"Actualización {VersionDisponible} lista. Se aplicará al cerrar.";

        /// <summary>
        /// Verifica y descarga en background. Una sola vez por proceso.
        /// </summary>
        [RelayCommand]
        public async Task IniciarAsync()
        {
            if (_iniciado) return;
            _iniciado = true;

            try
            {
                if (!_servicio.EstaInstalada) return;

                var disponible = await _servicio.BuscarAsync();
                if (disponible is null) return;

                VersionDisponible = disponible.Version;

                if (disponible.ListaParaAplicar)
                {
                    ListaParaAplicar = true;
                    BannerVisible = true;
                    return;
                }

                BannerVisible = true;
                Descargando    = true;

                bool descargada = await _servicio.DescargarAsync(new Progress<int>(p => Progreso = p));

                Descargando = false;
                if (!descargada)
                {
                    BannerVisible = false;
                    return;
                }

                Progreso          = 100;
                ListaParaAplicar  = true;
            }
            catch (Exception)
            {
                // El servicio ya registra el detalle. Un fallo de actualización nunca
                // puede romper el login ni la operación de la app.
                Descargando   = false;
                BannerVisible = false;
            }
        }

        private bool PuedeReiniciar => ListaParaAplicar && !Aplicando;

        [RelayCommand(CanExecute = nameof(PuedeReiniciar))]
        private void ReiniciarAhora() => ReinicioSolicitado?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void Ocultar() => BannerVisible = false;

        /// <summary>
        /// La vista llama acá recién después de que el usuario confirma el reinicio.
        /// </summary>
        public void AplicarReinicio()
        {
            if (!ListaParaAplicar || Aplicando) return;

            Aplicando = true;
            _servicio.ReiniciarYAplicar();
        }
    }
}
