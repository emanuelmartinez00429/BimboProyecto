using CapaAplicacion.Search.Handlers;
using CapaAplicacion.Reportes;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Search.Registry;
using CapaAplicacion.Search.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace CapaAplicacion;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(UniversalSearchHandler).Assembly));

        services.AddScoped<ISearchStrategy, ProductoSearchStrategy>();
        services.AddScoped<ISearchStrategy, EmpleadoSearchStrategy>();
        services.AddScoped<ISearchStrategy, ClienteSearchStrategy>();
        services.AddScoped<SearchStrategyRegistry>();
        services.AddTransient<IReportGeneratorService, ReportGeneratorService>();

        return services;
    }
}
