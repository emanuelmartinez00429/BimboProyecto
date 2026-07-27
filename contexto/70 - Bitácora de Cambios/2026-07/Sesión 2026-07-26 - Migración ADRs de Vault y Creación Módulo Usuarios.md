---
title: "Sesión 2026-07-26 - Migración ADRs de Vault y Creación Módulo Usuarios"
tags: [sesion, bimbo, gestion-usuarios, adr, migracion]
date: 2026-07-26
branch: main
autor_cambios: "Emanuel (warthunderlover), dirigido por opencode"
---

# Sesión 2026-07-26 - Migración ADRs de Vault y Creación Módulo Usuarios

## Resultado

Se migraron 7 Architecture Decision Records (ADRs) del vault personal (`BimboPesaje/Decisiones/`) al repo compartido (`contexto/45 - Decisiones/`), y se documentó el gap del Módulo Usuarios.

## Problema

El vault personal contenía 7 ADRs muy detallados sobre el módulo de gestión de usuarios que no existían en el repo compartido de contexto. Estos cubrían decisiones críticas de arquitectura: sesión, auth, permisos, RPC, paginación, y limpieza de legacy. Sin estas notas, cualquier agente nuevo entrando al proyecto no tendría visibilidad sobre el "por qué" detrás de estas decisiones.

## Cambios realizados

### Archivos creados (7 ADRs en `45 - Decisiones/`)

| Archivo | Tema |
|---------|------|
| `ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico.md` | Reemplazo de estáticos por DI Singleton |
| `ADR-008 - Cliente Temporal para SignUp de Usuarios.md` | Preservar sesión admin al crear usuario |
| `ADR-009 - RPC crear_usuario_empleado_seguro para vinculacion auth-empleado.md` | Vinculación atómica auth-empleado |
| `ADR-010 - Permisos desde BD en vez de switch hardcodeado.md` | Carga de permisos desde tablas BD |
| `ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML.md` | Puente DI ↔ XAML bindings |
| `ADR-012 - Paginacion server-side con timeout y generacion counter.md` | Protección contra race conditions |
| `ADR-013 - Eliminacion de SesionActual y servicioSesionActual legacy.md` | Limpieza de código muerto |

### Archivos modificados

- `00 - MOC/Conocimiento Principal.md` — agregadas filas para ADR-007 a ADR-013 en la tabla de "Decisiones arquitecturales"

### Formato aplicado

- Frontmatter con `tags: [adr, decision, bimbo, gestion-usuarios, ...]`, `date`, `estado: aceptado`
- Secciones: Contexto, Opciones consideradas (tabla), Decisión tomada, Por qué, Consecuencias/Trade-offs, Relaciones
- `## Relaciones` al final de cada ADR con wikilinks cruzados

## Verificación

- Todos los archivos existen y son legibles
- Wikilinks apuntan a notas existentes (`[[Módulo Usuarios]]`, `[[ADR-007]]`, etc.)
- `Conocimiento Principal.md` tiene las 7 entradas nuevas en la tabla de decisiones

## Lo que no cambió

- No se modificó el contenido semántico de los ADRs (solo formato y frontmatter)
- No se tocaron los archivos originales del vault personal
- No se creó `Módulo Usuarios.md` (pendiente para sesión separada)

## Relaciones

- [[Módulo Usuarios]] — módulo documentado por estos ADRs
- [[Arquitectura Actual]] — estado del proyecto que estos ADRs describen
- [[ADR-007]] a [[ADR-013]] — los 7 ADRs migrados
- [[Conocimiento Principal]] — MOC actualizado con nuevos links
