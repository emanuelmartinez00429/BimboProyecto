namespace CapaAplicacion.Preferencias;

/// <summary>
/// Política del escalado propio de la aplicación: rango permitido, paso y factor
/// sugerido según la escala de Windows.
/// <para/>
/// Vive acá y no en <c>CapaUI</c> porque es aritmética pura, sin WPF: así se puede
/// probar sin levantar una ventana. El mecanismo —aplicar el <c>LayoutTransform</c>—
/// sí es de la capa de UI.
/// </summary>
public static class EscalaUi
{
    /// <summary>Factor sin escalado propio: la app se ve como la dibuja Windows.</summary>
    public const double Normal = 1.00;

    public const double Minimo = 0.70;
    public const double Maximo = 1.30;

    /// <summary>
    /// Salto entre valores elegibles. Es discreto y no continuo porque cada cambio
    /// dispara un relayout completo del árbol visual.
    /// </summary>
    public const double Paso = 0.05;

    /// <summary>
    /// Margen para comparar factores. Dos <c>double</c> que representan el mismo paso
    /// pueden diferir en el último bit; comparar con <c>==</c> haría que la app crea
    /// que cambió la escala cuando no cambió.
    /// </summary>
    private const double Tolerancia = 0.001;

    /// <summary>
    /// Lleva un factor arbitrario al valor elegible más cercano: lo recorta al rango y
    /// lo alinea al paso. Un valor fuera de rango guardado por una versión anterior
    /// —o editado a mano en la caché local— entra por acá y no rompe la UI.
    /// </summary>
    public static double Ajustar(double factor)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) return Normal;

        var recortado = Math.Clamp(factor, Minimo, Maximo);
        var alineado  = Math.Round(recortado / Paso, MidpointRounding.AwayFromZero) * Paso;

        // Segundo redondeo: 0.05 no es exacto en binario y el producto arrastra ruido
        // (0.8500000000000001), que después se escribiría así en el JSON.
        return Math.Round(alineado, 2);
    }

    /// <summary>¿Es el factor neutro, dentro de la tolerancia?</summary>
    public static bool EsNormal(double factor) => Math.Abs(factor - Normal) < Tolerancia;

    /// <summary>¿Son el mismo factor, dentro de la tolerancia?</summary>
    public static bool SonIguales(double a, double b) => Math.Abs(a - b) < Tolerancia;

    /// <summary>
    /// Factor sugerido para una escala de Windows dada. Es una heurística para que el
    /// usuario no arranque a tantear desde cero — no una fórmula: puede ignorarla y
    /// elegir cualquier valor del rango.
    /// </summary>
    /// <param name="escalaDpi">Escala de Windows como factor (1.0 = 100 %, 1.75 = 175 %).</param>
    public static double Sugerido(double escalaDpi)
    {
        if (double.IsNaN(escalaDpi) || double.IsInfinity(escalaDpi)) return Normal;

        // Tabla del plan. Se busca el tramo más cercano en vez de interpolar: los
        // valores salen de mirar pantallas reales, no de una curva.
        (double escala, double factor)[] tabla =
        [
            (1.00, 1.00),
            (1.25, 0.95),
            (1.50, 0.85),
            (1.75, 0.80),
            (2.00, 0.75),
        ];

        var masCercano = tabla[0];
        foreach (var tramo in tabla)
        {
            if (Math.Abs(tramo.escala - escalaDpi) < Math.Abs(masCercano.escala - escalaDpi))
                masCercano = tramo;
        }

        return Ajustar(masCercano.factor);
    }
}
