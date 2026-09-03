---
title: "Sesión 2026-09-03 — Implementación de ADR-026 y socket Realtime autenticado"
tags:
  - sesion
  - cache
  - realtime
  - fusioncache
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando (agente)
---

# Sesión 2026-09-03 — Implementación de ADR-026 y socket Realtime autenticado

> [!success] Resultado
> Implementado [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] completo (fases 0 a 4) y corregido un bug de producción por el que el socket de Realtime corría autenticado como `anon`. Una revisión posterior encontró y cerró una colisión de claves de caché introducida en la misma sesión (P-050). Build en 0 errores / 0 advertencias y 235/235 pruebas.

---

## Problema / motivo

Dos frentes independientes que se cruzaron en la misma sesión.

**1. Caché de catálogos.** El `CatalogoCache` estático de [[ADR-015 - Cache de catalogos mostrar y revalidar]] revalidaba en **cada** apertura, así que un acierto de caché pagaba el viaje a la red igual. Además no tenía expiración, vivía en `CapaUI` (la vista decidiendo política de datos), no protegía contra estampidas, y —lo más grave— `InvalidarTodo()` no tenía ningún call site: los catálogos de un usuario sobrevivían a su cierre de sesión.

**2. Notificaciones caídas.** El panel mostraba `ERROR P0001 invalid column for filter id_usuario` pese a que la columna existe.

---

## Cambios aplicados

### Caché (ADR-026)

**Contratos — `CapaAplicacion4/Common/Cache/`** (sin dependencias externas; el paquete NuGet vive solo en `CapaDatos`):
- `ICacheService.cs` — `ObtenerOCrearAsync`, `Invalidar`, `InvalidarEtiqueta` (síncrono), `InvalidarEtiquetaAsync`, `LimpiarTodoAsync`.
- `PoliticaCache.cs` — record con duración, jitter, tolerancia a valor viejo y umbral de refresco anticipado.
- `TagsCache.cs` — etiquetas canónicas y `DeCatalogo(tabla)`.
- `IInvalidadorCacheRealtime.cs` — `Suscribir()` / `Desuscribir()`.

**Infraestructura — `CapaDatos/Cache/`**:
- `FusionCacheService.cs` — adaptador sobre `IFusionCache`, única clase que conoce la librería.
- `PoliticasCache.cs` — 4 familias: UltraEstable 24 h, Negocio 2 h, Dinámico 30 min, RBAC 1 h.
- `InvalidadorCacheRealtime.cs` — suscriptor de vida larga sobre las 8 tablas de catálogo publicadas.

**Decorador — `CapaDatos/Repositories/Catalogos/CachedCatalogoRepository.cs`**: implementa `ICatalogoRepository`, así que ni `CapaAplicacion` ni `CapaUI` saben que hay caché.

**Eliminado**: `CapaUI/Core/Catalogos/CatalogoCache.cs` (178 líneas) y el `static _catalogoCache` + `SemaphoreSlim` de `RolPermisoRepository`, que ahora usa `ICacheService` con etiqueta `rbac:definiciones`.

**Ciclo de vida — `CapaUI/Formularios/Principal/MainWindow.xaml.cs`**: `Suscribir()` en `OnLoaded` de cada sesión; en `LimpiarRecursosAsync`, `LimpiarTodoAsync()` + `Desuscribir()` **antes** de `DesconectarAsync()`.

**Consumidores migrados**: `SelectorCatalogoModal` (se retiró `alRevalidar` y el repintado en caliente), `ProductosViewModel` (4 combos) y `ProductoModal` (combo de unidades).

### Socket Realtime autenticado — `CapaDatos/Realtime/RealtimeService.cs`

`grep -rn "SetAuth" --include=*.cs .` no devolvía **ninguna** aparición en todo el repo. `ConexionSupabase` crea el cliente con `AutoConnectRealtime = true`, así que el WebSocket se conectaba con la anon key, y `AuthService.LoginAsync` descartaba el `AccessToken` de la sesión.

`realtime.subscription_check_filters()` arma la lista de columnas válidas filtrando por `has_column_privilege(claims->>'role', ...)`. Verificado contra la base: `authenticated` tiene SELECT sobre las 6 columnas de `notificaciones_usuario`, y `anon` sobre **ninguna**. Con rol `anon` la lista vuelve vacía y toda columna se rechaza.

