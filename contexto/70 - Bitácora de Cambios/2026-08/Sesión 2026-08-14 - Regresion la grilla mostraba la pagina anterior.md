---
title: "Sesión 2026-08-14 — Regresión: la grilla mostraba las filas de la página anterior con el número nuevo"
tags:
  - sesion
  - paginacion
  - regresion
  - bugfix
  - mvvm
date: 2026-08-14
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Opus 5 (Claude Code)
---

# Sesión 2026-08-14 — Regresión: la grilla mostraba las filas de la página anterior con el número nuevo

> [!failure] Esto fue una regresión propia
> La causó el fix de [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]], hecho unas horas antes el mismo día. No fue un bug preexistente. Vale la pena leer las dos notas juntas.

> [!success] Resultado
> `RefrescarPaginacion()` dejó de rebindear la grilla; ahora reconstruye solo el árbol de botones. El rebind de filas quedó donde corresponde: el `case` de `PageRows`. Build: `0 errores`, 2 advertencias preexistentes (`RolesViewModel`, sin relación).

---

## Problema reportado

> "hay un bug las paginaciones al navegar me están mostrando páginas erróneas o repetidas más que todo en las de final"

Al repreguntar, el usuario precisó lo que resultó ser el dato decisivo:

> "sale el número correcto pero la página es la misma que la anterior"

O sea: los botones numerados dibujaban bien y el resaltado avanzaba, pero la grilla seguía mostrando las filas de la página anterior.

---

## Diagnóstico

El fix de la sesión anterior agregó `case nameof(XxxViewModel.TotalPages): RefrescarPaginacion(); break;` al switch de `OnVmPropertyChanged`. El problema es que **`RefrescarPaginacion()` mezclaba dos responsabilidades** que hasta ese momento siempre ocurrían juntas:

```csharp
private void RefrescarPaginacion()
{
    if (_vm == null) return;
    DgProductos.ItemsSource = _vm.PageRows;   // ← (a) rebind de la GRILLA
    PaginacionPanel.Items.Clear();            // ← (b) reconstrucción de los BOTONES
    ...
}
```

Y el setter de `Page` (`ProductosViewModel.cs:179-192`) notifica `TotalPages` **incondicionalmente y antes de salir a la red**:

```csharp
set {
    if (_page == value) return;
    _page = value;                          // (1) número nuevo
    OnPropertyChanged();
    OnPropertyChanged(nameof(PageInfo));
    OnPropertyChanged(nameof(TotalPages));  // (2) ← el case nuevo dispara ACÁ
    NotifyPaginationCanExecuteChanged();
    _ = CargarPaginaAsync();                // (3) recién acá arranca el viaje de red
}
```

Secuencia al hacer clic en un botón de página:

1. `_page = 11`
2. `OnPropertyChanged(nameof(TotalPages))` → el `case` nuevo → `RefrescarPaginacion()`
3. `DgProductos.ItemsSource = _vm.PageRows` → **las filas todavía son las de la página 10**
4. Botones reconstruidos con `current = 11` → **número correcto**
5. Recién ahora `CargarPaginaAsync()` sale a la red (~300-600 ms a `us-west-2`)
6. Al volver: `PageRows = new ObservableCollection(...)` → `case PageRows` → filas correctas

Entre el paso 3 y el 6, la pantalla muestra literalmente **el número nuevo con las filas viejas**.

Detalle relevante: `TotalPages` **no cambió** en el paso 2 — deriva de `_filteredCount`, que el setter no toca. Ese aviso siempre fue espurio; era inofensivo hasta que se le enganchó `RefrescarPaginacion()`.

**Por qué se quedaba pegado y no era solo un parpadeo:** `CargarPaginaAsync` tiene cuatro returns tempranos que **no reasignan `PageRows`** — timeout (`:344-350`), generación invalidada (`:346`, `:353`) y `!r.Success` (`:355-360`). Si la carga sale por cualquiera de ellos, el `case PageRows` nunca dispara y nadie corrige la grilla. Eso explica el "más que todo en las de final": las páginas altas tardan más y tienen más chance de timeout o de que una revalidación de Realtime les pise la generación.

---

## Corrección

Separar las dos responsabilidades. `RefrescarPaginacion()` se ocupa **solo** del árbol de botones; el rebind de la grilla vive en el `case` de `PageRows`, que es el único momento en que hay filas nuevas:

