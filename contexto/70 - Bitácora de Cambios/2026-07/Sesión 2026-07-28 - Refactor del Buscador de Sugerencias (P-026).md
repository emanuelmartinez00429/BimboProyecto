---
title: Sesión 2026-07-28 — Refactor del buscador de sugerencias (P-026)
type: sesion
status: vigente
tags:
  - sesion
  - buscador
  - refactor
  - mvvm
  - deuda-tecnica
date: 2026-07-28
updated: 2026-07-28
summary: "El puente ViewModel → SuggestionSearchBox, que estaba copiado en 9 pantallas, quedó centralizado. Se borraron ~250 líneas duplicadas y las dos clases de bug del…"
scope:
  - CapaUI/Core/Controls
symbols:
  - CancellationTokenSource
  - IsViewingContacts
  - ObservableObject
  - OnVmPropertyChanged
  - RealtimeAwareViewModel
  - RefrescarSugerenciasAsync
  - Seleccionado
  - SeleccionarSugerencia
  - SelectorProductosViewModel
  - ShowSuggestions
branch: fix/buscador-sugerencias
autor_cambios: Claude (agente)
---

# Sesión 2026-07-28 — Refactor del buscador de sugerencias (P-026)

> [!success] Resultado
> El puente ViewModel → `SuggestionSearchBox`, que estaba copiado en 9 pantallas, quedó centralizado. Se borraron ~250 líneas duplicadas y las dos clases de bug del buscador pasaron a ser **imposibles por construcción**. Cierra **P-026**. Sin cambios de comportamiento ni de rendimiento.

---

## Problema / motivo

Continuación directa de [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]]. Al arreglar ese bug hubo que tocar 18 archivos para lo que conceptualmente es *una* función, y el usuario preguntó lo obvio: si es la misma lógica, ¿por qué no está en un solo lugar?

La revisión confirmó que tenía razón, con dos datos concretos:

1. **`Suggestions` y `ShowSuggestions` nunca se bindeaban en XAML.** Existían únicamente para alimentar el code-behind. No eran modelo de presentación: eran plomería del acoplamiento. Y eran **dos señales para un solo hecho** ("esto es lo que hay que mostrar") — de ahí el bug: la vista tenía que combinarlas a mano y una de las dos no notificaba.

2. **De las ~28 líneas de `RefrescarSugerenciasAsync`, variaba UNA.** La llamada al repositorio. El resto — CTS, `Task.Delay(300, token)`, los dos guards de cancelación, el early-return de query vacía, el `catch (OperationCanceledException)` — era copia carácter por carácter en los 9.

Con 9 repeticiones y 2 defectos replicados en dos meses (Usuarios con `TextBox` plano el 26/07, el popup congelado el 28/07), la Rule of Three quedó atrás hace rato. No había riesgo de abstracción prematura.

---

## Cambios aplicados

Se hizo **en dos pasos**, en commits separados, para que el cambio de superficie (XAML + vistas) se verificara aparte del cambio interno de los ViewModels.

### Paso 1 — `SuggestItems` por binding, se borra el puente

Commit `dd5a410`.

El ViewModel expone **una sola propiedad, ya mapeada**:

```csharp
/// null o lista vacía = popup cerrado
[ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;
```

El mapeo `DTO → SuggestionItemData` — lo único legítimamente distinto entre módulos — se mudó del code-behind al ViewModel como método estático:

```csharp
private static SuggestionItemData Map(ProductoDto p) => new()
{
    Codigo = p.CodigoInterno,
    Nombre = p.Nombre,
    Meta   = $"{p.Fabricante} · {p.Pais} · {p.Categoria}",
    Activo = p.IdEstado == Activo,
    Source = p,
};
```

Y el XAML se bindea directo:

```xml
<controls:SuggestionSearchBox
    Query="{Binding Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
    SuggestItems="{Binding SuggestItems}"
    HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}"
    ItemSelected="SearchBox_ItemSelected"/>
```

**Se borraron:** los 9 `ActualizarSuggestions()`, los `case` del switch de `OnVmPropertyChanged` y las propiedades `Suggestions` / `ShowSuggestions`.

**Se conservó** `SearchBox_ItemSelected` en el code-behind: además de avisar al VM hace `SeleccionarEnTabla()`, que sí es responsabilidad de la vista.

> [!note] Poner `SuggestionItemData` en el ViewModel no viola MVVM
> Es un tipo de la capa UI (`CapaUI/Core/Controls/`) y los ViewModels también son capa UI. No cruza ninguna frontera de capas.

### Paso 2 — `SuggestionDebouncer` compartido

Commit `6d5e961`. Nuevo archivo `CapaUI/Core/Controls/SuggestionDebouncer.cs` (~85 líneas) que encapsula el `CancellationTokenSource`, el `Task.Delay(300)`, los guards y el `catch`.

```csharp
public async Task EjecutarAsync(
    string query,
    Func<string, CancellationToken, Task<IReadOnlyList<SuggestionItemData>?>> buscar,
    Action<IReadOnlyList<SuggestionItemData>?> aplicar)
```

