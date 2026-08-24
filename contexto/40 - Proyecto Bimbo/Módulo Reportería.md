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
| Entrada de materia prima | Producto y proveedor mediante lupa, fecha desde y fecha hasta | Entradas no anuladas, abiertas o cerradas dentro del rango inclusivo |
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

## Selectores de catálogo y vista previa

Producto, Proveedor y Categoría reutilizan `SelectorCatalogoModal`, alojado en
Reportería dentro de una tarjeta centrada de 820×650 con el marco degradado de
los modales del sistema. El overlay oscurece la vista, pero el selector ya no se
expande sobre toda la pantalla.

Los tres usan `CatalogoConfig` con `PageSize = 50` y `ForzarPaginacion = true`:
la consulta permanece server-side aun cuando el catálogo tenga menos de 200
registros, el pie de paginación se mantiene visible y cada búsqueda vuelve a la
primera página. Esta configuración es local a Reportería; los demás consumidores
del selector conservan el modo adaptativo con caché en memoria.

## Contenido de la vista previa y los archivos

La vista previa y el documento exportado comparten exactamente `_columnas` y
`_snapshot`; una columna agregada en ese snapshot aparece tanto en pantalla como
en PDF/Excel. En `Entrada de materia prima`, la tabla siempre contiene fecha/hora
y los pesos bruto, tara y neto. Producto y proveedor permanecen como metadatos.
Placa y pesador son dinámicos: con un solo valor aparecen como metadato; con más
de un valor pasan a columnas y conservan la asociación de cada pesaje. Un dato
vacío se representa como `—` y cuenta como valor distinto para evitar atribuir
información conocida a una fila incompleta. En `Reporte por proveedor`, cada
fila incluye el Producto consultado y el nombre del Proveedor seleccionado.

Los IDs seleccionados y el rango de fechas permanecen dentro de
`ParametrosJson` de la bitácora como trazabilidad. La fecha `Hasta` no puede ser
posterior a la fecha actual; la UI limita el calendario y el ViewModel valida
otra vez antes de consultar. `Desde` y `Hasta` se muestran horizontalmente
después de la tabla: sobre la paginación en la vista previa y después de los
totales en PDF/Excel. El rango se aplica de forma inclusiva en el repositorio
sobre las filas autorizadas que devuelve la RPC vigente.

El bloque de autoría compartido por PDF y Excel contiene solamente **Correo
usuario** y **Rol**. Nombre y apellido del empleado fueron retirados de
`ReportAuthorDto`: el correo ya identifica la cuenta y evita duplicar datos
personales que no aportan al documento.

La grilla de vista previa de los 4 reportes adopta el diseño unificado de tablas
del sistema: tarjeta blanca con sombra `#0F172A`, encabezados con color corporativo
`EmpresaPrimaryBrush`, filas alternas `#E8F0FA`, hover `#C8D8F0`, selección `#C2F2E0`
con barra indicadora `#34D399`, scrollbars modernas `ModernScrollBarAny`, spinner
animado de carga, estado vacío y barra de paginación completa (`«`, `‹`, números
con elipsis `Paginacion.Calcular`, `›`, `»`).

## Archivos clave

- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaViewModel.cs`
- `CapaAplicacion4/Reportes/Interfaces/IReporteConsultaRepository.cs`
- `CapaAplicacion4/Reportes/Dtos/ReporteOperativoDtos.cs`
- `CapaDatos/Repositories/Reportes/ReporteConsultaRepository.cs`
- `CapaDatos/Reportes/PdfReportStrategy.cs`
- `CapaDatos/Reportes/ExcelReportStrategy.cs`

## Verificación pendiente

La solución compila y las estrategias tienen pruebas automatizadas. Para el
cambio del 2026-08-23 quedan pendientes la prueba visual autenticada de las tres
lupas, la adaptación del marco a 960×600 y la navegación real entre páginas de
50 registros. La validación automatizada terminó con 0 errores de build y 113
pruebas superadas.

## Relaciones

- [[Arquitectura Actual]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Módulo Bitácora]]
- [[Sesión 2026-08-17 - Módulo Reportería operativo]]
- [[Sesión 2026-08-23 - Selectores compactos y paginados en Reportería]]
