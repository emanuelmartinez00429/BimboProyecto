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

> [!important] Regla de borrado de productos
> Un producto **con pesajes registrados no se puede quitar** (botón deshabilitado + tooltip). Uno agregado por error y sin pesar, sí. Doble guarda: binding + chequeo en el handler.

La sección de Productos **no carga la tabla al abrir** — solo al tocar "Agregar producto" se abre el selector.

### Selector de productos
`Modales/SelectorProductosModal` + `SelectorProductosViewModel`. Tabla con código, paginación server-side, buscador con debounce, y toggle "Solo proveedor / Todo el catálogo".

> [!tip] Por qué no se reutiliza ProductosViewModel
> Hereda de `RealtimeAwareViewModel`, cuyo **constructor** ya se engancha a `IConexionMonitor`, y en la carga se suscribe a Realtime. Abrir el modal dejaría suscripciones vivas cada vez. El VM del selector es un `ObservableObject` plano, con `PageSize=15` y `Dispose()` que cancela el debounce.

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
  Modales/ProcesoDescargaModal          — wizard + megamodal
  Modales/SelectorProductosModal        — tabla de productos
  Modales/PesajeModal                   — la pesada (bruto + tara extra opcional)
  Modales/TaraExtraTotalModal           — tara extra total, repartida entre pesadas
  Modales/ReporteModal                  — cierre de camión
```

## Estilos de los modales

Los modales de Pesaje comparten `Modales/PesajeModalStyles.xaml` (prefijo `M`): `MLabel`, `MInput`, `MCombo`, `MSegBtn`, `GhostBtn`, `SolidBtn`, `CloseBtn` y los iconos `MIcoX`.

> [!bug] `ModalSegBtn` NO existe acá — es `MSegBtn`
> Los modales CRUD (Categoría, Empleado, Producto…) definen `ModalSegBtn` **localmente** en sus propias `UserControl.Resources`. Ese estilo **no es visible** desde los modales de Pesaje: los recursos locales de un UserControl no se comparten con otros. Usar `{StaticResource ModalSegBtn}` acá compila sin error y **revienta en runtime** con `XamlParseException: No se puede encontrar el recurso con el nombre 'ModalSegBtn'`.
>
> Pasó al crear `SelectorProductosModal` (2026-07-26). La solución fue agregar `MSegBtn` al diccionario compartido de Pesaje.

> [!warning] El build verde NO garantiza que los StaticResource resuelvan
> WPF resuelve `StaticResource` y `FindResource(...)` **en tiempo de ejecución**. Un `dotnet build` con 0 errores puede esconder recursos inexistentes que revientan al abrir el modal. Al crear un XAML nuevo, cruzar sus `StaticResource` contra: sus propias `Resources`, el diccionario que importe, y `CapaUI/Resources/Styles.xaml` (global vía `App.xaml`).

## Deuda técnica conocida

- **Tara plana vs por bulto** (arriba) y **catálogo de taras con datos de prueba**.
- `CapaDatos/Repositorios/productos_movimientos/RepositorioMovimiento.cs` y `RepositorioMovimientoProducto.cs` son una implementación **vieja y sin usar** (métodos estáticos, sin Result Pattern). `CapaUI` no los referencia. Candidatos a eliminar.

## Relaciones

- [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]]
- [[Sesión 2026-07-01 - Pantalla Pesaje WPF y Buscador por Proveedor (Fase 1)]]
- [[Sesión 2026-07-01 - Pesaje Fase 2 - Persistencia Real]]
- [[Deuda Técnica - Pendientes]]
- [[Módulo Productos]] · [[Result Pattern]] · [[Bug - Filter OR con Op.Equals en postgrest-csharp]]
