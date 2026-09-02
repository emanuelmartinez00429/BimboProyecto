namespace CapaUI.Navigation;

/// <summary>
/// Constantes de las rutas de navegación del menú principal.
/// Los valores deben coincidir con los atributos Tag de los botones en MainWindow.xaml.
/// </summary>
public static class Routes
{
    // ── Usuarios ─────────────────────────────────────────────────────────
    public const string Usuarios  = "usuarios-sub";
    public const string Empleados = "empleados";
    public const string Roles     = "roles";
    public const string Bitacora  = "bitacora";
    public const string Notificaciones = "notificaciones";

    // ── Productos ────────────────────────────────────────────────────────
    public const string Productos            = "productos-sub";
    public const string Proveedores          = "proveedores";
    public const string Fabricantes          = "fabricantes";
    public const string Categorias           = "categorias";
    public const string Presentaciones       = "presentaciones";
    public const string ContactosProveedores = "contactos-proveedores";
    public const string ContactosFabricantes = "contactos-fabricantes";

    // ── Pesajes ──────────────────────────────────────────────────────────
    public const string Pesajes = "pesajes-sub";

    // ── Reportería ───────────────────────────────────────────────────────
    public const string Dashboard     = "dashboard";
    public const string CrearReportes = "crear-reportes";

    // ── Especiales ───────────────────────────────────────────────────────
    public const string Bienvenida = "bienvenida";
    public const string MiUsuario  = "mi-usuario";
}
