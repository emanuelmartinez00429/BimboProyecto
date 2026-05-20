namespace CapaAplicacion.Search.Dtos;

public record SearchResultDto
{
    public string  EntityType      { get; init; } = string.Empty;
    public string  Id              { get; init; } = string.Empty;
    public string  DisplayText     { get; init; } = string.Empty;
    public string  SubText         { get; init; } = string.Empty;
    public string  Icon            { get; init; } = string.Empty;
    public object? NavigationParam { get; init; }
}
