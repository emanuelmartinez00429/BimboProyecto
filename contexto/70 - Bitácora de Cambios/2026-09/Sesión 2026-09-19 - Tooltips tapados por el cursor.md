---
title: "Sesión 2026-09-19 — Tooltips tapados por el cursor"
tags:
  - sesion
  - wpf
  - ui
  - tooltip
date: 2026-09-19
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente) con Fernando
---

# Sesión 2026-09-19 — Tooltips tapados por el cursor

> [!success] Resultado
> En toda la app, el puntero del mouse (la mano de los botones) ya no tapa el tooltip: sale pegado bajo la punta del cursor. Se resolvió con un desfase vertical global, verificado con hover real y aprobado visualmente por Fernando.

## Síntoma

Al pasar el mouse por un botón o celda con tooltip, la mano quedaba encima de la esquina superior izquierda del tooltip y tapaba parte del texto.

## Causa raíz

WPF ubica el tooltip (`PlacementMode.Mouse`, el valor por defecto) unos **17 px por debajo del punto caliente del mouse**, sin mirar cuánto mide el cursor que se está dibujando. La flecha estándar cabe en esa distancia; la mano (`Cursors.Hand`) mide **32 px**, así que pisaba unos 15 px del borde superior del tooltip.

Medido con un banco de pruebas con hover real (`SetCursorPos` + lectura del rectángulo de la ventana del popup):

| Configuración | Distancia mouse → borde superior del tooltip | Solape con la mano |
|---|---|---|
| WPF por defecto | 17 px | 15 px |
| `Placement=Bottom` | 20 px (botón de 40 px) | 12 px |
| `VerticalOffset` global de 17 | 34 px | ninguno (2 px de aire) |

`Placement=Bottom` se descartó: en elementos chicos sigue solapando.

## Intento descartado: usar el tamaño de cursor de Windows

La primera versión leía `CursorBaseSize` del registro (Fernando lo tiene en 48) y sumaba un desfase de 35 px. El tooltip quedó **demasiado lejos** (52 px del punto del mouse): la mano que dibuja la app mide 32 px aunque Windows tenga el cursor agrandado. Se cambió a un valor fijo calculado para el cursor real.

## Solución

- **`CapaUI/Core/ToolTipPlacement.cs`** (nuevo): `Configurar()` sobreescribe el valor por defecto de `ToolTipService.VerticalOffsetProperty` para `FrameworkElement` con `AltoCursor (32) − SeparacionPorDefectoWpf (17) + Margen (2) = 17`.
- **`CapaUI/App.xaml.cs`**: `ToolTipPlacement.Configurar()` en `OnStartup`, justo después de `base.OnStartup(e)` y antes de crear cualquier ventana.

Se eligió `OverrideMetadata` y no un `Style TargetType="ToolTip"` implícito: en la prueba con hover real el estilo no llegó a los tooltips de texto que WPF crea solo, mientras que la metadata sí.

## Alcance

Aplica a **todos** los tooltips de la app (Login, Dashboard, Pesaje, Productos, Roles, modales…). Se verificó por grep que ninguna pantalla fija su propio `ToolTipService.VerticalOffset` ni un `Placement` de tooltip, así que no hay nada que le gane al ajuste global. No afecta a los `Popup` que no son ToolTip (sugerencias del buscador, ComboBox). Un control puede seguir fijando su propio `ToolTipService.VerticalOffset`.

## Ajuste futuro

Si un tooltip queda muy separado o todavía roza la mano, ajustar la constante `Margen` en `ToolTipPlacement.cs`.

## Verificación

- **Compilación:** `dotnet build CapaUI/CapaUI.csproj` → 0 errores, 0 advertencias.
- **Medición:** banco de pruebas con hover real (ver tabla), repetido tras cada cambio de valor.
- **Prueba visual:** verificada por Fernando en la app.
- **Sin tests xUnit:** es una configuración de presentación de WPF; no hay lógica de dominio que probar.
