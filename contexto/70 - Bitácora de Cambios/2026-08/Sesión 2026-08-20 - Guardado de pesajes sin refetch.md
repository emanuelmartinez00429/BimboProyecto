---
title: Sesión 2026-08-20 — Guardado de pesajes sin refetch
type: sesion
status: vigente
tags:
  - sesion
  - pesaje
  - performance
date: 2026-08-20
updated: 2026-08-20
summary: "\"Seguir pesando\" tardaba demasiado en responder. Diagnóstico contra la BD real (MCP de Supabase): no era la base de datos — era arquitectura, 4–5 round trips…"
scope:
  - CapaAplicacion4/Pesaje/Interfaces
  - CapaDatos/Repositories
  - CapaDatos/Repositories/Pesaje
symbols:
  - AbrirPesajeModal
  - Action<T>
  - ActualizarUI
  - AgregarEntradaEnMemoria
  - AnularEntradaAsync
  - CargarProductosAsync
  - CerrarCamion
  - CrearEntradaAsync
  - EntradaDto
  - EntradaEnEdicion
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
---

# Sesión 2026-08-20 — Guardado de pesajes sin refetch

> [!success] Resultado
> "Seguir pesando" tardaba demasiado en responder. Diagnóstico contra la BD real (MCP de Supabase): no era la base de datos — era arquitectura, 4–5 round trips secuenciales por cada pesada guardada. Se redujo a 1–2, se agregó guarda de reentrada + estado visual de guardado, y el modal ahora se queda abierto entre pesadas en vez de cerrarse. Probado por Fernando en la app — funcionó.

---

## Problema / motivo

Fernando reportó que el botón "Seguir pesando" del [[Módulo Pesaje|modal de pesaje]] tardaba demasiado en responder, y pidió encontrar el cuello de botella (¿arquitectura? ¿base de datos?) y decidir si corregirlo total o parcialmente.

## Diagnóstico

Antes de tocar código se verificó la hipótesis de "la BD es lenta" contra el proyecto real `Bimbo_Pesaje` (`bzmmrifjgzlvsphctais`, us-west-2) vía MCP de Supabase:

- Volumen: `entradas_producto` 54 filas · `movimiento_productos` 8 · `movimientos` 4 · `productos` 178.
- Índices presentes y correctos en todas las FK relevantes (`idx_entradas_mov_producto`, `idx_mov_productos_movimiento`, `idx_movimientos_estado`, PKs en todo).
- Triggers: solo `calcular_pesos_entrada` (**BEFORE INSERT/UPDATE**, confirmado con `information_schema.triggers` — dato clave para el fix, ver abajo) y `actualizar_updated_at`. Triviales.
- RLS: predicados planos `usuario_autenticado()` / `true`, sin subqueries por fila.

**Conclusión: la BD resuelve en microsegundos.** El costo real era que `GuardarEntradaAsync` disparaba, en serie: `AnularEntradaAsync` (si edita) → `CrearEntradaAsync` → `CargarProductosAsync`, y esa última son **3 round trips más** (`movimiento_productos`+`productos`, vista de tara, `entradas_producto`) para volver a traer *todo* el camión y así reflejar una fila que el cliente ya conocía.

A ~200–350 ms por viaje desde una red de planta (no localhost), eso son 1–5 s de pantalla congelada — agravado por tres cosas más: cero feedback visual (sin guarda de reentrada, dos clics insertaban dos pesajes), el modal se cerraba al guardar ("Seguir pesando" no seguía pesando), y `PropertyChanged` disparaba el barrido completo de `ActualizarUI()` (~20 controles) en cada una de las notificaciones que dispara un guardado.

## Cambios aplicados

### 1. Cronometraje permanente en `RepositorioBase` (instrumentar antes de tocar nada)

- `CapaDatos/Repositories/RepositorioBase.cs` — `TryAsync` (ambas sobrecargas) cronometra cada operación con `Stopwatch` y loguea `"[Repo] {Contexto} — {Ms} ms ({Resultado})"` a nivel `Debug`. Cubre **todos** los repositorios de una vez, no solo Pesaje.
- `CapaUI/App.xaml.cs` + `CapaUI/App.config` — nuevo `LOG_LEVEL` configurable (default `Warning`, igual que antes). Subirlo a `Debug` habilita el cronometraje sin recompilar.
- Documentado como extensión del patrón en [[Base Repository con TryAsync]].

### 2. El INSERT deja de descartar la fila que ya trae calculada

`trg_calcular_pesos_entrada` es BEFORE INSERT, así que la respuesta del INSERT ya trae `peso_tara_individual`/`peso_tara_total`/`peso_neto` calculados por el trigger.

- `CapaAplicacion4/Pesaje/Interfaces/IPesajeRepository.cs` — `CrearEntradaAsync` devuelve `Task<Result<EntradaDto>>` en vez de solo el id.
- `CapaDatos/Repositories/Pesaje/PesajeRepository.cs` — reutiliza el `MapEntrada` que ya existía (antes solo se usaba al leer `GetProductosAsync`) en vez de duplicar el mapeo.

