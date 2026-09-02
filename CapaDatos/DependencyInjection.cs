using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Bitacora.Interfaces;
using CapaAplicacion.Categorias.Interfaces;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Notificaciones.Interfaces;
using CapaAplicacion.Perfil;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Realtime;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Auth;
using CapaDatos.Conexion;
using CapaDatos.Perfil;
using CapaDatos.Realtime;
using CapaAplicacion.Empleados.Interfaces;
using CapaAplicacion.Empresa.Interfaces;
using CapaDatos.Repositories.Categorias;
using CapaDatos.Repositories.Contactos;
using CapaDatos.Repositories.GestionEmpleados;
using CapaDatos.Repositories.Empresa;
using CapaDatos.Repositories.Fabricantes;
using CapaDatos.Repositories.Pesaje;
using CapaDatos.Repositories.Notificaciones;
using CapaDatos.Repositories.Presentaciones;
using CapaDatos.Repositories.Productos;
using CapaDatos.Repositories.Proveedores;
using CapaDatos.Repositories.Search;
using CapaDatos.Repositories.Reportes;
using CapaDatos.Repositories.Usuarios;
using CapaDatos.Reportes;
using CapaDominio.Entities;
using CapaDominio.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CapaDatos;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        // Autenticación
        services.AddTransient<IAuthService, AuthService>();

        // Perfil del usuario — singleton porque mantiene estado entre login y logout
        services.AddSingleton<IPerfilUsuarioService, PerfilUsuarioService>();

        // Realtime — singleton: una sola conexión WebSocket, canales on-demand
        services.AddSingleton<IRealtimeService, RealtimeService>();

        // Monitor de conexión — singleton: una sola vigilancia de red para toda la app
        services.AddSingleton<IConexionMonitor, ConexionMonitor>();

        // Buscador universal (entidad de dominio)
        // Transient: repos sin estado mutable — Scoped era engañoso en WPF (sin scopes = singleton de facto)
        services.AddTransient<IRepository<Producto>, ProductoSearchRepository>();
        services.AddTransient<IRepository<Empleado>, EmpleadoRepository>();
        services.AddTransient<IRepository<Cliente>,  ClienteRepository>();

        // Formulario de productos (DTO con FKs)
        services.AddTransient<IProductoRepository, ProductoCrudRepository>();

        // Lectura uniforme de catálogos chicos (alimenta el selector genérico de lupa)
        services.AddTransient<CapaAplicacion.Common.Catalogos.ICatalogoRepository,
                              Repositories.Catalogos.CatalogoRepository>();

        // Formularios de catálogo
        services.AddTransient<IProveedorRepository, ProveedorCrudRepository>();
        services.AddTransient<IFabricanteRepository, FabricanteCrudRepository>();
        services.AddTransient<ICategoriaRepository, CategoriaCrudRepository>();
        services.AddTransient<IPresentacionRepository, PresentacionCrudRepository>();

        // Contactos de fabricantes y proveedores
        services.AddTransient<IContactoFabricanteRepository, ContactoFabricanteCrudRepository>();
        services.AddTransient<IContactoProveedorRepository, ContactoProveedorCrudRepository>();

        // Pesaje — selector de productos por proveedor (extensión aditiva del buscador)
        services.AddTransient<IPickerProductoRepository, PickerProductoRepository>();

        // Pesaje — persistencia real (movimientos / movimiento_productos / entradas_producto)
        services.AddTransient<IPesajeRepository, PesajeRepository>();

        // Notificaciones internas — Supabase es la única fuente de verdad.
        services.AddTransient<INotificacionRepository, NotificacionRepository>();

        // Usuarios — CRUD, consulta de roles, y servicio de sesion
        services.AddTransient<IUsuarioRepository, UsuarioRepository>();
        services.AddTransient<IRolRepository, RolRepository>();
        services.AddSingleton<IUsuarioSesionService, UsuarioSesionService>();
        services.AddTransient<IRolPermisoRepository, RolPermisoRepository>();

        // Configuración de la empresa y logo corporativo
        services.AddTransient<IEmpresaRepository, EmpresaRepository>();

        // Empleados — solo lectura por ahora (módulo en revisión)
        services.AddTransient<IEmpleadoRepository, EmpleadoCrudRepository>();

        // Bitácora — solo lectura por diseño (la escribe el sistema, no la UI)
        services.AddTransient<IBitacoraRepository, Repositories.Bitacora.BitacoraCrudRepository>();

        // Reportes PDF/Excel y registro auditado mediante RPC
        services.AddTransient<IReportStrategy, PdfReportStrategy>();
        services.AddTransient<IReportStrategy, ExcelReportStrategy>();
        services.AddTransient<IReporteRepository, ReporteRepository>();
        services.AddTransient<IReporteConsultaRepository, ReporteConsultaRepository>();

        return services;
    }
}
