using CapaAplicacion;
using CapaAplicacion.Search.Queries;
using CapaDatos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDataLayer();
services.AddApplicationLayer();
var provider = services.BuildServiceProvider();

var mediator = provider.GetRequiredService<IMediator>();

string[] términos = { "bimbo", "pan", "empleado" };

foreach (var término in términos)
{
    Console.WriteLine($"\n{'=',50}");
    Console.WriteLine($"  Buscando: \"{término}\"");
    Console.WriteLine($"{'=',50}");

    try
    {
        var resultado = await mediator.Send(new UniversalSearchQuery(término, 5));

        if (resultado.Items.Count == 0)
        {
            Console.WriteLine("  (sin resultados)");
        }
        else
        {
            Console.WriteLine($"  {resultado.TotalCount} resultado(s) en {resultado.Elapsed.TotalMilliseconds:F0} ms\n");
            foreach (var item in resultado.Items)
            {
                Console.WriteLine($"  {item.Icon}  [{item.EntityType}]  {item.DisplayText}");
                Console.WriteLine($"       {item.SubText}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERROR: {ex.Message}");
    }
}

Console.WriteLine("\nPrueba completada.");
