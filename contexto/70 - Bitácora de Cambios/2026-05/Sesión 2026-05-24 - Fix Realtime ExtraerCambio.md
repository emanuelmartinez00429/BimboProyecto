---
title: "Sesión 2026-05-24 — Fix Realtime: ExtraerCambio accedía al nivel equivocado"
type: sesion
status: vigente
tags:
  - sesion
  - bugfix
  - realtime
  - supabase
date: 2026-05-24
updated: 2026-05-24
summary: "Al buscar obj.Value<long?>(\"idproducto\") → null (no existe a ese nivel). Al buscar obj.Value<int?>(\"idestado\") → null (no existe a ese nivel)."
scope:
  - CapaDatos/Realtime
symbols:
  - ExtraerCambio
  - RealtimeService
  - SocketResponsePayload
---

# Fix Realtime — ExtraerCambio accedía al nivel equivocado del payload

> [!bug] Síntoma
> Cambios en la base de datos no se reflejaban en la tabla de Productos, a pesar de que Realtime estaba habilitado en Supabase.

---

## Causa raíz

`ExtraerCambio` en `RealtimeService` accedía a `data.Record` (el wrapper interno del SDK) en vez de `data.Record.Record` (los valores reales de la fila de la BD).

### Estructura real del payload de Supabase Realtime

```
change.Payload                         → PostgresChangesPayload<...>
change.Payload.Data                    → SocketResponsePayload<SocketResponsePayload>
change.Payload.Data.Record             → SocketResponsePayload  ← WRAPPER del SDK
change.Payload.Data.Record.Record      → object (JObject)       ← DATOS REALES de la BD
```

### Qué hacía el código incorrecto

```csharp
// ❌ INCORRECTO — serializa el wrapper, no los datos de la fila
var data = change.Payload?.Data;
var obj = data?.Record != null ? JObject.FromObject(data.Record) : null;
```

`JObject.FromObject(data.Record)` producía un JObject con las propiedades del **wrapper**:
```json
{
  "schema": "public",
  "table": "productos",
  "type": "UPDATE",
  "record": { ... },
  "old_record": null,
  "columns": [...]
}
```

Al buscar `obj.Value<long?>("id_producto")` → `null` (no existe a ese nivel).
Al buscar `obj.Value<int?>("id_estado")` → `null` (no existe a ese nivel).

### Consecuencia en cadena

```
IdRegistro = null (siempre)
    ↓
afectaPaginaActual = false (siempre)
    ↓
UPDATE → RefrescarConteosAsync() (solo actualiza contadores)
    ↓
PageRows nunca se recarga → tabla nunca se actualiza
```

---

## Fix aplicado

### 1. RealtimeService.cs — ExtraerCambio

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs`

```csharp
// ✅ CORRECTO — baja un nivel más para llegar a los datos reales
// data.Record   → SocketResponsePayload (wrapper del SDK)
// data.Record.Record → object (JObject con los valores reales de columnas)
var data    = change.Payload?.Data;
var rowData = data?.Record?.Record; // object? — JObject en runtime
if (rowData is JObject obj)
{
    if (_pkColumns.TryGetValue(tabla, out var pkCol))
        id = obj.Value<long?>(pkCol);

    estado = obj.Value<int?>("id_estado");
}
```

`rowData is JObject obj` compila correctamente porque `rowData` es `object?` — el patrón `is` es válido entre `object` y cualquier tipo de referencia, sin generar CS8121.

### 2. ProductosViewModel.cs — OnCambioProducto (fallback defensivo)

**Archivo:** `CapaUI/.../Productos/ProductosViewModel.cs`

```csharp
else if (string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase))
{
    // Si IdRegistro es null (no se pudo extraer del payload) → recargar por seguridad
    if (afectaPaginaActual || !cambio.IdRegistro.HasValue)
        _ = CargarPaginaAsync();
    else
        _ = RefrescarConteosAsync();
}
```

Si por algún edge case el payload viene sin datos extraíbles, se recarga la página en vez de solo actualizar conteos — garantizando que la UI siempre refleja la BD.

---

## Por qué no detectamos el error antes

El error CS8121 original ("can't use `is JObject` on `SocketResponsePayload`") nos hizo cambiar a `JObject.FromObject(data.Record)`, que compila correctamente. El problema es que `JObject.FromObject` acepta cualquier objeto — serializa lo que le pasas, sin importar si es el nivel correcto. La compilación exitosa no garantizó que accediéramos al dato correcto.

---

## Build resultado

| Proyecto | Errores |
|---|---|
| CapaDatos | 0 |
| CapaUI | 0 |

---

## Relaciones

- [[Sesión 2026-05-24 - Implementación Gestor Realtime]] — Implementación original donde se introdujo el bug
- [[Gestor Realtime - Diseño Arquitectónico]] — Diseño completo del sistema
