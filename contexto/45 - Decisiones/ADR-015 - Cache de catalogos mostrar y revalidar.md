---
title: "ADR-015 — Caché de catálogos: mostrar y revalidar"
type: adr
status: vigente
tags:
  - adr
  - decision
  - cache
  - realtime
date: 2026-08-13
updated: 2026-08-13
summary: "La caché no tenía vencimiento ni invalidación efectiva. Editar una presentación en la base no se veía nunca en la lupa: recargar la grilla va por otro camino y…"
scope: []
symbols:
  - CatalogoCache
  - FiltroItem
  - IRealtimeService
  - Observar
  - ObtenerCompletoAsync
  - ObtenerParaComboAsync
  - ProductosViewModel
  - RealtimeService
  - SonIguales
estado: aceptado
---

# ADR-015 — Caché de catálogos: mostrar y revalidar

## Contexto

`CatalogoCache` guarda en un `static ConcurrentDictionary` los catálogos que entran enteros en la primera página (`Total <= 200`): presentaciones, taras, categorías, fabricantes, proveedores, países y unidades. A la escala real del proyecto son **todos** — el más grande tiene 11 filas.

La caché **no tenía vencimiento ni invalidación efectiva**. Editar una presentación en la base no se veía nunca en la lupa: recargar la grilla va por otro camino y no toca esa memoria, así que el único modo de refrescarla era cerrar la aplicación.

La invalidación prevista no funciona. `ProductosViewModel` hace `Observar("fabricante", _ => CatalogoCache.Invalidar("fabricantes"))`, pero la publicación `supabase_realtime` solo tiene `categoria`, `empleados`, `entradas_producto`, `movimiento_productos`, `movimientos`, `paises`, `productos` y `usuarios`. Ni `presentacion_producto`, ni `fabricante`, ni `proveedores`, ni `tara`. Ese handler lleva tiempo sin ejecutarse y nadie se enteró — el modo de falla es silencioso.

Restricción de producto: **al reabrir la lupa el dato tiene que estar correcto**, no "correcto dentro de un rato".

## Decisión

Patrón *stale-while-revalidate* dentro de la propia caché.

`ObtenerCompletoAsync` y `ObtenerParaComboAsync` aceptan un callback `alRevalidar` opcional. Ante un hit:

1. Devuelven la lista cacheada **al instante**, sin esperar red.
2. En paralelo vuelven a consultar la tabla.
3. Si la lista es idéntica (`SonIguales`, campo por campo — `FiltroItem` es `class` y no tiene igualdad por valor) **no se avisa nada**: la pantalla ni se entera y no parpadea.
4. Si cambió, se actualiza la caché y se invoca `alRevalidar`, que repinta preservando la búsqueda y la fila marcada.
5. Si el catálogo creció más allá del umbral, se invalida la entrada para que la próxima apertura arranque en modo paginado.

La revalidación es silenciosa ante errores: en pantalla hay una lista válida, y molestar con un error por una verificación de fondo sería peor que quedarse con lo que había.

Consecuencia explícita y aceptada: **se hace una consulta por apertura de lupa, exactamente las mismas que si no hubiera caché**. La caché deja de servir para ahorrar consultas y pasa a servir para pintar al instante.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| **Mostrar y revalidar** | Abre en 0 ms y siempre se corrige contra la base. Un solo lugar (`CatalogoCache`) cubre los 7 catálogos y los que se agreguen. No depende de configuración externa. | 1 consulta por apertura (<1 KB). Requiere repintar preservando estado de la tabla. | ✅ |
| Vencimiento por tiempo (TTL 60 s) | Trivial de implementar. | **Deja una ventana en la que lo mostrado es viejo** — es justo lo que había que eliminar. Rechazado por el usuario. | ❌ |
| Invalidar por Realtime | 0 consultas por apertura. Reusa `IRealtimeService`. | Necesita `ALTER PUBLICATION` sobre producción **y** un suscriptor de vida larga: `RealtimeService` cierra el canal con el último suscriptor, y los `Observar` viven en los ViewModels, así que un cambio hecho con esa pantalla cerrada no lo escucha nadie. Obliga a mantener 4 lugares de acuerdo por catálogo (publicación, `_pkColumns`, `Observar`, clave de caché) — uno ya está desalineado: `_pkColumns` dice `"taras"` y la tabla es `tara`. **Su falla es silenciosa.** | ❌ |
| Sacar la caché de las lupas | Lo más simple de entender y mantener; el dato siempre es el de la base. | Cada apertura muestra "Cargando…" ~300 ms. | ❌ |

Costo en dinero: los cuatro empatan en ~0 a esta escala. La decisión se tomó por mantenimiento y modo de falla, no por consumo.

## Consecuencias

- **Se gana:** apertura instantánea y dato siempre verificado contra la base, sin depender de la configuración de Realtime ni de que el websocket esté vivo.
- **Se sacrifica:** una consulta por apertura de lupa y por entrada a la pantalla de Productos. Payload menor a 1 KB.
- **Degradación aceptable:** si la revalidación falla, no se actualiza esa vez y se reintenta en la siguiente apertura. No puede podrirse en silencio como la invalidación por Realtime.
- **Compatible con Realtime:** si más adelante se publican los catálogos, `Invalidar(...)` se suma encima sin tocar el selector. Los dos diseños conviven — ver P-034 en [[Deuda Técnica - Pendientes]].
- **Cuidado al repoblar combos:** `ComboFiltro.Poblar` vuelve a "(Todos)" y limpia su id sin notificar. Por eso la revalidación de los combos de filtro se saltea cuando ese filtro está en uso; si no, el combo diría "(Todos)" con la grilla todavía filtrada.

---

## Relaciones

- [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]] — implementación
- [[ADR-014 - Precarga unica y cache del catalogo RBAC]] — otra caché de sesión, con otro criterio de invalidación
- [[Gestor Realtime - Diseño Arquitectónico]]
- [[Módulo Productos]]
- [[Arquitectura Actual]]
