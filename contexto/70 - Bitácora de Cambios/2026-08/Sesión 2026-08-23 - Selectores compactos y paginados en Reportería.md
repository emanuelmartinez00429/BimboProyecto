---
title: "Sesión 2026-08-23 — Selectores compactos y paginados en Reportería"
tags: [sesion, reporteria, selector-catalogo, paginacion, wpf]
date: 2026-08-23
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-08-23 — Selectores compactos y paginados en Reportería

## Objetivo

Evitar que las lupas de Reportería expandan el selector sin marco sobre toda la
vista. Producto, Proveedor y Categoría debían usar una tarjeta compacta, con el
estilo de `ProductoModal` y páginas de 50 registros.

## Trabajo realizado

- `ReporteriaView` separó el host de los selectores del host de
  `FormatoReporteModal` y agregó una tarjeta centrada de 820×650, responsiva al
  espacio disponible, con gradiente corporativo, sombra y esquinas redondeadas.
- `CatalogoConfig` incorporó `PageSize` y `ForzarPaginacion`. Sus valores por
  defecto conservan el comportamiento de los consumidores existentes.
- Reportería configura los tres catálogos con 50 registros por página y fuerza
  consulta server-side incluso debajo del umbral de caché de 200 registros.
- La búsqueda sigue usando debounce, vuelve a la página 1 y reutiliza las
  consultas existentes de `ICatalogoRepository`; no hubo cambios de esquema,
  RPC, RLS ni permisos.

## Archivos modificados

- `CapaUI/Core/Catalogos/CatalogoConfig.cs`
- `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml.cs`
- `contexto/40 - Proyecto Bimbo/Plan Fase 9 - Subsistema de Reportes.md`
- `contexto/40 - Proyecto Bimbo/Módulo Reportería.md`
- `contexto/20 - Patrones/Selector de Catálogo - Selector genérico y multiselección.md`

## Decisiones

- La paginación forzada es configurable por consumidor; no se cambió globalmente
  el comportamiento adaptativo del selector.
- El selector continúa siendo chromeless. Reportería aporta el marco desde su
  propio overlay para no duplicar búsqueda, tabla, teclado ni selección.
- No se creó ADR porque se extendió un patrón existente sin cambiar la
  arquitectura del módulo.

## Pruebas y validaciones

- `git diff --check` — correcto; solo avisos informativos de normalización LF/CRLF.
- `dotnet build BimboProyecto.sln --no-incremental` — 0 errores, 66 advertencias
  preexistentes. `NU1900` indicó que no se pudo consultar el índice de
  vulnerabilidades de NuGet.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build` — 113
  pruebas superadas, 0 fallidas, 0 omitidas.

## Pendientes

- Prueba visual autenticada de Producto, Proveedor y Categoría.
- Confirmar adaptación a 960×600, scroll interno y navegación entre páginas con
  catálogos de más de 50 registros.

## Relaciones

- [[Módulo Reportería]]
- [[Selector de Catálogo - Selector genérico y multiselección]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[Anatomia compartida de los modales]]

## Corrección posterior — Producto y Proveedor dentro de la tabla

Se detectó que `consultar_reporte_entrada_materia_prima` y
`EntradaMateriaPrimaFila` ya entregaban Producto y Proveedor, pero
`ReporteriaViewModel.CargarEntradaAsync` los descartaba al construir `_columnas`
y `_snapshot`. Como ese snapshot alimenta tanto la vista previa como PDF/Excel,
los dos destinos perdían los nombres.

### Cambios

- `Entrada de materia prima` agregó Producto y Proveedor como columnas por fila.
- `Reporte por proveedor` agregó el nombre del Proveedor dentro de cada fila; el
  Producto ya estaba presente.
- Producto y Proveedor dejaron de repetirse como metadatos visibles fuera de las
  tablas. Los IDs se conservan en el JSON de auditoría.
- `ReportAuthorDto` quedó reducido a correo y rol. PDF y Excel ya no imprimen
  nombre ni apellido del empleado; Excel ahora muestra explícitamente ambos
  metadatos compartidos (`Correo usuario` y `Rol`).
- Se actualizaron los productores de reportes de Reportería, Bitácora y Pesaje,
  además de las pruebas de estrategias.

### Archivos adicionales modificados

- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaViewModel.cs`
- `CapaUI/Formularios/Principal/Pantallas/Bitacora/BitacoraViewModel.cs`
- `CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeViewModel.cs`
- `CapaAplicacion4/Reportes/Dtos/ReportAuthorDto.cs`
- `CapaDatos/Reportes/PdfReportStrategy.cs`
- `CapaDatos/Reportes/ExcelReportStrategy.cs`
- `BimboProyecto.Tests/Reportes/ReportStrategyTests.cs`

