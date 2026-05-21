using CapaAplicacion.Productos.Interfaces;
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
        // Buscador universal (entidad de dominio)
        services.AddScoped<IRepository<Producto>, ProductoSearchRepository>();
        services.AddScoped<IRepository<Empleado>, EmpleadoRepository>();
        services.AddScoped<IRepository<Cliente>,  ClienteRepository>();

        // Formulario de productos (DTO con FKs)
        services.AddScoped<IProductoRepository, ProductoCrudRepository>();

        return services;
    }
}
