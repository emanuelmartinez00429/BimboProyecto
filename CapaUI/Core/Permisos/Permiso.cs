namespace CapaUI.Core.Permisos;

/// <summary>
/// Contrato tipado de las acciones existentes en public.acciones.
/// El identificador C# no se compara directamente con la BD: use
/// <see cref="PermisoCatalogo.NombreBaseDatos(Permiso)"/>.
/// </summary>
public enum Permiso
{
    CrearProducto,
    ModificarProducto,
    EliminarProducto,
    ConsultarProducto,
    CrearEmpleado,
    ModificarEmpleado,
    EliminarEmpleado,
    ConsultarEmpleado,
    RegistrarEntrada,
    ModificarPesaje,
    CompletarPesaje,
    CancelarPesaje,
    ConsultarPesaje,
    CrearProveedor,
    ModificarProveedor,
    EliminarProveedor,
    CrearFabricante,
    ModificarFabricante,
    GenerarReporte,
    ExportarReporte,
    ConsultarReporte,
    ModificarConfiguracion,
    CrearUsuario,
    ModificarUsuario,
    EliminarUsuario,
    ConsultarProveedor,
    ConsultarFabricante,
    ConsultarUsuario,
    ConsultarRol,
    CrearRol,
    ModificarRol,
    EliminarRol,
    AsignarPermisosRol,
    AsignarRolUsuario,
}

public static class PermisoCatalogo
{
    private static readonly IReadOnlyDictionary<Permiso, string> Nombres =
        new Dictionary<Permiso, string>
        {
            [Permiso.CrearProducto] = "Crear Producto",
            [Permiso.ModificarProducto] = "Modificar Producto",
            [Permiso.EliminarProducto] = "Eliminar Producto",
            [Permiso.ConsultarProducto] = "Consultar Producto",
            [Permiso.CrearEmpleado] = "Crear Empleado",
            [Permiso.ModificarEmpleado] = "Modificar Empleado",
            [Permiso.EliminarEmpleado] = "Eliminar Empleado",
            [Permiso.ConsultarEmpleado] = "Consultar Empleado",
            [Permiso.RegistrarEntrada] = "Registrar Entrada",
            [Permiso.ModificarPesaje] = "Modificar Pesaje",
            [Permiso.CompletarPesaje] = "Completar Pesaje",
            [Permiso.CancelarPesaje] = "Cancelar Pesaje",
            [Permiso.ConsultarPesaje] = "Consultar Pesaje",
            [Permiso.CrearProveedor] = "Crear Proveedor",
            [Permiso.ModificarProveedor] = "Modificar Proveedor",
            [Permiso.EliminarProveedor] = "Eliminar Proveedor",
            [Permiso.CrearFabricante] = "Crear Fabricante",
            [Permiso.ModificarFabricante] = "Modificar Fabricante",
            [Permiso.GenerarReporte] = "Generar Reporte",
            [Permiso.ExportarReporte] = "Exportar Reporte",
            [Permiso.ConsultarReporte] = "Consultar Reporte",
            [Permiso.ModificarConfiguracion] = "Modificar Configuración",
            [Permiso.CrearUsuario] = "Crear Usuario",
            [Permiso.ModificarUsuario] = "Modificar Usuario",
            [Permiso.EliminarUsuario] = "Eliminar Usuario",
            [Permiso.ConsultarProveedor] = "Consultar Proveedor",
            [Permiso.ConsultarFabricante] = "Consultar Fabricante",
            [Permiso.ConsultarUsuario] = "Consultar Usuario",
            [Permiso.ConsultarRol] = "Consultar Rol",
            [Permiso.CrearRol] = "Crear Rol",
            [Permiso.ModificarRol] = "Modificar Rol",
            [Permiso.EliminarRol] = "Eliminar Rol",
            [Permiso.AsignarPermisosRol] = "Asignar Permisos a Rol",
            [Permiso.AsignarRolUsuario] = "Asignar Rol a Usuario",
        };

    private static readonly IReadOnlyDictionary<string, Permiso> PorNombre =
        Nombres.ToDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);

    public static string NombreBaseDatos(this Permiso permiso) => Nombres[permiso];

    public static bool IntentarResolver(string nombreBaseDatos, out Permiso permiso) =>
        PorNombre.TryGetValue(nombreBaseDatos, out permiso);

    public static IReadOnlyCollection<string> TodosLosNombres => Nombres.Values.ToArray();
}
