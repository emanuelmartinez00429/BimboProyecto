---
title: Módulo Pesaje (Recepción de Materia Prima)
tags:
  - modulo
  - pesaje
  - movimientos
date: 2026-08-13
---

# Módulo Pesaje — Recepción de Materia Prima

Registro de la descarga de camiones de materia prima: qué trae cada camión según el manifiesto, y cuánto pesa realmente en la báscula.

---

## Modelo de datos — tres tablas en cascada

```
movimientos            →  movimiento_productos    →  entradas_producto
(el camión / la placa)    (qué productos trae)       (cada pesada individual)
```

| Tabla | Qué representa | Modelo C# |
|---|---|---|
| `movimientos` | Un camión con su placa y proveedor | `CapaDatos/Modelados/Pesajes/Movimiento.cs` |
| `movimiento_productos` | Productos declarados del camión (peso manifestado, bultos declarados) | `MovimientoProducto.cs` |
| `entradas_producto` | Cada pesada física en báscula | `EntradaProducto.cs` |

Estados compartidos en [[EstadosPesaje]]: `Abierto=7`, `Cerrado=8`, `Anulado=9`, `Activo=1`.

> [!important] "Quitar" nunca borra
> Todas las operaciones de quitar hacen `UPDATE id_estado = 9` (Anulado). Las lecturas filtran anulados, así que desaparecen de la UI sin romper FKs ni perder auditoría.

---

## Los tres pesos y sus taras

**Tara** = todo lo que pesa y no es producto. Se resta del bruto para saber cuánto producto real llegó — y eso es lo que se le paga al proveedor.

| Concepto | Dónde vive | Cómo funciona |
|---|---|---|
| **Tara de empaque** | `productos.id_tara → tara.peso_tara_envalaje` | El empaque propio del producto |
| **Tara extra** | `entradas_producto.peso_tara_extra` | Tarimas, forros, separadores. **Se pesa** (nunca se calcula) y se guarda por pesada |
| **Peso teórico** | `productos.peso_teorico` | Peso unitario del producto, sin empaque |

### La tara extra se pesa, no se calcula (rediseño 2026-08-13)

El peso de una tarima varía (madera, plástico), así que **no hay forma de estimarlo**. Las tarimas se juntan, se pesan en bloque, y ese peso se resta. Si no se pueden pesar todas juntas, se pesan por partes y se suman.

**La fuente de verdad es `entradas_producto.peso_tara_extra`.** El total nunca se persiste por separado: es `Σ` de las entradas. Eso hace la operación idempotente — si se agrega una pesada después de cargar el total, se vuelve a repartir el mismo `Σ` entre N+1 entradas y el total se conserva.

Dos formas de cargarla, ambas disponibles **en cualquier momento** desde la primera pesada:

| Modo | Dónde | Qué hace |
|---|---|---|
| **Por pesada** | Campo opcional en `PesajeModal` | El operario pesa la tarima junto con el bulto y la captura ahí mismo. Es lo único que da un conteo de bultos exacto en tiempo real. |
| **Total** | Botón «Tara extra» → `TaraExtraTotalModal` | Se carga el total ya pesado (de una vez o sumando parciales) y el sistema lo **reparte en partes iguales** entre las pesadas del alcance elegido: un producto o todo el camión. |

El reparto usa `PesajeCalc.RepartirTaraExtra`, que pone el residuo del redondeo en la última cuota para que `Σ cuotas == total` exacto.

> [!important] Pre-vuelo obligatorio antes de repartir
> `PesajeViewModel.RepartirTaraExtraAsync` valida **todas** las pesadas antes de escribir ninguna: si alguna quedara con `peso_neto ≤ 0` (lo rechaza el CHECK de la BD) no se escribe nada y se informa el máximo repartible. No hay transacción — son N PATCH sueltos — y el `peso_neto` es lo que se le paga al proveedor: mejor fallar entero que dejar un reparto a medias.

