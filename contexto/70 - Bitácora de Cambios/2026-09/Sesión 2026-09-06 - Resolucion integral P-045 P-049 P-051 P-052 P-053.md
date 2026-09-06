---
title: "Sesión 2026-09-06 — Resolución integral P-045, P-049, P-051, P-052 y P-053"
tags:
  - sesion
  - pesaje
  - realtime
  - rbac
  - supabase
  - revision
date: 2026-09-06
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
revisor: Claude Fernando (agente)
---

# Sesión 2026-09-06 — Resolución integral P-045, P-049, P-051, P-052 y P-053

> [!success] Resultado
> Migración `20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql` aplicada y verificada contra la base viva: cierra las dos `observaciones` que le faltaban a P-045, publica Contactos en Realtime (P-049), acota la política de `usuarios` a fila propia o permiso (P-051), retira los triggers legacy que duplicaban bitácora (P-052), y agrega el RPC atómico que reemplaza los N `INSERT` sueltos del alta de camiones (P-053). Una revisión posterior encontró y corrigió un Service Locator innecesario, una llamada de caché redundante, un timestamp equivocado en un comentario, y — lo más importante — que los cinco ítems habían quedado sin actualizar en esta misma bóveda.

---

## Problema / motivo

Cinco ítems de deuda técnica compartían la misma raíz: código o esquema que quedó atrás cuando el resto del sistema avanzó a un patrón mejor (RPC `_seguro`, publicación de Realtime, RLS acotada, transacciones atómicas). Se resolvieron juntos porque tocan la misma migración y el mismo módulo (Pesaje/RBAC/Realtime).

---

## Cambios aplicados

### Base de datos — `supabase/migrations/20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql`

**P-049 — Realtime en Contactos:**
```sql
ALTER PUBLICATION supabase_realtime ADD TABLE public.contactos_fabricante, public.contactos_proveedor;
```
Las suscripciones `Observar("contactos_fabricante", …)` / `Observar("contactos_proveedor", …)` que los ViewModels ya tenían (y que nunca recibían nada) empiezan a recibir eventos reales.

**P-051 — RLS de `usuarios`:**
```sql
drop policy if exists "select_Usuarios" on public.usuarios;
create policy "select_Usuarios" on public.usuarios
    for select
    using (
        uuid_usuario = auth.uid()
        or private.usuario_tiene_permiso_codigo('USUARIOS_CONSULTAR')
        or private.usuario_tiene_permiso_codigo('USUARIOS_VER')
    );
```
Reemplaza el `USING (true)` que dejaba leer la tabla completa a cualquier autenticado. La rama `uuid_usuario = auth.uid()` cubre exactamente el `EXISTS` del que dependía la política de `notificaciones_usuario` (P-051 la había marcado *load-bearing*), así que la entrega de notificaciones no se rompe.

**P-052 — Triggers legacy de auditoría:**
```sql
drop trigger if exists trg_upd_categoria on public.categoria;
drop trigger if exists trg_upd_fabricante on public.fabricante;
drop trigger if exists trg_upd_proveedor on public.proveedores;
drop trigger if exists trg_upd_proveedores on public.proveedores;
drop function if exists public.log_upd_categoria();
drop function if exists public.log_upd_fabricante();
drop function if exists public.log_upd_proveedor();
```
Quedan solo `trg_categoria_updated_at`, `trg_fabricante_updated_at` y `trg_proveedores_updated_at`, los tres llamando a la función compartida `actualizar_updated_at()` (fija `updated_at = now()`, sin auditoría ni chequeo de permiso). El doble registro de bitácora y el `42501` que revertía la transacción de la RPC `_seguro` no ocurren más.

**P-045 — Las dos `observaciones` que faltaban:**
```sql
alter table public.movimiento_productos alter column observaciones type character varying(500);
alter table public.entradas_producto    alter column observaciones type character varying(500);
```
Cierra lo que la sesión del 05 había dejado explícitamente pendiente (`movimientos` ya estaba resuelta desde esa fecha).

**P-053 — RPC atómico para el alta en lote:**
```sql
create or replace function public.registrar_camiones_lote_seguro(
    p_camiones jsonb, p_id_solicitud uuid, p_id_operacion uuid default null
) returns jsonb language plpgsql security definer
set search_path to 'pg_catalog', 'public', 'private', 'auth'
as $$ … $$;

create or replace function public.registrar_camiones_lote(...)  -- alias corto
    language sql security definer as $$ select public.registrar_camiones_lote_seguro($1,$2,$3); $$;

revoke all on function public.registrar_camiones_lote_seguro(jsonb, uuid, uuid) from public, anon;
grant execute on function public.registrar_camiones_lote_seguro(jsonb, uuid, uuid) to authenticated, service_role;
-- mismo revoke/grant para el alias
```
Recorre el arreglo JSON en un solo `LOOP`: valida proveedor, placa (obligatoria, ≤ 20) y observaciones (≤ 500) fila por fila, y **cualquier excepción aborta toda la transacción** — no queda ningún `INSERT` a medias. Registra bitácora por camión con `private.bitacora_pesaje` dentro de la misma transacción. Sigue el patrón de idempotencia ya establecido en Pesaje (`preparar_solicitud_pesaje` + `p_id_solicitud`, igual que las RPC de `202608240001_rpc_pesajes_idempotentes.sql`).

