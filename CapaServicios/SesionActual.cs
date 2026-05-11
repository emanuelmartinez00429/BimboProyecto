namespace CapaServicios
{
    /// <summary>
    /// Almacena el usuario autenticado en memoria durante la sesión.
    /// TODO: Asignar IdUsuario al completar el login con Supabase Auth.
    /// </summary>
    public static class SesionActual
    {
        public static int    IdUsuario     { get; set; } = 1;
        public static string NombreUsuario { get; set; } = "Usuario";
    }
}