### Validación adicional

- `dotnet build BimboProyecto.sln --no-incremental` — 0 errores y 53
  advertencias preexistentes.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build` — 113
  pruebas superadas, 0 fallidas y 0 omitidas.
- Sigue pendiente abrir una vista previa y archivos PDF/Excel con datos reales
  para validar visualmente el ancho de las nuevas columnas.

## Corrección posterior — Metadatos y fechas de entrada de materia prima

La interpretación anterior se corrigió porque `Entrada de materia prima`
representa un producto y un proveedor seleccionados. Esos nombres, junto con las
placas y los pesadores únicos del resultado, volvieron a mostrarse fuera de la
tabla en la vista previa y en los archivos exportados.

### Cambios

- La tabla quedó limitada a fecha/hora, peso bruto, peso tara y peso neto.
- Producto, proveedor, placas y pesadores se presentan como metadatos; los
  valores múltiples de placa o pesador se consolidan sin duplicados.
- Se habilitaron `Desde` y `Hasta` para este reporte. Ambos son obligatorios, el
  rango es inclusivo y `Hasta` no puede superar la fecha actual.
- `EntradaMateriaPrimaFiltro` transporta el rango y el repositorio lo aplica
  sobre las filas ya restringidas por producto, proveedor y autorización de la
  RPC existente.
- La bitácora del reporte registra ambas fechas y únicamente las columnas que
  realmente componen la tabla exportada.

### Supabase

Se verificó la RPC remota vigente en modo lectura. No se alteró su firma: la
documentación oficial desaconseja sobrecargas y el conector disponible rechazó
la creación de una función nueva por operar en una transacción de solo lectura.
El filtrado quedó resuelto localmente sin romper consumidores de la RPC actual.

### Validación

- `dotnet build BimboProyecto.sln --no-restore` — 0 errores y 53 advertencias
  preexistentes.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-restore` —
  113 pruebas superadas, 0 fallidas y 0 omitidas.
- Pendiente: prueba visual autenticada con múltiples placas/pesadores y revisión
  de los PDF/Excel resultantes.

### Corrección de enlace WPF

La apertura de Reportería reveló que `DatePicker.DisplayDateEnd` intentaba usar
el modo bidireccional heredado y escribir sobre la propiedad calculada
`FechaMaxima`. Los dos enlaces se fijaron explícitamente como `Mode=OneWay`,
eliminando la excepción durante `Loaded` sin convertir la fecha máxima en estado
mutable.

### Columnas dinámicas y rango en el pie

`Entrada de materia prima` ahora evalúa los valores únicos de placa y pesador.
Cada dato permanece en los metadatos superiores cuando es único; si aparece más
de uno, se convierte en columna de la tabla para conservar su correspondencia
por pesaje. Los valores vacíos se normalizan como `—` y participan en la regla
de multiplicidad.

Las fechas `Desde` y `Hasta` dejaron de formar parte del encabezado. El nuevo
`FooterMetadata` de `TabularReportDto` las presenta horizontalmente sobre la
paginación de la vista previa y después de tabla/totales en PDF y Excel. La
lista de columnas almacenada en `ParametrosJson` también se construye según las
columnas dinámicas realmente exportadas.

## Relaciones

- [[Módulo Reportería]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[Selector de Catálogo - Selector genérico y multiselección]]
