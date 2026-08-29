---
title: Sesión 2026-07-28 — Fix buscador global (PostgrestException PGRST100)
type: sesion
status: vigente
tags:
  - sesion
  - buscador
  - bugfix
  - supabase
date: 2026-07-28
updated: 2026-07-28
summary: El buscador global (topbar) crasheaba con una excepción no controlada al buscar productos o empleados. Corregido el filtro OR mal construido en los dos…
scope:
  - CapaAplicacion4/Search/Handlers
  - CapaDatos/Auth
  - CapaDatos/Repositories/Search
  - CapaUI/ViewModels/Search
  - CapaUI/bin
symbols:
  - ClienteSearchStrategy
  - DispatcherUnhandledException
  - EmpleadoSearchStrategy
  - IRepository<T>
  - ISearchStrategy
  - OperationCanceledException
  - PostgrestException
  - ProductoCrudRepository
  - ProductoSearchStrategy
  - RepositorioBase
branch: feat/fase7-GestióndeUsuarios
autor_cambios: Claude (agente)
---

# Sesión 2026-07-28 — Fix buscador global (`PostgrestException` PGRST100)

> [!success] Resultado
> El buscador global (topbar) crasheaba con una excepción no controlada al buscar productos o empleados. Corregido el filtro OR mal construido en los dos repositorios afectados, y agregada una red de contención en el pipeline para que ninguna falla de una entidad vuelva a tumbar el buscador completo.

---

## Problema / motivo

Reportado por el usuario con una captura de Visual Studio: excepción no controlada parada justo en el `.Get()` de `ProductoSearchRepository.SearchAsync`, buscando el término `dede` desde el buscador global. Mensaje exacto capturado del debugger:

```
Supabase.Postgrest.Exceptions.PostgrestException: '{"code":"PGRST100",
"details":"unexpected \"e\" expecting \"(\"","hint":null,
"message":"\"failed to parse logic tree (eq.(nombre_producto.ilike.%dede%,
codigo_producto.ilike.%dede%,contenido.ilike.%dede%))\" (line 1, column 3)"}'
```

### Causa raíz

`ProductoSearchRepository.cs` y `EmpleadoRepository.cs` (ambos en `CapaDatos/Repositories/Search/`, la implementación del **buscador global/universal**, distinto del buscador por formulario) usaban el antipatrón ya conocido:

```csharp
.Filter("or", Operator.Equals,
    $"(nombre_producto.ilike.{pattern},codigo_producto.ilike.{pattern},contenido.ilike.{pattern})")
```

Esto genera `?or=eq.(...)`. PostgREST parsea el valor de `or` como un "logic tree" y espera que arranque con `(`; al encontrar `eq.` primero, lo rechaza con `PGRST100`.

**Esto contradice lo que decía [[Bug - Filter OR con Op.Equals en postgrest-csharp]]**, que afirmaba que este patrón "se ejecuta sin excepciones, siempre retorna 0 resultados" — comportamiento documentado a partir de una lectura de la librería, nunca reproducido contra el servidor real. Se corrigió esa nota como parte de esta sesión: PostgREST **sí** valida la gramática de `or` y **sí** puede rechazarla con una excepción real.

El buscador por formulario de Productos ya había corregido este mismo antipatrón el 2026-05-28 (`ProductoCrudRepository`). El buscador **global** nunca se tocó y quedó con el bug en dos repositorios (Productos y Empleados) — el tercer repo de esa carpeta, `ClienteRepository.cs`, es un stub sin Supabase y no aplica.

### Por qué llegó sin capturar hasta el debugger

Recorrido completo del pipeline (`UniversalSearchViewModel` → MediatR `UniversalSearchQuery` → `UniversalSearchHandler` → `SearchStrategyRegistry` → `ISearchStrategy` → `IRepository<T>`):

