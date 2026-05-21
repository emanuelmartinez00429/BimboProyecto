using CapaAplicacion.Search.Dtos;
using CapaDominio.Entities;
using CapaDominio.Interfaces;

namespace CapaAplicacion.Search.Strategies;

public class ProductoSearchStrategy : ISearchStrategy
{
    private readonly IRepository<Producto> _repo;
    public string EntityType => "Producto";
    public int    Priority   => 1;

    public ProductoSearchStrategy(IRepository<Producto> repo) => _repo = repo;

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string term, CancellationToken ct)
    {
        var productos = await _repo.SearchAsync(term, ct);
        return productos.Select(p => new SearchResultDto
        {
            EntityType      = EntityType,
            Id              = p.Id.ToString(),
            DisplayText     = $"{p.Nombre}  {p.Fabricante}  {p.Contenido}  {p.Presentacion}".Trim(),
            SubText         = $"Cód: {p.CodigoInterno}  |  {p.Categoria}  |  {p.Pais}",
            Icon            = "📦",
            NavigationParam = p.Id
        });
    }
}
