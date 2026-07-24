---
title: "Sesión 2026-05-28 — Deuda Técnica P-006, P-007, P-008"
tags:
  - sesion
  - deuda-tecnica
  - realtime
  - documentacion
date: 2026-05-28
---

# Sesión 2026-05-28 — Deuda Técnica P-006, P-007, P-008

> [!success] Resultado
> Los tres ítems menores de deuda técnica resueltos. 0 errores de compilación en CapaDatos, CapaAplicacion y CapaUI.

---

## P-006 — Realtime acoplado a paginación

**Tipo de resolución:** Documentación (sin cambio de código)

Creada nota [[Checklist - Replicar Módulo con Realtime]] con 6 puntos concretos que deben revisarse al copiar el patrón `OnCambioProducto` a un módulo nuevo:

1. Registrar la tabla en `_pkColumns` de `RealtimeService`
2. Tipo de PK (long vs int vs Guid)
3. Heurística INSERT en última página (asume sort por PK ASC)
4. Guard `afectaPaginaActual` asume propiedad `Id` en el DTO
5. Módulos sin paginación → simplificar handler a recarga completa
6. Patrón Suscripción/Dispose — este bloque es copiable sin adaptación

---

## P-007 — `GetConteosAsync` workaround SDK

**Archivo:** `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs:231`

Comentario TODO mejorado:

```csharp
// TODO P-007: Workaround — Supabase SDK v1.1.1: Count() no aplica filtros correctamente.
// Se construyen dos queries independientes y se usa Get() que sí respeta los filtros.
// Revisar al actualizar el paquete Supabase NuGet: si Count(CountType.Exact) con Filter
// funciona correctamente, reemplazar ambas queries por una sola con Count().
```

---

## P-008 — Mapeo tabla→PK manual en RealtimeService

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs` — `ExtraerCambio`

Agregado `else` con `Serilog.Log.Warning` cuando la tabla no está en `_pkColumns`:

```csharp
if (_pkColumns.TryGetValue(tabla, out var pkCol))
    id = obj.Value<long?>(pkCol);
else
    Serilog.Log.Warning(
        "Realtime P-008: tabla '{Tabla}' no tiene PK mapeada en _pkColumns. " +
        "Agrégala para que IdRegistro se extraiga correctamente.", tabla);
```

Antes fallaba silenciosamente con `id = null`. Ahora el warning aparece inmediatamente en los logs de Serilog.

---

## Estado final de toda la Deuda Técnica

| ID | Descripción | Estado |
|---|---|---|
| P-001 | Filtros duplicados 3x | ✅ Resuelto |
| P-002 | Magic numbers de estados | ✅ Resuelto |
| P-003 | `_filteredCount` duplicado | ✅ Resuelto |
| P-004 | PropertyChanged handler masivo | ✅ Resuelto |
| P-005 | VisualTreeHelper frágil | ✅ Resuelto |
| P-006 | Realtime acoplado a paginación | ✅ Documentado |
| P-007 | GetConteosAsync workaround SDK | ✅ TODO en código |
| P-008 | Mapeo tabla→PK manual | ✅ Warning en log |

**Toda la deuda técnica identificada en la auditoría del 2026-05-28 está resuelta o mitigada.**

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — tabla de origen
- [[Checklist - Replicar Módulo con Realtime]] — artefacto creado para P-006
- [[Sesión 2026-05-28 - Fix Realtime ConnectAsync]] — otra sesión del mismo día
