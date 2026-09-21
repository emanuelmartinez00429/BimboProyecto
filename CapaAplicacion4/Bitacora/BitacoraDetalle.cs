using CapaAplicacion.Bitacora.Dtos;

namespace CapaAplicacion.Bitacora;

/// <summary>
/// Proyección de solo lectura compartida por el modal y el reporte individual.
/// Mantiene en un solo lugar las etiquetas amigables y el formato visible.
/// </summary>
public static class BitacoraDetalle
{
    public const string SinInformacion = "Sin información";

    public static IReadOnlyList<BitacoraDetalleCampo> CrearCampos(BitacoraDto registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        return
        [
            new("REGISTRO DE BITÁCORA", registro.IdBitacora.ToString()),
            new("FECHA / HORA", registro.FechaHora?.ToString("dd/MM/yyyy HH:mm") ?? SinInformacion),
            new("USUARIO", Valor(registro.AliasUsuario)),
            new("MÓDULO", Valor(registro.NombreModulo)),
            new("ACCIÓN", Valor(registro.NombreAccion)),
            new("CAMPO AFECTADO", Valor(registro.CampoAfectado)),
            new("ESTADO ANTERIOR", Valor(registro.EstadoAnterior)),
            new("DETALLE", Valor(registro.EstadoActual)),
            new("INFORMACIÓN ADICIONAL", Valor(registro.CampoExtra)),
            new("ORIGEN DEL REGISTRO", Valor(registro.TablaAfectada)),
            new("REFERENCIA DEL REGISTRO", registro.IdRegistroAfectado is > 0
                ? registro.IdRegistroAfectado.Value.ToString()
                : SinInformacion),
        ];
    }

    private static string Valor(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? SinInformacion : valor.Trim();
}

public sealed record BitacoraDetalleCampo(string Etiqueta, string Valor);