> [!warning] `movimientos.peso_tara_extra` quedó congelada
> Es la columna del flujo viejo. **Nunca se vuelve a escribir** — `ActualizarCamionAsync` la excluye a propósito, porque editar un camión legado le borraría su tara histórica. Se lee como `CamionPesaje.TaraExtraLegado` solo para reconocer camiones del esquema anterior.

### El trigger de BD calcula el neto guardado

`trg_calcular_pesos_entrada` → `calcular_pesos_entrada`. Al insertar una entrada, la app envía solo bruto y tara extra; el trigger completa:

- `peso_tara_individual` = tara de empaque del producto (**plana**, una vez por pesada)
- `peso_tara_total` = tara_extra + tara_individual
- `peso_neto` = bruto − tara_total (con excepción si sale ≤ 0)

**El trigger NO usa `numero_bultos_recibido`** — verificado 2026-08-13. Por eso dejar de capturar bultos no altera el neto guardado: el único punto donde los bultos lo tocaban era el prorrateo que hacía la app, que ya no existe.

> [!success] El trigger también cubre UPDATE (verificado 2026-08-24)
> `trg_calcular_pesos_entrada` está declarado para `INSERT OR UPDATE`, por lo que vuelve a calcular las columnas derivadas cuando cambia `peso_tara_extra`. La duda histórica quedó cerrada durante [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]].

---

## Bultos declarados vs bultos teóricos

Dos conceptos distintos que antes se llamaban igual:

| | Qué es | De dónde sale |
|---|---|---|
| **Bultos declarados** | Lo que dice el manifiesto en papel, sin verificar | Columna `movimiento_productos.bultos_teoricos` (nombre histórico) |
| **Bultos estimados** | Cuántos bultos representa el peso realmente pesado | **Calculado**, no se guarda |

```
bultos_estimados = (peso_bruto − tara_extra_de_esa_entrada) / (peso_teorico + tara_empaque)
```

Implementado en `PesajeCalc.BultosTeoricos`. Devuelve `null` (la UI muestra "—") si el bruto no es positivo, si falta el peso teórico del producto, o si el bruto no alcanza a cubrir la tara extra.

**Ya no se capturan a mano**: desde el rediseño 2026-08-13 el modal de pesaje pide **solo el peso bruto** (más la tara extra, opcional). `numero_bultos_recibido` queda `NULL` en las entradas nuevas — no se persiste una estimación como si fuera un dato medido. Las entradas viejas conservan su valor y la grilla lo muestra tal cual, sin marca de aproximado.

Cuando una pesada no tiene tara extra cargada, el peso de las tarimas se cuenta como si fuera producto y la estimación queda alta: se avisa en el modal, en la columna «BULTOS (EST.)» (en ámbar) y en la barra de productos.

> [!warning] Inconsistencia conocida — tara plana vs tara por bulto
> El **trigger** resta la tara de empaque **plana** (una vez por pesada), pero la **fórmula de bultos estimados** la usa **por bulto** en el divisor. Sigue conviviendo a propósito: el neto guardado no cambia (es plata que se paga al proveedor) y el indicador es informativo. El rediseño 2026-08-13 no cerró P-024, solo le cambió la forma — ver [[Deuda Técnica - Pendientes]].

> [!bug] Los datos de tara son de prueba
> Verificado 2026-07-26: la tabla `tara` tiene **una sola fila** (20 kg) usada por 503 de 505 productos, y **500 de esos productos pesan menos de 10 kg**. El empaque pesaría el doble o más que el producto. Los otros 5 (1,000–10,000 kg) tienen nombres tipo "Producto Number 1". Hasta que se cargue el catálogo real, los bultos teóricos van a dar números que no reflejan la realidad.

---

## Flujo de la UI (rediseño 2026-07-26)

### Estado vacío
Si **no hay ningún camión abierto**, la pantalla muestra "Actualmente no hay camiones descargándose" con un botón para iniciar el proceso, en vez de tres paneles vacíos.

