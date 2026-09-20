---
title: "Fase 7 — Matriz de prueba visual del escalado"
tags:
  - qa
  - escalado
  - wpf
  - matriz
date: 2026-09-20
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente)
revisor: Fernando
estado: Pendiente de ejecución
---

# Fase 7 — Matriz de prueba visual del escalado

> Absorbe el guion de la [[Fase 4 — Puertas de riesgo del escalado (guion de prueba)]] y le suma lo
> que dejaron pendiente las Fases 5 y 6. **Es una sola pasada por la app, no tres.**

---

## Cómo forzar el factor

```powershell
$env:UI_ESCALA = "0.8"; dotnet run --project CapaUI
```

O `CapaUI/App.config` → `<add key="UI_ESCALA" value="0.8" />`. Rango 0,70–1,30 en pasos de 0,05.
La ventana de login siempre va a 1.0 por diseño; la escala entra al iniciar sesión.

---

## Lo que ya está probado — **no hace falta repetirlo**

Un arné automático recorrió **43 controles × 5 factores** (0,75 · 0,8 · 0,9 · 1,0 · 1,1) sobre el
XAML real del proyecto. Todo verde. Concretamente, ya está descartado que:

- algún control **lance excepción** al cargar a cualquier factor;
- algún control **colapse** a tamaño cero;
- el contenido **no entre** en 1366×768, ni siquiera a 1,1× (el caso más apretado);
- el desplegable de `ComboBox` o el `ToolTip` **no tomen** el factor — lo toman en los 5, y el
  desplegable además **sigue el cambio en vivo**.

Build 0/0 y suite 676/676 en cada fase.

> [!warning] Lo que el arné **no** puede decir
> Si se ve bien. Nitidez del texto, bordes de 1 px, posicionamiento real de los popups en pantalla,
> hit-testing, fluidez del scroll y rendimiento de las sombras: todo eso necesita ojos. Es lo único
> que queda en esta matriz.

---

## Pasada 1 — Factor 0,80

### A. Nitidez y trazos finos *(el riesgo principal)*

A un factor no entero, `TextFormattingMode` pasa de `Display` a `Ideal`. Es el único cambio que
revierte parcialmente una decisión de la sesión 2026-06-21, así que mirarlo bien.

| # | Dónde | Pasa si |
|:--|:--|:--|
| A.1 | Texto corrido en Productos y Pesajes | Sin encabalgamiento de glifos ni espaciado irregular |
| A.2 | Texto blanco sobre fondo de color (barra superior, sidebar) | Sin "cerrucho" en los bordes de las letras |
| A.3 | Texto chico: etiquetas de 10–12 px, versión "v1.0.0" del sidebar | Legible, no emborronado |
| A.4 | Separadores y bordes de 1 px (tarjetas, filas de grilla) | Ninguno desaparece ni se engrosa a 2 px de forma despareja |
| A.5 | Íconos vectoriales del sidebar | Nítidos, sin bordes sucios |

### B. Hit-testing y la barra de título

`WindowChrome.CaptionHeight` se escala junto con el contenido (corrección preventiva de la Fase 4).
Esto confirma que alcanza.

| # | Paso | Pasa si |
|:--|:--|:--|
| B.1 | Clic sostenido **justo debajo** de la barra superior, sobre el borde del sidebar, y mover | La ventana **no** se arrastra |
| B.2 | Clic en el primer ítem del sidebar | Abre el submenú |
| B.3 | Clic en los íconos de notificaciones y configuración | Responden |
| B.4 | Arrastrar desde la zona vacía de la barra superior | La ventana **sí** se mueve |
| B.5 | Doble clic en la barra superior | Maximiza / restaura |
| B.6 | Drag & drop donde exista (reordenar, arrastrar filas) | El puntero coincide con lo que agarra |

### C. Popups y tooltips — posición, no tamaño

El tamaño ya está verificado. Lo que falta es **dónde caen**.

| # | Paso | Pasa si |
|:--|:--|:--|
| C.1 | Abrir el popup de notificaciones | Su borde derecho queda alineado con el botón, no corrido |
| C.2 | Abrir cualquier `ComboBox` | El desplegable queda del mismo ancho que el combo y pegado abajo |
| C.3 | Escribir en el buscador hasta que aparezcan sugerencias | El panel coincide en ancho con el cuadro y no se superpone |
| C.4 | Pasar el mouse sobre un botón con tooltip | El tooltip **no** queda tapado por el cursor |
| C.5 | Dashboard → selector de período | Cae pegado a la píldora |
| C.6 | Pesajes → quitar un producto | La confirmación aparece centrada sobre la grilla |

### D. Grillas y scroll

