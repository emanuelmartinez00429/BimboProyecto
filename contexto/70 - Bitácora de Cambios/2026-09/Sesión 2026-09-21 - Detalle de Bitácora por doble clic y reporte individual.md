---
title: "Sesión 2026-09-21 — Detalle de Bitácora por doble clic y reporte individual"
tags:
  - sesion
  - bitacora
  - reportes
  - wpf
date: 2026-09-21
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-09-21 — Detalle de Bitácora por doble clic y reporte individual

> [!success] Resultado
> La tabla de Bitácora permite abrir el detalle completo de una fila mediante doble clic y exportar únicamente ese registro a PDF o Excel, sin alterar consulta, filtros, búsqueda, paginación, orden ni selección existentes.

---

## Problema / motivo

La tabla resumía cada evento a seis columnas y recortaba textos extensos. No existía una vista que reuniera el estado anterior, la información adicional, la tabla de origen y la referencia del registro, ni una exportación individual iniciada desde esa vista.

## Cambios aplicados

- `BitacoraView` detecta el doble clic únicamente cuando el origen pertenece a un `DataGridRow` con un `BitacoraDto`; ignora encabezados, espacios vacíos y controles interactivos. Un guard impide overlays o detalles duplicados.
- `BitacoraDetalleModal` presenta el registro como solo lectura, con scroll, cierre por `Regresar`/`×`, error no destructivo y restauración del foco en la fila al cerrar.
- `BitacoraDetalle.CrearCampos` centraliza los nombres amigables, el orden de los once datos, el formato de fecha y el fallback `Sin información` para que UI y archivos coincidan.
- `FormatoReporteModal` se reutiliza con una descripción específica. Cancelarlo devuelve al detalle; no se creó otro selector.
- `BitacoraViewModel.GenerarReporteDetalleAsync` exporta un único registro y conserva permiso `Generar Reporte`, sesión activa, generación Strategy, temporal, auditoría RPC y apertura del archivo. Un fallo no cierra el detalle.
- `TabularReportDto` incorporó `RecordCount` y `Landscape`; `ReportColumnDto`, `WidthCm`. Sus defaults preservan los reportes existentes, mientras el detalle usa conteo 1, A4 vertical y columnas CAMPO/VALOR.

El trabajo quedó incluido en `fc3d1c7` (`fixes`), publicado en `origin/feat/fase8-MaquetadodeRoles`.

## Verificación

- Pruebas de Bitácora y Reportes: 28/28 aprobadas.
- `BitacoraDetalleTests`: etiquetas, fecha visible, valores nulos/vacíos y referencias no positivas.
- `ReportStrategyTests`: conteo semántico de un registro representado por varias filas y contenido Excel.
- `dotnet build BimboProyecto.sln --no-restore --nologo`: 0 errores y 0 advertencias.
- La suite completa alcanzó 687/691; los cuatro fallos reproducibles pertenecen a `PreferenciasInicioSesionServiceTests`/DPAPI y no intersectan Bitácora ni Reportes.
- Emanuel solicitó registrar la funcionalidad como vigente el 2026-09-21.

## Lo que NO cambió

- No se modificaron tablas, RPC, RLS, funciones ni datos de Supabase.
- No se consulta nuevamente el registro al abrir el detalle.
- No se alteraron columnas visibles, formatos de la grilla, filtros, búsqueda, paginación, ordenamiento ni selección por clic/arrastre.
- No se duplicaron el selector de formato, los servicios de PDF/Excel ni la lógica de auditoría.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Bitácora]]
- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Sesión 2026-09-02 - Selección avanzada de filas en Bitácora]]
- [[Sesión 2026-09-18 - Auditoría legible y parámetros de reportes en texto]]
