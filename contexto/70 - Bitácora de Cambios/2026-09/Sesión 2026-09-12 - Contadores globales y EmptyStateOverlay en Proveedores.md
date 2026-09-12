---
title: "Sesión 2026-09-12 — Contadores globales y EmptyStateOverlay en Proveedores"
date: 2026-09-12
tags:
  - bitacora
  - sesion
  - wpf
  - mvvm
  - proveedores
  - productos
  - ui
aliases:
  - Contadores globales y EmptyStateOverlay en Proveedores
---

# Sesión 2026-09-12 — Contadores globales y EmptyStateOverlay en Proveedores

## Resumen

Se resolvió la anomalía en el módulo de Proveedores donde las pastillas de métricas del encabezado (**TOTAL**, **ACTIVOS**, **INACTIVOS**) se recalculaban contaminadas por el filtro de estado seleccionado, en lugar de mantenerse globales. Se replicó la arquitectura y el comportamiento estándar del módulo de Productos: desacoplamiento de la consulta RPC de conteo respecto al estado, cálculo reactivo de la paginación con `ResolverFilteredCount`, adopción del componente reutilizable `<controls:EmptyStateOverlay>` con mensajes contextuales inteligentes (`MensajeSinResultados`) y propagación segura de `CancellationToken` en la capa de datos.

---

## Causa Raíz

1. **Contaminación de conteos en `ProveedorCrudRepository.cs`:**
   En `GetPagedInternal`, la llamada a `GetConteosRpcAsync` recibía directamente la instancia `filtros` de la vista (que incluye `IdEstado = 1` para Activos o `IdEstado = 2` para Inactivos). `GetConteosRpcAsync` pasaba `p_estado` a la función SQL de Supabase `contar_proveedores(p_estado)`:
   ```sql
   SELECT
     COUNT(*)::int AS total,
     COUNT(*) FILTER (WHERE id_estado = 1)::int AS activos,
     COUNT(*) FILTER (WHERE id_estado = 2)::int AS inactivos
   FROM proveedores
   WHERE p_estado IS NULL OR id_estado = p_estado;
   ```
   Al recibir `p_estado`, la cláusula `WHERE` filtraba la tabla antes de contar, provocando que **TOTAL** coincidiera con el estado elegido y que el estado no seleccionado mostrara 0.
2. **Carencia de estados vacíos contextuales:**
   `ProveedoresView.xaml` mantenía un `StackPanel` anticuado previo al `DataGrid` con un texto estático ("Sin resultados..."), en lugar de utilizar el control desacoplado `EmptyStateOverlay` centrado con `HeaderOffset="40"`. Además, `ProveedoresViewModel.cs` carecía de la propiedad computada `MensajeSinResultados` para informar con precisión al usuario (e.g., "No hay registros inactivos", "No se encontraron resultados con los filtros actuales").
3. **Omisión de `CancellationToken`:**
   `ProveedorCrudRepository.GetPagedInternal` no propagaba el token `ct` hacia `.Get(ct)` de Supabase Postgrest, desaprovechando la cancelación de peticiones de página en vuelo.

---

## Cambios Aplicados

### 1. Capa de Datos (`CapaDatos`)
- **`CapaDatos/Repositories/Proveedores/ProveedorCrudRepository.cs`**:
  - En `GetPagedAsync`, se propaga `ct` a `GetPagedInternal(page, size, filtros, ct)`.
  - En `GetPagedInternal`, se instancia `var filtrosConteo = new ProveedorFiltros();` (con `IdEstado = null`), asegurando que `GetConteosRpcAsync` invoque `contar_proveedores(p_estado := NULL)`. De esta forma, el servidor retorna las métricas globales totales, activas e inactivas de todo el catálogo.
  - Se propaga el token `ct` en la consulta paginada de filas: `query.Order("id_proveedor", Ord.Ascending).Range(from, to).Get(ct)`.

### 2. Capa de Presentación (`CapaUI`)
- **`CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresViewModel.cs`**:
  - Simplificación de `NoResults`: `!IsLoading && (_filteredCount == 0 || !string.IsNullOrWhiteSpace(ErrorCarga))`.
  - Incorporación de `MensajeSinResultados` con lógica contextual:
    - `"No hay registros inactivos"` cuando se filtra por Inactivos y la cuenta es cero.
    - `"No hay registros activos"` cuando se filtra por Activos y no hay activos pero sí existen otros registros.
    - `"No se encontraron resultados con los filtros actuales"` cuando hay texto en `Query`.
    - `"No hay registros"` cuando la tabla carece por completo de datos.
  - Notificación de `MensajeSinResultados` en `[NotifyPropertyChangedFor]` de `_isLoading`, en `CargarPaginaAsync`, `CargarPaginaSilenciosamenteAsync` y `RefrescarConteosAsync`.
- **`CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml`**:
  - Eliminado el `StackPanel` anticuado de `EmptyState` que precedía al `DataGrid`.
  - Incorporado `<controls:EmptyStateOverlay>` posterior al `DataGrid` con `Panel.ZIndex="5"`, `HeaderOffset="40"`, `Mensaje="{Binding MensajeSinResultados}"` y `EstaVacio="{Binding NoResults}"`.

### 3. Pruebas Automatizadas (`BimboProyecto.Tests`)
- **`BimboProyecto.Tests/Proveedores/ProveedoresWhiteBoxTests.cs`**:
  - Añadida prueba `ResolverFilteredCount_CalculaFilasFiltradas_SinAfectarMetricasGlobales` para verificar que la resolución de filas filtradas para paginación no altera las métricas globales de `Total`, `Activos` e `Inactivos`.
  - Añadida prueba `ProveedorCrudRepository_DesacoplaConteosDeIdEstado_YPropagaToken` para validar por inspección que los filtros de conteo se instancian aislados y que se propaga `ct`.
  - Añadida prueba `ProveedoresView_UtilizaEmptyStateOverlayDeclarativo` para asegurar que la vista utiliza `EmptyStateOverlay` con enlace a `MensajeSinResultados` y compensación `HeaderOffset="40"`.

---

## Verificación de Calidad

- **Pruebas Unitarias:** `dotnet test BimboProyecto/BimboProyecto.Tests/BimboProyecto.Tests.csproj` → **427/427 (100%) superadas**.
- **CapaDatos:** `dotnet build BimboProyecto/CapaDatos/CapaDatos.csproj` → **0 advertencias, 0 errores**.
- **CapaDominio, CapaAplicacion, CapaDatos y Tests:** Compilación limpia en .NET 10.
