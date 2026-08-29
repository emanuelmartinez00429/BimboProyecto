---
title: WPF — Bucle de Layout por Medir en ArrangeOverride
type: referencia
status: vigente
tags:
  - referencia
  - wpf
  - layout
  - rendimiento
  - panel
date: 2026-08-12
updated: 2026-08-12
summary: La pantalla se traba y consume CPU al redimensionar o maximizar la ventana. Empeora cuanto más ancha está. Detectado en RolesView el 2026-08-12.
scope: []
symbols:
  - ArrangeOverride
  - ContextLayoutManager
  - DesiredSize
  - MeasureDuringArrange
  - MeasureOverride
  - Panel
  - RolesView
  - ScrollContentPresenter
  - ScrollViewer
  - UpdateLayout
lifecycle: verified
---

# WPF — Bucle de Layout por Medir en `ArrangeOverride`

> [!danger] La regla en una frase
> **Un panel a medida nunca debe llamar `Measure()` dentro de `ArrangeOverride` con una restricción distinta a la que usó en `MeasureOverride`.** Si lo hace de forma sistemática, el layout no converge y la aplicación se traba.

---

## El síntoma

La pantalla se traba y consume CPU al redimensionar o maximizar la ventana. Empeora cuanto más ancha está. Detectado en `RolesView` el 2026-08-12.

## El mecanismo

Un `Panel` a medida hacía esto:

```csharp
protected override Size MeasureOverride(Size disponible)
    => Distribuir(CalcularAnchoColumna(disponible.Width), medir: true);

protected override Size ArrangeOverride(Size final)
    => Distribuir(CalcularAnchoColumna(final.Width), medir: true);   // ❌ vuelve a medir
```

El panel vivía dentro de un `ScrollViewer` con `HorizontalScrollBarVisibility="Auto"`. Eso es lo que cierra la trampa:

| Pasada | Ancho que recibe | Ancho de columna |
|---|---|---|
| `MeasureOverride` | **∞** (el `ScrollContentPresenter` mide sin restricción en la dimensión con scroll) | cae al mínimo, 250 |
| `ArrangeOverride` | el real, ~1400 | 342,5 |

Los hijos tenían `TextWrapping="Wrap"`, así que **su altura depende del ancho** y el `DesiredSize` cambiaba entre las dos pasadas. Y acá está la parte que no es obvia: WPF activa su bandera interna `MeasureDuringArrange` **solo** en su propio camino `Arrange→Measure`. Cuando el `Measure()` lo llama tu código a mano, la bandera está en `false`, así que se ejecuta:

```
parent.OnChildDesiredSizeChanged(this)  →  InvalidateMeasure()
```

Es decir:

```
Measure(250) → Arrange(342,5) → cambia DesiredSize
   → InvalidateMeasure → otra pasada → Measure(250) otra vez → ∞
```

**Nunca converge**, porque measure siempre ve infinito y arrange siempre ve el ancho real. El `ContextLayoutManager` de WPF corta a las **153 iteraciones** por `UpdateLayout`. Con dos paneles anidados y ~34 hijos, eran **~12.900 mediciones por gesto**.

---

## La solución

**1. Que measure y arrange reciban el mismo ancho.** `HorizontalScrollBarVisibility="Disabled"` en el `ScrollViewer` contenedor. Con scroll horizontal habilitado la medición llega con ancho infinito **siempre**, así que la divergencia es estructural, no accidental.

**2. Que el reparto sea una función pura del ancho.** Misma entrada → misma salida, en las dos pasadas:

```csharp
protected override Size MeasureOverride(Size disponible)
{
    var rejilla = CalcularRejilla(disponible.Width);
    return new Size(rejilla.AnchoTotal, Recorrer(rejilla, medir: true,  arreglar: false));
}

protected override Size ArrangeOverride(Size final)
{
    Recorrer(CalcularRejilla(final.Width), medir: false, arreglar: true);   // ✅ no mide
    return final;
}
```

**3. Responsividad sin scroll horizontal.** En vez de encoger las columnas hasta tapar el texto y sacar una barra, el ancho mínimo **reduce la cantidad de columnas**:

```csharp
int caben = (int)Math.Floor((anchoDisponible + gap) / (minimo + gap));
int columnas = Math.Clamp(caben, 1, tope);
```

---

## Cómo detectarlo

- La pantalla se traba **al redimensionar**, no al cargar.
- Empeora cuanto más grande es la ventana (más divergencia entre los dos anchos).
- Un `Panel` a medida con `Measure()` dentro de `ArrangeOverride`.
- Un `ScrollViewer` con scroll habilitado en la misma dimensión que el panel reparte.
- Hijos cuya altura depende del ancho (`TextWrapping="Wrap"`).

> [!tip] Señal de alarma en el código
> Si un comentario justifica medir en el arreglo diciendo *"medir con la misma restricción es un no-op"*, verificá que **de verdad sea la misma restricción**. En este caso el comentario admitía dos líneas antes que los anchos eran distintos.

---

## Relaciones

- [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]]
- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]]
- [[Vista Descargada Durante un await (async void Loaded)]]
