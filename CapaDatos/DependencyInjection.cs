using CapaDatos.Repositories.Search;
using CapaDominio.Entities;
using CapaDominio.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CapaDatos;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        services.AddScoped<IRepository<Producto>, ProductoRepository>();
        services.AddScoped<IRepository<Empleado>, EmpleadoRepository>();
        services.AddScoped<IRepository<Cliente>,  ClienteRepository>();
        return services;
    }
}
