---
title: "Sesión 2026-07-23 — Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock"
tags:
  - sesion
  - animacion
  - sidebar
  - wpf
  - deuda-tecnica
  - regresion
date: 2026-07-23
branch: feat/fase6-IntegracionWpf/MenuPrincipal
---

# Sesión 2026-07-23 — Reconciliación documentación vs. código real

> [!warning] Motivo de esta sesión
> [[Sesión 2026-05-26 - Optimización Animación Sidebar]] documentó la animación del hamburger/sidebar tal como quedó el **2026-05-26**. Desde entonces el código se modificó **dos veces más sin actualizar la documentación**:
> 1. Se introdujo la técnica de "pinchar" `ContentArea.Width` al tamaño final durante la animación (no documentada, ya reemplazada).
> 2. Commit `2f5489d` (2026-07-18, branch `feat/fase6-IntegracionWpf/MenuPrincipal`) — *"se arreglo el problema del burger bar borrando visualmente el contenido y recargandolo con una animación ligera"* — reemplazó esa técnica por ocultar/mostrar `ContentAreaBorder` completo con `Visibility` + fade de opacidad, y **de paso eliminó la sincronización de ancho de `BrandBlock`** (ver regresión abajo). Ninguno de los dos cambios quedó registrado en el vault.
>
> Este documento describe el estado **actual y real** del código (verificado en `CapaUI/Formularios/Principal/MainWindow.xaml.cs`) y reemplaza el timeline de la sesión 2026-05-26 como referencia vigente.

---

## Qué cambió respecto a lo documentado el 2026-05-26

| Aspecto | Documentado (2026-05-26) | Código actual (commit `2f5489d`) |
|---|---|---|
| Contenido de la pantalla durante la animación de ancho | No se tocaba — solo se pinchaba `ContentArea.Width` al valor final para evitar relayout | Se oculta por completo: fade-out (50ms) → `Visibility.Collapsed` → tras la animación, `Visibility.Visible` + fade-in (80ms) |
| `BrandBlock` (bloque de marca en la top bar, `Width="226"` fijo en XAML) | Se animaba en paralelo a `Sidebar` con `AnimateWidth(BrandBlock, …, 160)` en ambas direcciones | **Ya no se anima en ningún punto** — la llamada fue eliminada en `2f5489d` |
| `ContentArea.Width` pinning | No existía todavía en la versión documentada (se agregó después, tampoco documentado, y ya fue removido) | Removido; ahora se maneja vía `ContentAreaBorder.Visibility` |

---

## 🔴 Regresión encontrada: `BrandBlock` ya no sincroniza su ancho con `Sidebar`

`CapaUI/Formularios/Principal/MainWindow.xaml:110` define:

```xml
<!-- BRAND BLOCK (mismo ancho que sidebar, anima junto) -->
<Border x:Name="BrandBlock" Grid.Column="0" Width="226" ... >
```

El comentario del propio XAML dice explícitamente que `BrandBlock` debe animar junto con `Sidebar`. Antes del commit `2f5489d`, `CollapseSidebar()`/`ExpandSidebar()` llamaban:

```csharp
AnimateWidth(Sidebar,    SidebarCollapsed, 160);
AnimateWidth(BrandBlock, SidebarCollapsed, 160);
```

El commit `2f5489d` dejó solo la línea de `Sidebar` y quitó la de `BrandBlock` (ver diff completo con `git show 2f5489d -- CapaUI/Formularios/Principal/MainWindow.xaml.cs`). No hay ningún binding ni trigger XAML que reemplace esa sincronización — se confirmó que `BrandBlock` no aparece en ningún otro punto del proyecto salvo su declaración.

**Efecto visible:** al colapsar el sidebar a 72px, el bloque de marca en la barra superior (logo + botón hamburger) se queda fijo en 226px — quedan desalineados verticalmente respecto al sidebar colapsado.

Se registró como **P-009** en [[Deuda Técnica - Pendientes]].

---

## Timeline actual verificado (`CollapseSidebar`)

