---
title: "Módulo Reportería"
tags: [modulo, reportes, pdf, excel, supabase]
date: 2026-08-17
---

# Módulo Reportería

## Propósito

La pantalla `ReporteriaView` concentra cuatro reportes operativos en una grilla de dos columnas por dos filas. Cada tarjeta abre un subformulario coherente con el diseño WPF del sistema y presenta una vista previa paginada antes de exportar.

## Reportes vigentes

| Reporte | Filtros | Alcance |
|---|---|---|
| Entrada de materia prima | Producto y proveedor mediante lupa | Entradas no anuladas, abiertas o cerradas |
| Por proveedor | Proveedor mediante lupa y rango de fechas | Movimientos cerrados; fecha física de entrada |
| Productos con más merma | Rango de fechas y categoría opcional mediante lupa | Movimientos cerrados; mermas positivas primero y luego cero/negativas |
| Primeros 10 productos | Sin filtros | Productos activos ordenados por ID ascendente |

El reporte de proveedor calcula bultos como estimación con la fórmula vigente de peso bruto, tara extra, peso teórico y tara de embalaje. La diferencia monetaria es `(peso recibido - peso teórico) × precio por kg`.

## Flujo técnico

1. `ReporteriaViewModel` valida filtros y permiso `Consultar Reporte`.
2. `IReporteConsultaRepository` invoca una RPC tipada por reporte.
3. La misma instantánea alimenta la vista previa y el `TabularReportDto` de exportación.
4. El usuario elige PDF o Excel y una ruta.
5. `ReporteRepository` registra primero el reporte mediante `ingresar_reporte_tabla_bitacora`, protegido por `Generar Reporte`.
6. Solo con un ID positivo se genera y escribe el archivo definitivo.
7. Al terminar correctamente, Windows abre el PDF o Excel con la aplicación predeterminada. Si la apertura falla, el archivo permanece guardado y se muestra una advertencia.

Las cuatro RPC son `SECURITY INVOKER`, fijan `search_path`, niegan ejecución a `anon`, permiten `authenticated` y vuelven a validar sesión activa, permiso y catálogos seleccionados. Esto evita confiar únicamente en la visibilidad de botones del cliente.

## Archivos clave

- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaViewModel.cs`
- `CapaAplicacion4/Reportes/Interfaces/IReporteConsultaRepository.cs`
- `CapaAplicacion4/Reportes/Dtos/ReporteOperativoDtos.cs`
- `CapaDatos/Repositories/Reportes/ReporteConsultaRepository.cs`
- `CapaDatos/Reportes/PdfReportStrategy.cs`
- `CapaDatos/Reportes/ExcelReportStrategy.cs`

## Verificación pendiente

La solución compila y las estrategias tienen pruebas automatizadas. Falta la prueba visual manual con sesión autenticada: abrir lupas, consultar los cuatro reportes, confirmar la vista previa y abrir un PDF y un Excel reales.

## Relaciones

- [[Arquitectura Actual]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Módulo Bitácora]]
- [[Sesión 2026-08-17 - Módulo Reportería operativo]]