Durante la primera carga se muestra un indicador "Cargando camiones en proceso de descarga…" en lugar del estado vacío.

> [!bug] Un estado vacío calculado sobre una colección debe excluir la carga
> `MostrarEstadoVacio` es `!IsLoading && CamionesActivos == 0`. **El `!IsLoading` no es opcional.**
>
> Sin él, la secuencia era: `IsLoading = true` → dispara `PropertyChanged` → `ActualizarUI()` → `CamionesActivos == 0` porque **los datos todavía no llegaron** → se mostraba el formulario de iniciar descarga durante todo el viaje a Supabase, y recién después aparecían los camiones. Se veía como si la pantalla se equivocara y se corrigiera sola.
>
> Regla general: cualquier "estado vacío" derivado de una colección que se llena por red tiene que distinguir **"vacío porque no hay nada"** de **"vacío porque todavía no cargó"**. Detectado y corregido el 2026-07-26.

### Tabla de camiones — una fila por recepción (2026-09-18)

> [!important] Ya no se agrupa por placa
> El panel «Camiones de Entrega» es un `DataGrid` plano (`DgCamiones`): **# · PLACA · PROVEEDOR · KG · ESTADO · 🗑**, y **una fila = una recepción** (placa + proveedor), igual que en `movimientos`. Un camión con carga de tres proveedores se registra **tres veces con la misma placa**. Cada fila muestra su propio KG manifestado; no hay total por placa. La agrupación visual por placa (2026-09-11 a 2026-09-16: `GrupoCamionPesaje`, `EsPlacaVacia`, modo `soloPlaca`, propagar la placa con N updates) se retiró entera. Decisión en [[ADR-029 - Recepciones de pesaje planas con reglas en trigger de tabla]]; detalle en [[Sesión 2026-09-18 - Camiones en tabla plana y reglas de recepción en BD]].

| # | Regla | Dónde |
|---|---|---|
| R1 | La placa es obligatoria, de 20 caracteres como máximo, y se guarda normalizada (`upper(trim)`) | `ReglasCamion` · `ValidadorFormulario` · trigger |
| R2 | El proveedor es obligatorio y debe estar activo | `ReglasCamion` · RPC |
| R3 | La descripción tiene 500 caracteres como máximo | `ReglasCamion` · `varchar(500)` |
| R4 | **Como máximo 5 recepciones abiertas, contadas por fila** | `ReglasCamion.MaxRecepcionesAbiertas` · trigger con `pg_advisory_xact_lock` |
| R5 | Placa + proveedor únicos entre las abiertas; **misma placa con otro proveedor sí**, sin aviso | `ReglasCamion.ValidarRecepciones` · trigger · índice `ux_movimientos_abierto_placa_proveedor` |
| R6 | Editar cambia **solo esa fila** (`ChangeTracker`; sin cambios, no hay red) | `RegistroCamionesModal` · RPC + trigger |
| R7 | Quitar solo si la recepción no tiene productos | basurero deshabilitado · trigger |
| R8 | Cerrar cierra solo la fila seleccionada | RPC `cambiar_estado_movimiento_…` |
| R9 | El reporte se elige **por placa** (todas sus recepciones) y sale en un solo archivo | `ReporteModal` · `GenerarReportePesajesAsync` |

La BD aplica R1, R4, R5 y R7 en el trigger `trg_validar_recepcion_movimiento` (migración `20260918194910_regla_recepciones_abiertas_camiones`), así que también se cumplen con dos terminales a la vez. El mensaje del trigger ya está escrito para el operador: `PesajeRepository.ConMensajeDelServidor` lo desenvuelve del JSON de PostgREST.

### Proceso de descarga — alta en tabla, edición de a uno

