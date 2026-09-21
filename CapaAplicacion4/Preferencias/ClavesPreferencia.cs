using System.Globalization;

namespace CapaAplicacion.Preferencias;

/// <summary>
/// Claves y ámbitos válidos de <c>public.usuario_preferencias</c>.
/// <para/>
/// Existen como constantes y no como cadenas sueltas porque la tabla tiene un
/// <c>CHECK</c> de formato sobre <c>clave</c> y <c>ambito</c>: un valor mal escrito
/// no falla en compilación, falla en la base con un error de restricción.
/// </summary>
public static class ClavesPreferencia
{
    /// <summary>Factor de escala de la UI. Es la única clave que usa un ámbito distinto de <see cref="AmbitoGlobal"/>.</summary>
    public const string EscalaUi = "escala_ui";

    public const string FilasPorPagina  = "filas_por_pagina";
    public const string DensidadTablas  = "densidad_tablas";
    public const string PantallaInicio  = "pantalla_inicio";
    public const string RecordarFiltros = "recordar_filtros";

    /// <summary>
    /// Apodo personal con el que el sistema saluda al usuario («Bienvenido, Paco»).
    /// Solo reemplaza el <b>nombre para mostrar</b>: nunca toca los datos reales del
    /// empleado. Sin fila, el saludo vuelve al nombre completo real.
    /// </summary>
    public const string Apodo = "apodo";

    /// <summary>
    /// Minutos de inactividad antes de cerrar la sesión sola (Fase 4.1 del Plan de
    /// Seguridad). Se consume en <c>SesionInactividadService</c>; sin fila vale 30.
    /// </summary>
    public const string TimeoutInactividad = "timeout_inactividad";

    /// <summary>Ámbito por defecto: la preferencia no depende de la pantalla.</summary>
    public const string AmbitoGlobal = "global";

    /// <summary>
    /// Arma la huella de pantalla que usa <see cref="EscalaUi"/> como ámbito, con el
    /// formato <c>ANCHOxALTO@ESCALA</c> (ej. <c>1920x1080@1.75</c>).
    /// <para/>
    /// El formato lo valida un <c>CHECK</c> en la base, así que el separador decimal
    /// tiene que ser punto: con la cultura del sistema en español, <c>1.75</c> se
    /// escribiría <c>1,75</c> y el <c>INSERT</c> sería rechazado. De ahí
    /// <see cref="CultureInfo.InvariantCulture"/>, y de ahí que exista este helper
    /// en vez de dejar que cada quien interpole la cadena a mano.
    /// </summary>
    public static string HuellaPantalla(int anchoPx, int altoPx, double escalaDpi)
    {
        // La escala de Windows viene en pasos de 25 % (1.0, 1.25, 1.5...). Se redondea
        // a dos decimales para que la misma pantalla no genere dos huellas distintas
        // por ruido de punto flotante.
        var escala = Math.Round(escalaDpi, 2);

        return string.Create(CultureInfo.InvariantCulture, $"{anchoPx}x{altoPx}@{escala:0.##}");
    }
}