- `UniversalSearchHandler.Handle` disparaba las 3 estrategias (`ProductoSearchStrategy`, `EmpleadoSearchStrategy`, `ClienteSearchStrategy`) con `Task.WhenAll` — si una fallaba, `Task.WhenAll` propagaba esa excepción y se perdían también los resultados de las otras dos, aunque hubieran funcionado bien.
- `UniversalSearchViewModel.SearchAsync` solo tenía `catch (OperationCanceledException) { }` — ninguna otra excepción se capturaba ahí.
- No existe ningún `DispatcherUnhandledException` ni manejador global en `CapaUI/App.xaml.cs`.
- `SupabaseRepository<TDomain,TSupabase>` (la base de estos repos) no hereda `RepositorioBase`, así que tampoco tiene el `TryAsync`/`Result<T>` que sí usa el resto del proyecto — ítem ya documentado como pendiente en [[Base Repository con TryAsync]]. Migrar todo `IRepository<T>` a `Result<T>` es un refactor más grande (toca 4 archivos en 3 capas) que se dejó **fuera de alcance** de este fix, por decisión explícita del usuario.

## Cambios aplicados

### 1-2. Filtro OR corregido en los dos repositorios

`CapaDatos/Repositories/Search/ProductoSearchRepository.cs` y `EmpleadoRepository.cs` — reemplazado `.Filter("or", Operator.Equals, "(...)")` por `.Or(new List<IPostgrestQueryFilter>{...})`, el patrón ya usado en todo el resto del proyecto (`ProductoCrudRepository.cs:147-152`).

### 3. Aislamiento por estrategia en `UniversalSearchHandler`

`CapaAplicacion4/Search/Handlers/UniversalSearchHandler.cs` — cada `ISearchStrategy.SearchAsync` se envuelve en un helper (`SearchSeguroAsync`) que captura excepciones (no `OperationCanceledException`, que sigue propagándose) y devuelve vacío solo para esa entidad, en vez de tumbar `Task.WhenAll` completo. Log vía `System.Diagnostics.Debug.WriteLine` (mismo mecanismo que usa `RepositorioBase.TryAsync`; `CapaAplicacion4` no referencia Serilog y no valía la pena sumar esa dependencia para esto).

### 4. Backstop final en `UniversalSearchViewModel`

`CapaUI/ViewModels/Search/UniversalSearchViewModel.cs` — el único `catch` se amplió con un `catch (Exception ex)` genérico que loguea vía `Serilog.Log.Error` (ya usado así en `CapaDatos/Auth/AuthService.cs`) y pone `StatusText = "Error al buscar"`, para que la UI nunca quede colgada si algo se escapa fuera del handler.

## Verificación

`dotnet build` — **0 errores CS** en los tres proyectos tocados (`CapaDatos`, `CapaAplicacion4`, `CapaUI`). La copia final de DLLs a `CapaUI/bin` falló por bloqueo de archivo (`MSB3027`/`MSB3021`): había una instancia de `CapaUI` corriendo desde una sesión de depuración de Visual Studio (la misma donde se reprodujo la excepción). No es un error de compilación — el usuario deberá cerrar esa sesión y recompilar para probar en ejecución.

Pendiente de correr por el usuario:
1. Buscar `dede` (o cualquier término) desde el buscador global — no debe crashear.
2. Confirmar que Productos y Empleados devuelven resultados reales cuando el término coincide (antes del fix, aunque no hubiera tirado excepción, tampoco habría filtrado nada — el `or` mal formado se habría ignorado).
3. Confirmar navegación al seleccionar un resultado (`ResultSelected`).

## Lo que NO cambió

- `SupabaseRepository<TDomain,TSupabase>` sigue sin heredar `RepositorioBase`/`Result<T>` — decisión explícita de mantener el fix quirúrgico en vez de migrar toda la arquitectura del buscador global.
- El buscador por formulario (`SuggestionSearchBox`, `ProductoCrudRepository`, etc.) no se tocó — ya tenía el patrón correcto desde 2026-05-28.
- `ClienteRepository.cs` — stub sin Supabase, no aplica.

---

## Relaciones

- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — nota corregida con la excepción real reproducida
- [[ADR-002 - CQRS y Strategy para Buscador Universal]] — arquitectura del pipeline tocado
- [[Base Repository con TryAsync]] — ítem pendiente relacionado (buscador global sin Result Pattern)
- [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]] — el mismo bug, corregido antes en el buscador por formulario
