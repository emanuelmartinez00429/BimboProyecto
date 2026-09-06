---
title: Bitácora Archivada
tags: [bitácora, archivo, referencia]
---

# 📦 Bitácora Archivada

Este directorio contiene sesiones técnicas que:

✅ **Ya fueron implementadas** y no volverán a cambiar  
✅ **Están documentadas** en `Arquitectura Actual.md` o en módulos específicos (`Módulo Productos.md`, etc.)  
✅ **No impactan decisiones presentes** — sirven como referencia histórica y análisis de por qué se hicieron cosas

## ¿Por qué archivar?

- 🎯 **Reducir contexto:** El agente re-lee menos contenido en cada sesión
- 📚 **Mantener enfoque:** `70 - Bitácora` en nivel padre concentra solo decisiones **activas**
- 🔍 **Recuperable:** Búsqueda en Obsidian (`path:archivo/ + término`) recupera cualquier sesión

## ¿Cómo recuperar una sesión archivada?

Búsqueda en Obsidian:

```
path:archivo/ "ComboBox Fabricante"
```

O navega: `contexto/70 - Bitácora de Cambios/archivo/2026-05-Archivadas/`

---

## 📋 Índice de lo archivado

### 2026-05 (25 sesiones)

**Arquitectura Core archivada (documentada en Arquitectura Actual.md):**
- ✅ Sesión 2026-05-21 - Análisis Segunda Opinión TryAsync
- ✅ Sesión 2026-05-21 - Implementación RepositorioBase + TryAsync → **Ver `Arquitectura Actual` sección "Result Pattern"**
- ✅ Sesión 2026-05-21 - Refactor Fases 4-7
- ✅ Sesión 2026-05-22 - Refactor Arquitectural Fase 1 & 2 → **Ver `Arquitectura Actual` sección "CapaServicios disuelto"**
- ✅ Sesión 2026-05-24 - Implementación Gestor Realtime Completa → **Ver [[Gestor Realtime - Diseño Arquitectónico]]**
- ✅ Sesión 2026-05-29 - Eliminación BimboPesaje y Finalización C13-C14 → **Ver `Arquitectura Actual` sección "Proyectos eliminados"**
- ✅ Sesión 2026-05-29 - Módulos Proveedores Fabricantes y Disolución CapaServicios → **Ver `Arquitectura Actual`**
- ✅ Sesión 2026-05-29 - SuggestionSearchBox Compartido y Fix 5 Bugs → **Ver `Arquitectura Actual` sección "SuggestionSearchBox compartido"**

**Supersiciones encontradas — NO USAR (reemplazadas por julio):**
- ⚠️ Sesión 2026-05-22 - Refactor Login y QA Fix Perfil → **SUPERSEDIDA por 2026-07-23**
- ⚠️ Sesión 2026-05-23 - Refactor Navegación Fase 1 → **SUPERSEDIDA por 2026-07-23**
- ⚠️ Sesión 2026-05-23 - INavigationService eliminado + IPickerService → **SUPERSEDIDA por 2026-07-23**

**Fixes, optimizaciones y features tácticas (ya completadas):**
- Sesión 2026-05-22 - Interceptar Cierre de Ventana
- Sesión 2026-05-22 - Recuperación de Contraseña
- Sesión 2026-05-23 - Fix LimpiarFiltros Productos
- Sesión 2026-05-24 - Fix Realtime ExtraerCambio
- Sesión 2026-05-24 - Implementación Gestor Realtime (v1)
- Sesión 2026-05-26 - ComboBox Fabricante Buscable
- Sesión 2026-05-26 - Optimización Animación Sidebar
- Sesión 2026-05-28 - Auditoría Deuda Técnica y Fix Memory Leak
- Sesión 2026-05-28 - Deuda Técnica P006-P008
- Sesión 2026-05-28 - Eliminación Memory Leaks Ciclo Completo
- Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos
- Sesión 2026-05-28 - Fix Realtime ConnectAsync
- Sesión 2026-05-28 - Refactor Críticos P001-P003
- Sesión 2026-05-28 - Refactor P004-P005
- Sesión 2026-05-29 - Auditoría Profunda y Plan de Remediación
- Sesión 2026-05-29 - Fix Fuga de Timers Realtime
- Sesión 2026-06-15 - GhostTextBox Autocompletado Dominio Login

### 2026-06 (0 sesiones)

Nada archivado — todas las sesiones de junio tienen pendientes o son decisiones activas.

### 2026-07 (7 sesiones)

**Fixes y refactores tácticos:**
- Sesión 2026-07-26 - Centralización Estilos ComboBox y TextBox
- Sesión 2026-07-26 - Fix Buscador Usuarios
- Sesión 2026-07-26 - Refactor Flujo Creación de Usuario desde Empleados
- Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021
- Sesión 2026-07-26 - Revisión QA Centralización Estilos (opencode)
- Sesión 2026-07-28 - Fix Buscador Global (PostgrestException PGRST100)
- Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias
- Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)
- Sesión 2026-07-23 - Limpieza de Huérfanos y Coloreado del Grafo

---

## ⚠️ Decisiones arquitectónicas vigentes (no archivadas)

**Sesiones ACTIVAS en `70 - Bitácora de Cambios/`** (no en archivo/):

- 🔴 **2026-06-21 — Soporte Multi-Resolución y DPI Per-Monitor V2** (PENDIENTE: prueba visual)
- 🔴 **2026-06-25 — Optimización DropShadowEffect** (PENDIENTE: prueba en otra PC)
- 🔴 **2026-07-01 — Pesaje Fase 2 — Persistencia Real** (PENDIENTE: e2e con login)
- 🔴 **2026-07-23 — Reconciliación Sidebar** (PENDIENTE: prueba visual)
- 🔴 **2026-07-26 — Rediseño Flujo Pesajes** (PENDIENTE: verificación visual)
- 🟢 **2026-07-23 — Plan Preparar Bóveda Multi-Agente** (completado)
- 🟢 **2026-07-23 — Sesión y permisos refactorizados** (commit f105047, Elena)
- 🟢 **2026-07-26 — Investigación Motor de Reportes** (decisión pendiente Fase 9)
- 🟢 **2026-07-26 — Migración ADRs y Módulo Usuarios** (completado)
- 🟢 **2026-06-21 — Módulos Contactos** (completado)

---

## 📚 Notas finales

- La mayoría de las sesiones archivadas están **resumidas en `Arquitectura Actual.md`**
- Las que contienen patrones reutilizables (paginación, realtime, etc.) también existen en **nodos MOC específicos**
- **Nunca elimines** una sesión archivada — el archivo es recuperable siempre

---

*Última actualización: 2026-09-06*
