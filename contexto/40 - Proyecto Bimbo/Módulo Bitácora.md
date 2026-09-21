---
title: Módulo Bitácora
tags:
  - modulo
  - bitacora
  - auditoria
date: 2026-07-26
---

# Módulo Bitácora

Pantalla de auditoría: muestra el historial de acciones registradas por el sistema en la tabla `bitacora`. **Solo lectura por diseño** — la bitácora la escribe el propio sistema al ejecutar operaciones, nunca esta pantalla. `IBitacoraRepository` no expone `Create`/`Update`/`Delete` a propósito.

---

## Qué muestra

Tabla ordenada por **fecha descendente** (más reciente primero), paginada 50/página server-side:

| Columna | Origen |
|---|---|
| FECHA / HORA | `bitacora.fecha_hora` (`timestamptz`) |
| USUARIO | `usuarios.alias_usuario` (embed) |
| MÓDULO | `modulos.nombre_modulo` (embed) |
| ACCIÓN | `acciones.nombre_accion` (embed) |
| CAMPO AFECTADO | `bitacora.campo_afectado` |
| DETALLE | `bitacora.estado_actual` (con `ToolTip` para el texto completo) |

Un solo chip de stats: **TOTAL** — la bitácora no tiene concepto de activo/inactivo, así que no aplica el trío TOTAL/ACTIVOS/INACTIVOS de los demás módulos.

## Detalle de un registro

Desde 2026-09-21, un doble clic izquierdo sobre una fila válida abre `BitacoraDetalleModal` dentro del overlay de la pantalla. El modal es de solo lectura y recibe directamente el `BitacoraDto` enlazado a la fila; no reconstruye valores desde las celdas ni vuelve a consultar Supabase.

`BitacoraDetalle.CrearCampos` es la proyección compartida por el modal y el reporte individual. Presenta etiquetas administrativas y los valores disponibles del registro:

- registro de bitácora, fecha/hora, usuario, módulo y acción;
- campo afectado, estado anterior y detalle actual;
- información adicional, origen y referencia del registro.

La fecha conserva `dd/MM/yyyy HH:mm`; cualquier texto nulo, vacío o referencia no positiva se muestra como **Sin información**. El contenido extenso usa ajuste de línea y desplazamiento vertical.

La apertura es defensiva: no responde sobre encabezados, espacios vacíos, filas sin `BitacoraDto` ni controles interactivos internos. Si ya hay un overlay o detalle abierto, no crea otra instancia. El clic simple, la selección múltiple, el arrastre, los filtros, la búsqueda y la paginación mantienen su flujo anterior. Al cerrar con **Regresar** o `×`, se conserva la selección y el foco vuelve a la fila que abrió el detalle.

## Filtros

Todos combinables y aplicados **server-side** (importante: la tabla crece a miles de registros):

- **Módulo** — dropdown poblado desde `modulos`
- **Acción** — dropdown en **cascada**: se repuebla filtrado por el módulo elegido (`ObtenerAccionesAsync(idModulo)`). Al cambiar de módulo se limpia el filtro de acción anterior, porque puede no existir en el módulo nuevo.
- **Usuario** — dropdown desde `usuarios` (alias)
- **Desde / Hasta** — `DatePicker`, **primer uso de este control en el proyecto**

> [!tip] `FechaHasta` incluye el día completo
> `fecha_hora` es `timestamptz`, no `date`. El repositorio lleva `FechaHasta` a las 23:59:59 de ese día (`.Date.AddDays(1).AddSeconds(-1)`) antes de filtrar — si se pasara la fecha cruda se perderían todos los registros de ese día posteriores a medianoche.

Buscador de texto libre (`SuggestionSearchBox`, igual que los demás módulos) sobre `campo_afectado`, `estado_actual` y `tabla_afectada`.

## Archivos clave

