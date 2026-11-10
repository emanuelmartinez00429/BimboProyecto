---
title: "Sesión 2026-11-09 — Configuración de graphify para consulta local de AST"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-3.8-flash-high
rama: chore/graphify
commits: [6004b17]
  - 99dcd1e
tags:
  - graphify
  - ast
  - tooling
  - devsecops
summary: "Configuración de graphify para navegación AST C# y XAML sin LLM: .graphifyignore, nota de referencia y confirmación de gitignore y gitattributes."
deuda: []
adr: []
---

# Sesión 2026-11-09 — Configuración de graphify para consulta local de AST

## Problema o motivo

Para optimizar la navegación de código y la reducción del consumo de contexto de agentes de IA y colaboradores (Requisito R7), se requería configurar el soporte formal de `graphify` en el repositorio:
1. Definir un archivo `.graphifyignore` con exclusiones claras para evitar indexar artefactos temporales, generados, templates y metadatos de agentes.
2. Garantizar que la salida de indexación `graphify-out/` esté excluida del árbol de control de versiones en `.gitignore`.
3. Confirmar la configuración del merge driver `graphify-out/graph.json merge=graphify` en `.gitattributes` para evitar conflictos en ramas concurrentes.
4. Documentar el uso, arquitectura y gobernanza de la herramienta en una nota técnica en `contexto/50 - Referencia/graphify.md`.

## Cambios

1. **`.graphifyignore`:**
   - Creado en la raíz del repositorio.
   - Excluye carpetas binarias (`bin/`, `obj/`), análisis temporales (`analisis-muerto/`), artefactos de agentes (`.agents/`), control de contexto (`contexto/.control/`, `contexto/.obsidian/`), salida de indexación (`graphify-out/`), dependencias (`node_modules/`), índices regenerables (`contexto/INDEX*.md`, `contexto/_templates/`) y plantillas/apuntadores (`CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, `.github/pull_request_template.md`).

2. **`.gitignore` y `.gitattributes`:**
   - Se verificó y confirmó la presencia de `graphify-out/` en `.gitignore` (incorporado en el mapa de control).
   - Se verificó y confirmó la directiva `graphify-out/graph.json merge=graphify` en `.gitattributes`.

3. **`contexto/50 - Referencia/graphify.md`:**
   - Creada nota de referencia técnica que detalla la extracción determinista de AST para C# y XAML con cero costos de LLM (`GRAPHIFY_BACKEND=none`).
   - Describe los comandos esenciales (`graphify build`, `graphify query`, `graphify hook install`).
   - Refuerza la directiva obligatoria de `AGENTS.md` de consultar `graphify query` antes de leer código en frío.

## Verificación

- `node scripts/contexto/build-index.js`: índices regenerados exitosamente.
- `node scripts/contexto/verificar.js`: 0 errores en 313 notas analizadas.
- `dotnet build BimboProyecto.sln -c Release`: 0 errores y 0 advertencias.
- `dotnet test BimboProyecto.sln -c Release`: 735 pruebas aprobadas, 3 omitidas.

## Lo que no cambió

- No se alteró código de la aplicación en `CapaUI`, `CapaDatos`, `CapaDominio` ni `CapaAplicacion4`.
- No se subieron al árbol de Git índices locales ni archivos bajo `graphify-out/`.

## Pendientes

- Cada desarrollador o integrador puede opcionalmente ejecutar `graphify hook install` en su entorno local si desea la regeneración automática por hook de Git.

## Relaciones

- [[AGENTS]]
- [[E-01 - Estándar DevSecOps]]
- [[Convenciones de código]]