```
t=0ms     AnimateOpacity(ContentAreaBorder, 0, 50)
t=55ms    ContentAreaBorder.Visibility = Collapsed
          Fade-out etiquetas/chevrones (70ms, SineEase.EaseOut) + AnimateWidth(Sidebar, 72, 160, QuarticEase.EaseOut)
t=130ms   Visibility.Collapsed en etiquetas (ya invisibles) + restore Opacity=1
          CompactUserCard fade-in (60ms)
t=230ms   ContentAreaBorder.Visibility = Visible, Opacity=0 → fade-in (80ms)
t=310ms   _animating = false
```

## Timeline actual verificado (`ExpandSidebar`)

```
t=0ms     ContentAreaBorder.Visibility = Collapsed   (sin fade — ver nota abajo)
          CompactUserCard fade-out (60ms)
t=65ms    CompactUserCard.Visibility = Collapsed
          Etiquetas Visible con Opacity=0 + AnimateWidth(Sidebar, 226, 160, QuarticEase.EaseOut)
t=165ms   Fade-in etiquetas (80ms) + reabrir submenú activo si aplica
t=245ms   ContentAreaBorder.Visibility = Visible, Opacity=0 → fade-in (80ms)
t=325ms   _animating = false
```

> [!note] Asimetría Collapse vs. Expand
> `CollapseSidebar` desvanece `ContentAreaBorder` antes de ocultarlo (`AnimateOpacity` + `Task.Delay(55)`). `ExpandSidebar` lo oculta de golpe (`ContentAreaBorder.Visibility = Visibility.Collapsed` sin fade previo) porque al entrar a esta función el contenido ya está visible desde el `Collapse` anterior. Es intencional (evita esperar un fade-out redundante), pero no está documentado como tal — ver duda en [[Deuda Técnica - Pendientes]] P-010.

Duración total subió de **~165–180ms** (documentado 2026-05-26) a **~310–325ms** (actual) por las dos fases nuevas de `ContentAreaBorder`.

---

## Actualización de referencia de easings

Se agregaron filas nuevas a la tabla de producción en [[Animaciones WPF - Referencia de Easings]] para el fade de `ContentAreaBorder` (no existían el 2026-05-26).

---

## Lo que NO cambió

- `AnimateWidth`, `AnimateSubMenu`, `AnimateChevron`, `AnimateOpacity` — mismos easings y duraciones base documentados en [[Animaciones WPF - Referencia de Easings]]
- Guard `_animating` — sigue existiendo, mismo problema de cobertura parcial ya que `BtnModulo_Click` no lo verifica antes de invocar `ExpandSidebar()` (ver P-011)
- Estructura de `_moduleMap` / `_subMap`

---

## Resolución aplicada (mismo día, 2026-07-23)

Los 4 hallazgos se corrigieron en `CapaUI/Formularios/Principal/MainWindow.xaml.cs`, sin tocar XAML y sin cambiar ningún timing/easing existente:

- **P-009:** nuevo helper de instancia `AnimateSidebarWidth(double to)` — anima `Sidebar` y `BrandBlock` juntos desde un único call site. Ya no pueden desincronizarse por un cambio futuro que solo toque una de las dos líneas.
- **P-010:** nuevo campo `private readonly UIElement[] _sidebarChromeElements`, poblado en el constructor tras `InitializeComponent()`. Los 4 bloques de 12 líneas repetidas en `CollapseSidebar()`/`ExpandSidebar()` se reemplazaron por `foreach` sobre este array.
- **P-011:** `if (_animating) return;` agregado como primera línea de `BtnModulo_Click`, igual que ya tenía `BtnHamburger_Click`.
- **P-012:** 15 constantes de duración con nombre (`ContentFadeOutMs`, `ChromeFadeOutMs`, `SidebarWidthAnimMs`, `SubMenuOpenMs`, `SubMenuCloseMs`, `ChevronRotateMs`, etc.) agrupadas junto a `SidebarExpanded`/`SidebarCollapsed`/`SubItemHeight`, reemplazando los literales en `Task.Delay`/`AnimateOpacity`/`AnimateWidth`/`AnimateSubMenu`/`AnimateChevron`.

