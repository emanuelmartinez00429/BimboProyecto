namespace CapaUI.Core.Permisos;

/// <summary>
/// Contrato tipado de las acciones existentes en public.acciones.
/// El identificador C# no se compara directamente con la BD: use
/// <see cref="PermisoCatalogo.CodigoBaseDatos(Permiso)"/>.
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
    CambiarEstadoRol,
    AsignarPermisosRol,
    AsignarRolUsuario,
    ConsultarNotificaciones,
    GestionarNotificaciones,
    ConsultarCategoria,
    CrearCategoria,
    ModificarCategoria,
    DesactivarCategoria,
    ActivarCategoria,
}

public static class PermisoCatalogo
{
    private static readonly IReadOnlyDictionary<Permiso, string> Codigos =
        new Dictionary<Permiso, string>
        {
            [Permiso.CrearProducto]="PRODUCTOS_CREAR", [Permiso.ModificarProducto]="PRODUCTOS_MODIFICAR",
            [Permiso.EliminarProducto]="PRODUCTOS_ELIMINAR", [Permiso.ConsultarProducto]="PRODUCTOS_CONSULTAR",
            [Permiso.CrearEmpleado]="EMPLEADOS_CREAR", [Permiso.ModificarEmpleado]="EMPLEADOS_MODIFICAR",
            [Permiso.EliminarEmpleado]="EMPLEADOS_ELIMINAR", [Permiso.ConsultarEmpleado]="EMPLEADOS_CONSULTAR",
            [Permiso.RegistrarEntrada]="PESAJES_REGISTRAR_ENTRADA", [Permiso.ModificarPesaje]="PESAJES_MODIFICAR",
            [Permiso.CompletarPesaje]="PESAJES_COMPLETAR", [Permiso.CancelarPesaje]="PESAJES_CANCELAR",
            [Permiso.ConsultarPesaje]="PESAJES_CONSULTAR", [Permiso.CrearProveedor]="PROVEEDORES_CREAR",
            [Permiso.ModificarProveedor]="PROVEEDORES_MODIFICAR", [Permiso.EliminarProveedor]="PROVEEDORES_ELIMINAR",
            [Permiso.CrearFabricante]="FABRICANTES_CREAR", [Permiso.ModificarFabricante]="FABRICANTES_MODIFICAR",
            [Permiso.GenerarReporte]="REPORTES_GENERAR", [Permiso.ExportarReporte]="REPORTES_EXPORTAR",
            [Permiso.ConsultarReporte]="REPORTES_CONSULTAR", [Permiso.ModificarConfiguracion]="CONFIGURACION_MODIFICAR",
            [Permiso.CrearUsuario]="USUARIOS_CREAR", [Permiso.ModificarUsuario]="USUARIOS_MODIFICAR",
            [Permiso.EliminarUsuario]="USUARIOS_ELIMINAR", [Permiso.ConsultarProveedor]="PROVEEDORES_CONSULTAR",
            [Permiso.ConsultarFabricante]="FABRICANTES_CONSULTAR", [Permiso.ConsultarUsuario]="USUARIOS_CONSULTAR",
            [Permiso.ConsultarRol]="ROLES_CONSULTAR", [Permiso.CrearRol]="ROLES_CREAR",
            [Permiso.ModificarRol]="ROLES_MODIFICAR", [Permiso.CambiarEstadoRol]="ROLES_CAMBIAR_ESTADO",
            [Permiso.AsignarPermisosRol]="ROLES_ASIGNAR_PERMISOS", [Permiso.AsignarRolUsuario]="USUARIOS_ASIGNAR_ROL",
            [Permiso.ConsultarNotificaciones]="NOTIFICACIONES_CONSULTAR",
            [Permiso.GestionarNotificaciones]="NOTIFICACIONES_GESTIONAR",
            [Permiso.ConsultarCategoria]="CATEGORIAS_CONSULTAR",
            [Permiso.CrearCategoria]="CATEGORIAS_CREAR",
            [Permiso.ModificarCategoria]="CATEGORIAS_MODIFICAR",
            [Permiso.DesactivarCategoria]="CATEGORIAS_DESACTIVAR",
            [Permiso.ActivarCategoria]="CATEGORIAS_ACTIVAR",
        };

