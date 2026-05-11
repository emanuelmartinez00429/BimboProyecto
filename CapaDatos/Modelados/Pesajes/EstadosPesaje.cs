namespace CapaDatos.Modelados.Pesajes
{
    /// <summary>
    /// IDs reales de estado_general en Supabase para el módulo de pesaje.
    /// Verificados tras la migración de Fase 0.
    /// </summary>
    public static class EstadosPesaje
    {
        public const int Abierto   = 7;
        public const int Cerrado   = 8;
        public const int Anulado   = 9;
        public const int Activo    = 1;
        public const int Inactivo  = 2;
        public const int Completado = 4;
    }
}
