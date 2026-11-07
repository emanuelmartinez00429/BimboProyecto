using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapaUI.Services.Actualizacion
{
    /// <summary>
    /// Resultado de una verificación de actualizaciones.
    /// <para/>
    /// <paramref name="ListaParaAplicar"/> es <c>true</c> cuando el paquete ya está
    /// descargado y solo falta aplicarlo al cerrar (o reiniciar).
    /// </summary>
    public sealed record ActualizacionDisponible(string Version, bool ListaParaAplicar);

    /// <summary>
    /// Contrato del runtime de actualizaciones. Aísla a los ViewModels de los tipos
    /// de Velopack: el servicio guarda internamente el <c>UpdateInfo</c>/<c>VelopackAsset</c>
    /// y los tests pueden reemplazarlo por un fake.
    /// </summary>
    public interface IActualizacionService
    {
        /// <summary>La app corre desde una instalación de Velopack (no desde <c>bin\</c>).</summary>
        bool EstaInstalada { get; }

        /// <summary>Versión del ensamblado, sin metadatos de build (ej. <c>1.0.0</c>).</summary>
        string VersionActual { get; }

        /// <summary>
        /// Verifica si hay una actualización. Devuelve <c>null</c> cuando no hay nada
        /// nuevo, cuando la app no está instalada o cuando la verificación falla
        /// (el error se registra y no se propaga al usuario).
        /// </summary>
        Task<ActualizacionDisponible?> BuscarAsync(CancellationToken ct = default);

        /// <summary>
        /// Descarga el paquete detectado por <see cref="BuscarAsync"/>. Devuelve
        /// <c>false</c> si no había nada pendiente o si la descarga falló.
        /// </summary>
        Task<bool> DescargarAsync(IProgress<int>? progreso = null, CancellationToken ct = default);

        /// <summary>
        /// Deja preparado el updater para aplicar el paquete cuando el proceso termine.
        /// No reinicia la app.
        /// </summary>
        void AplicarAlCerrar();

        /// <summary>
        /// Aplica el paquete y reinicia la app inmediatamente. El llamador debe apagar
        /// el proceso después de invocarlo.
        /// </summary>
        void ReiniciarYAplicar();
    }
}
