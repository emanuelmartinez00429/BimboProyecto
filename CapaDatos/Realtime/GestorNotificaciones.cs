using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;

namespace CapaDominio
{
    // Conecta los eventos de GestorRealtime con ServicioNotificaciones.
    // Llamar Iniciar() una vez después de que GestorRealtime esté listo.
    public static class GestorNotificaciones
    {
        private static bool _iniciado;

        public static void Iniciar()
        {
            if (_iniciado) return;
            _iniciado = true;

            GestorRealtime.OnMovimientosChanged += OnMovimiento;
            GestorRealtime.OnProductosChanged   += OnProducto;
            GestorRealtime.OnEmpleadosChanged   += OnEmpleado;
            GestorRealtime.OnUsuariosChanged    += OnUsuario;
        }

        public static void Detener()
        {
            if (!_iniciado) return;

            GestorRealtime.OnMovimientosChanged -= OnMovimiento;
            GestorRealtime.OnProductosChanged   -= OnProducto;
            GestorRealtime.OnEmpleadosChanged   -= OnEmpleado;
            GestorRealtime.OnUsuariosChanged    -= OnUsuario;

            _iniciado = false;
        }

        // ── Handlers ──────────────────────────────────────────────────

        private static void OnMovimiento(PostgresChangesResponse change)
        {
            if (change.Payload?.Data?.Type == EventType.Insert)
                Agregar("Nuevo pesaje registrado",
                        "Se registró un nuevo movimiento de entrada.",
                        TipoNotificacion.Info);
        }

        private static void OnProducto(PostgresChangesResponse change)
        {
            var tipo = change.Payload?.Data?.Type;
            if (tipo == EventType.Insert)
                Agregar("Producto agregado",
                        "Se añadió un nuevo producto al catálogo.",
                        TipoNotificacion.Exito);
            else if (tipo == EventType.Delete)
                Agregar("Producto eliminado",
                        "Se eliminó un producto del catálogo.",
                        TipoNotificacion.Advertencia);
        }

        private static void OnEmpleado(PostgresChangesResponse change)
        {
            if (change.Payload?.Data?.Type == EventType.Insert)
                Agregar("Nuevo empleado registrado",
                        "Se agregó un empleado al sistema.",
                        TipoNotificacion.Exito);
        }

        private static void OnUsuario(PostgresChangesResponse change)
        {
            if (change.Payload?.Data?.Type == EventType.Insert)
                Agregar("Nuevo usuario creado",
                        "Se creó una cuenta de usuario.",
                        TipoNotificacion.Info);
        }

        // ── Helper ────────────────────────────────────────────────────

        private static void Agregar(string titulo, string descripcion, TipoNotificacion tipo)
        {
            ServicioNotificaciones.Agregar(new Notificacion
            {
                Titulo      = titulo,
                Descripcion = descripcion,
                Tipo        = tipo,
            });
        }
    }
}
