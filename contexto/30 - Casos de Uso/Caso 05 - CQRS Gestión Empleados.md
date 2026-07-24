---
title: "Caso 05 — CQRS en Gestión de Empleados"
tags:
  - caso-de-uso
  - cqrs
  - empleados
  - dotnet
---

# Caso 05 — CQRS en Gestión de Empleados

> [!example] Caso real
> **Referencia:** CQRS con MediatR para HR Management — separación de queries (consultar empleados) y commands (contratar, despedir, actualizar salario)  
> **Fuente:** [CQRS and Mediator — Medium](https://medium.com/@darshana-edirisinghe/cqrs-and-mediator-design-patterns-f11d2e9e9c2e)

---

## El problema sin CQRS

```csharp
// ❌ Un solo repositorio hace todo — el modelo de lectura y escritura se mezclan
public class EmpleadoService
{
    public Task<List<Empleado>> ObtenerTodos() { ... }
    public Task Contratar(Empleado e) { ... }
    public Task ActualizarSalario(int id, decimal salario) { ... }
    public Task Despedir(int id, string motivo) { ... }
}
```

Problemas: el modelo de `Empleado` para lectura tiene campos calculados (antigüedad, nombre completo) que no necesitan la escritura. La escritura solo necesita campos básicos.

---

## Con CQRS

### Query side (lectura)

```csharp
// Query — solo preguntas
public record GetEmpleadosQuery(string? Busqueda, int Page) : IRequest<PagedResult<EmpleadoDto>>;

// Handler — responde la pregunta
public class GetEmpleadosHandler : IRequestHandler<GetEmpleadosQuery, PagedResult<EmpleadoDto>>
{
    public async Task<PagedResult<EmpleadoDto>> Handle(GetEmpleadosQuery q, CancellationToken ct)
    {
        return await _repo.SearchAsync(q.Busqueda, q.Page, ct);
    }
}

// DTO de lectura — tiene campos de display
public class EmpleadoDto
{
    public string NombreCompleto => $"{Nombres} {Apellidos}";
    public string Identidad { get; init; }
    // ...
}
```

### Command side (escritura)

```csharp
// Command — solo acciones
public record ContratarEmpleadoCommand(string Nombres, string Identidad, ...) : IRequest<int>;

// Handler — ejecuta la acción
public class ContratarEmpleadoHandler : IRequestHandler<ContratarEmpleadoCommand, int>
{
    public async Task<int> Handle(ContratarEmpleadoCommand cmd, CancellationToken ct)
    {
        var empleado = new EmpleadoInsertar { ... };
        return await _repo.InsertarAsync(empleado, ct);
    }
}
```

---

## Cómo aplica al buscador de Bimbo

El `EmpleadoRepository` ya implementa la ruta de lectura:

```csharp
// Para el buscador universal — solo lectura, solo search
public class EmpleadoRepository : SupabaseRepository<Empleado, Empleados>
{
    public override async Task<IEnumerable<Empleado>> SearchAsync(string term, ct)
    {
        .Filter("or", Operator.Equals,
            "(nombre_empleado.ilike.%term%,apellido_empleado.ilike.%term%,...)")
    }
}
```

La escritura de empleados es un módulo separado que no toca este repositorio.

---

## Relaciones

- [[CQRS + Mediator]] — Patrón completo
- [[Repository Pattern]] — `EmpleadoRepository` para la ruta de lectura
- [[Strategy Pattern]] — `EmpleadoSearchStrategy` es la estrategia de búsqueda
- [[Caso 02 - Buscador Universal]] — El buscador que usa esta estrategia
- [[Buscador Universal Bimbo]] — Implementación real
