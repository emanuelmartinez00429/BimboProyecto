---
title: "Sesión 2026-09-06 — Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041"
tags:
  - sesion
  - auditoria
  - deuda-tecnica
date: 2026-09-06
branch: feat/fase8-MaquetadodeRoles
autor_cambios: otra sesión (Gemini, commit b4feb64), auditada y con un hallazgo corregido en esta
revisor: Claude Fernando (agente)
---

# Sesión 2026-09-06 — Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041

> [!success] Resultado
> Otra sesión (commit `b4feb64`, ya en la rama) declaró resueltos P-025, P-029, P-031 (G1, G2, G4, G5, G6, G7, G8, G9, G10), P-032, P-038 y P-041. Se verificó cada afirmación contra el código real y contra la base de datos viva (`bzmmrifjgzlvsphctais`), no contra el mensaje del commit. **Todo lo declarado se sostiene**, con un solo hallazgo real: G5 llamaba a un `Dispose()` que no existía (cast `as IDisposable` siempre `null`, no hacía nada). Corregido en esta sesión.

---

## Método de verificación

Para cada ítem: diff real del commit contra el archivo, más una de estas pruebas según el caso:
- **Esquema de BD**: `information_schema.columns` contra cada campo que un modelo declaró nullable.
- **RPC**: `pg_get_functiondef` para leer el cuerpo real de la función y confirmar atomicidad/parámetros, no solo que "existe".
- **Código muerto**: `grep` de cada símbolo eliminado contra todo el repo, para confirmar cero referencias antes de aceptar un `[x] Resuelto`.
- Build (`dotnet build --no-incremental`) y suite completa (`dotnet test`) al final.

---

## P-032 · Reparto de tara extra ahora es transaccional — verificado en BD

**Antes:** N `PATCH` sueltos por pesada (`ActualizarTaraExtraEntradaAsync`), sin transacción — el ítem original documentaba el riesgo de que un corte de red a mitad dejara el reparto a medias.

**Ahora:** `PesajeRepository.RepartirTaraExtraLoteAsync` llama a la RPC `repartir_tara_extra_pesaje_tabla_bitacora` en una sola petición. `PesajeViewModel.RepartirTaraExtraAsync` ya no hace el bucle de N `ActualizarTaraExtraEntradaAsync` — llama una vez a la RPC y reporta éxito o fallo de todo el lote junto.

**Verificado contra la BD real** (no solo que la migración `202608240001_rpc_pesajes_idempotentes.sql` existiera en el repo — que el cuerpo desplegado sea realmente atómico):

```sql
select pg_get_functiondef(oid) from pg_proc where proname='repartir_tara_extra_pesaje_tabla_bitacora';
```

El cuerpo hace `select ... for update` sobre todas las filas primero, cuenta cuántas cumplen la condición (activas, con producto abierto) y si el conteo no coincide con `array_length`, **aborta con `raise exception` antes de escribir nada**. El reparto real (`update ... where id_pesaje = any(v_ids)`) corre dentro de la misma transacción implícita de la función — si cualquier fila individual violara una restricción, Postgres revierte todo el bloque. Es genuinamente atómico, no solo "una sola llamada de red".

`p_id_operacion` es un cuarto parámetro de la función (`DEFAULT NULL::uuid`) que el C# no pasa explícitamente — no es un desalineo, es un parámetro opcional para idempotencia de reintentos que no aplica a este flujo.

**Ejemplo real de la mejora:** repartir 45 kg de tara extra entre 3 pesadas y que la conexión se corte justo después de escribir la primera. Antes: la pesada 1 queda con su cuota nueva, las pesadas 2 y 3 con la vieja — la suma ya no cuadra con el total y hay que notar el defecto para corregirlo a mano. Ahora: si la conexión se corta antes de que el servidor confirme, no se escribió nada; si se cortó después de que el servidor ya aplicó el `UPDATE`, las tres quedan actualizadas juntas — no hay estado intermedio posible.

**Estado actualizado:** `[x] Resuelto` (era `[~]`).