> [!warning] `ProcesoDescargaModal` ya no existe
> Esta sección describía el wizard/megamodal unificado (720×720, secciones "Datos del camión" + "Productos de la carga"). Ese componente **se partió en dos** —`CamionModal` y `ProductoCamionModal`— y la nota no se había actualizado. Corregido el 2026-09-05.

Hoy el camión y sus productos son dos caminos separados:

| Componente | Qué hace | Se abre desde |
|---|---|---|
| `Modales/RegistroCamionesModal` | **Altas, ediciones y bajas** de hasta 5 recepciones, en tabla | Estado vacío · «Agregar» · «Editar» · doble clic en una fila |
| `Modales/ProductosCargaModal` | La carga entera de la recepción (altas, cambios y bajas de productos) | Panel de Movimiento |

`CamionModal` se eliminó el 2026-09-18: el alta y la edición pasan por `RegistroCamionesModal`.

#### Alta múltiple (2026-09-05)

En el andén los camiones llegan juntos, así que darlos de alta de a uno significaba abrir y cerrar el mismo modal cinco veces. `RegistroCamionesModal` es una tabla con `MaxCamiones` filas —**placa · proveedor · descripción**, con lupa de proveedor por fila— más basurero, "Registrar otro camión" y contador.

> [!important] Las filas se ocupan, no se crean
> Las 5 `FilaCamion` existen desde que abre el modal; `Activa` decide si se ven como camión o como "espacio libre". Al borrar una, los **valores** de las de abajo suben un lugar — las filas no se mueven.
>
> No es un detalle de implementación: cada fila arma su propio `ValidadorFormulario` con el número **capturado en la etiqueta** ("La placa del camión 3 es obligatoria" — en una tabla de cinco, "La placa es obligatoria" no dice cuál). Si las filas se reordenaran, esas etiquetas empezarían a mentir.

> [!bug] Al compactar se decide por posición, no por estado
> La fila que hay que vaciar al final es la **última de la colección**, no `Last(f => f.Activa)`. El bucle que sube los valores también copia el flag de ocupada, así que preguntar después por "la última ocupada" devuelve una fila que el propio bucle ya liberó — y borra un camión que el operador sí quería. Se detectó en producción de pruebas el 2026-09-05: borrar el camión 3 borraba también el 2. Ver [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]].

Las reglas entre filas (cupo R4 y duplicados R5) ya no viven en el modal: las decide `ReglasCamion.ValidarRecepciones` al guardar (ver la tabla de arriba). Las altas se guardan con la RPC transaccional `registrar_camiones_lote_seguro` ([[Deuda Técnica - Pendientes|P-053]], resuelto); si el lote falla no se guarda nada.

> [!note] Historial
> Hasta el 2026-09-18 el cupo se contaba por **placa distinta** y había un aviso ámbar de «placa ya abierta con otro proveedor». Las dos cosas se retiraron con la tabla plana.

> [!important] Regla de borrado de productos
> Un producto **con pesajes registrados no se puede quitar** (botón deshabilitado + tooltip). Uno agregado por error y sin pesar, sí. Doble guarda: binding + chequeo en el handler.

Los productos **no se cargan al abrir** — solo al tocar "Agregar producto" se abre el selector.

### Selector de productos
No es un modal propio de Pesaje: usa `CapaUI/Core/Controls/SelectorCatalogoModal`, el mismo selector genérico que Proveedor (paso 1) y que `ProductoModal`. Hasta el 2026-08-19 existía `Modales/SelectorProductosModal` + `SelectorProductosViewModel`, un picker autocontenido a medida — se **retiró entero** (código y ViewModel) porque duplicaba lo que el genérico ya resuelve, y su `MaxWidth`/`MaxHeight` atados a un `Border` ancestro se rompían al quedar anidado dentro de un modal de 720px. Ver [[Selector de Catálogo - Selector genérico y multiselección]] para el contrato completo.

