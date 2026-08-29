---
title: Animaciones WPF — Referencia de Easings
type: patron
status: vigente
tags:
  - patron
  - wpf
  - animacion
  - ux
date: 2026-05-26
updated: 2026-07-23
summary: "Guía de qué EasingFunction usar en cada situación dentro del proyecto, basada en lo aprendido al optimizar la animación del sidebar. Incluye los timings reales…"
scope: []
symbols:
  - ContentAreaBorder
  - EaseIn
  - EaseInOut
  - EaseOut
  - EasingFunction
  - Height
  - Linear
  - MaxHeight
  - Opacity
  - QuarticEase
---

# Animaciones WPF — Referencia de Easings

> [!abstract]
> Guía de qué `EasingFunction` usar en cada situación dentro del proyecto, basada en lo aprendido al optimizar la animación del sidebar. Incluye los timings reales usados en producción.

---

## Regla principal: EaseOut vs EaseInOut

| Modo | Curva | Cuándo usar |
|---|---|---|
| `EaseOut` | Rápido → lento | **Siempre que el usuario inicia la acción** (click, tap). La respuesta debe sentirse instantánea. |
| `EaseIn` | Lento → rápido | Elementos que salen de pantalla por sí solos (notificaciones auto-dismiss). |
| `EaseInOut` | Lento → rápido → lento | Transiciones automáticas sin input del usuario (loading states, loops). **Evitar en respuesta a clicks** — el inicio lento parece lag. |

> [!danger] Anti-patrón
> `EaseInOut` en animaciones disparadas por click del usuario. Los primeros 50ms casi sin movimiento hacen que la UI parezca congelada ("trabada").

---

## Easings por tipo de propiedad animada

### Ancho / Alto (`Width`, `Height`, `MaxHeight`)

```csharp
// Colapsar o expandir un panel — respuesta a click
new QuarticEase { EasingMode = EasingMode.EaseOut }
// Duración: 150–180ms

// Acordeón (submenú) — apertura
new QuadraticEase { EasingMode = EasingMode.EaseOut }
// Duración: 200–220ms

// Acordeón (submenú) — cierre
new QuadraticEase { EasingMode = EasingMode.EaseOut }
// Duración: 150–180ms  (cierre siempre más rápido que apertura)
```

> [!tip] ¿Por qué `QuarticEase` para el sidebar?
> Potencia 4 → curva mucho más pronunciada al inicio. En los primeros 20ms ya recorrió ~50% del camino. Ideal cuando el cambio debe percibirse instantáneo (colapsar sidebar a 72px).

### Opacidad (`Opacity`) — fades

```csharp
// Fade in / fade out de texto, tarjetas, overlays
new SineEase { EasingMode = EasingMode.EaseOut }
// Duración: 60–100ms

// Fade del modal overlay
new QuadraticEase { EasingMode = EasingMode.EaseOut }
// Duración: 200ms entrada, 150ms salida
```

> [!tip] ¿Por qué `SineEase` para fades?
> La curva sinusoidal es la más parecida a la percepción humana de luminosidad. Los fades se sienten "naturales" sin abrupciones.

### Rotación (`RotateTransform.Angle`) — chevrones

```csharp
// Rotar chevrón de acordeón (0° → 180°)
new CubicEase { EasingMode = EasingMode.EaseOut }
// Duración: 180ms
```

### Posición (`TranslateTransform`) — elementos deslizantes

```csharp
// Slide-in desde lateral
new QuarticEase { EasingMode = EasingMode.EaseOut }
// Duración: 180–220ms

// Slide-out al cerrar
new QuadraticEase { EasingMode = EasingMode.EaseOut }
// Duración: 150ms
```

---

## Timings de referencia usados en producción

| Elemento | Acción | Easing | Duración |
|---|---|---|---|
| Sidebar | Colapsar/expandir | `QuarticEase.EaseOut` | 160ms |
| Etiquetas sidebar | Fade out al colapsar | `SineEase.EaseOut` | 70ms |
| Etiquetas sidebar | Fade in al expandir | `SineEase.EaseOut` | 80ms |
| Tarjeta compacta | Aparecer/desaparecer | `SineEase.EaseOut` | 60ms |
| Submenú acordeón | Abrir | `QuadraticEase.EaseOut` | 220ms |
| Submenú acordeón | Cerrar | `QuadraticEase.EaseOut` | 160ms |
| Chevrón | Rotar | `CubicEase.EaseOut` | 180ms |
| Modal overlay | Entrada | `QuadraticEase.EaseOut` | 200ms |
| Modal overlay | Salida | `QuadraticEase.EaseOut` | 150ms |
| Spinner carga | Rotación continua | `Linear` / sin easing | 800ms loop |
| `ContentAreaBorder` (contenido pesado) | Fade-out antes de ocultar (colapsar/expandir sidebar) | `SineEase.EaseOut` | 50ms |
| `ContentAreaBorder` (contenido pesado) | Fade-in al restaurar tras animación de ancho | `SineEase.EaseOut` | 80ms |

> [!info] Agregado 2026-07-23
> Filas de `ContentAreaBorder` añadidas al reconciliar la documentación con el código real — ver [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]]. Patrón: ocultar contenido pesado (`Visibility.Collapsed`) mientras el panel contenedor anima su ancho, para que el árbol de layout sea liviano durante la animación; restaurarlo con fade-in al terminar.

---

## Patrón: animaciones en dos fases con `async Task`

Cuando una animación requiere que ocurra algo **después** de que termine otra, usar `async Task` con `await Task.Delay` en el code-behind:

```csharp
private async Task ColapsarPanel()
{
    // Fase 1 (t=0): fade + resize simultáneos
    AnimateOpacity(contenido, 0, 70);
    AnimateWidth(panel, targetWidth, 160);

    // Fase 2 (t=75ms): aplicar Visibility cuando ya está invisible
    await Task.Delay(75);
    contenido.Visibility = Visibility.Collapsed;
    contenido.Opacity    = 1;  // restaurar para próxima vez

    // Fase 3 (t=165ms): liberar guard
    await Task.Delay(90);
    _animating = false;
}
```

> [!important] Restaurar Opacity después de Collapsed
> Al hacer `Visibility.Collapsed`, el elemento sale del layout. Al restaurar `Opacity = 1` inmediatamente después, el elemento estará listo para la siguiente animación sin necesidad de resetearlo antes.

---

## Patrón: aparecer sin salto visual

Cuando un elemento debe aparecer gradualmente (no de golpe), establecer `Opacity = 0` antes de `Visibility.Visible`:

```csharp
// MAL — el elemento aparece en su tamaño completo en frame 0:
elemento.Visibility = Visibility.Visible;
AnimateOpacity(elemento, 1, 80);

// BIEN — invisible pero en layout, luego fade-in:
elemento.Visibility = Visibility.Visible;
elemento.Opacity    = 0;              // sin salto en frame 0
AnimateOpacity(elemento, 1, 80);      // ahora sí el fade tiene sentido
```

---

## Patrón: guard contra doble click

```csharp
private bool _animating = false;

private async void BtnAccion_Click(object sender, RoutedEventArgs e)
{
    if (_animating) return;          // ignorar click durante animación
    await EjecutarAnimacionAsync();
}

private async Task EjecutarAnimacionAsync()
{
    _animating = true;
    // ... animaciones ...
    await Task.Delay(totalMs);
    _animating = false;
}
```

---

## Relaciones

- [[Sesión 2026-05-26 - Optimización Animación Sidebar]] — donde se aplicaron estos patrones
- [[Módulo Productos]] — spinner de carga (DoubleAnimation en RotateTransform)