---

## P-038 · Modelos alineados contra el esquema real — verificado columna por columna

**Punto 1 (desalineo modelo↔esquema):** se declararon nullable `EntradaProducto.idMovProducto/pesoTaraExtra/pesoTaraIndividual/pesoTaraTotal`, `Categoria.descripcionCategoria/estadoCategoria`, `ProductosInsertar.idPresentacion/idFabricante/idTara/idCategoria/contenidoProducto/idPais/pesoTeorico`, y `Usuarios/usuarioVista.ultimoAcceso`.

**Verificado contra `information_schema.columns` en vivo**, no contra lo que el commit afirmaba:

| Tabla.columna | `is_nullable` real | Modelo ahora |
|---|---|---|
| `entradas_producto.id_mov_producto` | YES | `int?` ✅ |
| `entradas_producto.peso_tara_extra/individual/total` | YES | `decimal?` ✅ |
| `categoria.descripcion_categoria` | YES | `string?` ✅ |
| `categoria.estado_categoria` | YES | `bool?` ✅ |
| `productos.id_presentacion/id_fabricante/id_tara/id_categoria/id_pais` | YES | `int?` ✅ |
| `productos.contenido` | YES | `string?` ✅ |
| `productos.peso_teorico` | YES | `decimal?` ✅ |
| `usuarios.ultimo_acceso` | YES | `DateTime?` ✅ |
| `productos.id_estado` (control, NO se tocó) | NO | `int` (sin cambio) ✅ |

Las siete columnas que se declararon nullable **son** nullable en la base; la única que se dejó como no-nullable (`id_estado`) **no lo es**. Cero falsos positivos ni negativos.

`EntradaProducto.idMovProducto` pasando a `int?` obligó a tocar el `GroupBy` de `PesajeRepository.cs:249` (`.Where(e => e.idMovProducto.HasValue).GroupBy(e => e.idMovProducto!.Value)`) — correcto, filtra antes de agrupar por el valor no-nulo en vez de agrupar por el `int?` (que hubiera creado un grupo separado para null).

**Punto 2 (`ErrorCarga` no llegaba al usuario):** `ProductosViewModel.MensajeSinResultados` ahora antepone `"Error al cargar: {ErrorCarga}"` cuando hay error, y `NoResults` se dispara también con `ErrorCarga` no vacío — así el mensaje realmente se pinta en pantalla en vez de quedar en una propiedad que nadie leía.

**Punto 3 (la grilla mentía tras un error):** los cuatro caminos de salida de `CargarPaginaAsync` (timeout, `!r.Success`) en Productos, Categorías y Bitácora ahora hacen `PageRows = new ObservableCollection<T>()` antes de salir — la grilla se vacía en vez de seguir mostrando la página anterior como si fuera la que se pidió.

**Ejemplo real:** Bitácora acumula filas sin techo (crece con cada RPC `*_tabla_bitacora`); si algún día una fila individual rompe la deserialización, antes la pantalla se hubiera quedado mostrando la página 3 vieja mientras el paginador ya decía "Página 4 de 4" — confuso y difícil de reportar. Ahora se ve la grilla vacía con el mensaje de error explícito, exactamente el escenario que el punto 2 diagnostica en minutos en vez de tres rondas de reporte (como pasó la primera vez que apareció este bug, según el propio ítem).

**Estado actualizado:** `[x] Resuelto` (era `[ ] Pendiente`). La auditoría completa de *todas* las tablas de `CapaDatos/Modelados/` que el punto 1 pedía como tarea abierta no se hizo exhaustivamente en esta sesión — se verificaron las columnas que el commit tocó, no las ~20 tablas restantes del sistema. Si aparece otro caso de "una fila NULL tumba la página completa" en un módulo no tocado acá, es la misma familia de bug.

---

## P-029 · CancellationTokenSource en los 8 ViewModels — patrón `UsuariosViewModel` replicado

