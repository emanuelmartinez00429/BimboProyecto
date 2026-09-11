using System;
using System.Windows.Media;
using Serilog;

namespace CapaUI.Core.Diagnosticos;

/// <summary>
/// Telemetry Service para la monitorización de la canalización Direct3D de WPF.
/// Extrae la clasificación gráfica de hardware (RenderCapability.Tier >> 16) y audita
/// mutaciones dinámicas como conexiones RDP o cambio de adaptador GPU.
/// Conforme a las recomendaciones de Microsoft Learn y la auditoría técnica de renderizado WPF.
/// </summary>
public sealed class PipelineTelemetryService : IDisposable
{
    private static PipelineTelemetryService? _instance;
    private bool _isDisposed;

    public static PipelineTelemetryService Instance => _instance ??= new PipelineTelemetryService();

    /// <summary>
    /// Vincula los delegados globales para auditoría arquitectónica dinámica del sistema.
    /// </summary>
    public void StartTelemetryMonitoring()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // Intercepta mutaciones de hardware en caliente (ej: Inserción/Expulsión de docks, RDP connect/disconnect)
        RenderCapability.TierChanged += OnRenderTierStateChanged;

        EvaluateHardwareRenderingPipeline();
    }

    /// <summary>
    /// Método de conveniencia estático para inicializar la auditoría gráfica en el inicio de la aplicación.
    /// </summary>
    public static void AuditarCapacidadesHardware()
    {
        Instance.StartTelemetryMonitoring();
    }

    /// <summary>
    /// Extrae de forma determinista la capacidad gráfica operativa desplazando la 
    /// palabra de orden superior de la propiedad de sistema RenderCapability.Tier.
    /// </summary>
    public void EvaluateHardwareRenderingPipeline()
    {
        // La capa de representación oficial (MilCore Classification) se obtiene mediante 
        // desplazamiento aritmético a la derecha (High-order word extraction).
        int renderingTier = RenderCapability.Tier >> 16;

        // Pattern matching para resolución de estados basado en las especificaciones oficiales DirectX de WPF
        var (tierName, isHwAccelerated, diagnostic) = renderingTier switch
        {
            0 => ("Tier 0 (Software Rasterizer / Degradación Crítica)", false,
                  "FALLO DIRECTX: Canalización WDDM desactivada. CPU ejecutando rasterización. Riesgo inminente de saturación por Tasa de Relleno (Fill Rate)."),

            1 => ("Tier 1 (Partial Hardware Acceleration / Híbrido)", true,
                  "ADVERTENCIA VRAM: Hardware (DirectX >= 9.0) limitado a < 120MB. Operaciones gráficas densas e IRTs pueden provocar micro-tirones y fallback a CPU."),

            2 => ("Tier 2 (Full Hardware Acceleration / Óptimo)", true,
                  "SISTEMA ESTABLE: Pipeline de hardware habilitado íntegramente con soporte completo de sombreadores (PixelShader 2.0+)."),

            _ => ($"Tier Desconocido ({renderingTier})", false, "Estado arquitectónico no identificado.")
        };

        if (!isHwAccelerated || renderingTier == 1)
        {
            Log.Warning(
                "[PipelineTelemetry] Degradación Arquitectónica Detectada.\n" +
                "Nivel Actual: {TierName}\nDiagnóstico: {Diagnostic}\n" +
                "Acción Requerida: Suspender efectos espaciales de interpolación en interfaces de alta densidad para evitar caída de subprocesos UI.",
                tierName, diagnostic);
        }
        else
        {
            Log.Information(
                "[PipelineTelemetry] Inicialización exitosa del orquestador WPF MilCore.\n" +
                "Nivel Actual: {TierName}\nDiagnóstico: {Diagnostic}",
                tierName, diagnostic);
        }
    }

    private void OnRenderTierStateChanged(object? sender, EventArgs e)
    {
        Log.Warning("[PipelineTelemetry] [WPF-WDDM] Mutación de hardware interceptada. El controlador de video o protocolo de visualización fue alterado.");
        EvaluateHardwareRenderingPipeline();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            RenderCapability.TierChanged -= OnRenderTierStateChanged;
            _isDisposed = true;
        }
    }
}
