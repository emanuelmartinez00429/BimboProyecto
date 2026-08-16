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
  Dtos/BitacoraDto.cs
  Queries/BitacoraFiltros.cs                    — IdUsuario, IdModulo, IdAccion, FechaDesde, FechaHasta
  Interfaces/IBitacoraRepository.cs             — SOLO lectura + lookups de dropdowns
CapaDatos/Repositories/Bitacora/BitacoraCrudRepository.cs
CapaUI/.../Pantallas/Bitacora/BitacoraViewModel.cs
CapaUI/.../Pantallas/Bitacora/BitacoraView.xaml(.cs)
```

## Reportes de filas seleccionadas

Desde 2026-08-16 la grilla permite seleccionar una o varias filas de la página visible mediante casillas individuales o la casilla del encabezado. La selección habilita **Crear reporte**, que ofrece PDF o Excel y exporta únicamente las seis columnas visibles.

El archivo incorpora, fuera de la tabla, la identidad de quien lo genera: correo de la sesión, nombre y apellido del empleado y rol. Los nombres del empleado se cargan en `UsuarioSesion` desde `PerfilUsuarioService`; no se reconstruyen a partir del correo ni del alias.

El flujo es transaccional desde la perspectiva de entrega del archivo:

1. `IReportGeneratorService` resuelve `PdfReportStrategy` o `ExcelReportStrategy` y genera el documento en memoria.
2. La UI escribe un archivo temporal junto al destino elegido.
3. `ReporteRepository` ejecuta `ingresar_reporte_tabla_bitacora`, enviando nombre, tipo, descripción, rango de las filas y un JSON con IDs seleccionados, filtros activos y columnas.
4. Solo si la RPC devuelve un entero positivo se mueve el temporal al nombre definitivo. Si falla, se elimina el temporal y se muestra el error.

La generación del reporte **no crea una entrada nueva en `bitacora` desde la UI**; registra el reporte exclusivamente mediante la RPC indicada. La selección se limita a la página actual y se limpia al cambiar de página o recargar filtros.

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
| Modal | Sí | solo selector de formato para reportes; no existe modal CRUD |
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
- [[Sesión 2026-08-16 - Reportes PDF y Excel desde Bitácora]]
- [[Sesión 2026-07-26 - Módulo Bitácora (auditoría, solo lectura)]]
