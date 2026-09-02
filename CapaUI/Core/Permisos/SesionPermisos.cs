using System.Linq;
using CapaAplicacion.Usuarios.Interfaces;

namespace CapaUI.Core.Permisos
{
    /// <summary>Fachada estática de permisos para bindings XAML.</summary>
    public static class SesionPermisos
    {
        private static IUsuarioSesionService? _sesionService;
        public static void Configurar(IUsuarioSesionService servicio) => _sesionService = servicio;
        public static int IdRolActual => _sesionService?.SesionActual?.IdRol ?? 0;
        public static bool TieneAlguno(params Permiso[] requeridos) => requeridos.Any(Tiene);
        public static bool TieneTodos(params Permiso[] requeridos) => requeridos.All(Tiene);

        /// <summary>Un permiso sin definición se registra y se deniega; nunca interrumpe el inicio de sesión.</summary>
        public static bool Tiene(Permiso permiso)
        {
            if (!PermisoCatalogo.IntentarObtenerCodigo(permiso, out var codigoAccion))
            {
                Serilog.Log.Error("Contrato RBAC inválido: el permiso tipado {Permiso} no tiene definición; se deniega la acción.", permiso);
                return false;
            }
            return _sesionService?.SesionActual?.TieneAccion(codigoAccion) ?? false;
        }

        /// <summary>Diagnóstico local: no interpreta permisos no asignados como un contrato ausente.</summary>
        public static void ValidarContraBD()
        {
            var errores = PermisoCatalogo.ValidarCobertura();
            if (errores.Count == 0)
                Serilog.Log.Debug("Contrato RBAC local válido: {Total} permisos definidos.", PermisoCatalogo.TodasLasDefiniciones.Count);
            else
                Serilog.Log.Error("Contrato RBAC local inválido: {Errores}", string.Join(" | ", errores));
        }

        public static void Limpiar() { /* la fuente de verdad se limpia en CerrarSesion() */ }
    }
}