```
CapaDatos/Modelados/Usuarios/Bitacora.cs        — modelo Supabase + embeds usuarios/acciones/modulos
CapaAplicacion4/Bitacora/
  BitacoraDetalle.cs                          — etiquetas, orden, formato y fallback compartidos
  Dtos/BitacoraDto.cs
  Queries/BitacoraFiltros.cs                    — IdUsuario, IdModulo, IdAccion, FechaDesde, FechaHasta
  Interfaces/IBitacoraRepository.cs             — SOLO lectura + lookups de dropdowns
CapaDatos/Repositories/Bitacora/BitacoraCrudRepository.cs
CapaUI/.../Pantallas/Bitacora/BitacoraViewModel.cs
CapaUI/.../Pantallas/Bitacora/BitacoraView.xaml(.cs)
CapaUI/.../Pantallas/Bitacora/BitacoraDetalleModal.xaml(.cs)
```

## Reportes de filas seleccionadas

Desde 2026-09-02 la grilla mantiene la selección múltiple sin mostrar una columna de casillas. Un clic normal selecciona una sola fila; mantener el clic y arrastrar selecciona un rango continuo; `Shift` + clic agrega o quita filas individuales no contiguas sin afectar las demás. El botón **Seleccionar página** marca todos los registros cargados y cambia a **Limpiar selección** cuando la página completa está seleccionada.

La selección habilita **Crear reporte**, que ofrece PDF o Excel y exporta únicamente las seis columnas visibles. Las filas se entregan al generador en el mismo orden visual de la tabla, independientemente del orden en que fueron seleccionadas.

El archivo incorpora, fuera de la tabla, la identidad de quien lo genera: correo de la sesión, nombre y apellido del empleado y rol. Los nombres del empleado se cargan en `UsuarioSesion` desde `PerfilUsuarioService`; no se reconstruyen a partir del correo ni del alias.

El flujo es transaccional desde la perspectiva de entrega del archivo:

1. `IReportGeneratorService` resuelve `PdfReportStrategy` o `ExcelReportStrategy` y genera el documento en memoria.
2. La UI escribe un archivo temporal junto al destino elegido.
3. `ReporteRepository` ejecuta `ingresar_reporte_tabla_bitacora`, enviando nombre, tipo, descripción, rango de las filas y texto descriptivo con IDs seleccionados, filtros activos y columnas.
4. Solo si la RPC devuelve un entero positivo se mueve el temporal al nombre definitivo. Si falla, se elimina el temporal y se muestra el error.

La generación del reporte **no crea una entrada nueva en `bitacora` desde la UI**; registra el reporte exclusivamente mediante la RPC indicada. La selección se limita a la página actual y se limpia al cambiar de página o recargar filtros.

### Reporte individual desde el detalle

**Imprimir reporte** reutiliza `FormatoReporteModal`; no existe un segundo selector de PDF/Excel. El botón conserva el permiso `Generar Reporte`, que se vuelve a validar antes de abrir el selector y antes de generar, por si la sesión perdió autorización.

El reporte contiene exclusivamente el `BitacoraDto` abierto y usa los mismos pares **CAMPO → VALOR** del modal. `TabularReportDto.RecordCount = 1` mantiene el conteo semántico aunque un registro ocupe varias filas; `Landscape = false` produce el PDF en A4 vertical y `ReportColumnDto.WidthCm` asigna anchos legibles a las dos columnas. Los valores largos se ajustan en PDF y Excel.

Después de escoger formato se solicita la ruta. Cancelar el selector o el diálogo de guardado restaura el detalle sin exportar. Si la generación o la auditoría falla, el error se muestra dentro del modal, que permanece abierto; el archivo temporal se elimina mediante el flujo transaccional existente. El reporte exitoso se registra mediante `ingresar_reporte_tabla_bitacora` y luego se abre con la asociación del sistema.

## Contrato de texto legible

Desde 2026-09-18 los campos visibles `estado_anterior`, `estado_actual` y `campo_extra` se guardan como texto administrativo, no como JSON. La migración `20260918192751_bitacora_y_reportes_texto` aplica dos defensas complementarias:

- `private.registrar_auditoria_rbac` convierte las estructuras recibidas por las RPC antes del `INSERT`.
- `trg_bitacora_texto` normaliza cualquier emisor adicional justo antes de insertar en `public.bitacora`.