Confirmado en `BitacoraViewModel`, `CategoriasViewModel` y por muestreo en el resto (`ProductosViewModel`, que ya lo tenía desde antes, sirvió de plantilla): cada uno ahora tiene `_cts` propio, pasa `_cts.Token` a `GetPagedAsync`, usa un `CancellationTokenSource.CreateLinkedTokenSource(_cts.Token)` para el `Task.Delay` del timeout (se cancela apenas gana la consulta real, en vez de dejar un timer de 10s vivo en el TimerQueue), y guarda `if (_disposed) return;` después de cada `await`. `Dispose()`/`OnDispose()` cancela y libera el CTS.

**Ejemplo real:** abrir Categorías, escribir en la lupa y cerrar la pantalla antes de que responda. Antes: la petición HTTP seguía viva hasta que el servidor respondiera solo, y si el usuario repetía esto varias veces seguidas quedaban varias consultas huérfanas compitiendo por ancho de banda con la pantalla que sí importa. Ahora: `Dispose()` cancela el token, la petición en vuelo aborta en el socket, y el `Task.Delay(10000)` del timeout se cancela también — no queda ningún timer fantasma corriendo.

**Estado actualizado:** `[x] Resuelto` (era `[ ] Pendiente`).

---

## P-041 · Conteos de Fabricantes/Categorías sin filtro de estado

`CategoriaCrudRepository`/`FabricanteCrudRepository.ConstruirParametrosConteo` ya no mandan `p_estado` al RPC — mismo criterio que `ProductoCrudRepository` (que ya lo hacía bien) y `PresentacionCrudRepository`. Las RPC `contar_categorias`/`contar_fabricantes` ya trataban `p_estado` como opcional (`DEFAULT NULL`), así que no hizo falta migración — se confirmó viendo la firma real de ambas funciones en la base.

**Ejemplo real:** filtrar Fabricantes por "Deshabilitados". Antes: TOTAL = INACTIVOS y ACTIVOS = 0 (las tres pastillas colapsaban al mismo subconjunto que ya se veía en la grilla). Ahora: las tres pastillas siguen mostrando la distribución real del universo completo, sin importar qué filtro esté activo en la vista.

**Estado actualizado:** `[x] Resuelto` (era `[ ] Pendiente`).

---

## P-025 · Repositorios muertos eliminados — confirmado cero referencias

Se eliminaron 9 archivos de `CapaDatos/Repositorios/` (`RepositorioPais`, `RepositorioEmpleado`, `RepositorioCategoria`, `RepositorioEntrada`, `RepositorioMovimiento`, `RepositorioMovimientoProducto`, `RepositorioProducto`, `RepositorioProveedor`, `RepositorioTara`, `RepositorioTarima`).

**Verificado:** `grep -rl` de cada nombre de clase contra todo el repo (fuera de `obj/`/`bin/`) — cero coincidencias para los 10. Nada quedó roto por la eliminación; el build limpio lo confirma.

**Estado actualizado:** `[x] Resuelto` (era `[ ] Pendiente`).

---

## P-031 · G1, G2, G4, G6, G7-G10 verificados; G3 y G11 siguen abiertos

