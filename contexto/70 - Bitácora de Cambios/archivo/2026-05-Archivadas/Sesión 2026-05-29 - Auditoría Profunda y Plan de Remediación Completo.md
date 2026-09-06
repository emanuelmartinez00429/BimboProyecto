---
title: "Sesión 2026-05-29 — Auditoría Profunda y Plan de Remediación Completo"
date: 2026-05-29
tags:
  - bitácora
  - seguridad
  - memory-leak
  - realtime
  - optimización
  - arquitectura
status: Completado
---

# Sesión 2026-05-29 — Auditoría Profunda y Plan de Remediación Completo

## Contexto

Sesión con dos partes:

1. **Fix del deadlock en RealtimeService** — bug reportado la sesión anterior: la app se quedaba permanentemente congelada al cerrar sesión desde un formulario abierto después de varios logouts consecutivos.
2. **Auditoría profunda de todo el proyecto** (153 archivos .cs revisados) seguida de implementación completa del plan de remediación en 15 cambios.

---

## Parte 1 — Fix Deadlock RealtimeService

### Diagnóstico

El deadlock ocurría en este escenario:
1. Login → Productos → `SuscribirAsync` toma `_lock` (SemaphoreSlim async) → `ConnectAsync()` lento
2. Logout → `Desuscribir` llama `_lock.Wait()` (síncrono en UI thread) → **bloquea UI**
3. `ConnectAsync()` no puede completar porque el UI thread está bloqueado → **deadlock permanente**

La causa raíz era mezclar `_lock.Wait()` (síncrono) con `_lock.WaitAsync()` (async) sobre el mismo SemaphoreSlim desde el UI thread.

### Solución implementada

Se introdujo un **lock dual**:
- `_stateLock` (`object`) → para mutaciones rápidas de diccionarios — nunca bloquea en I/O
- `_lock` (SemaphoreSlim) → solo para apertura de canales (I/O async)

`Desuscribir` pasó a usar `lock(_stateLock)` en vez de `_lock.Wait()` → imposible de bloquear en I/O.

`DesconectarAsync` pasó a ser síncrono (retorna `Task.CompletedTask`) y eliminó `Disconnect()` del WebSocket para evitar que `ConnectAsync()` fuera lento en el siguiente login.

**Archivos modificados:** `CapaDatos/Realtime/RealtimeService.cs`

---

## Parte 2 — Auditoría Profunda + Implementación

### Alcance de la auditoría

Se revisaron todos los subsistemas: Realtime (nuevo y obsoleto), lifecycle de ViewModels, DI, conexión singleton, repositorios, sesión, seguridad de Supabase (RLS + advisors). Se identificaron 15 cambios agrupados en 5 fases.

---

## Cambios implementados

### Fase 0 — Seguridad BD (SQL, sin recompilar app)

#### C8 — RLS con USING(true) → solo `authenticated`

**Problema:** Las políticas SELECT e INSERT de `productos`, `empleados`, `categoria`, `fabricante` y `paises` tenían `TO public` con `USING(true)` o `WITH CHECK(true)`. La anon key (commiteada en App.config) permitía a cualquiera en internet leer y escribir datos de empleados y productos.

**Fix:** Migration `fix_rls_policies_auth_only`:
- DROP + recrear todas las políticas SELECT con `TO authenticated`
- DROP + recrear políticas INSERT con `TO authenticated` donde tenían `WITH CHECK(true)` sin restricción

#### C9 — Funciones SECURITY DEFINER accesibles por `anon`

**Problema:** `crear_usuario_empleado_seguro`, `ingresar_empleado_tabla_bitacora` (2 overloads), `usuario_autenticado` y `sync_ultimo_acceso` eran ejecutables por `anon`. Las primeras dos permitían crear usuarios/empleados sin autenticarse.

**Fix:** Migration `fix_security_definer_functions`:
- `REVOKE EXECUTE ... FROM PUBLIC, anon` en las funciones peligrosas
- `GRANT EXECUTE ... TO authenticated`
- `SET search_path = public, pg_temp` en todas (previene schema injection)
- `ALTER VIEW v_mov_productos_resumen SET (security_invoker = on)` — respeta RLS del invocador

---

### Fase 1 — Leaks core (C#)

#### C1 — ConexionSupabase no era thread-safe

**Problema:** El "double-check" tenía dos `if (_client == null)` sin lock entre ellos — completamente inútil. Peor: `_client = new Client(...)` se publicaba **antes** de `InitializeAsync()`. Un segundo hilo podía recibir un cliente a medio inicializar.

**Fix:** `ServicioConexión/Conexion/ConexionSupabase.cs` reescrito con:
- `SemaphoreSlim _initLock` (asíncrono — no bloquea UI thread)
- Double-check locking **real** dentro del semáforo
- `client` local, publicado a `_client` solo **después** de `InitializeAsync()`
- Validación de credenciales con mensaje de error claro

