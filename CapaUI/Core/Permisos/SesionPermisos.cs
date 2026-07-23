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

        /// <summary>Limpieza en logout. La sesión real la limpia IUsuarioSesionService.</summary>
        public static void Limpiar() { /* noop — la fuente de verdad se limpia en CerrarSesion() */ }
    }
}
