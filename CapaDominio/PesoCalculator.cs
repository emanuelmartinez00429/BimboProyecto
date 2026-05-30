namespace CapaDominio
{
    /// <summary>
    /// Centraliza todas las fórmulas de cálculo de pesos del negocio.
    /// El Form la usa para preview visual. La BD las recalcula al guardar (trigger).
    /// </summary>
    public static class PesoCalculator
    {
        /// <summary>TaraExtra (báscula) + TaraInd (catálogo del producto)</summary>
        public static decimal TaraTotal(decimal taraExtra, decimal taraInd)
            => taraExtra + taraInd;

        /// <summary>PesoBruto - TaraTotal</summary>
        public static decimal PesoNeto(decimal bruto, decimal taraTotal)
            => bruto - taraTotal;

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