Config del catálogo: `Catalogos.Productos(_catalogos, prov?.Id, permiteMultiple: true, estaYaElegido: ...)` — acotado al proveedor del paso 1 (puente en 2 pasos producto→fabricante→proveedor, mismo patrón que usaba `PickerProductoRepository`), con checkboxes para agregar varios de una sola apertura, y los productos ya agregados a la carga salen atenuados/bloqueados en la tabla.

Se perdió el toggle "Todo el catálogo" que tenía el picker viejo (el acotamiento por proveedor ahora es fijo) y el `SelectorProductosViewModel` a medida — ya no hace falta, el genérico no depende de `RealtimeAwareViewModel` ni de nada que suscriba a Realtime.

### Modal de pesaje
Captura el **peso bruto** y, opcionalmente, la **tara extra de esa pesada**. Todo lo demás es contexto de solo lectura: placa, proveedor, producto y los cálculos. Muestra los **bultos estimados** en vivo.

> [!info] Panel de control y gráfico de pesadas (2026-09-18)
> El modal mide 980 px y tiene un panel lateral de 360 px con:
> - la diferencia y el contador de pesajes;
> - 4 tarjetas: Manifestado, Neto acumulado, Tara acumulada y Tara extra acumulada. La tara es **plana por pesada**, no bultos × tara;
> - la barra de avance;
> - el gráfico `PesadasChart` (`Pesaje/Controles/`), con las pesadas guardadas y el punto amarillo **"Ahora"** que se mueve mientras se teclea. En edición, "Ahora" reemplaza a la entrada que se corrige;
> - los avisos en cascada.
>
> Las cuentas viven en `CapaDominio/Reglas/ReglasPanelPesaje.cs`, con tests:
> - la **escala Y** es el doble del promedio, así la línea queda a media altura (10 kg → 0–20);
> - el raleo del eje X;
> - la cascada de avisos: sin bruto → neto ≤ 0 → Excedente → Sin tara extra → Pesada atípica ±35 % (con ≥ 2 previas) → OK. Se muestran como máximo 2 más "+N".
>
> El excedente se pinta en rojo pero **no bloquea el guardado**. Detalle en [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]].

> [!important] "Seguir pesando" se queda abierto (2026-08-20)
> Hasta esta fecha, guardar una pesada disparaba 4–5 round trips (INSERT + recargar el camión entero) y cerraba el modal — "Seguir pesando" no seguía pesando. Se redujo a 1–2 round trips aplicando en memoria el `EntradaDto` que ya devuelve el propio INSERT (el trigger es `BEFORE INSERT`), y el modal ahora se limpia y queda abierto para la siguiente tarima, con guarda de reentrada y estado "Guardando…" visible. Patrón completo en [[Guardado sin Refetch - Aplicar en memoria la respuesta del servidor]]; detalle de la sesión en [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]].

### Reportes de Pesaje — "Pesado de Insumos BES" (Fase 9 / 2026-08-21)
El botón **«Imprimir reporte»** (y «Cerrar todos») abre `ReporteModal` permitiendo generar el reporte institucional en **PDF** y **Excel** (.xlsx):
- **Consolidación por producto:** Las distintas pesadas de un mismo producto en el camión se consolidan en una sola fila.
- **Cálculo de diferencias:** `Diferencia (KG) = Peso Recibido (Neto) - Peso Manifestado` y `Diferencia (%) = ((Peso Recibido - Peso Manifestado) / Peso Manifestado) * 100`.
- **Columnas estándar (Figura 28):** `FECHA ASIG.`, `PLACA`, `PRODUCTO`, `PROVEEDOR`, `BULTOS (APROX)`, `PESO MANIFESTADO`, `PESO BRUTO`, `PESO TARA`, `PESO RECIBIDO`, `DIF. (KG)`, `DIF. (%)`.
- **Alcance por placa (2026-09-18):** Hay un `CheckBox` por placa, más «Todas» y «Ninguna». Marcar una placa incluye **todas sus recepciones**: el camión de tres proveedores sale unificado. «Imprimir reporte» abre con la placa seleccionada marcada. Varias placas van a un solo archivo, ordenado por placa → proveedor → producto. Los metadatos y la auditoría cuentan camiones (placas) y recepciones por separado.
- **Auditoría e Integración:** Registra la emisión vía RPC `ingresar_reporte_tabla_bitacora` antes de escribir el archivo y abrirlo automáticamente en Windows.
- **Parámetros legibles:** Desde 2026-09-18 la RPC recibe texto con origen, recepciones, cantidades y totales etiquetados; ya no persiste un objeto JSON en `reporteria.parametros_reporte`. Ver [[Sesión 2026-09-18 - Auditoría legible y parámetros de reportes en texto]].

