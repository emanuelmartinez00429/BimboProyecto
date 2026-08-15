---
title: "Panel de Filtros Fluido — Barra responsive con prioridad y equilibrado"
tags:
  - patron
  - wpf
  - layout
  - responsive
date: 2026-08-14
lifecycle: verified
---

# Panel de Filtros Fluido — Barra responsive con prioridad y equilibrado

> [!abstract]
> `CapaUI/Core/Controls/PanelFiltrosFluido.cs` — `Panel` custom para barras con varios grupos de controles de ancho distinto (filtros, pastillas, combos) que necesitan mantenerse en una sola línea mientras entren, y envolver a varias líneas **equilibradas** — sin huecos en blanco ni un solo control estirado a todo el ancho — cuando no. Escrito para la barra de filtros de [[Módulo Productos]]; reutilizable en cualquier pantalla con el mismo problema.

---

## El problema — ningún panel nativo de WPF hace las dos cosas

| Panel nativo | Envuelve a varias líneas | Reparte el sobrante de cada línea | Resultado si se usa solo |
|---|---|---|---|
| `WrapPanel` | ✅ | ❌ | Envuelve, pero deja huecos en blanco al final de cada línea — los grupos tienen anchos naturales distintos, así que el corte cae en puntos arbitrarios. |
| `UniformGrid` | ❌ | ✅ (en partes iguales) | Reparte parejo, pero nunca cambia la cantidad de columnas — a ventana angosta, recorta en vez de envolver. |

Ninguno alcanza solo. `PanelFiltrosFluido` combina ambos comportamientos con una lógica de `MeasureOverride`/`ArrangeOverride` a medida.

---

## Cómo funciona

1. **Mide cada hijo sin restricción** (`availableSize` infinito) para conocer su ancho **natural** — el mismo que actúa como piso de legibilidad si el hijo tiene `MinWidth` (ver [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]] para por qué esto hay que hacerlo a mano y no confiarlo a `Grid`/`StackPanel`).
2. **Arma líneas** metiendo grupos mientras entren a ese ancho natural (empaquetado codicioso) — da la cantidad **mínima** de líneas posible respetando el orden.
3. **Equilibra** dentro de esa cantidad mínima de líneas: en vez de llenar la primera línea y dejar el resto amontonado en la última (lo que deja un único control gigante y desproporcionado en la última línea), prueba todos los cortes posibles que producen la misma cantidad de líneas y elige el que **minimiza el ancho del hijo flexible más ancho**. Con pocos hijos (el caso normal de una barra de filtros) es barato — hay un tope (`MaxCombinaciones`) que cae al reparto codicioso si algún día hay demasiados hijos para explorar todas las combinaciones.
4. **Reparte el sobrante de cada línea** por una propiedad adjunta `Peso` (al estilo de las columnas `*` de un `Grid`): un hijo con `Peso="0"` (por defecto) conserva su ancho natural — no se estira; los de `Peso > 0` se reparten el sobrante en proporción.
5. **`MaxLineas`** (opcional): si envolver indefinidamente le termina robando alto a lo que sí importa (la tabla, debajo de la barra), fija un tope. Al alcanzarlo, el acomodo se **congela** en el ancho límite (calculado por bisección) — deja de reacomodar y lo que ya no entra se recorta contra el borde, en vez de seguir agregando líneas.

---

## Uso

```xml
<controls:PanelFiltrosFluido EspacioHorizontal="16" EspacioVertical="12" MaxLineas="4">
    <!-- Peso="0" (default): pastillas, no se estiran -->
    <StackPanel Orientation="Horizontal">...</StackPanel>

    <!-- Peso="1": estos 4 combos se reparten el sobrante de su línea en partes iguales -->
    <Grid controls:PanelFiltrosFluido.Peso="1">...</Grid>
    <Grid controls:PanelFiltrosFluido.Peso="1">...</Grid>
    <Grid controls:PanelFiltrosFluido.Peso="1">...</Grid>
    <Grid controls:PanelFiltrosFluido.Peso="1">...</Grid>
</controls:PanelFiltrosFluido>
```

> [!tip] Por qué "una sola línea sin hijos flexibles no puntúa"
> Al equilibrar, una línea sin ningún hijo `Peso > 0` (por ejemplo, la de las dos pastillas segmentadas, ESTADO y ORDEN) no entra en el cálculo de "qué corte minimiza el ancho máximo". Su sobrante queda como margen a la derecha — que es exactamente cómo se ve normalmente una barra de herramientas. Sin esta exclusión, el algoritmo intentaría "llenar" esa línea también, lo cual no tiene sentido para controles que no deberían estirarse.

---

## Trampa que costó una iteración — cachear el ancho natural

`ArrangeOverride` necesita re-medir cada hijo al ancho **ya estirado** para que su contenido interno (p. ej. la columna `"*"` de un combo dentro de la celda) se acomode a la celda real. Pero eso **pisa** el `DesiredSize` del hijo con el ancho estirado, no el natural. Como WPF puede ejecutar un pase de `Arrange` sin un `Measure` previo, el siguiente reparto leería anchos ya inflados y el corte de línea se degradaría solo, iteración tras iteración, hasta terminar con un grupo por línea.

**Fix:** cachear el tamaño natural de cada hijo en un `Dictionary<UIElement, Size>` durante `MeasureOverride`, y leer siempre de ahí (nunca de `hijo.DesiredSize` directo) al decidir cortes de línea o repartir el sobrante.

---

## Cuándo reusarlo

Cualquier barra con controles de ancho natural distinto que deba:
- mantenerse en una sola línea cuando hay espacio,
- envolver a varias líneas sin dejar huecos cuando no,
- y (opcional) frenar el envolvido después de cierta cantidad de líneas para no robarle alto al contenido de abajo.

No aplica si todos los hijos deben tener el mismo ancho siempre (ahí `UniformGrid` alcanza) ni si nunca hace falta repartir el sobrante (ahí `WrapPanel` alcanza).

---

## Relaciones

- [[Módulo Productos]] — barra de filtros donde se usa (ESTADO, ORDEN, Proveedor, Fabricante, Categoría, País)
- [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]] — el porqué de medir sin restricción y cachear el tamaño natural
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — el filtro de Categoría que motivó tocar toda la barra el mismo día
