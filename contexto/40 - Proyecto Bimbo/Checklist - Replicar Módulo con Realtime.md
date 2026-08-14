---
title: "Checklist — Replicar Módulo con Realtime"
tags:
  - checklist
  - realtime
  - modulos
  - p006
date: 2026-05-28
---

# Checklist — Replicar Módulo con Realtime

> [!info] P-006
> `OnCambioProducto` en `ProductosViewModel` contiene lógica acoplada a la paginación y a la estructura concreta de Productos. Este checklist documenta los puntos que **deben revisarse y adaptarse** al copiar ese patrón a un nuevo módulo.

---

## 1. Nombre de tabla en `_pkColumns` de `RealtimeService`

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs`

Verificar que la tabla del nuevo módulo está en el diccionario:

```csharp
private static readonly Dictionary<string, string> _pkColumns = new()
{
    ["productos"]   = "id_producto",
    // ← agregar aquí la nueva tabla
    ["empleados"]   = "id_empleado",
    ...
};
```

Si no está → el log mostrará `"Realtime P-008: tabla '...' no tiene PK mapeada"` y `IdRegistro` será siempre `null`.

---

## 2. Tipo de PK

`ProductosViewModel` usa `p.Id` (tipo `int`). El `CambioRealtime.IdRegistro` es `long?`.

Si el nuevo módulo usa una PK de tipo diferente (por ejemplo `Guid`), el handler `OnCambioXxx` necesita adaptación — `_pkColumns` solo mapea a `long`.

---

## 3. Heurística INSERT en `CargarPaginaSilenciosamenteAsync`

```csharp
bool debeActualizarFilas = actualizarFilas
    && (!esInsert || _page == nuevoTotalPages);
```

Esta lógica asume que los registros nuevos aparecen en la **última página** (orden por PK ASC o fecha de creación ASC).

**Si el módulo nuevo tiene un orden diferente** (por nombre, fecha DESC, etc.), un INSERT puede aterrizar en cualquier página. En ese caso: siempre recargar la página actual para INSERT, independientemente de `nuevoTotalPages`.

> [!danger] Consecuencia que hay que replicar también: `case TotalPages` en la vista
> Cuando `debeActualizarFilas` sale `false` (INSERT crea página nueva, usuario en la vieja última página), `PageRows` **no cambia** — pero `TotalPages` sí, y el ViewModel lo notifica bien (`OnPropertyChanged(nameof(TotalPages))`). Si el `switch` de `OnVmPropertyChanged` en el code-behind del módulo nuevo solo tiene `case nameof(XxxViewModel.PageRows): RefrescarPaginacion(); break;`, los botones numerados de página se quedan con el árbol viejo hasta recargar el módulo entero — el `CanExecute` de `«/‹/›/»` queda bien (lee `TotalPages` directo), pero el `ItemsControl` de números no se entera.
>
> **Agregar siempre**, junto al `case` de `PageRows`:
> ```csharp
> case nameof(XxxViewModel.PageRows):
>     DgX.ItemsSource = _vm.PageRows;   // rebind de filas: SOLO acá
>     RefrescarPaginacion();
>     break;
> case nameof(XxxViewModel.TotalPages):
>     RefrescarPaginacion();             // SOLO el árbol de botones
>     break;
> ```
> Es idempotente correr `RefrescarPaginacion()` dos veces si `PageRows` y `TotalPages` cambian juntos (carga de usuario) — solo reconstruye botones en memoria, no hay costo real. Bug real encontrado y corregido en Productos + 5 módulos gemelos el 2026-08-14 — ver [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]].
>
> ⚠️ **`RefrescarPaginacion()` NO debe rebindear la grilla.** Si el método hace `DgX.ItemsSource = _vm.PageRows` adentro, el `case TotalPages` reintroduce un bug distinto: el setter de `Page` notifica `TotalPages` **antes** de pedir los datos, así que la grilla se repinta con las filas de la página anterior mientras los botones ya muestran el número nuevo — y se queda pegado así si la carga sale por un return temprano (timeout, generación invalidada, error). Pasó de verdad el mismo día: [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]].

---

## 4. Guard `afectaPaginaActual`

```csharp
bool afectaPaginaActual = cambio.IdRegistro.HasValue
    && PageRows.Any(p => p.Id == cambio.IdRegistro.Value);
```

- Asume que el DTO del módulo expone la PK como propiedad `Id` (tipo `int`)
- Si el DTO tiene una propiedad con nombre diferente (ej. `IdEmpleado`), cambiar el selector

---

## 5. Módulos sin paginación

Si el nuevo módulo carga **todos los registros** sin paginación (colección pequeña), no usar `CargarPaginaSilenciosamenteAsync`. Simplificar el handler:

```csharp
private void OnCambioXxx(CambioRealtime cambio)
{
    if (_disposed) return;
    _ = CargarTodosAsync(); // recarga completa, sin lógica de páginas
}
```

---

## 6. Suscripción y Dispose — patrón estándar

```csharp
// En CargarDatosAsync(), al final:
await _realtime.SuscribirAsync("nombre_tabla", OnCambioXxx);

// En Dispose():
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _realtime.Desuscribir("nombre_tabla", OnCambioXxx);
    _searchCts?.Cancel();
    _searchCts?.Dispose();
}
```

Copiar este bloque es seguro — no tiene acoplamiento a Productos.

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-006 origen de este documento
- [[Módulo Productos]] — módulo de referencia
- [[Gestor Realtime - Diseño Arquitectónico]] — diseño del sistema de suscripciones
