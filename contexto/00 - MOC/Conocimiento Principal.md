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
- [[Deuda Técnica - Pendientes]] — ítems abiertos
- [[Checklist - Replicar Módulo con Realtime]] — guía paso a paso para nuevos módulos
- [[CLAUDE]] — contexto completo para Claude Code

### Decisiones arquitecturales
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

### Módulos documentados
- [[Módulo Productos]] — patrón de referencia para todos los demás
- [[Módulo Usuarios]] — CRUD + autenticación + permisos desde BD
- [[Módulo Empleados]] — CRUD completo, crea usuario desde empleado
- [[Módulo Configuración de Empresa]] — singleton de empresa, logo en Storage y tema dinámico
- [[Módulo Reportería]] — cuatro consultas operativas con vista previa y exportación PDF/Excel auditada
- [[Buscador Universal Bimbo]] — Strategy + Mediator en acción

### Diseño Bimbo-específico
- [[Gestor Realtime - Diseño Arquitectónico]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]

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
Sesiones más recientes (2026-08-15):
- [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]]
- [[Sesión 2026-08-15 - Normalizacion de modales y advertencia al inactivar]]
- [[Sesión 2026-08-15 - UNIQUE en presentaciones y limpieza de MaxLineas]]
- [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]]
- [[Sesión 2026-08-15 - Icono dinámico del sidebar]]
- [[Sesión 2026-08-15 - Protección contra autoadministración de usuarios]]
