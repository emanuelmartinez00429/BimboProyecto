using System;
using System.Configuration;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace CapaUI.Services.Actualizacion
{
    /// <summary>
    /// Runtime de actualizaciones sobre Velopack + GitHub Releases.
    /// <para/>
    /// La instalación es per-user (<c>%LOCALAPPDATA%\BimboPesaje</c>), así que aplicar
    /// un paquete nunca requiere UAC. Toda falla de red o de configuración es silenciosa
    /// para el operador: se registra en el log y la app sigue funcionando igual.
    /// </summary>
    public sealed class ActualizacionService : IActualizacionService
    {
        private const string ClaveRepoUrl = "UPDATE_REPO_URL";
        private const string RepoUrlPorDefecto = "https://github.com/emanuelmartinez00429/BimboProyecto";

        private readonly Lazy<UpdateManager?> _manager;
        private UpdateInfo? _infoDisponible;
        private VelopackAsset? _assetPendiente;
        private bool _aplicacionSolicitada;

        public ActualizacionService()
        {
            _manager = new Lazy<UpdateManager?>(CrearManager);
        }

        public string VersionActual => ObtenerVersionActual();

        public bool EstaInstalada => _manager.Value?.IsInstalled ?? false;

        public async Task<ActualizacionDisponible?> BuscarAsync(CancellationToken ct = default)
        {
#if DEBUG
            if (SimulacionActiva)
                return new ActualizacionDisponible("1.0.1-simulada", ListaParaAplicar: false);
#endif
            var mgr = _manager.Value;
            if (mgr is null || !mgr.IsInstalled) return null;

            try
            {
                // Un paquete ya descargado en una corrida anterior se aplica sin red.
                var pendiente = mgr.UpdatePendingRestart;
                if (pendiente is not null)
                {
                    _assetPendiente = pendiente;
                    return new ActualizacionDisponible(pendiente.Version?.ToString() ?? "", ListaParaAplicar: true);
                }

                ct.ThrowIfCancellationRequested();
                var info = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
                if (info is null) return null;

                _infoDisponible = info;
                return new ActualizacionDisponible(
                    info.TargetFullRelease.Version?.ToString() ?? "",
                    ListaParaAplicar: false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Actualizacion] Falló la verificación de actualizaciones");
                return null;
            }
        }

        public async Task<bool> DescargarAsync(IProgress<int>? progreso = null, CancellationToken ct = default)
        {
#if DEBUG
            if (SimulacionActiva)
            {
                for (int i = 1; i <= 10; i++)
                {
                    await Task.Delay(250, ct).ConfigureAwait(false);
                    progreso?.Report(i * 10);
                }
                return true;
            }
#endif
            var mgr = _manager.Value;
            if (mgr is null || _infoDisponible is null) return false;

            try
            {
                await mgr.DownloadUpdatesAsync(
                    _infoDisponible,
                    p => progreso?.Report(p),
                    ct).ConfigureAwait(false);

                _assetPendiente = _infoDisponible.TargetFullRelease;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Actualizacion] Falló la descarga de la actualización");
                return false;
            }
        }

        public void AplicarAlCerrar() => Aplicar(restart: false);

        public void ReiniciarYAplicar() => Aplicar(restart: true);

        private void Aplicar(bool restart)
        {
            if (_aplicacionSolicitada) return;

            var mgr = _manager.Value;
            if (mgr is null) return;

            var asset = _assetPendiente ?? mgr.UpdatePendingRestart;
            if (asset is null) return;

            _aplicacionSolicitada = true;
            try
            {
                mgr.WaitExitThenApplyUpdates(asset, silent: true, restart: restart);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Actualizacion] No se pudo preparar la aplicación del paquete");
            }
        }

        private static UpdateManager? CrearManager()
        {
            try
            {
                string? url = Environment.GetEnvironmentVariable(ClaveRepoUrl)
                    ?? ConfigurationManager.AppSettings[ClaveRepoUrl];

                if (string.IsNullOrWhiteSpace(url)) url = RepoUrlPorDefecto;

                return new UpdateManager(new GithubSource(url.Trim(), accessToken: null, prerelease: false));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Actualizacion] No se pudo crear el UpdateManager");
                return null;
            }
        }

        private static string ObtenerVersionActual()
        {
            var asamblado = typeof(ActualizacionService).Assembly;
            string? informacional = asamblado
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informacional))
            {
                // El SDK agrega "+<commit>" cuando hay SourceLink; la UI muestra solo la versión.
                int corte = informacional.IndexOf('+');
                return corte > 0 ? informacional[..corte] : informacional;
            }

            return asamblado.GetName().Version?.ToString(3) ?? "0.0.0";
        }

#if DEBUG
        /// <summary>
        /// Permite ver el banner en desarrollo sin publicar un release:
        /// <c>set UPDATE_SIMULAR=1</c> y correr desde Visual Studio.
        /// Nunca se compila en Release.
        /// </summary>
        private static bool SimulacionActiva =>
            string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_SIMULAR"),
                "1",
                StringComparison.OrdinalIgnoreCase);
#endif
    }
}