### 3. El ViewModel aplica en memoria en vez de recargar

- `CapaUI/.../PesajeViewModel.cs` — `GuardarEntradaAsync` pasó de `Task` a `Task<bool>`. Ya no llama a `CargarProductosAsync` tras guardar: quita la entrada vieja de la colección (si editaba), agrega el `EntradaDto` confirmado vía nuevo `AgregarEntradaEnMemoria`, y llama a `producto.NotificarAgregados()` + `SelectedCamion.NotificarTotales()` (ambos ya existían para este propósito). Si el INSERT falla después de haber anulado una edición, ahí sí se hace el refetch completo — es el único camino de error donde memoria y BD podrían divergir.
- `QuitarEntradaAsync` recibió el mismo tratamiento: anula y quita de memoria en vez de recargar. Ubica el producto por `entrada.ProdId` (no por `SelectedProducto`) para seguir funcionando en la vista "Todo el camión".
- Patrón completo documentado en [[Guardado sin Refetch - Aplicar en memoria la respuesta del servidor]] — es reutilizable en cualquier otra pantalla con la misma política de "guardar ⇒ recargar todo".

### 4. El modal: guarda de reentrada, estado visual, y se queda abierto

- `CapaUI/.../Modales/PesajeModal.xaml.cs`:
  - `GuardarYSeguir` y `CerrarCamion` pasaron de `Action`/`Action<T>` a `Func<..., Task<...>>` — el modal ahora **espera** el guardado. De paso resuelve el `async void` sobre `Action<T>` que existía antes: una excepción ahí habría tumbado la aplicación sin que nada la atrapara (le agregué `try/catch` con log a los dos handlers `async void` que exige WPF).
  - Campo `_guardando` + `AplicarEstadoGuardando(bool)`: deshabilita los inputs y botones y muestra "Guardando…" mientras el pesaje viaja al servidor. Sin esto, dos clics rápidos insertaban dos pesajes — no había ninguna guarda.
  - `PrepararSiguientePesada()`: tras un guardado exitoso, limpia los campos, refresca fecha/hora reales (`_fecha`/`_hora` dejaron de ser `readonly` — si no, la segunda pesada se hubiera guardado con la hora de la primera), relee `_pesoRecibidoPrevio` desde `producto.Entradas` (no suma el neto que calculó el modal — el que manda es el que confirmó la BD), y pone el foco en Bruto. El modal ya no se cierra al guardar.
  - Nueva propiedad `EntradaEnEdicion` expuesta al host: el handler de `PesajeView` la lee en vez de la variable `editInitial` capturada al abrir el modal — necesario porque tras guardar una edición el modal pasa a modo "pesada nueva", y sin este cambio el segundo guardado hubiera intentado anular una entrada que ya estaba anulada.
- `CapaUI/.../Modales/PesajeModal.xaml` — `TextBlock` "Guardando…" (colapsado) junto a los botones, reutilizando los estilos existentes de `PesajeModalStyles.xaml` (sin agregar ningún estilo nuevo).
- `CapaUI/.../PesajeView.xaml.cs` — `AbrirPesajeModal` quitó el `CerrarModal()` del handler de `GuardarYSeguir`; se adaptó a las nuevas firmas `Func<...>`.

### 5. `ActualizarUI` coalescido

- `CapaUI/.../PesajeView.xaml.cs` — `PedirActualizarUI()` reemplaza la suscripción directa a `PropertyChanged`: agenda un solo `ActualizarUI()` por frame vía `Dispatcher.BeginInvoke(Background, ...)` con una bandera, en vez de correr el barrido completo (~20 controles) en cada una de las notificaciones que dispara un guardado. Comportamiento visual idéntico, sin tocar `ActualizarUI` en sí.

## Verificación

- `dotnet build BimboProyecto.sln` — 0 errores. Se comparó el conteo de advertencias antes/después (`git stash` del working tree + build limpio): **55 advertencias en ambos casos** — sin regresión.
- Probado por Fernando corriendo la app real: confirmó que funcionó.

## Lo que NO cambió (alcance deliberado)

- `RepartirTaraExtraAsync` (`TaraExtraTotalModal`) sigue haciendo el refetch completo tras repartir — es un UPDATE de N filas relacionadas entre sí, no aplica el mismo patrón sin más análisis. Documentado como límite explícito del patrón, no como deuda.
- Sin migraciones a la BD ni RPC — el plan aprobado descartó ese alcance a propósito (habría necesitado tocar Postgres). Sigue siendo la vía de mejora futura si algún guardado más complejo lo necesita.
- `PesajeView` sigue siendo code-behind con `ActualizarUI()` manual, no MVVM con bindings — también descartado del alcance para no arriesgar regresión visual en una pantalla recién ajustada (sesión anterior).

---

## Relaciones

- [[Módulo Pesaje]]
- [[Guardado sin Refetch - Aplicar en memoria la respuesta del servidor]] — el patrón nuevo que salió de esta sesión
- [[Base Repository con TryAsync]] — cronometraje agregado acá
- [[Deuda Técnica - Pendientes]]
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — sesión anterior sobre el mismo módulo
