---
title: "Sesión 2026-07-28 — Fix refresco del popup de sugerencias (9 módulos)"
tags:
  - sesion
  - buscador
  - mvvm
  - bugfix
date: 2026-07-28
branch: fix/buscador-sugerencias
autor_cambios: Claude (agente)
---

# Sesión 2026-07-28 — Fix refresco del popup de sugerencias (9 módulos)

> [!success] Resultado
> El popup de `SuggestionSearchBox` ahora refleja siempre el texto actual del buscador. Antes, con el popup abierto, seguir escribiendo no actualizaba la lista: quedaba congelada en el término anterior y solo se recuperaba borrando todo el texto. Corregido en las 9 pantallas que usan el control, sin cambios de arquitectura ni llamadas de red adicionales.

---

## Problema / motivo

Reportado por el usuario con un caso concreto:

> Escribo `Y49` (pero realmente quiero `Y490`) y me detengo. Pasa el tiempo de búsqueda, se activa la lista y muestra similares. Escribo el que falta y **la lista no se actualiza**, queda igual. Por más que borre y ponga letras no lo detecta — solo si borro todo.

### Causa raíz

La búsqueda **sí se ejecutaba siempre**. El debounce de 300 ms, el `CancellationTokenSource` y el repositorio funcionaban bien, y `Suggestions` sí se reemplazaba con los resultados nuevos. Lo que fallaba era la **propagación del resultado a la UI**.

El puente ViewModel → control es imperativo y se disparaba desde un único `case`:

```csharp
// ProductosView.xaml.cs — idéntico en las 9 pantallas
case nameof(ProductosViewModel.ShowSuggestions): ActualizarSuggestions(); break;
```

`ShowSuggestions` es `[ObservableProperty] private bool`. CommunityToolkit genera `SetProperty` con `EqualityComparer<bool>.Default`: **si el valor no cambia, no levanta `PropertyChanged`**.

En `RefrescarSugerenciasAsync`:

```csharp
Suggestions     = new ObservableCollection<ProductoDto>(r.Value!);  // cambia, pero nadie escuchaba
ShowSuggestions = r.Value!.Count > 0;                               // true → true: NO notifica
HighlightIndex  = -1;
```

Con el popup ya abierto, `ShowSuggestions` quedaba pegado en `true`. `ActualizarSuggestions()` nunca corría, `SearchBox.SuggestItems` seguía apuntando a la lista vieja y `OnSuggestItemsChanged` — el único lugar del control que abre o refresca el popup — no se disparaba. **Ningún View escuchaba `nameof(...Suggestions)`**, verificado en las 9.

Eso explica el síntoma exacto: borrar todo el texto lleva `ShowSuggestions` a `false`, y el siguiente tecleo hace la transición `false → true` que sí notifica.

Es la misma familia que el "bug #3" de [[Sesión 2026-05-29 - SuggestionSearchBox Compartido y Fix 5 Bugs Buscador]] (una propiedad que no notifica cuando el valor no cambia), pero sobre `ShowSuggestions` en vez de `HighlightIndex`.

### Bug secundario encontrado en el camino

`SeleccionarSugerencia(...)` asigna al **campo** `_query = ""`, no a la propiedad, así que no entra al setter y **el `_searchCts` en vuelo nunca se cancelaba**. Si el usuario pulsaba Enter con una búsqueda en curso, esa búsqueda terminaba después de la selección y ejecutaba `ShowSuggestions = true` (`false → true`, que sí notifica) → el popup se reabría solo con la caja de texto ya vacía.

Era especialmente probable justo por el bug principal: la lista visible era la anterior, así que el usuario tendía a seleccionar mientras había una búsqueda nueva en vuelo.

---

## Cambios aplicados

18 líneas en 18 archivos. `SuggestionSearchBox.xaml` / `.xaml.cs` **no se tocaron** — el control ya hacía lo correcto.

### 1. Las vistas escuchan `Suggestions` (9 archivos `*View.xaml.cs`)

```csharp
case nameof(ProductosViewModel.Suggestions):     ActualizarSuggestions(); break;
case nameof(ProductosViewModel.ShowSuggestions): ActualizarSuggestions(); break;
```

`Suggestions` se reasigna a una **instancia nueva** de `ObservableCollection` en todos los caminos de `RefrescarSugerenciasAsync`, así que la comparación por referencia siempre difiere y siempre notifica.

Se mantienen **los dos** `case` a propósito: el camino de error del repositorio (`if (!r.Success) { ShowSuggestions = false; return; }`) no reasigna `Suggestions`, así que `ShowSuggestions` sigue siendo necesario para cerrar el popup ante un fallo.

`ActualizarSuggestions()` **no se modificó**: su condición `(_vm.ShowSuggestions && _vm.Suggestions.Count > 0)` sigue siendo la correcta.

En `ContactosProveedoresView` y `ContactosFabricantesView` se usaron etiquetas apiladas (`case A: case B:`) para respetar el formato multilínea que ya tenían.

