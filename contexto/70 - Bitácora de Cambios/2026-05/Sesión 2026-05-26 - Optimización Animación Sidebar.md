---
title: Sesión 2026-05-26 — Optimización Animación Sidebar (Hamburger)
type: sesion
status: vigente
tags:
  - sesion
  - animacion
  - sidebar
  - ux
  - wpf
date: 2026-05-26
updated: 2026-07-23
summary: La animación del hamburger (colapsar/expandir sidebar) pasó de trabarse en el inicio a arrancar desde el primer frame. Se agregó fade cruzado de etiquetas para…
scope:
  - CapaUI/Formularios/Principal
symbols:
  - AnimateOpacity
  - AnimateSubMenu
  - AnimateWidth
  - BrandBlock
  - CollapseSidebar
  - ContentAreaBorder
  - EaseInOut
  - ExpUsuarios
  - ExpandSidebar
  - LblModuloUsuarios
---

# Sesión 2026-05-26 — Optimización Animación Sidebar

> [!warning] Desactualizado — ver [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]]
> El código avanzó dos veces más después de esta sesión (manejo de `ContentAreaBorder` durante la animación de ancho, y eliminación no documentada de la sincronización de `BrandBlock`). Los easings y duraciones base de esta sesión siguen vigentes; el timeline completo y los cambios de `Visibility` NO reflejan el código actual.

> [!success] Resultado
> La animación del hamburger (colapsar/expandir sidebar) pasó de trabarse en el inicio a arrancar desde el primer frame. Se agregó fade cruzado de etiquetas para eliminar el salto visual. Sin cambios en XAML ni en posición de iconos.

---

## Problema original

### Síntoma
Al hacer clic en el botón hamburger para ocultar el menú, la animación se "trababa" — parecía congelada al inicio, luego se movía bruscamente y volvía a trabarse al final.

### Causa raíz diagnosticada

**1. `CubicEase.EaseInOut` en `AnimateWidth`**

`EaseInOut` produce una curva en S:
- Primeros ~50ms: velocidad casi 0 → sidebar **parece congelado**
- Mitad: aceleración brusca
- Últimos ~50ms: frenada lenta → **parece trabado de nuevo**

```
EaseInOut:  ──── /‾‾‾‾\ ────   (lento → rápido → lento)
EaseOut:    ╲────────────       (rápido desde el inicio, frena suave)
```

**2. Visibility changes en frame 0**

Las etiquetas (`LblModuloUsuarios`, etc.) se ponían en `Visibility.Collapsed` **al inicio** de la animación. Durante los 200ms de animación de ancho, el sidebar mostraba iconos sueltos en un contenedor todavía ancho → salto visual brusco.

**3. Sin guard de doble click**

Si el usuario hacía clic rápido dos veces, se lanzaban dos animaciones encimadas que interferían entre sí.

**4. `AnimateSubMenu` también usaba `EaseInOut`**

Mismo problema en el acordeón: arranque lento y cierre lento (250ms).

---

## Cambios aplicados — `CapaUI/Formularios/Principal/MainWindow.xaml.cs`

### Cambio 1 — Flag `_animating`

```csharp
private bool _animating = false;
```

`BtnHamburger_Click` ahora retorna inmediatamente si una animación ya está en curso:
```csharp
private async void BtnHamburger_Click(object sender, RoutedEventArgs e)
{
    if (_animating) return;
    if (_collapsed) await ExpandSidebar();
    else            await CollapseSidebar();
}
```

---

### Cambio 2 — `AnimateWidth`: easing y duración

```csharp
// ANTES:
EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
// duración: 200ms

// DESPUÉS:
EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
// duración: 160ms (pasado como parámetro)
```

`QuarticEase.EaseOut` tiene una curva de cuarto grado que arranca cerca del 60% de la velocidad máxima en el primer frame, por eso se siente instantánea.

---

### Cambio 3 — `CollapseSidebar` → `async Task` con fade-out

**Antes:** Sincrónico. Visibility.Collapsed instantáneo + BeginAnimation simultáneo.

**Después:** Dos fases encadenadas con `await Task.Delay`:

```
t=0ms    Fade-out etiquetas (70ms) + AnimateWidth (160ms) — simultáneos
t=75ms   Visibility.Collapsed (ya invisibles → sin salto visual)
          Restore Opacity=1 (para próxima expansión)
          CompactUserCard fade-in (60ms)
t=165ms  _animating = false
```

Los iconos de módulo (dentro del DockPanel `ExpUsuarios` etc.) **no se tocan** — permanecen visibles durante toda la transición.

---

### Cambio 4 — `ExpandSidebar` → `async Task` con fade-in diferido

**Antes:** Sincrónico. Visibility.Visible instantáneo al inicio → texto aparecía en sidebar todavía estrecho.

**Después:** Tres fases:

```
t=0ms    CompactUserCard fade-out (60ms)
t=65ms   Visibility.Visible con Opacity=0 (invisible pero en layout)
          AnimateWidth (160ms)
t=100ms  Fade-in etiquetas (80ms) — sidebar ya casi expandido
t=180ms  _animating = false
```

El texto aparece cuando el sidebar ya tiene espacio → sin overflow ni truncamiento visible.

---

### Cambio 5 — `AnimateSubMenu`: easing y duración asimétrica

```csharp
// ANTES: 250ms EaseInOut (igual al expandir y contraer)
// DESPUÉS:
TimeSpan.FromMilliseconds(open ? 220 : 160)
EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
```

- Apertura: 220ms (más lento para que se vea que hay contenido)
- Cierre: 160ms (más rápido — el usuario ya vio el menú)

---

### Cambio 6 — `AnimateOpacity` (nuevo helper)

```csharp
private static void AnimateOpacity(UIElement target, double to, int ms)
{
    var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
    {
        EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
    };
    target.BeginAnimation(UIElement.OpacityProperty, anim);
}
```

`SineEase` para opacidad porque la curva sinusoidal es la más suave para fades perceptivos.

---

### Cambio 7 — `BtnModulo_Click` → `async void`

Para cuando el sidebar está colapsado y se hace clic en un módulo:

```csharp
// ANTES: ExpandSidebar() sync → OpenModule(id) simultáneo
// DESPUÉS:
await ExpandSidebar();   // espera a que termine la expansión
OpenModule(id);          // abre el submenú cuando el sidebar ya está completo
```

---

## Resumen de timings

| Animación | Antes | Después |
|---|---|---|
| Ancho sidebar colapsar | 200ms EaseInOut | 160ms EaseOut |
| Ancho sidebar expandir | 200ms EaseInOut | 160ms EaseOut |
| Etiquetas desaparecer | 0ms (instantáneo) | 70ms fade |
| Etiquetas aparecer | 0ms (instantáneo) | 80ms fade |
| Tarjeta compacta in/out | 0ms (instantáneo) | 60ms fade |
| Submenú cerrar | 250ms EaseInOut | 160ms EaseOut |
| Submenú abrir | 250ms EaseInOut | 220ms EaseOut |
| Chevrón rotar | 200ms EaseOut | 180ms EaseOut |

---

## Lo que NO cambió

- Posiciones de iconos — inalteradas
- XAML — sin tocar
- Estructura de `_moduleMap` y `_subMap`
- Lógica de estado (`_collapsed`, `_activeModuleId`, `_activeSubId`)
- Todos los demás event handlers (`BtnSub_Click`, `BtnHome_Click`, etc.)

---

## Relaciones

- [[Animaciones WPF - Referencia de Easings]] — nuevo documento de referencia de easings
- [[Arquitectura Actual]] — MainWindow en la capa UI
