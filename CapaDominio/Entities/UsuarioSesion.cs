namespace CapaDominio.Entities
{
    /// <summary>
    /// Contexto de sesion del usuario autenticado.
    /// Reemplaza SesionActual y servicioSesionActual como fuente unica de verdad.
    /// Los permisos se cargan una vez al login desde acciones_roles/acciones/modulo.
    /// </summary>
    public sealed class UsuarioSesion
    {
        private readonly HashSet<string> _acciones;

        public int    IdUsuario       { get; init; }
        public string Email           { get; init; } = string.Empty;
        public int    IdRol           { get; init; }
        public string NombreRol       { get; init; } = string.Empty;
        public string NombreEmpleado  { get; init; } = string.Empty;
        public string ApellidoEmpleado { get; init; } = string.Empty;
        public string NombreCompleto  { get; init; } = string.Empty;
        public string Iniciales       { get; init; } = string.Empty;
        public IReadOnlyList<ModuloPermisos> PermisosPorModulo { get; init; } = Array.Empty<ModuloPermisos>();

        /// <summary>
        /// Construye la sesion con los permisos agrupados por modulo
        /// y un HashSet interno para consultas O(1).
        /// </summary>
        public UsuarioSesion(IReadOnlyList<ModuloPermisos> permisosPorModulo)
        {
            PermisosPorModulo = permisosPorModulo;

            var acciones = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modulo in permisosPorModulo)
                foreach (var accion in modulo.Acciones)
                    acciones.Add(accion);

            _acciones = acciones;
        }

        /// <summary>
        /// Verifica si el usuario tiene una accion especifica. O(1).
        /// </summary>
        public bool TieneAccion(string nombreAccion)
            => _acciones.Contains(nombreAccion);
    }
}
