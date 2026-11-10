---
title: "Sesión AAAA-MM-DD — Título corto descriptivo"
type: sesion
status: vigente
date: AAAA-MM-DD
updated: AAAA-MM-DD
autor: alias           # tu id en contexto/.control/equipo.json (fernando | emanuel | carlos)
agente: claude-code    # claude-code | codex | antigravity | gemini-cli | copilot | cursor | opencode | humano
modelo: opcional       # por ejemplo claude-3-7-sonnet (opcional, omitir si no aplica)
rama: tipo/descripcion-corta
commits: []            # lista de hashes cortos [abc1234]
tags: [sesion]
summary: "Frase declarativa concisa que resume qué se construyó o corrigió y su resultado."
deuda: []              # lista de P-NNN si se creó o mitigó deuda técnica
adr: []                # lista de ADR-NNN si la sesión responde a una decisión arquitectónica
---

# Sesión AAAA-MM-DD — Título corto descriptivo

<!-- Guardar como contexto/70 - Bitácora de Cambios/AAAA-MM/AAAA-MM-DD-autor-titulo-corto.md -->

## Problema

Qué problema o necesidad motivó el trabajo de esta sesión.

## Cambios

- Componente o archivo → descripción del cambio realizado y su diseño.

## Verificación

Comandos ejecutados y resultados reales obtenidos:
- `dotnet build BimboProyecto.sln`: 0 errores y 0 advertencias.
- `node scripts/contexto/verificar.js`: 0 errores.
- Pruebas manuales o unitarias comprobadas.

## Lo que no cambió

Límites explícitos de la sesión y elementos dejados fuera de alcance a propósito.

## Pendientes

- Deuda técnica abierta o tareas siguientes por abordar.

## Relaciones

- [[Arquitectura Actual]]
- [[Deuda - Abierta]]
- [[Convenciones de código]]
