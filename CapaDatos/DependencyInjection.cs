using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Perfil;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Realtime;
using CapaDatos.Auth;
using CapaDatos.Perfil;
using CapaDatos.Realtime;
using CapaDatos.Repositories.Productos;
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

        // Buscador universal (entidad de dominio)
        // Transient: repos sin estado mutable — Scoped era engañoso en WPF (sin scopes = singleton de facto)
        services.AddTransient<IRepository<Producto>, ProductoSearchRepository>();
        services.AddTransient<IRepository<Empleado>, EmpleadoRepository>();
        services.AddTransient<IRepository<Cliente>,  ClienteRepository>();

        // Formulario de productos (DTO con FKs)
        services.AddTransient<IProductoRepository, ProductoCrudRepository>();

        return services;
    }
}
