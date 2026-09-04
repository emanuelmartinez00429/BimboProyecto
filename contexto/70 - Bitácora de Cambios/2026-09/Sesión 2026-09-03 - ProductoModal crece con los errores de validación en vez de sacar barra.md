---
title: "Sesión 2026-09-03 — ProductoModal crece con los errores de validación en vez de sacar barra"
tags:
  - sesion
  - ui
  - wpf
  - validacion
  - modales
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando
---

# Sesión 2026-09-03 — ProductoModal crece con los errores de validación en vez de sacar barra

> [!success] Resultado
> Al validar en "Crear/Editar producto", los renglones de error ("El código es
> obligatorio.", etc.) ahora **agrandan el marco del modal** en vez de dejarlo
> clavado y sacar un scrollbar en el `FormHost`. El alto fijo que necesita la
> tabla del selector (lupa) se mantiene, pero solo mientras esa tabla está abierta.

---

## Causa

`ProductoModal.OnLoaded` llamaba a `FijarAlturaOriginal()`, que **congelaba
`RootGrid.Height` al alto del formulario recién cargado (sin errores) para
siempre**. Ese congelamiento existe para que la fila `*` de la tabla del
`SelectorCatalogoModal` tenga una altura de verdad al hacer el swap
formulario ↔ tabla y no crezca/encoja al filtrar.

`ValidadorFormulario.MostrarRenglon()` inserta un `TextBlock` bajo el campo
(`panel.Children.Add(...)`), así que el contenido crece — pero como
`RootGrid.Height` estaba clavado, el `ScrollViewer` `FormHost` sacaba la barra y
el marco nunca crecía.

## Cambio (`ProductoModal.xaml.cs`)

Se movió el congelamiento de *"permanente, al cargar"* a *"solo mientras el
selector está abierto"* — el mismo patrón que ya usa `FabricanteModal`.

- `OnLoaded`: se quitó la llamada a `FijarAlturaOriginal()` y se eliminó el
  método. El formulario se auto-dimensiona; el `MaxHeight` del XAML lo acota al
  hueco disponible y la barra queda como último recurso real (ventana muy chica).
- `AbrirSelector`: `RootGrid.Height = RootGrid.ActualHeight` **antes** de colapsar
  `FormHost` — congela el marco al alto que tiene el formulario en ese momento
  (con o sin errores visibles, da igual). La tabla lo llena y no crece al filtrar.
- `CerrarSelector`: `RootGrid.Height = double.NaN` — libera el marco.
- `OverlayAncestor_SizeChanged`: ya no re-congela en cada resize. Solo actúa si el
  **selector está abierto** y la ventana se achicó por debajo del alto fijo: baja
  `RootGrid.Height` al disponible (`overlay.ActualHeight - 48`) y el scroll interno
  de la tabla absorbe el recorte. Con el formulario a la vista no hace nada porque
  el `MaxHeight` del XAML ya sigue el tamaño disponible.
- Se quitó `using System.Windows.Threading;` (solo lo usaba `FijarAlturaOriginal`).

No se tocó el XAML, ni `ValidadorFormulario`, ni `SelectorCatalogoModal`.

## Verificación

- `dotnet build CapaUI` → sin errores/advertencias de código (build completo
  bloqueado por la app en ejecución).
- `dotnet test` → 244/244.
- **Pendiente prueba manual**: en "Crear producto", tocar Guardar con campos
  vacíos → el modal debe **crecer** para mostrar los renglones de error, sin
  scrollbar (salvo ventana muy chica). Abrir una lupa → el marco queda fijo y la
  tabla no salta al filtrar. Volver → el marco se libera y sigue creciendo con
  nuevos errores.

## Pendiente / seguimiento

- `CamionModal` y `ProductoCamionModal` (Pesaje) tienen el mismo patrón de alto
  fijo (`_altoPropio` / `Height` declarado) y probablemente el mismo síntoma con
  la validación. No se tocaron (el usuario reportó solo ProductoModal y Pesaje
  tiene su propia deuda de migración). Aplicar el mismo criterio si se confirma.

## Relaciones

- [[Selector de Catálogo - Selector genérico y multiselección]] — el "freeze temporal de altura" del host
- [[Anatomía compartida de los modales]]
- [[Validacion de formularios]] — `ValidadorFormulario` inserta el renglón de error bajo el campo
- [[Sesión 2026-09-03 - FabricanteModal usa selector de proveedor por tabla]] — mismo patrón de freeze solo-durante-selector
