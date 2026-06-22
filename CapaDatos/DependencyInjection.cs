using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Categorias.Interfaces;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Perfil;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Realtime;
using CapaDatos.Auth;
using CapaDatos.Conexion;
using CapaDatos.Perfil;
using CapaDatos.Realtime;
using CapaDatos.Repositories.Categorias;
using CapaDatos.Repositories.Contactos;
using CapaDatos.Repositories.Fabricantes;
using CapaDatos.Repositories.Productos;
using CapaDatos.Repositories.Proveedores;
using CapaDatos.Repositories.Search;
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

        // Formularios de catálogo
        services.AddTransient<IProveedorRepository, ProveedorCrudRepository>();
        services.AddTransient<IFabricanteRepository, FabricanteCrudRepository>();
        services.AddTransient<ICategoriaRepository, CategoriaCrudRepository>();

        // Contactos de fabricantes y proveedores
        services.AddTransient<IContactoFabricanteRepository, ContactoFabricanteCrudRepository>();
        services.AddTransient<IContactoProveedorRepository, ContactoProveedorCrudRepository>();

        return services;
    }
}
