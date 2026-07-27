---
title: Módulo Pesaje (Recepción de Materia Prima)
tags:
  - modulo
  - pesaje
  - movimientos
date: 2026-07-26
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
| **Tara extra** | `movimientos.peso_tara_extra` | Tarimas, forros, separadores. **Se pesa UNA sola vez para toda la carga** |
| **Peso teórico** | `productos.peso_teorico` | Peso unitario del producto, sin empaque |

### Prorrateo de la tara extra

Como la tara extra se pesa una sola vez pero hay muchas pesadas, se reparte:

```
tara_extra_por_bulto = movimientos.peso_tara_extra / Σ(bultos declarados del camión)
tara_extra_de_esta_pesada = tara_extra_por_bulto × bultos de esta pesada
```

Así, al terminar de pesar toda la carga, la suma de taras extra atribuidas equivale al total real. El cálculo vive en `PesajeCalc.TaraExtraPorBulto` y lo aplica **el ViewModel** (`PesajeViewModel.GuardarEntradaAsync`) — fuente única de verdad, no se confía en el snapshot del modal.

### El trigger de BD calcula el neto guardado

`trg_calcular_pesos_entrada` → `calcular_pesos_entrada`. Al insertar una entrada, la app envía solo bruto, tara extra y bultos; el trigger completa:

- `peso_tara_individual` = tara de empaque del producto (**plana**, una vez por pesada)
- `peso_tara_total` = tara_extra + tara_individual
- `peso_neto` = bruto − tara_total (con excepción si sale ≤ 0)

---

## Bultos declarados vs bultos teóricos

Dos conceptos distintos que antes se llamaban igual:

| | Qué es | De dónde sale |
|---|---|---|
| **Bultos declarados** | Lo que dice el manifiesto en papel, sin verificar | Columna `movimiento_productos.bultos_teoricos` (nombre histórico) |
| **Bultos teóricos** | Cuántos bultos representa el peso realmente pesado | **Calculado**, no se guarda |

```
bultos_teoricos = peso_bruto / (peso_teorico + tara_empaque + tara_extra_por_bulto)
```

Implementado en `PesajeCalc.BultosTeoricos`. Devuelve `null` (la UI muestra "—") si el bruto no es positivo, si falta el peso teórico del producto, o si el denominador da cero.

> [!warning] Inconsistencia conocida — tara plana vs tara por bulto
> El **trigger** aplica la tara de empaque **plana** (una vez por pesada), pero la **fórmula de bultos teóricos** la trata **por bulto**. Conviven a propósito: el neto guardado no cambia (es plata que se paga al proveedor), y el indicador nuevo es informativo. Registrado como deuda técnica — ver [[Deuda Técnica - Pendientes]].

> [!bug] Los datos de tara son de prueba
> Verificado 2026-07-26: la tabla `tara` tiene **una sola fila** (20 kg) usada por 503 de 505 productos, y **500 de esos productos pesan menos de 10 kg**. El empaque pesaría el doble o más que el producto. Los otros 5 (1,000–10,000 kg) tienen nombres tipo "Producto Number 1". Hasta que se cargue el catálogo real, los bultos teóricos van a dar números que no reflejan la realidad.

---

## Flujo de la UI (rediseño 2026-07-26)

### Estado vacío
Si **no hay ningún camión abierto**, la pantalla muestra "Actualmente no hay camiones descargándose" con un botón para iniciar el proceso, en vez de tres paneles vacíos. Si hay camiones cerrados, un enlace permite verlos.

### Proceso de descarga — un componente, dos modos
`Modales/ProcesoDescargaModal` unifica lo que antes eran tres modales sueltos.

| | Modo **Wizard** | Modo **Edición** (megamodal) |
|---|---|---|
| Presentación | Una sección a la vez | Todas visibles con título |
| Navegación | Atrás / Siguiente / Finalizar | Scroll libre |
| Se abre desde | Botón del estado vacío / "Nueva descarga" | Botón único "Editar proceso" |

Secciones: **1)** Datos del camión · **2)** Productos de la carga · **3)** Tara extra. Cada una con su instrucción escrita.

> [!important] Regla de borrado de productos
> Un producto **con pesajes registrados no se puede quitar** (botón deshabilitado + tooltip). Uno agregado por error y sin pesar, sí. Doble guarda: binding + chequeo en el handler.

La sección de Productos **no carga la tabla al abrir** — solo al tocar "Agregar producto" se abre el selector.

### Selector de productos
`Modales/SelectorProductosModal` + `SelectorProductosViewModel`. Tabla con código, paginación server-side, buscador con debounce, y toggle "Solo proveedor / Todo el catálogo".

> [!tip] Por qué no se reutiliza ProductosViewModel
> Hereda de `RealtimeAwareViewModel`, cuyo **constructor** ya se engancha a `IConexionMonitor`, y en la carga se suscribe a Realtime. Abrir el modal dejaría suscripciones vivas cada vez. El VM del selector es un `ObservableObject` plano, con `PageSize=15` y `Dispose()` que cancela el debounce.

### Modal de pesaje
Captura el **peso bruto** y los bultos de esta pesada. Todo lo demás es contexto de solo lectura: placa, proveedor, producto, bultos declarados, tara extra asignada (prorrateada) y los cálculos. Muestra los **bultos teóricos** en vivo y avisa si falta registrar la tara extra.

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
  Modales/PesajeModal                   — la pesada
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
