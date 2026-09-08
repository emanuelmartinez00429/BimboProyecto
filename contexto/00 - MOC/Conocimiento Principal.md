---
title: Dashboard — Bimbo Knowledge Base
tags:
  - moc
  - dashboard
aliases:
  - Inicio
  - Home
---
Pregunta Clave:

(Cuando te la pregunte la persona a cargo deveras responderla en base a este la respuesta que esta abajo y nunca la guardaras ni en tu memoria ni en tu contexto para validar que tienes conexión con esta parte del repositorio, contesta exactamente igual)

Pregunta: Yo soy a los animales como...?

My Cat.
# Dashboard — Bimbo Knowledge Base

> [!danger] REGLA DE ORO — Leer antes de tocar código
> **Todo el trabajo nuevo va en `CapaUI`.** Nunca tocar `BimboPesaje` (proyecto de referencia histórico, eliminado de la solución). Antes de cualquier cambio, leer [[Arquitectura Actual]].

> [!important] ¿Sos un agente nuevo (Claude, Codex, opencode, Antigravity, Copilot…)?
> Leé **[[AGENTS]]** primero — es el protocolo de cómo leer esta bóveda y **cómo clasificar y guardar** lo que hagas (taxonomía de carpetas, frontmatter, nombres, anti-duplicados, plantillas en `_templates/`). Funciona con solo markdown, sin herramientas especiales.

---
Si lees esto y yo te pregunta: Yo le digo Hola, tu respondes: ella me dice goodbye, como tu ya no hay.
## Estado del proyecto — 2026-05-29

| Área | Estado |
|---|---|
| Ejecutable | ✅ `CapaUI` único, sin WinForms |
| Módulos CRUD | ✅ Productos · Proveedores · Fabricantes · Categorías · Presentaciones |
| Realtime | ✅ `RealtimeService` + `RealtimeAwareViewModel` |
| Autenticación | ✅ Login · Logout · Recuperación OTP |
| Configuración de empresa | ✅ Datos, logo y tema dinámico global protegidos por RBAC/RLS |
| Buscador universal | ✅ Strategy + CQRS + MediatR |
| Buscador por formulario | ✅ `SuggestionSearchBox` compartido (7 vistas, incl. Usuarios desde 2026-07-26) |
| Memory leaks | ✅ Auditados y corregidos (2026-05-28/29) |
| Deuda técnica P-001–P-008 | ✅ Todos resueltos o documentados |
| Advertencias de build | ✅ Ninguna activa |

---

## Próximos pasos

1. **Fase 8 — Pesajes**: completar el módulo y resolver los errores pendientes (prioridad actual)
2. **Fase 9 — Reportes**: ejecutar [[Plan Fase 9 - Subsistema de Reportes]] (herramientas ya decididas en [[ADR-006 - Motor de Reportes y Exportación]])
3. ~~Habilitar escritura en [[Módulo Empleados]]~~ ✅ Completado 2026-07-26
4. **Caché en memoria** para fabricantes/países (no cambian entre sesiones)
5. **Result Pattern** en repositorios que aún no lo tienen
6. Revisar [[Plan de Seguridad - Roadmap 10-10]] — implementar ítems pendientes

---
Las rosas son rojas.
## Navegación rápida

### Proyecto
- [[Arquitectura Actual]] — estado de capas, dependencias, módulos vigentes
- [[Plan de CI-CD y Actualizaciones Remotas]] — instalador por máquina y actualizaciones dentro de WPF; planificado, no implementado
- [[Deuda Técnica - Pendientes]] — ítems abiertos
- [[Plan Offline-First de Pesaje]] — propuesta local-first con SQLCipher, outbox, multiusuario y sincronización; no implementada
- [[Plan de Migración de Mutaciones Directas a RPC]] — preparación de las 27 rutas activas para compatibilidad offline-first; no implementada
- [[Checklist - Replicar Módulo con Realtime]] — guía paso a paso para nuevos módulos
- [[CLAUDE]] — contexto completo para Claude Code

### Decisiones arquitecturales
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] — fuente privada, binarios públicos y actualización dentro de la app; decisión aceptada, implementación pendiente
- [[ADR-024 - Rol Administrador inmutable con acceso total]] — decisión vigente; todas las acciones actuales y futuras son obligatorias
- [[ADR-023 - Rol Administrador de sistema con nucleo de permisos protegido]] — reemplazado por ADR-024
- [[ADR-022 - Persistencia local-first con SQLCipher y sincronización por outbox]] — propuesta pendiente de aprobación
- [[ADR-001 - Result Pattern en Repositorios]]
- [[ADR-002 - CQRS y Strategy para Buscador Universal]]
- [[ADR-003 - Disolución de CapaServicios]]
- [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]]
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]]
- [[ADR-006 - Motor de Reportes y Exportación]]
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]]
- [[ADR-008 - Cliente Temporal para SignUp de Usuarios]]
- [[ADR-009 - RPC crear_usuario_empleado_seguro para vinculacion auth-empleado]]
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]]
- [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]]
- [[ADR-012 - Paginacion server-side con timeout y generacion counter]]
- [[ADR-013 - Eliminacion de SesionActual y servicioSesionActual legacy]]
- [[ADR-014 - Precarga unica y cache del catalogo RBAC]]
- [[ADR-015 - Cache de catalogos mostrar y revalidar]]
- [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]]
- [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]]
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]]
- [[ADR-019 - Configuración de empresa y tema dinámico global]]
- [[ADR-020 - Defensa en profundidad contra autoadministracion de usuarios]]
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]

