---
title: "Sesión 2026-11-09 — Limpieza de repositorio, desmantelamiento de artefactos huérfanos y reubicación de specs"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-3.8-flash-high
rama: chore/limpieza-repo
commits: [39c5a83]
  - b748059
tags:
  - limpieza
  - refactor
  - devsecops
  - gobernanza
summary: "Limpieza del repo (R8): retiro de SearchTest y odd, reubicación de specs a Referencia y ServicioConexión a .gitignore."
deuda: []
adr: []
---

# Sesión 2026-11-09 — Limpieza de repositorio, desmantelamiento de artefactos huérfanos y reubicación de specs

## Problema o motivo

El repositorio acumulaba código huérfano, carpetas residuales y archivos fuera de la taxonomía del vault de contexto (Requisito R8):
1. `SearchTest/`: consola de prueba histórica de mayo 2026 ajena a la solución `BimboProyecto.sln`, que mantenía un `App.config` redundante.
2. `odd/`: carpeta de brief de tareas pasadas ya cerradas en septiembre 2026 (`odd/tasks/mi-usuario-apodo-saludo.md`).
3. `specs/investigacion-escalado-y-preferencias.md`: investigación técnica huérfana en una carpeta no estándar `specs/`.
4. `ServicioConexión/`: servicio disuelto en septiembre 2026 (P-056) cuyos residuos de compilación `bin/` y `obj/` no estaban explícitamente ignorados.

## Cambios

1. **Retiro de `SearchTest/`:**
   - Eliminados `App.config`, `Program.cs` y `SearchTest.csproj`.
   - Reduce superficie de ataque y elimina copias duplicadas de configuración.

2. **Retiro de `odd/`:**
   - Eliminado `odd/tasks/mi-usuario-apodo-saludo.md`.
   - Se suprime la carpeta huérfana del repositorio.

3. **Reubicación de `specs/investigacion-escalado-y-preferencias.md`:**
   - Trasladado a `contexto/50 - Referencia/investigacion-escalado-y-preferencias.md`.
   - Frontmatter normalizado con `type: investigacion`, `autor: fernando`, `status: vigente`, conservando el cuerpo íntegro y la sección de relaciones.
   - Eliminada la carpeta `specs/`.

4. **Actualización de `.gitignore`:**
   - Incorporado `ServicioConexión/` en la sección de exclusiones para asegurar que nunca se versionen artefactos residuales de compilación.

## Verificación

- `node scripts/contexto/build-index.js`: índices regenerados exitosamente.
- `node scripts/contexto/verificar.js`: 0 errores en 314 notas analizadas.
- `node --test scripts/contexto/`: 7 pruebas unitarias aprobadas en verde.
- `dotnet build BimboProyecto.sln -c Release`: 0 errores y 0 advertencias.
- `dotnet test BimboProyecto.sln -c Release`: 735 pruebas aprobadas, 3 omitidas.
- `actionlint`: 0 errores en todos los workflows.
- `gitleaks detect --redact`: 0 hallazgos en todo el historial.

## Lo que no cambió

- No se modificaron los proyectos productivos de la solución (`CapaUI`, `CapaDatos`, `CapaDominio`, `CapaAplicacion4`).
- No se tocaron `analisis-muerto/`, ramas ajenas ni stashes de otros desarrolladores.

## Pendientes

- Ninguno. La limpieza de artefactos huérfanos queda concluida.

## Relaciones

- [[AGENTS]]
- [[E-01 - Estándar DevSecOps]]
- [[Deuda - Resuelta]]
