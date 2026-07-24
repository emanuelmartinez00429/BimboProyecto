---
title: "Sesión 2026-05-29 — Fix Fuga de Timers Realtime (Keep-Open + Reset Total)"
date: 2026-05-29
tags:
  - bitácora
  - realtime
  - memory-leak
  - timers
  - supabase
  - arquitectura
status: Completado
---

# Sesión 2026-05-29 — Fix Fuga de Timers Realtime (Keep-Open + Reset Total)

## Resumen

El usuario reportó que el subsistema Realtime **acumula timers** que la memoria no recupera: arrancan al abrir un formulario, no bajan al cerrarlo, y se siguen apilando al reabrir formularios y al cerrar sesión / volver al login. Algunos timers bajan con el tiempo, **pero no todos**, por lo que cuanto más se usa la app más memoria retiene.

Se investigó la librería `Supabase.Realtime 7.0.2` a fondo (reflexión sobre el DLL instalado + código fuente de GitHub `v7.2.0`), se identificó la causa raíz, y se implementó la solución acordada: **modelo "keep-open" durante la sesión + reset total del cliente al cerrar sesión**. Compila con 0 errores.

> [!warning] Hallazgo crítico de arquitectura
> El fix de thread-safety **C1** documentado en [[Sesión 2026-05-29 - Auditoría Profunda y Plan de Remediación Completo]] se aplicó a `ServicioConexión/Conexion/ConexionSupabase.cs`, **pero la capa de datos NO referencia ese proyecto** — compila una **copia duplicada** en `CapaDatos/Conexion.cs`. Es decir: C1 nunca llegó a la clase que el Realtime realmente usa. En esta sesión se reescribió la copia correcta. Ver [[#Duda de arquitectura — clase ConexionSupabase duplicada]].

---

## Contexto — síntomas reportados

Cita literal del usuario:

> "El realtime […] crea timers cuando se abre pero al cerrarse la aplicación volver al login estos timers se quedan acumulados, además al abrir el mismo formulario de forma recurrente se incrementan los timers, algunos de estos bajan con el tiempo, pero no todos […]. La memoria inicia en 108 al abrir la aplicación, al abrir el formulario se va arriba de 120, al cerrar el formulario si estaba en 130 no regresa a 120, se queda o baja en 130, pero si se abre más veces y se navega entre páginas puede llegar a 140 y no bajar."

Traducción a comportamiento medible:

| Observación del usuario | Traducción técnica |
|---|---|
| "Crea timers al abrir" | Cada `Subscribe()` de canal dispara un `Push` con su `System.Timers.Timer` |
| "Algunos bajan con el tiempo, pero no todos" | Los `Push` que reciben respuesta paran su timer; los que no, lo dejan vivo |
| "Al cerrar el formulario no regresa la memoria" | El canal se quedaba vivo (rooteado por el socket) aunque se "desuscribiera" |
| "Al reabrir se incrementan" | `Channel()` deduplica por topic y `Register`/`AddPostgresChangeHandler` **apilan** bindings sobre el mismo canal |
| "Al cerrar sesión quedan acumulados" | El cliente Supabase singleton **nunca se disponía** — el socket y sus timers sobrevivían entre sesiones |

---

## Investigación — inventario real de timers en Supabase.Realtime 7.0.2

Se verificó con dos fuentes independientes:
1. **Reflexión** sobre el DLL instalado (`~/.nuget/packages/supabase.realtime/7.0.2`) con una sonda de consola .NET 8 (PowerShell 5.1 no puede cargar `netstandard2.1`).
2. **Código fuente** del repositorio en el tag `v7.2.0` (el tag `7.0.2` no existe; estructuralmente idéntico para este análisis).

### Los timers que existen

| Timer | Tipo | Cardinalidad | Quién lo crea | Cómo se libera |
|---|---|---|---|---|
| `Push._timer` | `System.Timers.Timer` | **Uno por cada mensaje/push** (Subscribe, etc.) | ctor de `Push` (`Channel_Push.cs:61,84`) | **Solo `Stop()`** al llegar la respuesta con `Ref` coincidente — **nunca `Dispose()`** |
| `RealtimeChannel._rejoinTimer` | `System.Timers.Timer` | Uno por objeto-canal | ctor de `RealtimeChannel` | No se libera explícitamente |
| `WebsocketClient._lastChanceTimer` | `System.Threading.Timer` | Uno por socket (singleton) | `WebsocketClient` interno | Solo al `Dispose()` del socket |
| `WebsocketClient._errorReconnectTimer` | `System.Threading.Timer` | Uno por socket (singleton) | `WebsocketClient` interno | Solo al `Dispose()` del socket |

> [!note] El heartbeat NO es un timer
> El latido se implementa con `Task.Run(EmitHeartbeat)` + `CancellationTokenSource _heartbeatTokenSource` + `await Task.Delay(HeartbeatInterval, token)`. Se cancela en `Disconnect()`. No cuenta como timer acumulable.

### Por qué se acumulan (causa raíz)

El gestor previo usaba un modelo de **churn de canales** (abrir al primer suscriptor, cerrar al último). Combinado con el diseño de la librería, eso producía fuga garantizada:

1. **`Client.Channel(string topic)` deduplica por topic** (diccionario `_subscriptions`). Reabrir `rt-productos` devuelve **el mismo objeto-canal**, no uno nuevo.
2. **`Register(PostgresChangesOptions)` y `AddPostgresChangeHandler(...)` son APPEND-only** — nunca reemplazan. Cada reapertura **vuelve a apilar** un binding y un handler sobre el canal reutilizado → despacho duplicado + estructuras que solo crecen.
3. **`IRealtimeClient.Remove(channel)` es la única vía de evicción** del diccionario, y el gestor **nunca lo llamaba**. `Unsubscribe()` no remueve el canal.
4. **El ctor de `RealtimeChannel` hace `Socket.AddStateChangedHandler(HandleSocketStateChanged)` y nunca lo quita** → el socket **rootea (mantiene vivo) cada canal** que se haya creado. Aunque sueltes todas tus referencias, el socket lo retiene.
5. **El ctor de `Push` hace `socket.AddMessageReceivedHandler(...)`** y solo se da de baja al recibir la respuesta con `Ref` coincidente (`Channel_Push.cs:146-160`). Si la respuesta no llega o no coincide (p. ej. `PresenceDiff`), el handler **y su `_timer` quedan colgados del socket**. Esto es exactamente *"algunos bajan, pero no todos"*.
6. **El cliente Supabase singleton nunca se disponía al cerrar sesión.** `RealtimeSocket.Disconnect()` cancela el heartbeat y para el socket, **pero NO dispone el `WebsocketClient`** ni sus dos `System.Threading.Timer`. Solo `(Socket as IDisposable).Dispose()` los libera. Como el singleton sobrevivía al logout, **todo lo acumulado se arrastraba a la siguiente sesión de login**.

En conjunto: reabrir formularios apila bindings/pushes sobre canales deduplicados que el socket nunca suelta, y el logout no disponía nada → crecimiento lento pero monótono. Coincide punto por punto con lo reportado.

---

## Decisión de diseño

Se preguntó al usuario qué profundidad de limpieza quería al cerrar sesión. Eligió:

> **Reset total del cliente (Recomendado)** — mantener los canales abiertos durante la sesión y, al cerrar sesión, disponer el socket Realtime y reconstruir un cliente limpio en el siguiente login.

Dos piezas:

1. **Keep-open durante la sesión** — un canal por tabla, abierto una sola vez y reutilizado. **No se cierra al quedar sin suscriptores.** Esto elimina el churn (causas 1-3): no se vuelven a apilar bindings ni a crear pushes nuevos al navegar/reabrir → **memoria plana** durante la sesión.
2. **Reset total al logout** — `Disconnect()` + `(Socket as IDisposable).Dispose()` + `_client = null`. Libera el `WebsocketClient` y sus timers, suelta el socket que rooteaba los canales, y deja el singleton listo para reconstruir limpio (causas 4-6) → **cero acumulación entre sesiones**.

> [!important] Esto SUPERA la decisión de la "Parte 1" del mismo día
> En [[Sesión 2026-05-29 - Auditoría Profunda y Plan de Remediación Completo]] (Parte 1, fix de deadlock) se había hecho `DesconectarAsync` síncrono y se **quitó** `Disconnect()` para que el socket quedara "caliente" y el `ConnectAsync()` del siguiente login fuera un no-op rápido. **Ese socket caliente era precisamente la fuente de la acumulación entre sesiones.** La nueva decisión revierte eso a propósito: aceptamos el costo único de reconstruir el cliente en el próximo login a cambio de cero fuga cross-sesión.

> [!check] El fix de deadlock se preserva
> El deadlock original venía de `Desuscribir` llamando `_lock.Wait()` (síncrono) sobre el UI thread mientras `ConnectAsync` tenía el `SemaphoreSlim`. Eso sigue resuelto: `Desuscribir` usa `lock(_stateLock)` (no toca el semáforo) y el nuevo `DesconectarAsync` es `async`, se **espera** desde `HandleCierreAsync` (que ya es async), y usa `ConexionSupabase._initLock` — un semáforo **distinto** del `_lock` de `RealtimeService`. No se reintroduce el bloqueo.

---

## Cambios implementados

### 1. `CapaDatos/Conexion.cs` — reescrito (la copia REAL que usa el Realtime)

Era la versión vieja, no thread-safe (double-check inútil con dos `if (_client == null)` sin lock, y `_client` publicado **antes** de `InitializeAsync()`). Reescrito con `SemaphoreSlim` (double-check real, publicación post-init) **y** se añadió `ResetAsync()`:

```csharp
public static async Task ResetAsync()
{
    await _initLock.WaitAsync();
    try
    {
        var old = _client;
        _client = null;            // publica el null primero: nuevas llamadas reconstruyen
        if (old is null) return;
        try
        {
            old.Realtime.Disconnect();                       // cancela heartbeat + para socket
            (old.Realtime.Socket as IDisposable)?.Dispose(); // libera WebsocketClient + sus 2 Timer
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConexionSupabase] Error en ResetAsync: {ex.Message}");
        }
    }
    finally { _initLock.Release(); }
}
```

Se añadió además un comentario de cabecera documentando la duplicación de clase.

### 2. `ServicioConexión/Conexion/ConexionSupabase.cs` — `ResetAsync()` añadido (consistencia)

Esta copia ya era thread-safe. Se le agregó el mismo `ResetAsync()` para que ambas queden idénticas, aunque **no es la que el Realtime enlaza** (ver duda de arquitectura abajo).

### 3. `CapaDatos/Realtime/RealtimeService.cs` — `Desuscribir` → keep-open

Ya no cierra el canal al quedar sin suscriptores; solo remueve el handler en memoria:

```csharp
public void Desuscribir(string tabla, Action<CambioRealtime> handler)
{
    lock (_stateLock)
    {
        if (!_suscriptores.TryGetValue(tabla, out var handlers)) return;
        handlers.Remove(handler);
        // Modelo "keep-open": el canal Supabase NO se cierra al quedar sin suscriptores.
        // Cerrar/reabrir provoca churn de Pushes y, como Channel() deduplica por topic,
        // reabrir reutiliza el MISMO canal y vuelve a apilar Register()/AddPostgresChangeHandler().
        if (handlers.Count == 0)
            _suscriptores.Remove(tabla);
    }
}
```

### 4. `CapaDatos/Realtime/RealtimeService.cs` — `DesconectarAsync` → reset total

Pasó de síncrono (`Task.CompletedTask`) a `async`, y delega el reset completo del cliente:

```csharp
public async Task DesconectarAsync()
{
    lock (_stateLock)
    {
        _canales.Clear();
        _suscriptores.Clear();
        _estadoHandlerRegistrado = false;   // el próximo login re-registra sobre el cliente nuevo
    }

    // Reset total: dispone el socket Realtime y el cliente Supabase completo (con todos sus timers).
    // El próximo login reconstruye un cliente limpio, sin canales ni handlers heredados.
    await ConexionSupabase.ResetAsync();

    Serilog.Log.Information("Realtime: reset total — socket dispuesto y estado limpio");
}
```

El orden de teardown en `MainWindow.HandleCierreAsync` ya es correcto: primero `Vm.Dispose()` (que dispara los `Desuscribir` en memoria), **después** `await _realtimeService.DesconectarAsync()` (el reset).

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `CapaDatos/Conexion.cs` | **Reescrito** — thread-safe (SemaphoreSlim, publish-after-init) + `ResetAsync()`. Es la copia que el Realtime realmente compila |
| `ServicioConexión/Conexion/ConexionSupabase.cs` | `ResetAsync()` añadido (consistencia con la copia anterior) |
| `CapaDatos/Realtime/RealtimeService.cs` | `Desuscribir` → keep-open; `DesconectarAsync` → `async` + reset total del cliente |

---

## Estado de compilación

- **Compilación correcta. 0 Errores** (34 warnings preexistentes, no relacionados).
- Verificado que no quedan referencias huérfanas en `RealtimeService.cs` (`CerrarCanal`, `.Unsubscribe(`, `Task.CompletedTask`: sin coincidencias).

---

## Impacto esperado

| Escenario | Antes | Después |
|---|---|---|
| Abrir un formulario | Crea canal + Push + timer | Igual la 1ª vez; reusa el canal en adelante |
| Cerrar y reabrir el mismo formulario | Apila bindings/handlers sobre el canal deduplicado; timers crecen | Reusa el canal existente — **sin apilar** |
| Navegar entre páginas/módulos | Churn de canales → fuga lenta | Memoria plana |
| Cerrar sesión → login | Socket y timers sobreviven → se arrastran | Socket dispuesto, cliente reconstruido → **cero arrastre** |
| `WebsocketClient._lastChanceTimer` / `_errorReconnectTimer` | Vivos tras logout | Liberados en `ResetAsync` |

---

## Verificación pendiente (test de aceptación para el usuario)

1. Arrancar la app y anotar memoria base (~108 MB).
2. Abrir Productos, navegar varias páginas, cerrar. Reabrir **N veces** (≥5) navegando cada vez.
   - **Esperado:** la memoria sube una vez al primer abrir y luego se mantiene **plana** (no escalona hacia arriba en cada reapertura).
3. Cerrar sesión y volver a iniciar sesión **varias veces**, abriendo Productos en cada sesión.
   - **Esperado:** la memoria al volver al login regresa cerca de la base; no crece sesión tras sesión.
4. (Opcional) Adjuntar el diagnóstico de timers de Visual Studio / dotnet-counters y confirmar que el conteo de `Timer` se estabiliza.

> Si en el paso 3 la memoria aún no baja del todo, el siguiente sospechoso son retenciones en CapaUI (ventanas/VMs), ya auditadas en [[Sesión 2026-05-28 - Eliminación Memory Leaks Ciclo Completo]] — no en el Realtime.

---

## Duda de arquitectura — clase `ConexionSupabase` duplicada

Existen **dos** clases `ConexionSupabase` en el **mismo namespace** `ServicioConexión.Conexion`, en ensamblados distintos:

- `CapaDatos/Conexion.cs` ← **la que compila la capa de datos** (repositorios + `RealtimeService`), porque `CapaDatos` **no referencia** el proyecto `ServicioConexión`.
- `ServicioConexión/Conexion/ConexionSupabase.cs` ← copia separada, usada (si acaso) por otros hosts.

Esto causó que el fix C1 de la sesión anterior se aplicara a la copia equivocada y no tuviera efecto en el Realtime. Ambas copias quedan ahora sincronizadas (thread-safe + `ResetAsync`), pero **deberían consolidarse en una sola**. Registrado como deuda técnica **P-009** en [[Deuda Técnica - Pendientes]].

---

## Relaciones

- [[Gestor Realtime - Diseño Arquitectónico]] — diseño canónico; **actualizado** en esta sesión de "cierra canal al desuscribir" → "keep-open + reset total"
- [[Sesión 2026-05-29 - Auditoría Profunda y Plan de Remediación Completo]] — origen de C1 (copia equivocada) y del fix de deadlock que aquí se preserva
- [[Sesión 2026-05-28 - Eliminación Memory Leaks Ciclo Completo]] — leaks de CapaUI (ventanas/VMs), complementarios a este fix de infraestructura
- [[Sesión 2026-05-28 - Fix Realtime ConnectAsync]] — historia previa del lifecycle del socket
- [[Deuda Técnica - Pendientes]] — P-009: consolidar `ConexionSupabase` duplicada
- [[Supabase .NET]] — referencia del SDK