`aplicar` solo se invoca si la búsqueda no fue cancelada, así que el llamador puede escribir en la UI sin guardas extra.

Cada ViewModel queda declarativo:

```csharp
private readonly SuggestionDebouncer _buscador = new();

private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
    _query,
    async (q, ct) =>
    {
        var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
        return r.Success ? r.Value!.Select(Map).ToList() : null;
    },
    items => { SuggestItems = items; HighlightIndex = -1; });
```

Más `_buscador.Cancelar()` en `SeleccionarSugerencia` y `_buscador.Dispose()` en `OnDispose()`/`Dispose()`.

> [!important] Composición, no herencia — y no fue una preferencia estética
> 5 de los 9 ViewModels heredan de `RealtimeAwareViewModel` y 4 de `ObservableObject`. C# tiene herencia simple: una clase base común para el buscador habría obligado a reorganizar esa jerarquía, con riesgo alto y cero ganancia. Un campo compuesto funciona idéntico en ambas ramas.

---

## Por qué esto elimina los bugs, no solo el código repetido

| Defecto | Antes | Ahora |
|---|---|---|
| Popup congelado al seguir escribiendo | Dependía de que la vista tuviera los `case` correctos en el `switch` | **Imposible**: no hay switch; el binding propaga siempre |
| Popup que se reabre solo tras seleccionar | Dependía de acordarse de cancelar el CTS en cada VM | **Imposible**: el cancel vive dentro del componente |
| Módulo nuevo replica el patrón mal | ~80 líneas a copiar y 4 puntos de conexión que recordar | ~10 líneas: la lambda y el `Map` |

---

## Impacto en rendimiento

**Ninguno.** El costo real del buscador es la llamada a Supabase (debounce 300 ms + `Limit(10)` server-side), y no se tocó. Mismo número de llamadas de red, mismos filtros, mismo `_loadGeneration`. La duplicación era un problema de mantenimiento, no de runtime.

---

## Verificación

`dotnet build BimboProyecto.sln` → **0 errores**, **0 advertencias en `CapaUI`** (las 45 restantes son `CS8618` preexistentes de `CapaDatos`).

Se construyó incrementalmente: primero se migró **solo Productos** y se compiló para validar el patrón, después los otros 8; y se compiló otra vez entre el Paso 1 y el Paso 2.

Prueba manual pendiente de ejecución por el usuario, por pantalla:

1. Escribir un término parcial, esperar el popup, agregar un carácter → la lista y el contador deben cambiar sin cerrar el popup.
2. Borrar con Backspace → debe volver a los resultados anteriores.
3. Término sin coincidencias → cierra. Escribir algo válido → reabre.
4. Enter sobre una sugerencia mientras hay búsqueda en vuelo → el popup no debe reabrirse solo.
5. ↑↓ con wrap-around, Escape limpia, hover pinta el item.
6. **Contactos Proveedores / Contactos Fabricantes:** verificar además el drill-down (`IsViewingContacts`), que es el punto con más estado alrededor del buscador.

---

## Lo que NO cambió

- **`SuggestionSearchBox.xaml` / `.xaml.cs`** — el control nunca fue el problema.
- **Repositorios, `CapaDatos`, `CapaAplicacion`, SQL** — sin tocar.
- **`SeleccionarSugerencia`** sigue en cada ViewModel, y con razón: Productos y Bitácora navegan cross-page con `_pendingSelectionId`, Categorías solo asigna `Seleccionado`. Es lógica de dominio del módulo, no del buscador.
- **`SelectorProductosViewModel` (Pesaje)** — usa el control sin popup, con resultados en una tabla paginada. Conserva su propio `_searchCts` a propósito.
- **Buscador universal** (`UniversalSearchViewModel` + MediatR) — otro pipeline.

---

## Documentación actualizada

- [[Deuda Técnica - Pendientes]] — **P-026 marcado resuelto** con la solución aplicada.
- [[Módulo Productos]] — callout nuevo con el patrón vigente completo (VM + XAML) reemplazando al de los dos `case`, que quedó obsoleto en el mismo día.
- [[CLAUDE]] — sección "Búsqueda con sugerencias" reescrita, con la regla explícita de no volver a exponer un `bool ShowSuggestions`.
- [[Paginación y Búsqueda - Arquitectura Detallada]] §4 — diagrama de flujo y `SeleccionarSugerencia` actualizados.

---

## Relaciones

- [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]] — el bug que originó este refactor
- [[Deuda Técnica - Pendientes]] — P-026 (resuelto acá), P-006 (misma familia, sigue abierta)
- [[Módulo Productos]] — patrón de referencia actualizado
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Sesión 2026-05-29 - SuggestionSearchBox Compartido y Fix 5 Bugs Buscador]] — origen del control compartido
- [[Sesión 2026-07-26 - Fix Buscador Usuarios (SuggestionSearchBox)]] — el otro defecto replicado
- [[Arquitectura Actual]]
