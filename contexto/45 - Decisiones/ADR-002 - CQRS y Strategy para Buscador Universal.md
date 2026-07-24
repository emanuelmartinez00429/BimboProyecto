# ADR-002 — CQRS + Strategy Pattern para el Buscador Universal

**Fecha:** 2026-05-21  
**Estado:** Aceptado

---

## Contexto

El buscador universal necesita buscar en múltiples entidades (Productos, Empleados, Clientes) con lógica diferente por entidad: columnas distintas, joins distintos, reglas de formato distintas. La pantalla principal tiene un único cuadro de búsqueda que agrega resultados de todas las entidades.

---

## Opciones consideradas

1. **Un servicio con `switch` por entidad** — simple al principio, pero crece linealmente con cada entidad nueva. Viola Open/Closed.
2. **Strategy pattern directo sin CQRS** — cada entidad es una estrategia, se inyectan todas, se llaman en paralelo. Más limpio pero el dispatcher sigue acoplado al consumidor.
3. **CQRS + Strategy** — `SearchQuery` como mensaje, `SearchHandler` como dispatcher, cada `ISearchStrategy` como implementación intercambiable. Se registran en DI como colección.

---

## Decisión

**Opción 3: CQRS con MediatR + Strategy.**

- `UniversalSearchViewModel` envía `SearchQuery` vía MediatR.
- `UniversalSearchHandler` recibe todas las `ISearchStrategy` por DI y las ejecuta.
- Cada entidad implementa `ISearchStrategy` e inyecta `IRepository<TEntidad>`.
- Agregar una entidad nueva = crear una estrategia + registrar en DI. Sin tocar código existente.

---

## Consecuencias

- **Positivo:** completamente extensible. Nuevas entidades no tocan código existente.
- **Positivo:** cada estrategia es testeable de forma aislada.
- **Positivo:** el ViewModel no sabe nada de las entidades concretas.
- **Negativo:** más archivos y abstracciones para búsquedas simples.
- **Negativo:** MediatR añade una dependencia externa; si se elimina, hay que reescribir el dispatcher.

---

## Separación crítica del módulo Productos

El módulo Productos tiene **dos rutas independientes**:

| Ruta | Propósito | Repositorio |
|---|---|---|
| Buscador universal | Typeahead global | `ProductoSearchRepository` |
| Formulario Productos | CRUD + paginación | `ProductoCrudRepository` |

Nunca mezclar. El formulario no usa `IRepository<Producto>` ni el buscador usa `IProductoRepository`.

---

## Archivos clave

- `CapaAplicacion/Search/Strategies/` — estrategias por entidad
- `CapaAplicacion/Search/UniversalSearchHandler.cs` — dispatcher
- `CapaDatos/Repositories/Search/` — implementaciones de búsqueda
