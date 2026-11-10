<!-- GENERADO por scripts/contexto/build-index.js — no editar a mano -->
# Índice del contexto

Una línea por nota: `tipo · estado · actualizado` — resumen.
Lee este índice y abre solo las 3 a 6 notas necesarias para la tarea.
De bitácora, solo las 3 sesiones más recientes; el resto en contexto/INDEX-bitacora.md.

## 00 - MOC — Mapas de contenido y dashboard

- [[Conocimiento Principal]] · moc · vigente

## 10 - Arquitectura — Directrices y conceptos arquitectónicos

- [[Capa de Dominio]] · arquitectura · vigente
- [[Clean Architecture]] · arquitectura · vigente
- [[SOLID]] · arquitectura · vigente

## 20 - Patrones — Patrones de código y UI reutilizables

- [[Anatomia compartida de los modales]] · patron · vigente
- [[Animaciones WPF - Referencia de Easings]] · patron · vigente
- [[Base Repository con TryAsync]] · patron · vigente
- [[Buscador y Barra de Herramientas Responsive - Proteccion de Ancho con AnchoMinimoAVisibilidad]] · patron · vigente
- [[CQRS + Mediator]] · patron · vigente
- [[Columna de Numero de Fila en DataGrid]] · patron · vigente
- [[Columnas de Auditoria Temporal en DataGrid - Estandarizacion Creado y Actualizado]] · patron · vigente
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]] · patron · vigente
- [[Convenciones de código]] · patron · vigente · 2026-11-09 — Convenciones de arquitectura, MVVM, repositorios Supabase...
- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]] · patron · vigente
- [[Guardado sin Refetch - Aplicar en memoria la respuesta del servidor]] · patron · vigente
- [[Interceptar Cierre de Ventana]] · patron · vigente
- [[Observer Pattern]] · patron · vigente
- [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]] · patron · vigente
- [[Recuperacion de Contrasenia con Supabase OTP]] · patron · vigente
- [[Repository Pattern]] · patron · vigente
- [[Result Pattern]] · patron · vigente
- [[Selector de Catálogo - Selector genérico y multiselección]] · patron · vigente
- [[Strategy Pattern]] · patron · vigente
- [[Validacion de formularios]] · patron · vigente
- [[Vista Descargada Durante un await (async void Loaded)]] · patron · vigente

## 30 - Casos de Uso — Casos de uso y flujos operativos

- [[Caso 01 - CRUD con Paginación]] · caso-uso · vigente
- [[Caso 02 - Buscador Universal]] · caso-uso · vigente
- [[Caso 03 - ERP Inventario]] · caso-uso · vigente
- [[Caso 04 - WPF MVVM Clean Architecture]] · caso-uso · vigente
- [[Caso 05 - CQRS Gestión Empleados]] · caso-uso · vigente

## 40 - Proyecto Bimbo — Estado vivo: módulos, arquitectura y deuda abierta

- [[Arquitectura Actual]] · doc · vigente
- [[Buscador Universal Bimbo]] · doc · vigente
- [[Checklist - Replicar Módulo con Realtime]] · doc · vigente
- [[Deuda Técnica - Pendientes]] · doc · vigente
- [[Gestor Realtime - Diseño Arquitectónico]] · doc · vigente
- [[Módulo Bitácora]] · doc · vigente
- [[Módulo Configuración de Empresa]] · doc · vigente
- [[Módulo Contactos (Drill-down)]] · doc · vigente
- [[Módulo Dashboard]] · doc · vigente
- [[Módulo Empleados]] · doc · vigente
- [[Módulo Notificaciones]] · doc · vigente
- [[Módulo Pesaje]] · doc · vigente
- [[Módulo Productos]] · doc · vigente
- [[Módulo Reportería]] · doc · vigente
- [[Módulo Usuarios]] · doc · vigente
- [[Módulos de Catálogos Administrativos]] · doc · vigente
- [[Paginación y Búsqueda - Arquitectura Detallada]] · doc · vigente
- [[Plan Fase 9 - Subsistema de Reportes]] · doc · vigente
- [[Plan Offline-First de Pesaje]] · doc · propuesto
- [[Plan de Auditoría SBOM]] · doc · vigente
- [[Plan de CI-CD y Actualizaciones Remotas]] · doc · vigente
- [[Plan de Migración de Mutaciones Directas a RPC]] · doc · propuesto
- [[Plan de Migración de Presentaciones a RPC segura]] · doc · vigente
- [[Plan de Seguridad - Roadmap 10-10]] · doc · vigente
- [[Plan de Tests Unitarios]] · doc · vigente

## 45 - Decisiones — Registros de decisión de arquitectura (ADR)

