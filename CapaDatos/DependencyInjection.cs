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
        services.AddScoped<IRepository<Producto>, ProductoSearchRepository>();
        services.AddScoped<IRepository<Empleado>, EmpleadoRepository>();
        services.AddScoped<IRepository<Cliente>,  ClienteRepository>();

        // Formulario de productos (DTO con FKs)
        services.AddScoped<IProductoRepository, ProductoCrudRepository>();

        return services;
    }
}