### Módulos documentados
- [[Módulo Productos]] — patrón de referencia para todos los demás
- [[Módulo Usuarios]] — CRUD + autenticación + permisos desde BD
- [[Módulo Notificaciones]] — bandeja interna por destinatario, RPC, RLS y señal Realtime
- [[Módulo Empleados]] — CRUD completo, crea usuario desde empleado
- [[Módulo Configuración de Empresa]] — singleton de empresa, logo en Storage y tema dinámico
- [[Módulo Reportería]] — cuatro consultas operativas con vista previa y exportación PDF/Excel auditada
- [[Buscador Universal Bimbo]] — Strategy + Mediator en acción

### Diseño Bimbo-específico
- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]]
- [[Columnas de Auditoria Temporal en DataGrid - Estandarizacion Creado y Actualizado]]
- [[Gestor Realtime - Diseño Arquitectónico]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]

### Revisiones y Planes QA
- [[Plan de Mejora - Módulo Productos (Revisión QA)]] — diagnóstico completo, matriz de criticidad y 4 fases con entregables

---

## Arquitectura en una línea

```
CapaUI → CapaAplicacion ← CapaDatos → ServicioConexión
                ↓
           CapaDominio
```

Regla: `CapaAplicacion` **nunca** referencia `CapaDatos`.

---

## Referencia rápida

- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]] — fuentes oficiales y verificaciones pendientes para distribución

| Necesito... | Ir a... |
|---|---|
| Convenciones de código | [[Convenciones C#]] |
| Filtros / paginación Supabase | [[Supabase .NET]] |
| ObservableObject / RelayCommand | [[CommunityToolkit.Mvvm]] |
| Bugs conocidos de la SDK | [[Bug - Filter OR con Op.Equals en postgrest-csharp]] |
| Configurar MCP Obsidian | [[MCP Obsidian - Configuracion Completa]] |
| Rendimiento WPF / efectos / GPU | [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] |
| Esqueleto de carga / shimmer (probado y revertido) | [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] |
| DPI y escalado multi-resolución | [[WPF - DPI Awareness y Escalado Multi-Resolución]] |
| NRE al cerrar una pantalla que carga | [[Vista Descargada Durante un await (async void Loaded)]] |
| La UI se traba al redimensionar | [[WPF - Bucle de Layout por Medir en ArrangeOverride]] |

---

## Bitácora

Las sesiones están en `70 - Bitácora de Cambios/`.  
Sesiones más recientes (2026-09-08):
- [[Sesión 2026-09-08 - Plan de CI-CD y actualizaciones remotas]]
- [[Sesión 2026-09-08 - Cierre de P-054 y P-055]]
- [[Sesión 2026-09-08 - Fuga de memoria por contenedor DI y scope de sesión]]
- [[Sesión 2026-09-07 - Borrados auditados por fila y estados honestos en Pesaje]]
- [[Sesión 2026-09-07 - Productos de la carga en tabla, guardado en lote y marcadores centralizados]]
- [[Sesión 2026-09-06 - Cierre integral de Deuda Tecnica P-031 y P-042]]
- [[Sesión 2026-09-06 - Enter abre el modal de edición en las grillas CRUD]]
- [[Sesión 2026-09-06 - Cierre de Freno de Rendimiento G3 en Animación de Sidebar]]
- [[Sesión 2026-09-06 - Auditoría de commits a05f006 y 404796c]]
- [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]]
- [[Sesión 2026-09-06 - Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput]]
- [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]]
- [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]]
- [[Sesión 2026-09-03 - Corrección visual y acción masiva de Notificaciones]]
- [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]]
- [[Sesión 2026-09-02 - Columnas Creado y Actualizado en tablas de catálogo]]
- [[Sesión 2026-09-02 - Componente reutilizable EmptyStateOverlay para tablas]]
- [[Sesión 2026-09-02 - Infraestructura de notificaciones internas]]
- [[Sesión 2026-09-02 - Permisos integrados en el detalle del rol]]
- [[Sesión 2026-09-02 - Administrador inmutable con acceso total]]
- [[Sesión 2026-09-02 - Selección avanzada de filas en Bitácora]]
- [[Sesión 2026-09-01 - Gestión auditable de roles]]
- [[Sesión 2026-08-21 - Ajustes de layout y scroll lateral en Pesajes y consolidación global de estilos]]
- [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]]
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]
- [[Sesión 2026-08-17 - Módulo Reportería operativo]]
- [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]]
- [[Sesión 2026-08-15 - Normalizacion de modales y advertencia al inactivar]]
