---
title: "Sesión 2026-05-28 — Fix Realtime: Socket must exist"
type: sesion
status: vigente
tags:
  - sesion
  - realtime
  - fix
  - supabase
date: 2026-05-28
updated: 2026-05-28
summary: El formulario de Productos ya no lanza la excepción al abrir. Canal Realtime se establece correctamente.
scope:
  - CapaDatos/Realtime
symbols:
  - AbrirCanalAsync
  - AutoConnectRealtime
  - ConexionSupabase
  - Connect
  - ProductosViewModel
  - RealtimeService
  - SupabaseOptions
---

# Sesión 2026-05-28 — Fix Realtime: "Socket must exist, was Connect called?"

> [!success] Resultado
> El formulario de Productos ya no lanza la excepción al abrir. Canal Realtime se establece correctamente.

---

## Error

```
System.Exception: 'Socket must exist, was `Connect` called?'
```

Lanzado en `RealtimeService.AbrirCanalAsync` al ejecutar `client.Realtime.Channel(...)`.

---

## Causa raíz

`ConexionSupabase` usa `AutoConnectRealtime = true` en las `SupabaseOptions`, lo que hace que `InitializeAsync()` inicie la conexión WebSocket. Sin embargo, esa conexión es **asíncrona y no bloqueante** — cuando el `ProductosViewModel` llama a `SuscribirAsync()` al terminar `CargarDatosAsync()`, el socket puede no haber completado su handshake todavía.

El SDK de Supabase lanza la excepción si se intenta `Channel()` o `Subscribe()` antes de que el socket esté `IsConnected = true`.

---

## Fix aplicado

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs` — método `AbrirCanalAsync`

```csharp
// Antes:
private async Task AbrirCanalAsync(string tabla)
{
    var client = await ConexionSupabase.GetClientAsync();

    var channel = client.Realtime.Channel($"rt-{tabla}");
    ...
}

// Después:
private async Task AbrirCanalAsync(string tabla)
{
    var client = await ConexionSupabase.GetClientAsync();

    // AutoConnectRealtime = true inicia la conexión, pero puede no estar lista aún.
    // ConnectAsync() es idempotente — si ya está conectado, no hace nada.
    if (client.Realtime.Socket is null || !client.Realtime.Socket.IsConnected)
        await client.Realtime.ConnectAsync();

    var channel = client.Realtime.Channel($"rt-{tabla}");
    ...
}
```

---

## Por qué `ConnectAsync()` es seguro de llamar siempre

La librería `realtime-csharp` (usada internamente por el SDK de Supabase C#) implementa `ConnectAsync()` de forma **idempotente**:

- Si el socket ya está conectado → retorna inmediatamente sin hacer nada
- Si el socket está en proceso de conectarse → espera a que termine
- Si el socket es `null` (nunca conectado) → lo crea y conecta

Por lo tanto, llamarlo antes de `Channel()` es la forma correcta y segura de garantizar que la conexión está lista, independientemente del timing de `AutoConnectRealtime`.

---

## Alternativa considerada (descartada)

Mover la suscripción más tarde en el ciclo de vida del VM (por ejemplo, después de un delay). Descartada porque:
- Introduce timing frágil
- El fix correcto es garantizar la conexión, no retrasar el uso

---

## Relaciones

- [[Sesión 2026-05-24 - Implementación Gestor Realtime Completa]] — Implementación original de `RealtimeService`
- [[Sesión 2026-05-26 - Realtime Silent Refresh Productos]] — Silent refresh en ProductosViewModel
- [[Plan de Implementación - Gestor Realtime]] — Plan base del gestor
