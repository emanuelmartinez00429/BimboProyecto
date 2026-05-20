using CapaAplicacion.Common.Specifications;
using CapaAplicacion.Search.Dtos;
using CapaDominio.Entities;
using CapaDominio.Interfaces;

namespace CapaAplicacion.Search.Strategies;

public class EmpleadoSearchStrategy : ISearchStrategy
{
    private readonly IRepository<Empleado> _repo;
    public string EntityType => "Empleado";
    public int    Priority   => 2;

    public EmpleadoSearchStrategy(IRepository<Empleado> repo) => _repo = repo;

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string term, CancellationToken ct)
    {
        var spec      = new EmpleadoSearchSpecification(term);
        var empleados = await _repo.FindAsync(spec.Criteria, ct);
        return empleados.Select(e => new SearchResultDto
        {
            EntityType      = EntityType,
            Id              = e.Id.ToString(),
            DisplayText     = $"{e.Nombres} {e.Apellidos}",
            SubText         = $"Identidad: {e.Identidad}  |  Tel: {e.Telefono}",
            Icon            = "👤",
            NavigationParam = e.Id
        });
    }
}
