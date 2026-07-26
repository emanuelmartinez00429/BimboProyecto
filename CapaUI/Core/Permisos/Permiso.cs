namespace CapaUI.Core.Permisos
{
    /// <summary>
    /// CONTRATO enum ↔ BD: cada valor de este enum DEBE existir literalmente
    /// (mismo texto, mismo casing) en la columna <c>acciones.nombre_accion</c>
    /// de Supabase. La comparación es <c>permiso.ToString()</c> contra ese string
    /// (ver <see cref="SesionPermisos.Tiene"/>): si los nombres divergen, el
    /// permiso desaparece EN SILENCIO (el botón no aparece, sin error).
    /// - NO renombrar un valor sin migrar la fila correspondiente en `acciones`.
    /// - NO agregar un valor sin crear su fila en `acciones` y asignarla a roles.
    /// <see cref="SesionPermisos.ValidarContraBD"/> se ejecuta tras cada login y
    /// loguea (Serilog) los valores del enum que la sesión no reconoce.
    /// </summary>
    public enum Permiso
    {
        Empleados_Ver,
        Empleados_Crear,
        Empleados_Modificar,

        Usuarios_Ver,
        Usuarios_Crear,
        Usuarios_Modificar,

        Productos_Ver,
        Productos_Crear,
        Productos_Modificar,

        Categorias_Ver,
        Categorias_Crear,
        Categorias_Modificar,

        Fabricantes_Ver,
        Fabricantes_Crear,
        Fabricantes_Modificar,

        Proveedores_Ver,
        Proveedores_Crear,
        Proveedores_Modificar,

        Pesajes_Ver,
        Pesajes_Crear,
        Pesajes_Modificar,

        Reportes_Ver,
    }
}