### 2. Cancelar el debounce al seleccionar (9 archivos `*ViewModel.cs`)

```csharp
public void SeleccionarSugerencia(ProductoDto p)
{
    _searchCts?.Cancel();   // ← nuevo
    _query = "";
    OnPropertyChanged(nameof(Query));
    ShowSuggestions = false;
    ...
}
```

Seguro: `RefrescarSugerenciasAsync` ya empieza con `_searchCts?.Cancel()` y reemplaza el CTS, y el `catch (OperationCanceledException) { }` absorbe la cancelación.

### Archivos tocados

`CapaUI/Formularios/Principal/Pantallas/` → `Productos/`, `Proveedores/`, `Fabricantes/`, `Categorias/`, `ContactosProveedores/`, `ContactosFabricantes/`, `Usuarios/`, `Empleados/`, `Bitacora/` — el par `*View.xaml.cs` + `*ViewModel.cs` de cada uno.

---

## Impacto en rendimiento

Ninguno negativo, y algo positivo:

- **Cero llamadas de red adicionales.** Las búsquedas ya se ejecutaban; el resultado se descartaba. Ahora se usa.
- Costo de CPU añadido: un `Select().ToList()` sobre ≤10 items en las transiciones de estado.
- El debounce de 300 ms, el `CancellationTokenSource`, el `_loadGeneration` y el `Limit(10)` server-side quedan **intactos**.

---

## Verificación

`dotnet build BimboProyecto.sln` → **0 errores**, 45 advertencias (todas `CS8618` preexistentes en `CapaDatos`; ninguna en `CapaUI`).

Prueba manual pendiente de ejecución por el usuario:

1. **Productos:** escribir `Y49`, esperar el popup, escribir el `0` → la lista y el contador "N coincidencias" deben cambiar sin cerrar el popup. Backspace → debe volver a los resultados de `Y49`.
2. Término sin coincidencias → el popup cierra. Volver a escribir algo válido → reabre.
3. **Regresión del bug secundario:** escribir rápido y pulsar Enter sobre una sugerencia antes de que termine el debounce → el popup no debe reabrirse solo con la caja vacía.
4. **Regresión de 2026-05-29:** ↓↑ con wrap-around, Enter selecciona, Escape limpia, hover pinta el item, popup con 10 sugerencias sin cortar el último.
5. Repetir el paso 1 en **Usuarios** y **Bitácora**, que tienen variantes de repositorio distintas (vista SQL `vista_usuarios_busqueda` y orden por fecha DESC).

---

## Lo que NO cambió

- **`SuggestionSearchBox.xaml` / `.xaml.cs`** — el control ya se comportaba bien; el defecto estaba en cómo lo alimentaban las vistas.
- **Repositorios, `CapaDatos`, `CapaAplicacion`, SQL** — sin tocar.
- **`SelectorProductosModal` (Pesaje)** — no estaba afectado: usa el control solo como caja de texto estilizada, sin popup, y sus resultados van a una tabla. Fue un contraste diagnóstico útil.
- **Buscador universal** (`UniversalSearchViewModel` + MediatR) — otro pipeline, no comparte código.
- **La duplicación 9× del patrón** — registrada como **P-026**, no refactorizada.

---

## Correcciones de documentación

Además del fix, se corrigieron notas que documentaban el patrón defectuoso (si no, se vuelve a introducir al replicar un módulo):

- [[Módulo Productos]] — callout nuevo con los **dos** `case`; el diagrama de flujo de sugerencias ahora muestra el paso por `ActualizarSuggestions()`; la sección "Highlight de sugerencias" mostraba el `VisualTreeHelper` que se eliminó al resolver P-005 en 2026-05-28.
- [[Paginación y Búsqueda - Arquitectura Detallada]] §4 — el diagrama terminaba en `ShowSuggestions = true → Popup visible`, que era justo la premisa falsa; `SeleccionarSugerencia` ahora incluye el `_searchCts?.Cancel()`.
- [[CLAUDE]] — sección "Búsqueda con sugerencias" reescrita; también decía `VisualTreeHelper` / `ItemsControl`.
- [[Deuda Técnica - Pendientes]] — nuevo **P-026**.

---

## Relaciones

- [[Módulo Productos]] — patrón de referencia corregido
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura del buscador por formulario
- [[Deuda Técnica - Pendientes]] — origen de P-026
- [[Sesión 2026-05-29 - SuggestionSearchBox Compartido y Fix 5 Bugs Buscador]] — origen del control y de los 5 bugs previos
- [[Sesión 2026-05-28 - Refactor P004-P005]] — eliminación del `VisualTreeHelper`
- [[Sesión 2026-07-26 - Fix Buscador Usuarios (SuggestionSearchBox)]] — la vez anterior que el patrón se replicó mal
- [[Módulo Usuarios]]
- [[Módulo Bitácora]]
- [[Arquitectura Actual]]
