namespace CapaDominio
{
    /// <summary>
    /// Mantenido por compatibilidad con código legado.
    /// Para código nuevo usar SesionActual.
    /// </summary>
    public static class servicioSesionActual
    {
        public static int    IdUsuario     { get; private set; }
        public static string NombreUsuario { get; private set; } = "";

        public static void Iniciar(int idUsuario, string email)
        {
            IdUsuario     = idUsuario;
            NombreUsuario = email;
        }

        public static void Cerrar()
        {
            IdUsuario     = 0;
            NombreUsuario = "";
        }
    }
}