---

---

## Validación de los campos de texto

Pesaje entró al esquema de [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] el **2026-09-05**, y solo para el camión:

| Capa | Qué hay |
|---|---|
| **BD** | `movimientos.placa_vehiculo` `varchar(20)` · `movimientos.observaciones` `varchar(500)` — migración `20260905215247_limitar_texto_movimientos`. Reglas entre filas (cupo, unicidad, quitar con productos): trigger `trg_validar_recepcion_movimiento` — migración `20260918194910_regla_recepciones_abiertas_camiones` |
| **Dominio** | `ReglasCamion` en `CapaDominio/Reglas/ReglasEntidades.cs` (`Proveedor`, `Placa`, `Descripcion`, `MaxRecepcionesAbiertas`, `NormalizarPlaca`, `ValidarRecepciones`) |
| **UI** | `ValidadorFormulario` por fila en `RegistroCamionesModal`; el `MaxLength` del TextBox lo pone solo `Segun()` vía `TopePreventivo` |

> [!warning] Cobertura parcial — [[Deuda Técnica - Pendientes|P-045]] sigue abierta
> `movimiento_productos.observaciones` y `entradas_producto.observaciones` **siguen siendo `text` sin tope**, y `ProductoCamionModal`/`PesajeModal` siguen sin validador. Cuando les toque van como `ReglasProductoCamion` y `ReglasEntradaPesaje` — `ReglasEntidades.cs` se organiza por entidad, no una clase `ReglasPesaje` para todo.

> [!note] La migración tuvo que recrear una vista
> `v_mov_productos_resumen` lee `placa_vehiculo`, y Postgres bloquea el `ALTER COLUMN … TYPE` de una columna que una vista usa. Al recrearla hay que restaurar a mano `security_invoker` y los `GRANT`, que no viajan en el `CREATE VIEW`. Checklist completo en [[Supabase - Vistas SQL, RLS y security_invoker]].

---

## Archivos clave

```
CapaAplicacion4/Pesaje/
  Dtos/PesajeDtos.cs                    — CamionDto, MovProductoDto, EntradaDto
  Interfaces/IPesajeRepository.cs       — CRUD de camiones/productos/pesajes
  Interfaces/IPickerProductoRepository.cs — selector (incl. GetPagedAsync)

CapaDatos/Repositories/Pesaje/
  PesajeRepository.cs                   — persistencia real
  PickerProductoRepository.cs           — puente producto→fabricante→proveedor

CapaDominio/Reglas/
  ReglasEntidades.cs                    — ReglasCamion (placa 20, descripción 500, cupo 5, ValidarRecepciones)
  ReglasPanelPesaje.cs                  — escala del gráfico, raleo eje X, cascada de avisos del modal

CapaUI/.../Pantallas/Pesaje/
  PesajeView.xaml(.cs)                  — 3 paneles (DgCamiones plano) + estado vacío + impresión de reporte
  PesajeViewModel.cs                    — estado, MaxCamiones, RegistrarCamionesAsync, GenerarReportePesajesAsync
  Modelos/PesajeModels.cs               — PesajeCalc + modelos de UI
  Modales/RegistroCamionesModal         — altas/ediciones/bajas: tabla de hasta 5 recepciones (placa · proveedor · descripción)
  Modales/ProductosCargaModal           — la carga entera de la recepción
  Modales/PesajeModal                   — la pesada (bruto + tara extra opcional) + panel lateral en vivo
  Controles/PesadasChart.cs             — gráfico "Pesadas (kg neto)" dibujado en OnRender
  Modales/TaraExtraTotalModal           — tara extra total, repartida entre pesadas
  Modales/ReporteModal                  — formato (PDF/Excel) y alcance por placa (CheckBox por placa)

supabase/migrations/
  20260918194910_regla_recepciones_abiertas_camiones.sql — trigger + índice único de recepciones abiertas
```