Verificado con `dotnet build CapaUI/CapaUI.csproj` → **0 errores** (44 warnings preexistentes de nullable en `CapaDatos`, no relacionados). Pendiente: prueba visual manual en la app corriendo (colapsar/expandir sidebar, click en módulo estando colapsado, doble-click rápido).

Los timelines de arriba (t=0…310ms colapsar, t=0…325ms expandir) siguen siendo la referencia vigente — el refactor es estructural, no cambia ninguna duración.

---

## Bug adicional encontrado y resuelto el mismo día: flash blanco con modal abierto

**Síntoma:** si el usuario colapsaba/expandía el sidebar mientras tenía un modal abierto (`ModalOverlay` visible en la View activa — Productos, Fabricantes, Categorías, Proveedores, Contactos, Pesaje), aparecía un flash blanco en vez de mantenerse oscuro.

**Causa:** la Fase 0/3 de `CollapseSidebar()`/`ExpandSidebar()` oculta `ContentAreaBorder` (`Visibility.Collapsed`) para aligerar el layout durante el resize del sidebar (ver arriba). `ContentAreaBorder` no tiene `Background` propio, así que al desaparecer, TODO lo que contiene — la View y su `ModalOverlay` semitransparente oscuro — desaparece con él, revelando el `Background="White"` del `Border` raíz de la ventana. Con un modal abierto (fondo oscuro), ese hueco blanco es un flash muy notorio; sin modal, el hueco es casi imperceptible porque las Views ya usan fondos claros.

**Arreglo — sin tocar ninguna animación:**
- Nuevo `Border x:Name="ContentBackdrop"` en `MainWindow.xaml`, mismo `Grid.Column="1"` que `ContentAreaBorder` pero declarado **antes** (pinta por debajo), `Background="Transparent"` por defecto — invisible en el caso normal porque `ContentAreaBorder` lo tapa completamente.
- `ObtenerModalOverlayBrush()` en el code-behind: como `ContentArea.Content` es el ViewModel (la View real la instancia el `DataTemplate`), se busca el elemento `ModalOverlay` recorriendo el árbol visual ya renderizado (`FindNamedChild`, `VisualTreeHelper`) y, si está `Visible`, devuelve su **`Background` real** (`Border.Background`) en vez de un color inventado.
- Al inicio de `CollapseSidebar()`/`ExpandSidebar()`, si hay un `Brush` de modal, se lo asigna directo a `ContentBackdrop.Background` — mismo semitransparente exacto que ya usa esa View (`#990F172A` en la mayoría, `#8C0F172A` en Pesaje). Al terminar la fase de restauración, se vuelve a `Transparent`.
- **Primer intento (corregido):** se probó primero con un `SolidColorBrush` fijo opaco (`#0F172A`) — se veía notoriamente más oscuro que la sombra real del modal (que es semitransparente, `~60%` alfa), porque durante el hueco no hay contenido detrás que le dé la misma profundidad. Reusar el `Brush` real de `ModalOverlay` en vez de inventar un tono lo deja indistinguible de la sombra normal.
- Ningún `Task.Delay`, `AnimateOpacity`/`AnimateWidth`/easing existente se tocó — es puramente un fondo condicional detrás del hueco.

Verificado con `dotnet build CapaUI/CapaUI.csproj` → 0 errores, 0 advertencias.

---

## Relaciones

- [[Sesión 2026-05-26 - Optimización Animación Sidebar]] — versión anterior documentada, ahora desactualizada en cuanto al manejo de `ContentAreaBorder` y `BrandBlock`
- [[Animaciones WPF - Referencia de Easings]] — tabla de timings actualizada
- [[Deuda Técnica - Pendientes]] — P-009 a P-012, resueltos el mismo día
- [[Arquitectura Actual]] — MainWindow en la capa UI
