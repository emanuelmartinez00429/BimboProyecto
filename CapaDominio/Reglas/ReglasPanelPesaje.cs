namespace CapaDominio.Reglas;

/// <summary>Gravedad de un aviso del panel de pesaje, de menor a mayor.</summary>
public enum NivelAlertaPesaje { Info, Ok, Advertencia, Critica }

/// <summary>Un aviso del panel lateral del modal de pesaje.</summary>
public sealed record AlertaPesaje(NivelAlertaPesaje Nivel, string Titulo, string Mensaje);

/// <summary>
/// Cuentas y avisos del panel lateral del modal de pesaje (diferencia, gráfico de
/// pesadas y alertas en vivo). Viven acá y no en el modal para poder testearlas: el
/// proyecto de tests no referencia CapaUI.
/// </summary>
public static class ReglasPanelPesaje
{
    /// <summary>Desvío respecto del promedio a partir del cual una pesada se marca atípica.</summary>
    public const double DesvioAtipico = 0.35;

    /// <summary>Pesadas confirmadas mínimas para que el promedio signifique algo.</summary>
    public const int MinimoParaPromedio = 2;

    /// <summary>
    /// Tope del eje Y del gráfico: el doble del promedio, así la línea de las pesadas queda
    /// a media altura sea cual sea su magnitud (pesadas de 10 kg → eje 0–20). Si una pesada
    /// se sale de eso (un bruto mal tecleado), manda ella con un 10 % de aire para que no se
    /// corte.
    /// <para/>
    /// Antes se partía del 35 % de lo manifestado y se redondeaba a 100/250: con pesadas
    /// chicas eso dejaba la línea pegada al piso.
    /// <para/>
    /// Se redondea hacia arriba a un paso par (2·10ⁿ⁻¹), así la mitad del tope — la etiqueta
    /// del medio — también es un número redondo.
    /// </summary>
    public static double EscalaMaxima(IReadOnlyCollection<double> netos)
    {
        var validos = netos.Where(n => n > 0).ToList();
        if (validos.Count == 0) return 100;

        double objetivo = Math.Max(2 * validos.Average(), validos.Max() * 1.1);
        double paso = 2 * Math.Pow(10, Math.Floor(Math.Log10(objetivo)) - 1);
        return Math.Ceiling(Math.Round(objetivo / paso, 9)) * paso;
    }

    /// <summary>
    /// Si la etiqueta del eje X del nodo <paramref name="indice"/> se dibuja. Hasta 10 nodos
    /// van todas; con más se ralean de 2 en 2 (o de 5 en 5 pasados 25), pero la primera y
    /// la última se muestran siempre.
    /// </summary>
    public static bool MostrarEtiquetaEjeX(int indice, int total)
    {
        if (total <= 10 || indice == 0 || indice == total - 1) return true;
        int paso = total > 25 ? 5 : 2;
        return indice % paso == 0;
    }

    /// <summary>
    /// Avisos de la pesada en curso, del más grave al más leve.
    /// </summary>
    /// <param name="bruto">Peso bruto tecleado (0 si el campo está vacío).</param>
    /// <param name="neto">Neto de la pesada en curso (bruto − tara individual − tara extra).</param>
    /// <param name="taraExtra">Tara extra pesada en esta pesada.</param>
    /// <param name="netoPrevio">Suma de netos de las pesadas ya guardadas (sin la que se edita).</param>
    /// <param name="manifestado">Peso manifestado del producto.</param>
    /// <param name="netosConfirmados">Netos de las pesadas ya guardadas (sin la que se edita).</param>
    public static IReadOnlyList<AlertaPesaje> EvaluarAlertas(
        double bruto, double neto, double taraExtra,
        double netoPrevio, double manifestado, IReadOnlyCollection<double> netosConfirmados)
    {
        // Campo vacío va primero: si no, un bruto de 0 cae en "neto inválido" y el modal
        // abre con un error rojo antes de que el operador toque nada.
        if (bruto <= 0)
            return [new(NivelAlertaPesaje.Info, "Sin alertas",
                "Ingresá el peso bruto para validar la pesada en vivo.")];

        // Neto <= 0 excluye las demás: sin neto válido no hay avance ni desvío que medir.
        if (neto <= 0)
            return [new(NivelAlertaPesaje.Critica, "Peso neto inválido",
                "El peso neto quedaría en cero o negativo. Revisá el peso bruto o la tara extra.")];

        var alertas = new List<AlertaPesaje>();
        double acumulado = netoPrevio + neto;

        // "Esta pesada excede lo que falta" es la misma condición que esta (neto > manif −
        // previo ⇔ previo + neto > manif), así que no se evalúa aparte.
        if (manifestado > 0 && acumulado > manifestado)
            alertas.Add(new(NivelAlertaPesaje.Critica, "Excedente",
                $"El neto acumulado supera lo manifestado por {Kg(acumulado - manifestado)} kg."));

        if (taraExtra <= 0)
            alertas.Add(new(NivelAlertaPesaje.Advertencia, "Sin tara extra",
                "Sin la tara extra de esta pesada, los bultos estimados son aproximados. Podés pesarla ahora o cargar el total después con «Tara extra»."));

        if (netosConfirmados.Count >= MinimoParaPromedio)
        {
            double promedio = netosConfirmados.Average();
            if (promedio > 0 && Math.Abs(neto - promedio) / promedio > DesvioAtipico)
                alertas.Add(new(NivelAlertaPesaje.Advertencia, "Pesada atípica",
                    $"Se desvía más de un {DesvioAtipico * 100:0} % del promedio ({Kg(promedio)} kg). Revisá el bruto."));
        }

        if (alertas.Count == 0)
            alertas.Add(new(NivelAlertaPesaje.Ok, "Pesada dentro de lo esperado",
                $"Neto {Kg(neto)} kg · faltante {Kg(Math.Max(0, manifestado - acumulado))} kg"));

        return alertas;
    }

    private static string Kg(double v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
