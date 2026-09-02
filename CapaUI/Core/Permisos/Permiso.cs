namespace CapaUI.Core.Permisos;

/// <summary>Contrato tipado de las acciones existentes en public.acciones.</summary>
public enum Permiso
{
    CrearProducto, ModificarProducto, EliminarProducto, ConsultarProducto,
    CrearEmpleado, ModificarEmpleado, EliminarEmpleado, ConsultarEmpleado,
    RegistrarEntrada, ModificarPesaje, CompletarPesaje, CancelarPesaje, ConsultarPesaje,
    CrearProveedor, ModificarProveedor, EliminarProveedor,
    CrearFabricante, ModificarFabricante,
    GenerarReporte, ExportarReporte, ConsultarReporte, ModificarConfiguracion,
    CrearUsuario, ModificarUsuario, EliminarUsuario,
    ConsultarProveedor, ConsultarFabricante, ConsultarUsuario,
    ConsultarRol, CrearRol, ModificarRol, CambiarEstadoRol, AsignarPermisosRol, AsignarRolUsuario,
    ConsultarNotificaciones, GestionarNotificaciones,
    ConsultarCategoria, CrearCategoria, ModificarCategoria, DesactivarCategoria, ActivarCategoria,
}

/// <summary>Una definición completa del contrato tipado de autorización.</summary>
public sealed record DefinicionPermiso(Permiso Permiso, string CodigoAccion, string NombreVisible);

public static class PermisoCatalogo
{
    // Única fuente local de verdad. codigo_accion es estable; NombreVisible es una etiqueta de UI.
    private static readonly IReadOnlyList<DefinicionPermiso> Definiciones =
    [
        new(Permiso.CrearProducto, "PRODUCTOS_CREAR", "Crear Producto"), new(Permiso.ModificarProducto, "PRODUCTOS_MODIFICAR", "Modificar Producto"), new(Permiso.EliminarProducto, "PRODUCTOS_ELIMINAR", "Eliminar Producto"), new(Permiso.ConsultarProducto, "PRODUCTOS_CONSULTAR", "Consultar Producto"),
        new(Permiso.CrearEmpleado, "EMPLEADOS_CREAR", "Crear Empleado"), new(Permiso.ModificarEmpleado, "EMPLEADOS_MODIFICAR", "Modificar Empleado"), new(Permiso.EliminarEmpleado, "EMPLEADOS_ELIMINAR", "Eliminar Empleado"), new(Permiso.ConsultarEmpleado, "EMPLEADOS_CONSULTAR", "Consultar Empleado"),
        new(Permiso.RegistrarEntrada, "PESAJES_REGISTRAR_ENTRADA", "Registrar Entrada"), new(Permiso.ModificarPesaje, "PESAJES_MODIFICAR", "Modificar Pesaje"), new(Permiso.CompletarPesaje, "PESAJES_COMPLETAR", "Completar Pesaje"), new(Permiso.CancelarPesaje, "PESAJES_CANCELAR", "Cancelar Pesaje"), new(Permiso.ConsultarPesaje, "PESAJES_CONSULTAR", "Consultar Pesaje"),
        new(Permiso.CrearProveedor, "PROVEEDORES_CREAR", "Crear Proveedor"), new(Permiso.ModificarProveedor, "PROVEEDORES_MODIFICAR", "Modificar Proveedor"), new(Permiso.EliminarProveedor, "PROVEEDORES_ELIMINAR", "Eliminar Proveedor"), new(Permiso.ConsultarProveedor, "PROVEEDORES_CONSULTAR", "Consultar Proveedor"),
        new(Permiso.CrearFabricante, "FABRICANTES_CREAR", "Crear Fabricante"), new(Permiso.ModificarFabricante, "FABRICANTES_MODIFICAR", "Modificar Fabricante"), new(Permiso.ConsultarFabricante, "FABRICANTES_CONSULTAR", "Consultar Fabricante"),
        new(Permiso.GenerarReporte, "REPORTES_GENERAR", "Generar Reporte"), new(Permiso.ExportarReporte, "REPORTES_EXPORTAR", "Exportar Reporte"), new(Permiso.ConsultarReporte, "REPORTES_CONSULTAR", "Consultar Reporte"), new(Permiso.ModificarConfiguracion, "CONFIGURACION_MODIFICAR", "Modificar Configuración"),
        new(Permiso.CrearUsuario, "USUARIOS_CREAR", "Crear Usuario"), new(Permiso.ModificarUsuario, "USUARIOS_MODIFICAR", "Modificar Usuario"), new(Permiso.EliminarUsuario, "USUARIOS_ELIMINAR", "Eliminar Usuario"), new(Permiso.ConsultarUsuario, "USUARIOS_CONSULTAR", "Consultar Usuario"),
        new(Permiso.ConsultarRol, "ROLES_CONSULTAR", "Consultar Rol"), new(Permiso.CrearRol, "ROLES_CREAR", "Crear Rol"), new(Permiso.ModificarRol, "ROLES_MODIFICAR", "Modificar Rol"), new(Permiso.CambiarEstadoRol, "ROLES_CAMBIAR_ESTADO", "Cambiar Estado de Rol"), new(Permiso.AsignarPermisosRol, "ROLES_ASIGNAR_PERMISOS", "Asignar Permisos a Rol"), new(Permiso.AsignarRolUsuario, "USUARIOS_ASIGNAR_ROL", "Asignar Rol a Usuario"),
        new(Permiso.ConsultarNotificaciones, "NOTIFICACIONES_CONSULTAR", "Consultar Notificaciones"), new(Permiso.GestionarNotificaciones, "NOTIFICACIONES_GESTIONAR", "Gestionar Notificaciones"),
        new(Permiso.ConsultarCategoria, "CATEGORIAS_CONSULTAR", "Consultar Categoría"), new(Permiso.CrearCategoria, "CATEGORIAS_CREAR", "Crear Categoría"), new(Permiso.ModificarCategoria, "CATEGORIAS_MODIFICAR", "Modificar Categoría"), new(Permiso.DesactivarCategoria, "CATEGORIAS_DESACTIVAR", "Desactivar Categoría"), new(Permiso.ActivarCategoria, "CATEGORIAS_ACTIVAR", "Activar Categoría"),
    ];

