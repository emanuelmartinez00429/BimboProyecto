namespace CapaAplicacion.Realtime;

/// <summary>
/// DTO inmutable que representa un cambio detectado por Supabase Realtime.
/// El RealtimeService lo construye a partir del PostgresChangesResponse crudo.
/// </summary>
public record CambioRealtime(
    string Operacion,   // "INSERT" | "UPDATE"
    long?  IdRegistro,  // PK del registro afectado
    int?   NuevoEstado  // id_estado del registro nuevo (null si no aplica)
);
