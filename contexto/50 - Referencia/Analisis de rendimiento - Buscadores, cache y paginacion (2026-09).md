---
title: "Análisis de rendimiento — Buscadores, caché y paginación (2026-09)"
tags:
  - referencia
  - rendimiento
  - supabase
  - postgres
date: 2026-09-06
lifecycle: verified
---

# Análisis de rendimiento — Buscadores, caché y paginación

> [!info] Fuente
> Medición directa contra la base de producción (`bzmmrifjgzlvsphctais`) con `EXPLAIN ANALYZE` y consultas a `pg_class`/`pg_indexes`, cruzada con exploración de código de `CapaDatos/Repositories/**` y `CapaUI/.../*ViewModel.cs`. Hecho tras implementar [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]], en respuesta a la pregunta "qué más se puede mejorar en buscadores/caché/paginación".

## El hecho

**La base de producción es diminuta**, y eso invierte cualquier intuición de "hay que indexar más" u "optimizar el SQL":

| Tabla | Filas |
|---|---:|
| `productos` | 178 |
| `bitacora` | 121 |
| `fabricante` / `proveedores` | 45–48 |
| resto | < 20 |

`EXPLAIN ANALYZE` sobre la búsqueda de productos (`busqueda_producto ILIKE '%bimbo%'`, que sí tiene índice GIN de trigramas):

```
Seq Scan on productos  (actual time=0.647..1.623 rows=30)
  Filter: (busqueda_producto ~~* '%bimbo%')
  Rows Removed by Filter: 148
Execution Time: 2.249 ms
```

**La ejecución server-side cuesta ~2 ms.** Toda la lentitud percibida por el usuario es **cantidad de viajes de red**, no trabajo de base de datos.

## Por qué importa aquí

Reordena qué mejoras rinden y cuáles serían activamente contraproducentes:

**Rinde** (cuenta viajes, no reescribe SQL):
1. `FabricanteCrudRepository.BuscarSugerenciasAsync` (`CapaDatos/Repositories/Fabricantes/FabricanteCrudRepository.cs:174-195`) descarga las tablas **completas** de `proveedores` y `paises` en cada tecla (sin `Limit`), en vez de usar `ICatalogoRepository` que ya las tiene cacheadas desde ADR-026. 3 viajes por pulsación → debería ser 1. Mismo patrón en `GetPagedAsync` (4 viajes por página).
2. Bitácora, Empleados, Usuarios y el Picker de productos **cuentan descargando la tabla filtrada entera** al cliente (`Select("id_x").Get()` sin `Range`, `.Count` en memoria), mientras 5 otros módulos ya usan RPC `contar_*` que hacen `count(*) FILTER(...)` server-side en una sola pasada. Bitácora es la única tabla sin techo de crecimiento — es la mejora que previene un problema *futuro*, no uno actual.
3. El `CancellationToken` se descarta en las 7 implementaciones de `BuscarSugerenciasAsync` (ninguna llega a `.Get(ct)`) y en la mayoría de `GetPagedAsync`. Caso más engañoso: `UsuarioRepository.ObtenerPaginaAsync` **recibe** el token y lo ignora.
4. 8 de 10 ViewModels usan `Task.WhenAny(task, Task.Delay(10s))`, que no cancela la petición HTTP y deja un timer huérfano por carga. Solo `ProductosViewModel` migró al patrón correcto (CTS real + `_loadGeneration`).

**NO rinde, y sería contraproducente**:
- Índices GIN/trigramas en Proveedores/Fabricantes/Categorías/Empleados/Bitácora: con 45–121 filas el planner ya elige *Seq Scan* correctamente. Un índice ahí solo agrega costo de mantenimiento en cada escritura, a cambio de nada. El patrón de [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] está bien aplicado donde importa (`productos`, la tabla que crece); extenderlo al resto (P-039) no es urgente todavía — se vuelve urgente si alguna de esas tablas pasa de unos pocos miles de filas.
- Optimizar el SQL de las consultas: 2,2 ms medidos, no hay nada que ganar.
- Cachear sugerencias o grillas paginadas: clave de texto libre o combinatoria de filtros, tasa de aciertos ≈ 0. Ya decidido así en ADR-026 §5.2.
- Migrar las grillas a `RangeObservableCollection`: los 10 ViewModels ya reemplazan la instancia completa de la colección (un solo cambio de `ItemsSource`), que ya es óptimo.
- Paginación por cursor/keyset: con 178 filas máximo y páginas de 50, la tabla entera son 4 páginas.

## Hallazgo aparte — reproducibilidad, no rendimiento

5 RPC de conteo (`contar_productos`, `contar_proveedores`, `contar_fabricantes`, `contar_categorias`, `contar_presentaciones`) **existen solo en la base remota**, sin migración correspondiente en `supabase/migrations/`. Lo mismo con `sin_tildes()`, la columna generada `busqueda_producto` y su índice GIN. Un entorno nuevo levantado desde las migraciones del repo no reproduce la paginación de 5 módulos ni la búsqueda de productos.

## Ejemplo / workaround

Orden de ejecución si se retoma (ninguna fase implementada aún):

1. `FabricanteCrudRepository` → consumir `ICatalogoRepository` para los diccionarios de proveedores/países.
2. Propagar `ct` hasta `.Get(ct)` en las 7 `BuscarSugerenciasAsync` y en los `GetPagedAsync` que lo descartan.
3. Unificar los 8 ViewModels con `Task.WhenAny` al patrón de `ProductosViewModel:394-455` (depende de 2 para poder cancelar de verdad).
4. RPC `contar_*` nuevas para Bitácora/Empleados/Usuarios/Picker + versionar en migraciones las 5 existentes y el DDL de ADR-018.
5. Consumir `UniversalSearchQuery.MaxResultsPerEntity` (declarado, nunca leído) + detalles menores: fuga de `CancellationTokenSource` sin `Dispose()` en `SuggestionDebouncer.cs:58-59` y `SelectorCatalogoModal.xaml.cs:304-306`; tres constantes de debounce distintas (200/300/300 ms) sin unificar; falta `ScrollUnit="Pixel"` en el `DataGrid` del selector de catálogo.

---

## Relaciones

- [[Arquitectura Actual]]
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] — por qué no se cachean sugerencias ni grillas paginadas
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — el patrón de columna generada + GIN, y por qué no se extiende sin justificación de volumen
- [[Deuda Técnica - Pendientes]] — P-039 (extender ADR-018 a otras tablas)
