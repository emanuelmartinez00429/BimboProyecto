namespace CapaDominio
{
    /// <summary>
    /// Almacena el usuario autenticado en memoria durante la sesión activa.
    /// Falla explícitamente si se accede a IdUsuario sin haber iniciado sesión,
    /// para evitar auditoría falsa (antes tenía default = 1).
    /// Llamar Limpiar() en el logout para que el próximo login empiece en blanco.
    /// </summary>
    public static class SesionActual
    {
        private static int? _idUsuario;

        public static int IdUsuario
        {
            get => _idUsuario
                ?? throw new InvalidOperationException(
                    "SesionActual.IdUsuario: no hay sesión activa. " +
                    "Asignar IdUsuario en el login antes de usarlo.");
            set => _idUsuario = value;
        }

        public static string NombreUsuario { get; set; } = "";

        /// <summary>Llama al cerrar sesión para limpiar la identidad del usuario anterior.</summary>
        public static void Limpiar()
        {
            _idUsuario    = null;
            NombreUsuario = "";
        }
    }
}
