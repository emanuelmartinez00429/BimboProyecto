using CapaAplicacion.Common.Specifications;
using CapaAplicacion.Search.Dtos;
using CapaDominio.Entities;
using CapaDominio.Interfaces;

namespace CapaAplicacion.Search.Strategies;

public class ClienteSearchStrategy : ISearchStrategy
{
    private readonly IRepository<Cliente> _repo;
    public string EntityType => "Cliente";
    public int    Priority   => 3;

    public ClienteSearchStrategy(IRepository<Cliente> repo) => _repo = repo;

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string term, CancellationToken ct)
    {
        var spec     = new ClienteSearchSpecification(term);
        var clientes = await _repo.FindAsync(spec.Criteria, ct);
        return clientes.Select(c => new SearchResultDto
        {
            EntityType      = EntityType,
            Id              = c.Id.ToString(),
            DisplayText     = c.RazonSocial,
            SubText         = $"RTN: {c.RTN}  |  Cód: {c.Codigo}",
            Icon            = "🏪",
            NavigationParam = c.Id
        });
    }
}