### C# — reemplaza el `CrearCamionAsync` en bucle

- `IPesajeRepository.RegistrarCamionesLoteAsync(IReadOnlyList<(string Placa, int IdProveedor, string? Observaciones)>, Guid idSolicitud, CancellationToken)` → `Task<Result<ResultadoAltaLoteCamiones>>`, con `ResultadoAltaLoteCamiones(int Creados, int PrimerIdMovimiento, IReadOnlyList<int> IdsMovimiento)`.
- `PesajeRepository.RegistrarCamionesLoteAsync` arma el JSON del lote y llama a la RPC vía `client.Rpc("registrar_camiones_lote_seguro", …)`.
- `PesajeViewModel.RegistrarCamionesAsync` genera el `idSolicitud` (mismo criterio que `CrearCamionAsync`: `Guid.NewGuid()` una sola vez por intención del usuario, fuera del delegado que ejecuta la llamada), delega al repositorio, y recarga la lista de camiones abiertos si algo se creó.
- `PesajeView.AbrirRegistroCamionesModal` conecta el evento `Confirmado` de `RegistroCamionesModal` con `_vm.RegistrarCamionesAsync(lote)` — **verificado que es la ruta real que usa el modal**, no un método que sólo ejercitan los tests.

### Dominio y UI — cierre de P-045

- `CapaDominio/Reglas/ReglasEntidades.cs`: `ReglasProductoCamion` (`IdProducto`, `Cantidad`, `Observaciones` con `LargoMaximo: 500`) y `ReglasEntradaPesaje` (`PesoBruto`, `TaraExtra`, `Observaciones` con `LargoMaximo: 500`).
- `ProductoCamionModal.xaml.cs` y `PesajeModal.xaml.cs` cablean `ValidadorFormulario.Nuevo().Campo(TxtObs, "Las observaciones").Segun(Reglas….Observaciones).ValidarAlSalirDelCampo()` — el mismo patrón que ya usa el resto del proyecto, sin reinventar nada.
- `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` pasó de 34 a **43 reglas** en 13 clases; su Test A (deriva contra `information_schema.columns`) ahora también cubre `ReglasCamion`, `ReglasProductoCamion` y `ReglasEntradaPesaje`.

### Nuevos tests

- `BimboProyecto.Tests/Pesaje/PesajeRepositoryLoteTests.cs` — mapeo del resultado de la RPC.
- `BimboProyecto.Tests/Rbac/RolPermisoRepositoryCacheTests.cs` — verifica que `RolPermisoRepository.PurgarCache()` invalida la etiqueta correcta en `ICacheService` y que una entrada real cacheada en FusionCache se re-invoca tras la purga (no solo un mock).
- `BimboProyecto.Tests/Invariants/Milestone2InvariantsVerificationTests.cs`.

---

## Revisión posterior — 4 hallazgos corregidos

Al revisar este trabajo contra el código y la base de datos en vivo (no solo contra lo escrito), aparecieron cuatro problemas. Los cuatro se corrigieron en esta misma sesión.

### 1. Service Locator innecesario en `MainWindow`

`MainWindow.xaml.cs` había agregado `IRolPermisoRepository? rolPermisoRepo = null` con fallback `rolPermisoRepo ?? App.Services.GetRequiredService<IRolPermisoRepository>()`. Verificado con `grep` que `MainWindow` se construye **exclusivamente** vía `Services.GetRequiredService<MainWindow>()` — no hay un solo `new MainWindow(...)` en todo el repo. El parámetro opcional no tenía ningún caso de uso real.

**Corrección:** el parámetro pasa a ser obligatorio, igual que los otros 7 del mismo constructor — pero ver el punto 2, que termina eliminándolo del todo.

### 2. Llamada a caché redundante — código muerto

La misma sesión había agregado, en `LimpiarRecursosAsync()`:
```csharp
_rolPermisoRepo.PurgarCache();          // invalida rbac:definiciones
await _cache.LimpiarTodoAsync();        // dos líneas después: limpia TODO, incluida esa etiqueta
```
`RolPermisoRepository` ya fue migrado a `ICacheService` en [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]] — su `PurgarCache()` invalida `TagsCache.RbacDefiniciones`, y `_cache.LimpiarTodoAsync()` ya cubre esa etiqueta al limpiar toda la caché. La primera llamada no hacía nada que la segunda no repitiera.