- [[ADR-001 - Result Pattern en Repositorios]] · adr · vigente
- [[ADR-002 - CQRS y Strategy para Buscador Universal]] · adr · vigente
- [[ADR-003 - Disolución de CapaServicios]] · adr · vigente
- [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]] · adr · vigente
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] · adr · vigente
- [[ADR-006 - Motor de Reportes y Exportación]] · adr · vigente
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]] · adr · vigente
- [[ADR-008 - Cliente Temporal para SignUp de Usuarios]] · adr · vigente
- [[ADR-009 - RPC crear_usuario_empleado_seguro para vinculacion auth-empleado]] · adr · vigente
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] · adr · vigente
- [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]] · adr · vigente
- [[ADR-012 - Paginacion server-side con timeout y generacion counter]] · adr · vigente
- [[ADR-013 - Eliminacion de SesionActual y servicioSesionActual legacy]] · adr · vigente
- [[ADR-014 - Precarga unica y cache del catalogo RBAC]] · adr · vigente
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] · adr · vigente
- [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]] · adr · vigente
- [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]] · adr · vigente
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] · adr · vigente
- [[ADR-019 - Configuración de empresa y tema dinámico global]] · adr · vigente
- [[ADR-020 - Defensa en profundidad contra autoadministracion de usuarios]] · adr · vigente
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] · adr · vigente
- [[ADR-022 - Persistencia local-first con SQLCipher y sincronización por outbox]] · adr · propuesto
- [[ADR-024 - Rol Administrador inmutable con acceso total]] · adr · vigente
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]] · adr · vigente
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] · adr · propuesto
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] · adr · vigente
- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] · adr · vigente
- [[ADR-029 - Recepciones de pesaje planas con reglas en trigger de tabla]] · adr · vigente
- [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]] · adr · vigente
_(1 nota retirada: reemplazadas o archivadas, no se listan)_

## 50 - Referencia — Hechos externos, SDKs, APIs y configuración

- [[Analisis de rendimiento - Buscadores, cache y paginacion (2026-09)]] · referencia · vigente
- [[Bug - CREATE OR REPLACE FUNCTION con distinta cantidad de parametros duplica en vez de reemplazar (Postgres)]] · referencia · vigente
- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] · referencia · vigente
- [[CommunityToolkit.Mvvm]] · referencia · vigente
- [[Convenciones C#]] · referencia · vigente
- [[Detector-de-Conexion]] · referencia · vigente
- [[Guia Maestra de Comandos Antigravity]] · referencia · vigente
- [[Licencias de Librerías .NET - Auditoría 2026-07]] · referencia · vigente
- [[MCP Obsidian - Configuracion Completa]] · referencia · vigente
- [[NET - Normalizacion Unicode para Quitar Acentos]] · referencia · vigente
- [[PDFsharp MigraDoc - Referencia]] · referencia · vigente
- [[Supabase - Vistas SQL, RLS y security_invoker]] · referencia · vigente
- [[Supabase .NET]] · referencia · vigente
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]] · referencia · vigente
- [[WPF - Bucle de Layout por Medir en ArrangeOverride]] · referencia · vigente
- [[WPF - DPI Awareness y Escalado Multi-Resolución]] · referencia · vigente
- [[WPF - Escalar Popups y ToolTips que no heredan el LayoutTransform]] · referencia · vigente
- [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] · referencia · vigente
- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] · referencia · vigente
- [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]] · referencia · vigente
- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] · referencia · vigente
- [[WPF - Texto Dentado en Fondos de Color]] · referencia · vigente

## 60 - Revisiones QA — Auditorías de calidad y revisiones entre agentes

- [[2026-11-09 - Pedido DevSecOps y contexto para Antigravity]] · qa · vigente · 2026-11-09 — Revisión QA y pedido DevSecOps y contexto para Antigravit...
- [[Auditoría Externa — Optimizaciones WPF de Antigravity vs. Investigaciones QA]] · qa · vigente
- [[Auditoría Técnica — Animaciones DWM, Maximizado Multi-DPI y Seguridad OWASP]] · qa · vigente
- [[Fase 4 — Puertas de riesgo del escalado (guion de prueba)]] · qa · vigente
- [[Fase 7 — Matriz de prueba visual del escalado]] · qa · vigente
- [[Informe de Optimización de DataGrids y Bug de Salto de Columnas]] · qa · vigente
- [[Inventario — Superficie de escalado propio (LayoutTransform global)]] · qa · vigente
- [[Plan de Mejora - Módulo Productos (Revisión QA)]] · qa · vigente
- [[QA - Configuración de Empresa (Caché, Vista y Realtime)]] · qa · vigente
- [[QA - Inicio de Sesión (Testing por niveles de importancia)]] · qa · vigente

## 70 - Bitácora de Cambios — Sesiones de trabajo por mes y resúmenes

- [[Sesión 2026-11-08 - Resolución P-067 (Pkcs 10.0.14)]] · sesion · emanuel · opencode · 2026-11-08 — La resolución se hizo justo antes de publicar la próxima ...
- [[Sesión 2026-11-08 - Fix reporte de productos y actualización remota]] · sesion · emanuel · codex · 2026-11-08 — - La búsqueda de actualización no fue inmediata; el usuar...
- [[Sesión 2026-11-07 - Merge y MSI de actualizaciones Velopack]] · sesion · emanuel · codex · 2026-11-07 — - Validar el MSI con el compañero y obtener el texto/códi...
- _(3 sesiones más: ver contexto/INDEX-bitacora.md)_
