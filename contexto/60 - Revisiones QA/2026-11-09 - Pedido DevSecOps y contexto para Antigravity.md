---
title: "2026-11-09 — Pedido DevSecOps y contexto para Antigravity (teamwork)"
type: qa
status: vigente
date: 2026-11-09
updated: 2026-11-09
summary: "Revisión QA y pedido DevSecOps y contexto para Antigravity siguiendo el estándar de FOE 360, CITMec y Análogos"
autor: fernando
agente: claude-code
estado: cerrado
tags: [qa, devsecops, contexto, antigravity, teamwork]
---

# Pedido DevSecOps y contexto para Antigravity (teamwork)

Claude (orquestador) le pasa a Antigravity con `/teamwork-preview` el trabajo de llevar a Bimbo el estándar de FOE 360 Móvil, CITMec y Análogos.

- **Brief:** `TEAMWORK-devsecops-BRIEF.md` (R1–R8, prohibiciones, criterios de aceptación).
- **Respuestas a la entrevista:** `TEAMWORK-devsecops-RESPUESTAS.md`.
- **Plan aprobado:** contrato único de agentes, validador e índice generado, dieta del contexto de arranque, archivo de auditoría en un Supabase aparte, CI/CD con gitleaks/SBOM/aprobación, estándar de seguridad para personas e IA, graphify y limpieza.

## Decisiones de Fernando (2026-11-09)

- Las sesiones de un mes cerrado hace más de 60 días salen del repo y van a la base `bimbo-auditoria-dev`; queda un resumen mensual.
- La base de auditoría es un proyecto Supabase aparte, no el de producción. Todavía no existe: el 2026-11-09 no hubo cupo Free (Fernando, Emanuel y sanchezcarlosvalle-arch están en el tope de 2 proyectos activos) y Fernando la crea más adelante.
- Cada sesión declara su agente; no hay agente fijo por persona.
- Se propone el ADR-030 (código privado + repo público de binarios); lo decide Emanuel.

## Hallazgos que motivaron el pedido

- El repo `emanuelmartinez00429/BimboProyecto` es público y `main` no tiene protección; la rama por defecto es `master`.
- `publish-velopack-release.yml` publica en cada push a `main` sin esperar a que pase el CI, sin aprobación y sin firma.
- Las pruebas de integración se saltan en silencio y dan verde.
- El contexto de arranque son unos 65k tokens; `Deuda Técnica - Pendientes.md` es el 55 %.
- `autor_cambios` mezcla persona, agente y modelo, con unas 30 variantes.
- El "1.1 anon key fuera del repo ✅" del Roadmap 10-10 es falso.

## Relaciones

- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[Plan de Seguridad - Roadmap 10-10]]