`BitacoraCrudRepository` también usa `TextoAuditoria.Formatear` al mapear filas. Esa segunda defensa permite mostrar registros históricos JSON como pares etiqueta–valor sin modificar físicamente el historial append-only. Los estados se presentan por su significado de negocio y no por sus identificadores numéricos.

Los parámetros utilizados al registrar reportes se construyen con `ParametrosReporteTexto`; el JSON permanece reservado para contratos técnicos no visibles, como idempotencia y respuestas internas de RPC. Ver [[Sesión 2026-09-18 - Auditoría legible y parámetros de reportes en texto]].

Archivos adicionales:

```text
CapaDominio/Reportes/ReportFormat.cs
CapaAplicacion4/Reportes/                 — DTOs, contratos y orquestador Strategy
CapaDatos/Reportes/                       — estrategias PDFsharp/MigraDoc y ClosedXML
CapaDatos/Repositories/Reportes/ReporteRepository.cs
CapaUI/.../Bitacora/FormatoReporteModal.xaml(.cs)
BimboProyecto.Tests/Reportes/ReportStrategyTests.cs
```

> [!bug] Aliases obligatorios en el repositorio
> `BitacoraCrudRepository.cs` **debe** usar `using BitacoraModel = CapaDatos.Modelados.Usuarios.Bitacora;` y `using UsuariosModel = ...Usuarios.Usuarios;`. Los namespaces hermanos `CapaDatos.Repositories.Bitacora` y `CapaDatos.Repositories.Usuarios` ganan la resolución de nombres de C# sobre el `using CapaDatos.Modelados.Usuarios;` → `CS0118: es espacio de nombres pero se usa como tipo`. Es el mismo tropiezo que hubo con el [[Módulo Empleados]] (ahí se resolvió renombrando el namespace a `GestionEmpleados`); acá se resolvió con aliases, que es lo que ya hacía `UsuarioRepository.cs`.

## Particularidades vs. el patrón estándar

| Aspecto | Los demás módulos | Bitácora |
|---|---|---|
| Orden | `id_xxx` ASC | `fecha_hora` **DESC** |
| Comandos CRUD | Nuevo / Editar / Cambiar Estado | **ninguno** |
| Modal | Sí | detalle de solo lectura + selector de formato; no existe modal CRUD |
| Filtro de estado | Segmentado Activos/Inactivos/Todos | no existe (sin columna de estado) |
| Stats | TOTAL / ACTIVOS / INACTIVOS | solo TOTAL |
| `Seleccionado` | dispara Editar | solo resalta la fila al elegir sugerencia |

**`GetPaginaDeRegistroAsync`** también difiere: como el orden es por fecha DESC (no por ID ASC), calcula la página consultando la `fecha_hora` del registro y contando cuántos tienen fecha **mayor**. Sin desempate por ID — riesgo despreciable porque `timestamptz` tiene resolución de microsegundos.

## Calidad de los datos existentes

Los registros ya cargados en producción vienen con formato dispar: `tabla_afectada` a veces trae el literal `'Sin registro'` (con comillas simples dentro del string) e `id_registro_afectado = 0` como "no aplica". La pantalla los muestra tal cual — **no se normaliza nada en la UI**. Por eso `tabla_afectada` e `id_registro_afectado` quedaron fuera de los filtros: no son confiables como criterio de búsqueda todavía.

## Relaciones

- [[Módulo Empleados]] — mismo tropiezo de namespace, resuelto distinto
- [[Módulo Productos]] — patrón de referencia general
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Sesión 2026-09-02 - Selección avanzada de filas en Bitácora]]
- [[Sesión 2026-09-18 - Auditoría legible y parámetros de reportes en texto]]
- [[Sesión 2026-09-21 - Detalle de Bitácora por doble clic y reporte individual]]
- [[Sesión 2026-08-16 - Reportes PDF y Excel desde Bitácora]]
- [[Sesión 2026-07-26 - Módulo Bitácora (auditoría, solo lectura)]]