Solo se rompían las notificaciones porque es **la única suscripción del sistema con filtro**: las 8 `Observar(tabla, handler)` sin filtro nunca ejecutan ese bucle de validación.

Se agregó `AplicarTokenDeSesion(client)` —invocado en `AbrirCanalAsync` antes de conectar— y un listener de `AuthState.SignedIn` / `TokenRefreshed` dentro del guard `_estadoHandlerRegistrado` ya existente.

> [!warning] El arreglo que NO hay que hacer
> `GRANT SELECT ON notificaciones_usuario TO anon` daría a cualquier cliente sin autenticar lectura de las notificaciones de todos los usuarios. El problema no es que a `anon` le falten permisos: es que el socket no debe correr como `anon`.

---

### Colisión de claves detectada en revisión (P-050)

La primera versión del decorador armaba la clave como `catalogos:{tabla}[:{alcance}]`, **sin el tamaño solicitado**. `SelectorCatalogoModal.CargarPaginaAsync` pide `("", 1, PageSize)`, que en la ruta `ForzarPaginacion` es `("", 1, 50)` y colisionaba con el `("", 1, 200)` de la lupa normal.

Con un catálogo de, por ejemplo, 120 filas —más que el `PageSize` pero menos que el umbral de memoria— abrir primero la lupa dejaba 120 filas cacheadas, y el selector paginado las recibía enteras para una página que declaraba ser de 50.

Corregido incluyendo el tamaño en la clave: `$"{TagsCache.CatalogosRaiz}:{tabla}:{ambito}:{size}"`. Las etiquetas no cambian, así que la invalidación reactiva sigue alcanzando todas las variantes.

---

## Verificación

```
dotnet build BimboProyecto.sln --no-incremental
Compilación correcta.  0 Advertencia(s)  0 Errores

dotnet test BimboProyecto.sln
Correctas! - Con error: 0, Superado: 235, Omitido: 0, Total: 235
```

223 pruebas previas intactas + 12 nuevas:

- `BimboProyecto.Tests/Cache/FusionCacheServiceTests.cs` (7) — etiquetas compuestas, single-flight con 20 llamadas concurrentes, fallo que no envenena la clave, `esCacheable` en falso y purga total sin resucitación por fail-safe.
- `BimboProyecto.Tests/Cache/CachedCatalogoRepositoryTests.cs` (5) — contrato de claves del decorador: tamaños distintos, alcances distintos, término y página que esquivan la caché, y vista parcial que no se retiene.

La prueba de P-050 se validó contra el código defectuoso: con la clave anterior falla, con el arreglo pasa. Una prueba que no puede fallar no fija nada.

**Dependencias comprobadas empíricamente** con `dotnet list package --include-transitive`: `ZiggyCreatures.FusionCache 2.0.2` mantiene todo el árbol en `Microsoft.Extensions.* 8.x`.

**Pendiente de prueba manual**: el repintado en caliente de la lupa desapareció junto con `alRevalidar` — es el único cambio visual perceptible. También falta validar el flujo de notificaciones con un login real.

---

## Lo que NO cambió

- **La grilla sigue sin caché.** `IProductoRepository.GetPagedAsync` está registrado sin decorar, a propósito: la clave tendría que incluir página, tamaño, cinco filtros, orden y término, y la tasa de aciertos tendería a cero.
- **Zonas sin caché** confirmadas: Pesaje, bitácora, notificaciones, reportería, `acciones_roles`, sesión y permisos del usuario, contactos y sugerencias del buscador.
- **No se creó la función SQL `tablas_publicadas_realtime()`** de la trampa 5 del ADR. El mapa del invalidador cubre exactamente las 8 tablas publicadas, así que hoy no hay desalineo, pero no hay red de seguridad si alguien agrega una tabla no publicada.
- **`contactos_fabricante` / `contactos_proveedor`** siguen con sus `Observar()` inertes — es [[Deuda Técnica - Pendientes#P-049]] y es una decisión de producto.
- **No se agregó precarga de catálogos.** La primera apertura de cada lupa en la sesión sigue pagando su viaje a la red; ahora se nota más porque es el único momento lento que queda.

---

## Relaciones

- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] — diseño implementado en esta sesión
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — arquitectura reemplazada (sigue en `estado: aceptado` hasta validar en producción)
- [[Deuda Técnica - Pendientes]] — origen de P-050 (resuelto en la misma sesión) y P-051
- [[Arquitectura Actual]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]] — módulo que el fix de Realtime desbloquea