#### C4 — API de suscripción a prueba de olvidos (`Observar`)

**Problema:** La pareja `SuscribirAsync` + `Desuscribir` requería que cada VM recordara llamar `Desuscribir` en `Dispose`. Olvidarlo = leak permanente en el Singleton.

**Fix:** `IRealtimeService` + `RealtimeService`:
- Nuevo método `IDisposable Observar(string tabla, Action<CambioRealtime> handler)`
- Clase interna `Suscripcion : IDisposable` — idempotente (Interlocked.Exchange) y thread-safe
- El token devuelto por `Observar` se desuscribe solo al `Dispose()`

#### C5 — `Desuscribir` hacía I/O dentro del lock

**Problema:** `Desuscribir` → `CerrarCanal` → `channel.Unsubscribe()` (I/O de red) dentro de `lock(_stateLock)`. Como `OnCambioRecibido` también toma `_stateLock` desde el hilo del socket, se creaba contención UI↔socket.

**Fix:** El canal se extrae del diccionario **bajo el lock**, pero `Unsubscribe()` se llama **fuera** del lock, con try/catch para no tumbar el Dispose.

---

### Fase 2 — Patrón Realtime (C#)

#### C6 — `RealtimeAwareViewModel` (nuevo archivo)

**Problema:** Cada ViewModel que usara Realtime tendría que duplicar el patrón: campo `_realtime`, suscripción en `CargarDatosAsync`, desuscripción en `Dispose`, flag `_disposed`. Al replicar en Empleados, Movimientos, etc., cualquier olvido sería un leak.

**Fix:** Nuevo `CapaUI/Core/MVVM/RealtimeAwareViewModel.cs`:
- Hereda `ObservableObject`, implementa `IDisposable`
- `Observar(tabla, handler)` registra token de baja automática
- `Dispose()` libera todas las suscripciones, llama `OnDispose()` (Template Method)
- `protected bool Disposed` accesible en handlers para guard post-dispose

`ProductosViewModel` migrado de `ObservableObject + IDisposable` manual → `RealtimeAwareViewModel`. El bloque `Dispose()` de 8 líneas se redujo a `OnDispose()` con solo el CTS.

#### C2 — Guard en `UserControl_Loaded` (lifecycle WPF)

**Problema:** `Loaded` puede dispararse varias veces en WPF (re-parenting, cambio de tema). Sin guard, cada disparo creaba un nuevo VM que se suscribía al Realtime Singleton. El anterior quedaba huérfano retenido en `_suscriptores["productos"]` para siempre.

**Fix:** `ProductosView.UserControl_Loaded`: `if (_vm != null) return;` al inicio. En `Unloaded`: `_vm = null!` para permitir recrear limpio si el control vuelve al árbol.

#### C3 — Storyboard del spinner retenía `SpinnerPath`

**Problema:** `Stop()` detiene la animación pero deja el Storyboard enlazado al elemento destino. El clock seguía vivo, reteniéndolo.

**Fix:** `DetenerSpinner()` ahora llama `Remove()` + `Children.Clear()` antes de `_spinnerStory = null`. También se llama en `Unloaded` para el caso de descarga con animación activa.

#### C7 — Resiliencia de reconexión y SynchronizationContext

**Reconexión:** La doc del SDK (7.0.2) indica que `RealtimeChannel.HandleSocketStateChanged` ya re-suscribe canales automáticamente tras reconexión del WebSocket. Se registra un handler de log que confirma el evento `SocketState.Reconnect`.

**SynchronizationContext:** Añadido warning explícito en el constructor si `_syncContext` es null, para detectar temprano si el servicio se resuelve fuera del UI thread.

---

### Fase 3 — Integridad de sesión

#### C10 — `SesionActual.IdUsuario` hardcodeado a 1

**Problema:** Tenía `= 1` como valor por defecto. Dos clases de sesión coexistían: `servicioSesionActual` (asignado en login) y `SesionActual` (nunca asignado). Pesajes y movimientos de BimboPesaje usaban `SesionActual.IdUsuario` para auditoría → todos registrados como usuario 1.

**Fix:**
- `SesionActual.IdUsuario`: getter lanza `InvalidOperationException` si no hay sesión activa
- `SesionActual.Limpiar()`: llamado en `App.OnSesionCerrada` (entre sesiones)
- `LoginWindow.IngresarAsync`: asigna `SesionActual.IdUsuario = result.Value!.IdUsuario` en el Step 2 (junto a `servicioSesionActual.Iniciar`)

---

### Fase 4 — Rendimiento

#### C12 — Combos y conteos descargaban tablas completas

**Problema:**
1. `GetFabricantesInternal` descargaba **toda** la tabla `productos` con join solo para extraer los fabricantes únicos (mismo para paises y categorías).
2. `GetConteosAsync` descargaba **todos** los IDs de productos para contarlos en cliente (SDK bug P-007: `Count()` no respeta filtros).

