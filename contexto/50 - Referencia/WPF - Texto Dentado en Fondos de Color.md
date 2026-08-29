---
title: "WPF — Texto Dentado (\\"Cerrucho\\") en Fondos de Color"
type: referencia
status: vigente
tags:
  - wpf
  - rendering
  - bug-visual
  - cleartype
  - referencia
date: 2026-06-21
updated: 2026-06-21
summary: "Estas 3 propiedades de rendering son la Fase 2 del soporte multi-resolución. Para el panorama completo (DPI Per-Monitor V2, manifest, modales escalables) ver WPF…"
scope: []
symbols:
  - CornerRadius
  - Display
  - Ideal
  - LinearGradientBrush
---

# WPF — Texto Dentado ("Cerrucho") en Fondos de Color

> [!info] Parte de una estrategia mayor
> Estas 3 propiedades de rendering son la **Fase 2** del soporte multi-resolución. Para el panorama completo (DPI Per-Monitor V2, manifest, modales escalables) ver [[WPF - DPI Awareness y Escalado Multi-Resolución]]. Desde 2026-06-21 estas props están en los 6 modales **y** las 6 Views + MainWindow + LoginWindow.

> [!bug] Síntoma
> Las letras y los bordes redondeados se ven "dentados" o "serrados" (efecto sierra) cuando el texto se renderiza sobre un fondo de color sólido, degradado, o semitransparente.

---

## Causa raíz

WPF usa por defecto **ClearType** para renderizar texto. ClearType funciona aprovechando los sub-píxeles RGB de cada píxel físico para lograr bordes más suaves — pero **solo funciona correctamente sobre fondos blancos o muy claros**. Cuando el texto está sobre un fondo de color (como el degradado azul de los modales), el algoritmo de ClearType no puede hacer el cálculo de sub-píxeles correctamente y produce bordes visualmente dentados.

Adicionalmente, WPF por defecto posiciona los elementos en coordenadas de píxeles **lógicos** (device-independent units). En monitores con scaling de Windows (125%, 150%), los bordes de `Border` con `CornerRadius` pueden caer entre píxeles físicos, produciendo el mismo efecto dentado.

---

## Solución — tres propiedades en el `UserControl`

```xml
<UserControl ...
             UseLayoutRounding="True"
             TextOptions.TextRenderingMode="Grayscale"
             TextOptions.TextFormattingMode="Display">
```

### `TextOptions.TextRenderingMode="Grayscale"`
Cambia el algoritmo de anti-aliasing de ClearType (sub-píxel RGB) a **escala de grises**. El anti-aliasing en grises funciona correctamente sobre cualquier color de fondo — no depende de los sub-píxeles del monitor.

**Cuándo usar:** siempre que el texto esté sobre un fondo que no sea blanco (fondos de color, degradados, imágenes, fondos semitransparentes).

### `TextOptions.TextFormattingMode="Display"`
Usa el modo de formateo compatible con GDI, que **ancla el texto a la grilla de píxeles físicos**. El modo por defecto (`Ideal`) calcula posiciones ideales matemáticamente, que a tamaños pequeños pueden caer entre píxeles.

**Efecto:** texto más nítido y consistente, especialmente a tamaños menores a 14pt.

> [!warning] Cuándo NO usar `Display`
> En texto muy grande (títulos 30pt+) o en animaciones de texto, `Display` puede hacer que el kern y el espaciado se vean irregulares. Para esos casos, mantener `Ideal`.

### `UseLayoutRounding="True"`
Fuerza al motor de layout de WPF a **redondear las coordenadas al píxel físico más cercano**. Corrige el dentado en bordes redondeados (`CornerRadius`), líneas finas, y cualquier elemento cuya posición calculada caiga en coordenadas sub-píxel.

**Heredado:** al ponerlo en el `UserControl` raíz, se propaga automáticamente a todos los hijos.

---

## Dónde se aplicó en el proyecto

**Archivos modificados (2026-06-21) — todos los modales de la aplicación:**

| Modal | Módulo |
|---|---|
| `ContactoFabricanteModal.xaml` | Contactos Fabricantes |
| `ContactoProveedorModal.xaml` | Contactos Proveedores |
| `ProductoModal.xaml` | Productos |
| `FabricanteModal.xaml` | Fabricantes |
| `ProveedorModal.xaml` | Proveedores |
| `CategoriaModal.xaml` | Categorías |

Todos los modales tienen fondo con `LinearGradientBrush` — caso exacto donde ClearType produce el efecto dentado.

---

## Regla general para futuros modales

> [!tip] Plantilla para todo modal con fondo de color
> Agregar estas tres propiedades al `<UserControl>` raíz de cualquier modal o UserControl que tenga fondo que no sea blanco:
> ```xml
> UseLayoutRounding="True"
> TextOptions.TextRenderingMode="Grayscale"
> TextOptions.TextFormattingMode="Display"
> ```

---

## Tamaños de fuente actualizados (misma sesión)

Junto con el fix de rendering se aumentaron los tamaños en los modales de contactos para mejorar la legibilidad:

| Elemento | Antes | Después |
|---|---|---|
| Contexto ("NUEVO · CONTACTO") | 10 | 11 |
| Título del modal | 19 | 21 |
| Label de entidad ("FABRICANTE") | 9.5 | 11 |
| Nombre de la entidad | 13.5 | 15 |
| Labels de campos | 11.5 | 13 |
| TextBox inputs (Height también) | 13.5 / h34 | 15 / h38 |
| Botones (Height también) | 13.5 / h38 | 15 / h42 |

---

## Referencias

- [TextOptions.TextRenderingMode — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.textoptions.textrenderingmode)
- [UseLayoutRounding — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.uselayoutrounding)
- [ClearType overview — Microsoft Typography](https://learn.microsoft.com/en-us/typography/cleartype/index)

## Relaciones

- [[Módulo Contactos (Drill-down)]] — módulos donde se detectó y corrigió
- [[Animaciones WPF - Referencia de Easings]] — otras propiedades de rendering WPF
