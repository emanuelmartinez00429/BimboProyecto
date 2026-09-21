namespace CapaDominio
{
    /// <summary>
    /// Centraliza las fórmulas de cálculo de pesos del negocio.
    /// <para/>
    /// Vive en <c>CapaDominio</c> y no en el modal para poder probarse: el proyecto de
    /// tests no referencia <c>CapaUI</c>. El modal la usa para la vista previa y el
    /// trigger <c>calcular_pesos_entrada()</c> replica la misma cuenta al guardar — si
    /// una cambia, la otra tiene que cambiar con ella.
    /// </summary>
    public static class PesoCalculator
    {
        /// <summary>
        /// Bultos que representa una pesada.
        /// <para/>
        /// El peso de producto de la pesada es <c>bruto − taraExtra</c>: la tara extra es
        /// todo lo que no forma parte de la carga (tarima, sunchos, la lona). Lo que queda
        /// son bultos completos, cada uno con su contenido más su envase, así que dividirlo
        /// por <c>pesoTeorico + taraInd</c> da la cantidad.
        /// <para/>
        /// Se redondea al entero más cercano porque los bultos son objetos físicos: llegan
        /// 10 cajas o llegan 11, no 10,45. Nunca menos de 1 si hay carga.
        /// </summary>
        /// <param name="pesoTeorico">Contenido neto de UN bulto, sin envase.</param>
        /// <param name="taraInd">Envase de UN bulto (lata, caja, barril).</param>
        /// <returns><c>null</c> si falta el peso teórico o no hay carga: no hay con qué estimar.</returns>
        public static int? BultosEstimados(decimal bruto, decimal taraExtra, decimal pesoTeorico, decimal taraInd)
        {
            // Sin el contenido teórico de un bulto no hay forma de saber cuántos entraron.
            // La guarda va sobre pesoTeorico y no sobre la suma: con pesoTeorico en 0 y un
            // envase de 1 kg, `pesoPorBulto` daría 1 y la cuenta devolvería un bulto por
            // cada kilo de la carga.
            if (pesoTeorico <= 0) return null;

            var pesoDeLosBultos = bruto - taraExtra;
            if (pesoDeLosBultos <= 0) return null;

            var pesoPorBulto = pesoTeorico + taraInd;

            var estimados = (int)Math.Round(pesoDeLosBultos / pesoPorBulto, MidpointRounding.AwayFromZero);
            return Math.Max(estimados, 1);
        }

        /// <summary>
        /// Tara total de una pesada: lo que no es producto.
        /// <para/>
        /// <c>taraExtra</c> se cuenta <b>una vez</b> (es de la pesada) y <c>taraInd</c>
        /// <b>una vez por bulto</b> (es el envase de cada uno). Confundir esos dos alcances
        /// es lo que hacía que el neto quedara inflado.
        /// </summary>
        public static decimal TaraTotal(decimal taraExtra, decimal taraInd, int bultos)
            => taraExtra + taraInd * bultos;

        /// <summary>PesoBruto − TaraTotal.</summary>
        public static decimal PesoNeto(decimal bruto, decimal taraTotal)
            => bruto - taraTotal;

        /// <summary>
        /// Resuelve la pesada completa: cuántos bultos, cuánta tara y cuánto neto.
        /// <para/>
        /// Es el único punto que decide de dónde salen los bultos: el conteo real que hizo
        /// el operario si existe, y si no la estimación por peso. Contar cajas siempre le
        /// gana a inferirlas.
        /// <para/>
        /// Sin <paramref name="pesoTeorico"/> no hay forma de estimar, así que se asume
        /// <b>un</b> bulto. Es el comportamiento histórico y deja el neto más conservador
        /// que el real, nunca al revés.
        /// </summary>
        public static (int Bultos, decimal TaraTotal, decimal Neto) Resolver(
            decimal bruto,
            decimal taraExtra,
            decimal pesoTeorico,
            decimal taraInd,
            int? bultosCapturados = null)
        {
            var bultos = bultosCapturados is > 0
                ? bultosCapturados.Value
                : BultosEstimados(bruto, taraExtra, pesoTeorico, taraInd) ?? 1;

            var taraTotal = TaraTotal(taraExtra, taraInd, bultos);
            return (bultos, taraTotal, PesoNeto(bruto, taraTotal));
        }

        /// <summary>Suma de PesoNeto de todas las entradas de un movimiento-producto</summary>
        public static decimal PesoRecibido(IEnumerable<decimal> pesosNetos)
            => pesosNetos.Sum();

        /// <summary>PesoRecibido - PesoManifestado (negativo = faltante, positivo = excedente)</summary>
        public static decimal DiferenciaKg(decimal pesoRecibido, decimal pesoManifestado)
            => pesoRecibido - pesoManifestado;

        /// <summary>(DiferenciaKg / PesoManifestado) × 100</summary>
        public static decimal DiferenciaPct(decimal pesoRecibido, decimal pesoManifestado)
            => pesoManifestado > 0
                ? Math.Round((pesoRecibido - pesoManifestado) / pesoManifestado * 100, 2)
                : 0;

        /// <summary>DiferenciaKg × PrecioPorKg</summary>
        public static decimal DiferenciaUsd(decimal diferenciaKg, decimal precioPorKg)
            => diferenciaKg * precioPorKg;

        /// <summary>(PesoRecibido × BultosTeóricos) / PesoManifestado</summary>
        public static decimal BultosRecibidos(decimal pesoRecibido, int bultosTeóricos, decimal pesoManifestado)
            => pesoManifestado > 0
                ? Math.Round(pesoRecibido * bultosTeóricos / pesoManifestado, 2)
                : 0;

        /// <summary>BultosTeóricos - BultosRecibidos</summary>
        public static decimal BultosRestantes(int bultosTeóricos, decimal bultosRecibidos)
            => bultosTeóricos - bultosRecibidos;

        /// <summary>Porcentaje de peso pendiente por recibir respecto al manifestado</summary>
        public static decimal PctPesoRestante(decimal pesoRecibido, decimal pesoManifestado)
            => pesoManifestado > 0
                ? Math.Round((pesoManifestado - pesoRecibido) / pesoManifestado * 100, 2)
                : 0;
    }
}