## Estilos de los modales

Los modales de Pesaje comparten `Modales/PesajeModalStyles.xaml` (prefijo `M`): `MLabel`, `MInput`, `MCombo`, `MSegBtn`, `GhostBtn`, `SolidBtn`, `CloseBtn` y los iconos `MIcoX`.

> [!bug] `ModalSegBtn` NO existe acá — es `MSegBtn`
> Los modales de Pesaje usan `MSegBtn` (definido en `PesajeModalStyles.xaml`), no `ModalSegBtn`. Usar `{StaticResource ModalSegBtn}` acá compila sin error y **revienta en runtime** con `XamlParseException: No se puede encontrar el recurso con el nombre 'ModalSegBtn'`, porque un `UserControl` no ve automáticamente los recursos de otro sin merge explícito.
>
> Pasó al crear el viejo `SelectorProductosModal` (2026-07-26) — la solución fue agregar `MSegBtn` al diccionario compartido de Pesaje.
>
> **Corrección 2026-08-19:** la explicación original decía que `ModalSegBtn` "no es visible desde los modales de Pesaje" porque los CRUD lo definen localmente — **falso**, comprobado auditando el código: `ModalSegBtn` está centralizado en `CapaUI/Resources/Styles.xaml` (global, mergeado en `App.xaml`) desde antes de esa sesión, y **sí** es visible desde cualquier lado, Pesaje incluido. El error real de `XamlParseException` sigue siendo cierto si se escribe `ModalSegBtn` en vez de `MSegBtn` en un modal de Pesaje — pero no por la razón que decía esta nota. `MSegBtn` (Pesaje) y `ModalSegBtn` (global) además divergen en estilo — sin aro de foco `MSegBtn` — ver [[Deuda Técnica - Pendientes|P-042]].

> [!warning] El build verde NO garantiza que los StaticResource resuelvan
> WPF resuelve `StaticResource` y `FindResource(...)` **en tiempo de ejecución**. Un `dotnet build` con 0 errores puede esconder recursos inexistentes que revientan al abrir el modal. Al crear un XAML nuevo, cruzar sus `StaticResource` contra: sus propias `Resources`, el diccionario que importe, y `CapaUI/Resources/Styles.xaml` (global vía `App.xaml`).

## Pendientes (revisado 2026-09-18)

Revisado contra `Deuda Técnica - Pendientes.md` y contra el código. Ordenados por impacto.

