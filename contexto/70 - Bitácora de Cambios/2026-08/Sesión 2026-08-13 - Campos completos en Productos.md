---
title: "Sesión 2026-08-13 — Campos completos en Productos"
tags:
  - sesion
  - productos
  - ui
date: 2026-08-13
branch: feat/fase8-MaquetadodeRoles
autor_cambios: GPT-5 (Codex)
---

# Sesión 2026-08-13 — Campos completos en Productos

> [!success] Resultado
> El catálogo y modal de Productos ahora exponen los campos de producto que estaban ausentes de la UI y los cargan desde la consulta paginada.

---

## Problema / motivo

La pantalla mostraba solo una parte del registro `productos`; el DTO, el mapeo y el modal tampoco incluían peso teórico, tara, precio por kilogramo ni fechas de auditoría.

## Cambios aplicados

- `CapaDatos/Modelados/Productos/Productos.cs`: se mapearon `peso_teorico`, `id_tara`, `precio_por_kg`, `created_at` y `updated_at`, incluyendo la navegación existente a `tara`.
- `CapaAplicacion4/Productos/Dtos/ProductoDto.cs` y `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`: se propagaron los campos nuevos hacia la UI y se conservaron al crear o actualizar.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml`: se añadieron columnas para valores y nombres relacionados; los IDs de estado, tara, categoría y país, junto a las fechas de auditoría, se omiten de la tabla para priorizar información operativa. Las fechas se conservan en el modal de solo lectura.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.*`: se añadieron peso teórico, tara, precio por kg y fechas. La tara muestra su descripción en edición y tiene una lupa visual preparada para el selector futuro; las fechas son de solo lectura.
- El campo `contenido` ahora separa una unidad final reconocida (`g`, `kg`, `ml`, `l`, `oz`) al editar y la recompone con un espacio al guardar. Cuando no existe una unidad reconocida, conserva el texto y muestra `(Sin seleccionar)`.
- El `DataGrid` conserva los campos operativos (`peso_teorico`, nombre de tara, contenido, precio por kg y estado visual) y omite los identificadores técnicos de estado, tara, categoría y país. También se omitieron las fechas de auditoría de la tabla para evitar sobrecargarla; permanecen visibles en el modal.
- `ProductosView.xaml` se normalizó a finales de línea Windows (CRLF) para corregir el aviso de Visual Studio por finales de línea mezclados, sin cambios funcionales.

## Verificación

`dotnet build BimboProyecto.sln` finalizó correctamente el 2026-08-13 tras el cambio de unidades: **0 errores** y 2 advertencias preexistentes en `RolesViewModel`. Queda pendiente la prueba visual manual del modal de Productos con datos reales.

## Lo que NO cambió

- No se implementó todavía el selector interno de taras ni se alteró el flujo de selección actual para fabricantes, categorías, países o presentaciones.
- No se modificaron los cambios en curso del módulo Pesaje.
- `created_at` y `updated_at` continúan disponibles en el DTO y el modal, aunque no se muestran en la tabla.

---

## Relaciones

- [[Módulo Productos]]
- [[Arquitectura Actual]]