    private static readonly IReadOnlyDictionary<Permiso, string> Nombres = new Dictionary<Permiso,string>
    {
        [Permiso.CrearProducto]="Crear Producto",[Permiso.ModificarProducto]="Modificar Producto",[Permiso.EliminarProducto]="Eliminar Producto",[Permiso.ConsultarProducto]="Consultar Producto",
        [Permiso.CrearEmpleado]="Crear Empleado",[Permiso.ModificarEmpleado]="Modificar Empleado",[Permiso.EliminarEmpleado]="Eliminar Empleado",[Permiso.ConsultarEmpleado]="Consultar Empleado",
        [Permiso.RegistrarEntrada]="Registrar Entrada",[Permiso.ModificarPesaje]="Modificar Pesaje",[Permiso.CompletarPesaje]="Completar Pesaje",[Permiso.CancelarPesaje]="Cancelar Pesaje",[Permiso.ConsultarPesaje]="Consultar Pesaje",
        [Permiso.CrearProveedor]="Crear Proveedor",[Permiso.ModificarProveedor]="Modificar Proveedor",[Permiso.EliminarProveedor]="Eliminar Proveedor",[Permiso.ConsultarProveedor]="Consultar Proveedor",
        [Permiso.CrearFabricante]="Crear Fabricante",[Permiso.ModificarFabricante]="Modificar Fabricante",[Permiso.ConsultarFabricante]="Consultar Fabricante",
        [Permiso.GenerarReporte]="Generar Reporte",[Permiso.ExportarReporte]="Exportar Reporte",[Permiso.ConsultarReporte]="Consultar Reporte",[Permiso.ModificarConfiguracion]="Modificar Configuración",
        [Permiso.CrearUsuario]="Crear Usuario",[Permiso.ModificarUsuario]="Modificar Usuario",[Permiso.EliminarUsuario]="Eliminar Usuario",[Permiso.ConsultarUsuario]="Consultar Usuario",
        [Permiso.ConsultarRol]="Consultar Rol",[Permiso.CrearRol]="Crear Rol",[Permiso.ModificarRol]="Modificar Rol",[Permiso.CambiarEstadoRol]="Cambiar Estado de Rol",
        [Permiso.AsignarPermisosRol]="Asignar Permisos a Rol",[Permiso.AsignarRolUsuario]="Asignar Rol a Usuario",
        [Permiso.ConsultarNotificaciones]="Consultar Notificaciones",[Permiso.GestionarNotificaciones]="Gestionar Notificaciones",
        [Permiso.ConsultarCategoria]="Consultar Categoría",
        [Permiso.CrearCategoria]="Crear Categoría",
        [Permiso.ModificarCategoria]="Modificar Categoría",
        [Permiso.DesactivarCategoria]="Desactivar Categoría",
        [Permiso.ActivarCategoria]="Activar Categoría",
    };

    private static readonly IReadOnlyDictionary<string, Permiso> PorNombre = Nombres
        .Concat(Codigos).ToDictionary(x=>x.Value,x=>x.Key,StringComparer.Ordinal);

    public static string CodigoBaseDatos(this Permiso permiso) => Codigos[permiso];
    public static string NombreBaseDatos(this Permiso permiso) => Codigos[permiso];

    public static bool IntentarResolver(string nombreBaseDatos, out Permiso permiso) =>
        PorNombre.TryGetValue(nombreBaseDatos, out permiso);

    public static IReadOnlyCollection<string> TodosLosNombres => Nombres.Values.ToArray();
}