**🔴 Afectan el peso que se le paga al proveedor**
1. **[[Deuda Técnica - Pendientes#P-023|P-023]] · Catálogo de taras con datos de prueba.** `tara` tiene una sola fila de 20 kg y la usan 503 de 505 productos, casi todos de menos de 10 kg. La tara se resta del bruto, así que el neto pagado sale mal. Requiere **datos reales de planta**.
2. **[[Deuda Técnica - Pendientes#P-024|P-024]] · Tara plana o por bulto.** El trigger la resta una vez por pesada; `PesajeCalc.BultosTeoricos` la cuenta por bulto. Una de las dos está mal. **Bloqueado por P-023.**

**🟠 Funcionalidad faltante**

3. **[[Deuda Técnica - Pendientes#P-036|P-036]] · Sin pantalla CRUD de Tara.** Hoy solo se elige con la lupa al editar un producto; las filas se cargan directo en Supabase. El módulo de Presentaciones sirve de plantilla (falta el combo de unidad filtrado a Masa). Conviene resolverlo junto con P-023.

**🟡 Verificaciones y decisiones abiertas**

4. **[[Deuda Técnica - Pendientes#P-046|P-046]] · Descripciones de estado en Bitácora.** Falta una prueba visual en la grilla real: tiene que decir "Recepción cerrada" o "Pesaje anulado", nunca "Estado 8". El flujo integrado ya lo aprobó Emanuel el 2026-08-24.
5. **Decisión de negocio: ¿el excedente bloquea el guardado?** En `PesajeModal` el excedente se pinta en rojo, pero "Seguir pesando" sigue habilitado. Si se decide bloquear, es una línea en `PesajeModal.Valido`. Ver [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]].
6. **Endurecimiento de la BD, pospuesto a propósito al final del desarrollo.** Las RPC idempotentes y auditadas están desplegadas y `movimientos` y el ingreso de `entradas_producto` ya las usan. Falta migrar las demás escrituras directas, revocar el DML directo y ajustar RLS.

> [!note] Líneas quitadas de este apartado el 2026-09-18 por estar desactualizadas
> - **P-045** (topes de texto), **P-053** (alta múltiple sin transacción) y **P-042** (tercer estilo de input) figuraban como abiertos, pero están resueltos desde el 2026-09-06.
> - Los repositorios viejos `CapaDatos/Repositorios/productos_movimientos/RepositorioMovimiento*.cs` figuraban como "candidatos a eliminar", pero ya no existen en el repo: se borraron en el commit `b4feb64`.

## Relaciones

- [[Plan Offline-First de Pesaje]] — propuesta futura; no representa el comportamiento vigente
- [[ADR-022 - Persistencia local-first con SQLCipher y sincronización por outbox]] — decisión arquitectónica propuesta
- [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]]
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — retiro de `SelectorProductosModal`, marco cuadrado, multiselección de productos
- [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]] — "Seguir pesando" pasó de 4-5 round trips a 1-2, modal ya no se cierra al guardar
- [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] — diez RPC desplegadas; `movimientos` y el ingreso de pesaje ya integrados en C# con detalle legible en Bitácora, sin revocar DML ni modificar RLS
- [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]] — panel lateral, gráfico con punto "Ahora" y avisos en cascada
- [[Sesión 2026-09-18 - Camiones en tabla plana y reglas de recepción en BD]] — tabla plana, reglas R1–R9 en Dominio y trigger, reporte por placa
- [[ADR-029 - Recepciones de pesaje planas con reglas en trigger de tabla]] — por qué una fila por recepción y por qué un trigger
- [[Sesión 2026-09-18 - Auditoría legible y parámetros de reportes en texto]] — normalización textual de Bitácora y parámetros legibles en la RPC de reportes
- [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] — alta en tabla de hasta 5 camiones, `ReglasCamion` y topes reales en `movimientos`
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — el esquema de validación al que el camión por fin se sumó
- [[Supabase - Vistas SQL, RLS y security_invoker]] — por qué la migración de topes tuvo que recrear `v_mov_productos_resumen`
- [[Guardado sin Refetch - Aplicar en memoria la respuesta del servidor]] — el patrón que resolvió la lentitud del guardado
- [[Selector de Catálogo - Selector genérico y multiselección]] — el selector que ahora resuelve Proveedor y Producto acá
- [[Sesión 2026-07-01 - Pantalla Pesaje WPF y Buscador por Proveedor (Fase 1)]]
- [[Sesión 2026-07-01 - Pesaje Fase 2 - Persistencia Real]]
- [[Deuda Técnica - Pendientes]]
- [[Módulo Productos]] · [[Result Pattern]] · [[Bug - Filter OR con Op.Equals en postgrest-csharp]]
