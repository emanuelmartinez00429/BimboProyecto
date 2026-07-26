using System.Linq;
using CapaAplicacion.Usuarios.Interfaces;

namespace CapaUI.Core.Permisos
{
    /// <summary>
    /// Fachada estática de permisos para bindings XAML.
    /// Delega a IUsuarioSesionService (fuente de verdad desde BD).
    /// El enum Permiso se mapea a strings que coinciden con acciones.nombre_accion.
    /// </summary>
    public static class SesionPermisos
    {
        private static IUsuarioSesionService? _sesionService;

        /// <summary>Se una vez al arranque de la app (App.xaml.cs).</summary>
        public static void Configurar(IUsuarioSesionService servicio)
            => _sesionService = servicio;

        public static int IdRolActual
            => _sesionService?.SesionActual?.IdRol ?? 0;

        // ── Consultas ────────────────────────────────────────────────────

        /// <summary>Lógica OR: true si tiene AL MENOS UNO de los permisos requeridos.</summary>
        public static bool TieneAlguno(params Permiso[] requeridos)
            => requeridos.Any(Tiene);

        /// <summary>Lógica AND: true si tiene TODOS los permisos requeridos.</summary>
        public static bool TieneTodos(params Permiso[] requeridos)
            => requeridos.All(Tiene);

        /// <summary>Verifica un permiso específico contra la sesión activa.</summary>
        public static bool Tiene(Permiso permiso)
        {
            var sesion = _sesionService?.SesionActual;
            return sesion?.TieneAccion(permiso.ToString()) ?? false;
        }

        /// <summary>
        /// Diagnóstico del contrato enum↔BD (ver doc de <see cref="Permiso"/>).
        /// Llamar tras un login exitoso: loguea con Serilog los valores del enum
        /// que la sesión actual no reconoce. Un permiso ausente puede ser legítimo
        /// (el rol no lo tiene asignado) o un typo/renombre en `acciones.nombre_accion`
        /// — este log es la única señal visible de esa segunda causa.
        /// Nunca bloquea la app.
        /// </summary>
        public static void ValidarContraBD()
        {
            var sesion = _sesionService?.SesionActual;
            if (sesion is null) return;

            var faltantes = System.Enum.GetValues<Permiso>()
                .Where(p => !sesion.TieneAccion(p.ToString()))
                .Select(p => p.ToString())
                .ToList();

            if (faltantes.Count == 0)
                Serilog.Log.Debug("Permisos: el rol {Rol} reconoce los {Total} permisos del enum",
                    IdRolActual, System.Enum.GetValues<Permiso>().Length);
            else
                Serilog.Log.Warning(
                    "Permisos: el rol {Rol} no reconoce {Cantidad} permisos del enum: {Faltantes}. " +
                    "Si alguno debería estar disponible, revisar typo/renombre en acciones.nombre_accion",
                    IdRolActual, faltantes.Count, string.Join(", ", faltantes));
        }

        /// <summary>Limpieza en logout. La sesión real la limpia IUsuarioSesionService.</summary>
        public static void Limpiar() { /* noop — la fuente de verdad se limpia en CerrarSesion() */ }
    }
}