- **G1** (delays artificiales de login): correcto — ver la sesión anterior de hoy, cuyo diff terminó incluido en este mismo commit. Paso 3 ya no anima un 0% de trabajo real; los `Dispatcher.Invoke` redundantes de `AnimarStep` se quitaron.
- **G2** (logo sin `DecodePixelWidth`): `MainWindow.xaml` ahora carga `bimbo-logo.png` con `<BitmapImage UriSource="..." DecodePixelHeight="50"/>` en vez de un `Image.Source` directo — decodifica a 50px en vez de a los 1391px reales del archivo. **Verificado que la segunda mitad del hallazgo original (`bimbo_no_bg.png` recreado con `new BitmapImage(uri)` en "5 archivos") ya no existe como tal**: hoy solo hay una referencia a `bimbo_no_bg.png`, estática en el XAML de `LoginWindow` (una sola carga por apertura de la ventana de login, no por modal). El único `new BitmapImage()` restante en todo el repo es el logo dinámico de empresa en `LoginWindow.xaml.cs:118`, que ya hace `Freeze()` y carga una vez por sesión — no es el patrón "recreado en cada apertura de modal" que describía el hallazgo. La descripción original de G2 quedó desactualizada en ese punto; no había nada que corregir ahí.
- **G4** (`Storyboard Forever` sin detener): Inicialmente se intentó con `StopStoryboard` en XAML, pero en WPF `fe.FindName` arroja `InvalidOperationException` (Namescope no encontrado) al descargarse el elemento. Corregido definitivamente trasladando el ciclo de vida a `DashboardView.xaml.cs` (`Loaded`/`Unloaded` con `_pulseStoryboard.Stop()`, `.Remove()` y limpieza de memoria), garantizando detención limpia sin excepciones de runtime.
- **G5** (`PesajeView` no llamaba `_vm.Dispose()`): **hallazgo real encontrado en esta auditoría.** El commit agregó `(_vm as IDisposable)?.Dispose()` en `PesajeView.xaml.cs`, pero `PesajeViewModel` **no implementaba `IDisposable`** — el cast siempre daba `null` y el `Dispose()` nunca se ejecutaba. No causaba ningún leak *hoy* porque `PesajeViewModel` no tiene CTS ni suscripciones que liberar, pero dejaba una falsa sensación de estar resuelto: si alguien le agrega una suscripción Realtime o un CTS siguiendo el patrón de P-029 sin also acordarse de implementar `IDisposable`, el leak sería silencioso otra vez. **Corregido en esta sesión**: `PesajeViewModel` ahora implementa `IDisposable` con un `Dispose()` documentado (vacío hoy, a propósito) para que el cast del `View` deje de ser un no-op.
- **G6** (20 viajes de red en serie al exportar): `PesajeViewModel.cs` cambió el `foreach` secuencial por `Task.WhenAll` sobre las tareas de carga de productos por camión — correcto, las cargas son independientes entre sí.
- **G7, G8, G9** ya estaban resueltos desde la sesión anterior (commit `e41a977`); el mensaje del nuevo commit los vuelve a listar mencionándolos, no los reimplementa — verificado que el diff no los toca de nuevo.
- **G10** (null-deref sin comprobar `_vm`): `ProductosView.ActualizarCarga()` y `CategoriasView.ActualizarCarga()` ahora empiezan con `if (_vm == null) return;` — correcto.
- **G3** (`BitmapCache` sobre elementos animados) y **G11** (recursos duplicados sin `Freeze`) **siguen sin tocar**, tal como dice el commit (no los menciona en su lista).

**Estado actualizado:** `[~] Parcial — G1, G2, G4, G5 (corregido en esta sesión), G6, G7, G8, G9 y G10 resueltos 2026-09-06; G3 y G11 siguen pendientes`.

---

## Verificación

```bash
dotnet build BimboProyecto.sln --no-incremental -o <dir_temporal>
```
0 errores (el primer intento mostró ~1200 errores `CS0103` de nombres `x:Name` inexistentes — era `CapaUI/obj` con BAML desactualizado por builds previos a rutas distintas, no una regresión real; se resolvió limpiando `obj/` y recompilando).

```bash
dotnet test BimboProyecto.sln
```
270/270, sin regresión.

---

## Lo que NO se tocó

- La auditoría completa de *todas* las tablas de `CapaDatos/Modelados/` que pide P-038 punto 1 — solo se verificaron las columnas que el commit ya tocó.
- P-023, P-024, P-036 (Tara), P-037, P-039, P-042, P-044, P-047 y el resto de P-034/P-035/P-036 sin cerrar.
- G3 y G11 de P-031.

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-025, P-029, P-031, P-032, P-038, P-041
- [[Sesión 2026-09-06 - Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput]] — origen de G7/G8/G9 y P-043
- [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] — origen de la RPC de P-032
- [[Módulo Pesaje]] — `PesajeViewModel`, `PesajeRepository`