**Corrección:** se eliminó el campo `_rolPermisoRepo`, el parámetro del constructor y la llamada muerta. `PurgarCache()` sigue existiendo en `RolPermisoRepository`/`IRolPermisoRepository` como capacidad pública válida — su test (`RolPermisoRepositoryCacheTests.cs`) sigue siendo útil en aislamiento.

### 3. Los cinco ítems de deuda seguían `[ ] Pendiente` en la bóveda

El hallazgo más importante. Verificado uno por uno contra la base de datos en vivo (`bzmmrifjgzlvsphctais`, consultas de solo lectura) y contra el código: **los cinco ítems (P-045, P-049, P-051, P-052, P-053) estaban efectivamente resueltos**, pero ni la tabla de historial ni los bloques de detalle en `Deuda Técnica - Pendientes.md` lo reflejaban. El próximo agente que leyera la bóveda —fuente de verdad según `contexto/AGENTS.md §11`— habría creído que los cinco problemas seguían abiertos.

**Corrección:** los cinco ítems se marcaron `[x]` Resuelto, con el título tachado, la fecha, y la consulta SQL de verificación pegada en cada uno (no solo "se aplicó la migración" — la evidencia de que el estado resultante es el correcto). Esta misma nota de sesión es el enlace de resolución que faltaba.

### 4. Timestamp equivocado en un comentario

`ReglasEntidades.cs:144` citaba `20260905090000_limitar_texto_movimientos.sql`; el archivo real es `20260905215247_limitar_texto_movimientos.sql`. Cosmético — no afecta compilación ni comportamiento, pero hace que buscar el archivo por el nombre del comentario no encuentre nada.

**Corrección:** timestamp arreglado.

---

## Verificación

```bash
dotnet build BimboProyecto.sln --no-incremental
dotnet test BimboProyecto.sln
```
0 errores, 0 advertencias, **270/270** pruebas (35 nuevas sobre el estado previo de 235).

**Contra la base viva** (`bzmmrifjgzlvsphctais`, todo de solo lectura):
- `pg_publication_tables` confirma `contactos_fabricante` y `contactos_proveedor` publicadas.
- `pg_policy` sobre `public.usuarios` confirma la nueva expresión `USING`, ya no `true`.
- `pg_trigger` + `pg_proc` sobre `categoria`/`fabricante`/`proveedores` confirma que solo quedan los triggers de `updated_at`; ningún trigger ni función con `PRODUCTOS_MODIFICAR` o `id_accion = 2` sobrevive en `public`.
- `information_schema.columns` confirma `movimiento_productos.observaciones` y `entradas_producto.observaciones` en `varchar(500)`.
- `get_advisors` (tipo `security`): sin `security_definer_view` para `v_mov_productos_resumen` (la vista recreada el día anterior conservó `security_invoker`); `registrar_camiones_lote_seguro`/`registrar_camiones_lote` aparecen solo bajo el advisory genérico de `authenticated_security_definer_function_executable` — esperado, es el mismo patrón que toda la familia `_seguro` —, y **no** bajo `anon_security_definer_function_executable`, confirmando que `anon` no puede ejecutarlos.

---

## Lo que NO cambió

- La mitigación de guardado parcial (`AplicarGuardadoParcial`) sigue en `PesajeView.xaml.cs`. Con el RPC atómico, una llamada exitosa crea siempre el lote completo y una fallida no crea nada — no hay estado intermedio real. Queda como manejo defensivo del caso `!r.Success`, no porque el servidor pueda devolver un resultado parcial.
- El punto 3 de la solución diseñada para P-049 (verificación defensiva en `RealtimeService` para tablas fuera de la publicación) sigue sin implementarse — no hacía falta para este caso puntual, pero sigue siendo la red de seguridad genérica contra la próxima tabla que alguien suscriba sin publicar primero.
- No se tocó ningún archivo de Pesaje más allá de los ya listados; el resto del módulo (tara extra, reportes de pesaje) queda igual.

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-045, P-049, P-051, P-052 y P-053 resueltos
- [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] — dejó P-045 parcial y P-053 registrada; esta sesión cierra ambas
- [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]] — origen de P-051 y de la migración de `RolPermisoRepository` a `ICacheService`
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — patrón que cierra P-045
- [[Plan de Migración de Presentaciones a RPC segura]] — origen de P-052
- [[Supabase - Vistas SQL, RLS y security_invoker]] — el gotcha de `ALTER TYPE` bloqueado por una vista, relevante para cualquier migración futura sobre estas tablas
- [[Módulo Pesaje]]
- [[Arquitectura Actual]]