```csharp
case nameof(ProductosViewModel.PageRows):
    DgProductos.ItemsSource = _vm.PageRows;   // único punto donde hay filas nuevas
    RefrescarPaginacion();
    break;
case nameof(ProductosViewModel.TotalPages):
    RefrescarPaginacion();                     // solo el árbol de botones
    break;
```

Con esto el clic repinta el árbol al instante (el resaltado avanza, feedback inmediato) y la grilla cambia recién cuando llegan los datos. El fix de Realtime de la sesión anterior sigue intacto: un INSERT que crece `TotalPages` sin tocar `PageRows` reconstruye los botones sin tocar la grilla — que era exactamente lo que se buscaba.

**Archivos modificados (4, no 6):**

- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriasView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricantesView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml.cs`

> [!note] ContactosFabricantes y ContactosProveedores no necesitaron cambio
> El plan asumía que también tenían el rebind dentro de `RefrescarPaginacion()`. Al ir a tocarlos se verificó que **no**: su `RefrescarPaginacion()` (línea 271) nunca rebindeó la grilla, y el rebind ya estaba en el `case PageRows` (línea 62). Es decir, **esos dos módulos ya tenían la separación correcta desde antes** — el patrón que se aplicó a los otros cuatro no se inventó acá, se copió de ellos. La tercera ocurrencia de `DgX.ItemsSource = _vm.PageRows` en esos archivos (línea 201) está en `BtnVolver_Click`, que es un rebind legítimo al volver del panel de contactos al de la lista.

`Usuarios`, `Empleados` y `Bitacora` sí tienen el rebind dentro de su `RefrescarPaginacion()`, pero **no** recibieron el `case TotalPages` (no tienen Realtime), así que no exhiben el bug. Se dejaron sin tocar para no ampliar el alcance — anotado en P-037.

---

## Lección: por qué no se vio venir

El fix anterior se evaluó como "1 línea, idempotente, sin llamadas de red nuevas" — y las tres cosas eran ciertas. Lo que no se revisó fue **qué más hacía el método que se estaba invocando**. `RefrescarPaginacion()` tenía un efecto secundario (rebindear la grilla) que era invisible mientras su único disparador fuera `PageRows`, porque en ese caso el efecto siempre era correcto. Agregar un segundo disparador con otra semántica temporal lo convirtió en bug.

Regla que queda: **antes de agregar un disparador nuevo a un método existente, leer el método entero**, no solo confiar en su nombre. Un método llamado `RefrescarPaginacion` que además toca la grilla no anuncia ese efecto en su nombre.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**, 2 advertencias preexistentes (`RolesViewModel.cs`, firma de método parcial — sin relación).
- **Pendiente en runtime**: navegar con los botones numerados hacia las páginas del final en Productos y confirmar que el resaltado avanza **y** las filas corresponden a esa página. Comparar el primer registro contra `select id_producto, nombre_producto from productos order by id_producto offset 500 limit 5;` (página 11 con `PageSize=50`). Repetir en Fabricantes o Categorías.
- **Pendiente en runtime también**: confirmar que el fix original de Realtime sigue vivo — con la pantalla en la última página, insertar filas por SQL hasta cruzar el múltiplo de 50; los botones deben crecer solos sin recargar el módulo y la grilla **no** debe cambiar de filas.

## Lo que NO se hizo

- No se tocó el `OnPropertyChanged(nameof(TotalPages))` espurio del setter de `Page`. Con la separación de responsabilidades ya no hace daño, y encima es lo que hace que el resaltado del botón avance al instante al hacer clic. Sacarlo requeriría agregar un `case nameof(Page)` — más cambio del necesario para cerrar este bug.
- No se corrigieron los tres hallazgos secundarios de la investigación (ver P-037).

## Estado en git

Sin commitear al cierre de esta nota. El árbol de trabajo acumula varios trabajos del mismo día (catálogo de `unidad_medida`, logo de empresa, Realtime en columnas de join, el fix de paginación anterior y este). Separar los commits queda a criterio del usuario.

---

## Relaciones

- [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]] — el fix que causó esta regresión
- [[Sesión 2026-05-26 - Realtime Silent Refresh Productos]] — origen de `CargarPaginaSilenciosamenteAsync`
- [[Checklist - Replicar Módulo con Realtime]] — actualizado con la advertencia
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Deuda Técnica - Pendientes]] — origen de P-037
- [[Módulo Productos]]