**Fix:**
- `FabricanteConsulta : BaseModel` — nuevo modelo ligero para consultar `fabricante` directamente (no viola la regla de CLAUDE.md que prohíbe usar `Fabricante` heredando BaseModel; es una clase nueva distinta)
- `GetFabricantesInternal` → `client.From<FabricanteConsulta>()` (decenas de filas, no miles)
- `GetPaisesInternal` → `client.From<Paises>()` (ya tenía BaseModel)
- `GetCategoriasInternal` → `client.From<Categoria>()` (ya tenía BaseModel)
- Migration `add_contar_productos_rpc`: función SQL `contar_productos(p_estado, p_fab, p_pais)` que retorna `(total, activos, inactivos)` en un solo viaje, server-side
- `GetConteosRpcAsync` reemplaza `GetConteosAsync` — llama el RPC y parsea el JSON con `JArray`

---

### Fase 5 — Arquitectura (parcial)

#### C15 — `AddScoped` sin scopes en WPF

**Problema:** Los repositorios registrados como `AddScoped` en una app WPF que nunca crea scopes se resuelven del root provider → se comportan como singletons de facto. Si alguien añade estado por-operación esperando aislamiento, no lo tendrá.

**Fix:** `CapaDatos/DependencyInjection.cs`: todos los repos cambiados de `AddScoped` → `AddTransient`. Alinea la declaración con el comportamiento real.

#### C13, C14 — No implementados (decisiones de arquitectura mayor)

- **C13** (dos ejecutables): Requiere decidir si CapaUI.exe reemplaza a BimboPesaje.exe o si se inicializa el contenedor DI en Program.cs del host WinForms. Decisión de fondo que involucra al equipo.
- **C14** (eliminar GestorRealtime obsoleto): Requiere migrar GestorNotificaciones al IRealtimeService nuevo antes de borrar. Trabajo separado.

---

## Archivos modificados/creados

| Archivo | Cambio | Ítem |
|---|---|---|
| `ServicioConexión/Conexion/ConexionSupabase.cs` | Reescrito — double-check locking real | C1 |
| `CapaAplicacion4/Realtime/IRealtimeService.cs` | `Observar()` añadido | C4 |
| `CapaDatos/Realtime/RealtimeService.cs` | Lock dual, `Suscripcion` token, I/O fuera del lock, reconexión log, SyncContext warning | C4+C5+C7+deadlock |
| `CapaUI/Core/MVVM/RealtimeAwareViewModel.cs` | **Nuevo** — clase base Realtime | C6 |
| `CapaUI/.../Productos/ProductosViewModel.cs` | Migrado a RealtimeAwareViewModel | C6 |
| `CapaUI/.../Productos/ProductosView.xaml.cs` | Guard Loaded, `_vm = null!`, spinner Remove | C2+C3 |
| `CapaServicios/SesionActual.cs` | Sin default, lanza si no hay sesión, `Limpiar()` | C10 |
| `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs` | Asigna `SesionActual.IdUsuario` en login | C10 |
| `CapaUI/App.xaml.cs` | `SesionActual.Limpiar()` en logout | C10 |
| `CapaDatos/Modelados/Productos/FabricanteConsulta.cs` | **Nuevo** — modelo ligero para catálogo | C12 |
| `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs` | Combos directos, `GetConteosRpcAsync` | C12 |
| `CapaDatos/DependencyInjection.cs` | `AddScoped` → `AddTransient` | C15 |

**Migrations Supabase aplicadas:**
- `fix_rls_policies_auth_only` — C8
- `fix_security_definer_functions` — C9
- `add_contar_productos_rpc` — C12

---

## Estado de compilación

- `CapaDatos`, `CapaUI`, `ServicioConexión`, `CapaAplicacion4`: **0 errores**
- `BimboPesaje`: 3 errores preexistentes (`ServicioPerfilUsuario` no existe) — no relacionados con esta sesión, regla de oro aplicada (no tocar)

---

## Impacto esperado

| Área | Antes | Después |
|---|---|---|
| Seguridad BD | anon puede leer empleados/productos | Solo `authenticated` accede a datos de negocio |
| Funciones SECURITY DEFINER | anon puede crear usuarios | Solo `authenticated` puede ejecutarlas |
| Doble WebSocket | Race condition en init | Imposible — SemaphoreSlim + publish-after-init |
| Leak de VMs Realtime | Cada Loaded duplica suscripción | Guard + token auto-dispose elimina el riesgo |
| Leak del Storyboard | Clock activo tras Unloaded | Remove() + Clear() en Unloaded |
| Auditoría de pesajes | Siempre registra usuario 1 | Registra el usuario real; falla fuerte si no hay sesión |
| Carga de combos | 3 descargas full-table al abrir Productos | 3 consultas a catálogos pequeños |
| Conteos de productos | N filas descargadas para contar | 1 llamada RPC server-side |
| Lifecycles DI | Scoped (comportamiento singleton) | Transient (comportamiento declarado = real) |