| # | Paso | Pasa si |
|:--|:--|:--|
| D.1 | Productos: scrollear hasta el final | Llega al último registro, sin barra fantasma ni media fila cortada |
| D.2 | Productos: columnas vs. encabezados | Alineadas, sin desfase acumulado a la derecha |
| D.3 | Pesajes: las tres grillas | Ídem; `RowHeight` fijo (40/46/40) sin filas a medio dibujar |
| D.4 | Bitácora: scroll horizontal con Shift | Se mueve parejo |

### E. Rendimiento *(la puerta 1 de la Fase 4)*

`PesajeView` concentra 6 `DropShadowEffect`, 3 `DataGrid` y altos de fila fijos. Un efecto rasteriza
a la resolución que impone el transform.

| # | Paso | Pasa si |
|:--|:--|:--|
| E.1 | Entrar a Pesajes | Sin demora perceptible respecto de 1,0 |
| E.2 | Scrollear las tres grillas rápido | Sin tirones ni filas a destiempo |
| E.3 | Abrir `PesajeModal` y mover el panel lateral | El gráfico de pesadas responde igual que a 1,0 |
| E.4 | Mirar los bordes de las tarjetas con sombra | Sin halos sucios ni bandas |

### F. Ventanas y modales

| # | Paso | Pasa si |
|:--|:--|:--|
| F.1 | Abrir el detalle de una notificación | Abre **escalada**, no a 1,0 |
| F.2 | Abrir cualquier modal de catálogo | Centrado sobre el overlay, sin recorte |
| F.3 | Achicar la ventana hasta el tope | Frena antes de que el contenido se recorte |
| F.4 | Cualquier `MessageBox` | Sale a 1,0 — **es lo esperado**, es diálogo del sistema |
| F.5 | Roles → "Cambios pendientes" | Sale a 1,0 — **es lo esperado**, misma razón |

### G. Flujo de Roles *(lo que tocó la Fase 5)*

`RolModal` dejó de ser ventana. Esto es prueba **funcional**, no de escala.

| # | Paso | Pasa si |
|:--|:--|:--|
| G.1 | Roles → Nuevo rol | El modal aparece centrado sobre el overlay oscuro, no como ventana aparte |
| G.2 | Guardar con el nombre vacío | Muestra "El nombre del rol es obligatorio" y **no** cierra |
| G.3 | Guardar con un nombre válido | Cierra, aparece en la lista y sale el aviso "Rol X creado" |
| G.4 | Editar un rol existente | Abre con el nombre cargado y seleccionado |
| G.5 | Cancelar y el botón ✕ | Cierran sin guardar |
| G.6 | Con permisos sin guardar, volver a la lista | Sale el diálogo "Cambios pendientes" y las tres opciones funcionan |
| G.7 | Abrir el modal y mirar el fondo | El overlay oscurece la pantalla; el aviso efímero, si aparece, queda **debajo** del modal |

---

## Pasada 2 — Factor 1,10

Solo lo que puede empeorar al **agrandar**, que es cuando el contenido deja de entrar:

| # | Paso | Pasa si |
|:--|:--|:--|
| H.1 | Detalle de notificación (ancho fijo 560) | El contenido entra, sin texto cortado a la derecha |
| H.2 | `PesajeModal` (el más ancho: 980) en 1366×768 | No se comprime ni recorta; si hace falta, aparece scroll |
| H.3 | Achicar la ventana al mínimo | El mínimo es **mayor** que a 1,0 y el contenido sigue entrando |
| H.4 | Barra superior: repetir B.1 a B.5 | Igual que a 0,8 |
| H.5 | Umbrales responsive: achicar la ventana de a poco | Los botones de la barra superior se ocultan **antes** que a 1,0 — es lo esperado |
| H.6 | Texto a 1,1 | Nítido |

---

## Pasada 3 — Factor 1,00 (control)

Media docena de puntos para confirmar que **nada se rompió para quien no usa escalado**:
Productos, Pesajes, un modal, un `ComboBox`, un tooltip y la barra superior. Debe verse exactamente
como antes de todo este trabajo — a 1,0 el transform es identidad y `TextFormattingMode` sigue en
`Display`.

---

## Resultado

| Bloque | 0,80 | 1,10 | 1,00 | Notas |
|:--|:--|:--|:--|:--|
| A. Nitidez y trazos | | | | |
| B. Hit-testing y barra | | | | |
| C. Popups y tooltips | | | | |
| D. Grillas y scroll | | | | |
| E. Rendimiento | | — | | |
| F. Ventanas y modales | | | | |
| G. Flujo de Roles | | — | | funcional |

**Si algo falla, anotar el factor y la pantalla**: casi todos los remedios son locales y no
invalidan el mecanismo.

---

## Relaciones

- [[Inventario — Superficie de escalado propio (LayoutTransform global)]]
- [[Fase 4 — Puertas de riesgo del escalado (guion de prueba)]] — absorbido por este
- [[WPF - Escalar Popups y ToolTips que no heredan el LayoutTransform]]
- [[WPF - DPI Awareness y Escalado Multi-Resolución]]
- [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]]
