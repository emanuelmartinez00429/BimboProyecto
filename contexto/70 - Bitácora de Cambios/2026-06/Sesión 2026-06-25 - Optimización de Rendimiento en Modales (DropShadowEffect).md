---
title: Sesión 2026-06-25 — Optimización de Rendimiento en Modales (DropShadowEffect)
type: sesion
status: vigente
tags:
  - sesion
  - wpf
  - rendering
  - rendimiento
  - gpu
  - dropshadoweffect
date: 2026-06-25
updated: 2026-06-25
summary: "Una segunda PC presentaba tirones al hacer hover en modales, mouse lento y consumo de RAM oscilando ~30 MB, síntomas ausentes en la PC principal. Investigación…"
scope: []
symbols:
  - BitmapCache
  - DropShadowEffect
  - IsMouseOver
  - TextBox
  - Trigger
branch: feat/fase6-IntegracionWpf/MenuPrincipal
---

# Sesión 2026-06-25 — Optimización de Rendimiento en Modales (DropShadowEffect)

## Resumen

Una segunda PC presentaba **tirones al hacer hover en modales, mouse lento y consumo de RAM oscilando ~30 MB**, síntomas ausentes en la PC principal. Investigación (Microsoft Learn) + diagnóstico del código → la causa es el **abuso de `DropShadowEffect`**, no la versión de .NET ni una librería. Se aplicó la corrección (Prioridad 1) en los 6 modales. Build final: **0 errores**.

Referencia técnica completa: [[WPF - Rendimiento de Efectos y Niveles de Renderizado]].

---

## Reporte original (PC del amigo)

| | PC principal (fluida) | PC del amigo (con problemas) |
|---|---|---|
| CPU | i5-12500H (12ª gen) | i7-10750H (10ª gen) |
| **iGPU** | **Iris Xe (~80 EUs)** | **UHD 630 (~24 EUs)** |
| RAM | 24 GB DDR4-3200 | 16 GB DDR4-2933 |
| SDK .NET | 9.0.315 | 10.0.300 |
| Memoria observada | sube a 150→190-200, baja a ~160 y se estabiliza | "mucho más alta", oscila ~30 MB arriba/abajo |

El usuario sospechaba de la **versión de .NET** o de una **librería incompatible**.

---

## Hallazgo clave

**No es .NET ni una librería — es el renderizado.** Se contaron **72 `DropShadowEffect`** en 16 XAML. Cada modal tenía:

- Sombra de contenedor con **`BlurRadius="100"`** (o 80) → desenfoque gigante sobre todo el modal.
- Una `DropShadowEffect` **por cada `TextBox`**, y al enfocar **cambiaba a otra sombra** (`BlurRadius=8`) dentro de un `Trigger`.

**Mecanismo del lag:** al pasar el mouse sobre cualquier hijo del modal, su trigger `IsMouseOver` invalida ese elemento; como es hijo del borde con la sombra de radio 100, **WPF re-rasteriza todo el modal y reaplica el desenfoque en cada movimiento**. En la iGPU débil (UHD 630, ~3-4× más lenta que la Iris Xe) eso satura la GPU → mouse pegajoso; las texturas intermedias creadas/descartadas por frame → vaivén de RAM (no es fuga, es el GC).

Detalle de niveles de renderizado, GPU y el mito de .NET/DATAS en [[WPF - Rendimiento de Efectos y Niveles de Renderizado]].

---

## Archivos modificados (Prioridad 1)

Los **6 modales**, sin cambio visual perceptible:

| Archivo |
|---|
| `CapaUI/.../Productos/ProductoModal.xaml` |
| `CapaUI/.../Fabricantes/FabricanteModal.xaml` |
| `CapaUI/.../Categorias/CategoriaModal.xaml` |
| `CapaUI/.../Proveedores/ProveedorModal.xaml` |
| `CapaUI/.../ContactosFabricantes/ContactoFabricanteModal.xaml` |
| `CapaUI/.../ContactosProveedores/ContactoProveedorModal.xaml` |

### Cambios por modal

| Elemento | Antes | Ahora |
|---|---|---|
| Sombra del contenedor | `BlurRadius=100/80`, `ShadowDepth=40/30` | `BlurRadius=24/20`, `ShadowDepth=8/6` |
| Sombra en cada `TextBox` | `DropShadowEffect` por input | **eliminada** |
| Efecto al enfocar input | cambiaba a otra sombra (`BlurRadius=8`) | **solo cambia el borde a blanco** (gratis) |

---

## Lo que se dejó intacto (a propósito)

Sombras pequeñas y **estáticas** que no se re-renderizan en hover: logo del footer (`BlurRadius=6`), botón Guardar (`BlurRadius=8`), y las sombras de tarjetas en las *Views* de lista. Son baratas y aportan al diseño.

---

## Verificación

`dotnet build CapaUI.csproj` → **0 errores**, 41 advertencias (CS8618/CS8603 nullable preexistentes en CapaDatos, no relacionadas). Grep posterior: sin `BlurRadius="100"`/`"80"` ni etiquetas `<Border.Effect>` huérfanas.

**Pendiente:** prueba real en la PC del amigo comparando fluidez y RAM.

---

## Plan completo (por si hace falta más margen)

- **Prioridad 1 — ✅ hecha:** bajar sombras de contenedor y quitar sombras de inputs.
- **Prioridad 1 opcional:** `BitmapCache` en sombras estáticas; reducir las sombras de las *Views*.
- **Prioridad 2 (PC del amigo):** actualizar driver Intel UHD 630; verificar que la aceleración HW de WPF no esté desactivada.
- **Prioridad 3:** instalar el **.NET 8 Desktop Runtime** en ambas PCs para que corran el mismo WPF y eliminar la variable "versión de .NET".

---

## Relaciones

- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] — referencia técnica
- [[WPF - DPI Awareness y Escalado Multi-Resolución]] — sesión previa de rendering en los mismos modales
- [[Sesión 2026-06-21 - Soporte Multi-Resolución y DPI Per-Monitor V2]]
- [[Arquitectura Actual]]