    private static readonly IReadOnlyDictionary<Permiso, DefinicionPermiso> PorPermiso =
        Definiciones.GroupBy(d => d.Permiso).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
    private static readonly IReadOnlyDictionary<string, Permiso> PorNombre =
        Definiciones.GroupBy(d => d.NombreVisible, StringComparer.Ordinal).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Permiso, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, Permiso> PorCodigo =
        Definiciones.GroupBy(d => d.CodigoAccion, StringComparer.Ordinal).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single().Permiso, StringComparer.Ordinal);

    public static IReadOnlyCollection<DefinicionPermiso> TodasLasDefiniciones => Definiciones.ToArray();
    public static IReadOnlyCollection<string> TodosLosNombres => Definiciones.Select(d => d.NombreVisible).ToArray();

    public static bool IntentarObtenerDefinicion(Permiso permiso, out DefinicionPermiso definicion) => PorPermiso.TryGetValue(permiso, out definicion!);

    public static bool IntentarObtenerCodigo(Permiso permiso, out string codigoAccion)
    {
        if (IntentarObtenerDefinicion(permiso, out var definicion))
        {
            codigoAccion = definicion.CodigoAccion;
            return true;
        }
        codigoAccion = string.Empty;
        return false;
    }

    public static string CodigoBaseDatos(this Permiso permiso) => IntentarObtenerCodigo(permiso, out var codigo) ? codigo : throw new InvalidOperationException($"El permiso tipado '{permiso}' no tiene una definición RBAC.");
    public static string NombreBaseDatos(this Permiso permiso) => permiso.CodigoBaseDatos(); // Alias de compatibilidad.
    public static bool IntentarResolver(string valor, out Permiso permiso) => PorNombre.TryGetValue(valor, out permiso) || PorCodigo.TryGetValue(valor, out permiso);

    /// <summary>Errores de cobertura que las pruebas deben exigir vacíos.</summary>
    public static IReadOnlyList<string> ValidarCobertura()
    {
        var errores = new List<string>();
        var esperados = Enum.GetValues<Permiso>().ToHashSet();
        var definidos = Definiciones.Select(d => d.Permiso).ToHashSet();
        errores.AddRange(esperados.Except(definidos).Select(p => $"Falta definición para {p}."));
        errores.AddRange(definidos.Except(esperados).Select(p => $"Sobra definición para {p}."));
        errores.AddRange(Definiciones.GroupBy(d => d.Permiso).Where(g => g.Count() != 1).Select(g => $"Permiso duplicado: {g.Key}."));
        errores.AddRange(Definiciones.GroupBy(d => d.CodigoAccion, StringComparer.Ordinal).Where(g => g.Count() != 1).Select(g => $"Código duplicado: {g.Key}."));
        errores.AddRange(Definiciones.GroupBy(d => d.NombreVisible, StringComparer.Ordinal).Where(g => g.Count() != 1).Select(g => $"Etiqueta duplicada: {g.Key}."));
        errores.AddRange(Definiciones.Where(d => string.IsNullOrWhiteSpace(d.CodigoAccion) || string.IsNullOrWhiteSpace(d.NombreVisible)).Select(d => $"Definición vacía: {d.Permiso}."));
        return errores;
    }
}
