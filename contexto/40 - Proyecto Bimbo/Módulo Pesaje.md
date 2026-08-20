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

> [!warning] Sin verificar: ¿el trigger cubre UPDATE?
> `RepartirTaraExtraAsync` hace `UPDATE` de `peso_tara_extra` sobre entradas ya insertadas. No se pudo comprobar si el trigger está declarado `BEFORE INSERT` o `BEFORE INSERT OR UPDATE` (el MCP de Supabase de la sesión apuntaba a otro proyecto). Como mitigación, `PesajeRepository.ActualizarTaraExtraEntradaAsync` escribe también `peso_tara_total` y `peso_neto` calculados en el cliente con la misma fórmula: la fila queda consistente corra o no el trigger. Si esas columnas resultaran `GENERATED ALWAYS`, hay que poner la constante `EscribirDerivados = false`.

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

### Proceso de descarga — un componente, dos modos
`Modales/ProcesoDescargaModal` unifica lo que antes eran tres modales sueltos.

| | Modo **Wizard** | Modo **Edición** (megamodal) |
|---|---|---|
| Presentación | Una sección a la vez | Todas visibles con título |
| Navegación | Atrás / Siguiente / Finalizar | Scroll libre |
| Se abre desde | Botón del estado vacío / "Nueva descarga" | Botón único "Editar proceso" |

Secciones: **1)** Datos del camión · **2)** Productos de la carga. Cada una con su instrucción escrita. *(La tercera sección, «Tara extra», se eliminó en el rediseño 2026-08-13: no se puede pedir por adelantado algo que hay que pesar.)*

Marco fijo de **720×720** (2026-08-19): antes se autoajustaba al contenido. Un `Grid` con tres filas nombradas alterna cuál sección recibe el alto sobrante (`*`) según el paso — Observaciones se estira en el paso 1, la lista de productos en el paso 2 — nunca las dos a la vez, o el sobrante se repartiría 50/50 entre una sección visible y una colapsada. Ver [[Anatomía compartida de los modales]].

> [!important] Regla de borrado de productos
> Un producto **con pesajes registrados no se puede quitar** (botón deshabilitado + tooltip). Uno agregado por error y sin pesar, sí. Doble guarda: binding + chequeo en el handler.

La sección de Productos **no carga la tabla al abrir** — solo al tocar "Agregar producto" se abre el selector.

### Selector de productos
No es un modal propio de Pesaje: usa `CapaUI/Core/Controls/SelectorCatalogoModal`, el mismo selector genérico que Proveedor (paso 1) y que `ProductoModal`. Hasta el 2026-08-19 existía `Modales/SelectorProductosModal` + `SelectorProductosViewModel`, un picker autocontenido a medida — se **retiró entero** (código y ViewModel) porque duplicaba lo que el genérico ya resuelve, y su `MaxWidth`/`MaxHeight` atados a un `Border` ancestro se rompían al quedar anidado dentro de un modal de 720px. Ver [[Selector de Catálogo - Selector genérico y multiselección]] para el contrato completo.

Config del catálogo: `Catalogos.Productos(_catalogos, prov?.Id, permiteMultiple: true, estaYaElegido: ...)` — acotado al proveedor del paso 1 (puente en 2 pasos producto→fabricante→proveedor, mismo patrón que usaba `PickerProductoRepository`), con checkboxes para agregar varios de una sola apertura, y los productos ya agregados a la carga salen atenuados/bloqueados en la tabla.

Se perdió el toggle "Todo el catálogo" que tenía el picker viejo (el acotamiento por proveedor ahora es fijo) y el `SelectorProductosViewModel` a medida — ya no hace falta, el genérico no depende de `RealtimeAwareViewModel` ni de nada que suscriba a Realtime.

### Modal de pesaje
Captura el **peso bruto** y, opcionalmente, la **tara extra de esa pesada**. Todo lo demás es contexto de solo lectura: placa, proveedor, producto, bultos declarados y los cálculos. Muestra los **bultos estimados** en vivo, avisa en ámbar si la estimación es aproximada (sin tara extra) y en rojo si el neto quedaría en cero o negativo — el CHECK de la BD lo rechazaría con una excepción cruda de Postgrest.

### Camiones cerrados
Desde 2026-08-13 **no se listan en la pantalla**: `GetCamionesActivosAsync` filtra solo `Abierto`. Al cerrar un camión se sigue abriendo el `ReporteModal` (usa el objeto ya en memoria, así que funciona aunque el camión desaparezca de la lista). Van a volver cuando exista la sección de históricos.

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

CapaUI/.../Pantallas/Pesaje/
  PesajeView.xaml(.cs)                  — 3 paneles + estado vacío
  PesajeViewModel.cs                    — estado, GuardarProcesoAsync
  Modelos/PesajeModels.cs               — PesajeCalc + modelos de UI
  Modales/ProcesoDescargaModal          — wizard + megamodal (720x720, incluye Proveedor y Producto vía SelectorCatalogoModal)
  Modales/PesajeModal                   — la pesada (bruto + tara extra opcional)
  Modales/TaraExtraTotalModal           — tara extra total, repartida entre pesadas
  Modales/ReporteModal                  — cierre de camión
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

## Deuda técnica conocida

- **Tara plana vs por bulto** (arriba) y **catálogo de taras con datos de prueba**.
- `CapaDatos/Repositorios/productos_movimientos/RepositorioMovimiento.cs` y `RepositorioMovimientoProducto.cs` son una implementación **vieja y sin usar** (métodos estáticos, sin Result Pattern). `CapaUI` no los referencia. Candidatos a eliminar.

## Relaciones

- [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]]
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — retiro de `SelectorProductosModal`, marco cuadrado, multiselección de productos
- [[Selector de Catálogo - Selector genérico y multiselección]] — el selector que ahora resuelve Proveedor y Producto acá
- [[Sesión 2026-07-01 - Pantalla Pesaje WPF y Buscador por Proveedor (Fase 1)]]
- [[Sesión 2026-07-01 - Pesaje Fase 2 - Persistencia Real]]
- [[Deuda Técnica - Pendientes]]
- [[Módulo Productos]] · [[Result Pattern]] · [[Bug - Filter OR con Op.Equals en postgrest-csharp]]
