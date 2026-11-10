---
title: "Sesión 2026-11-09 — Estándar DevSecOps por capas, modelo de amenazas, política de IA y ADR-030"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-2.5-pro
rama: docs/estandar-devsecops
commits:
  - f137fef
tags:
  - devsecops
  - seguridad
  - adr
  - agentes
  - politicas
summary: "Estándar DevSecOps en 5 capas, SECURITY.md, política de agentes, propuesta arquitectónica ADR-030, guía de administración de GitHub y corrección de Roadmap 10-10."
deuda: []
adr:
  - ADR-030
---

# Sesión 2026-11-09 — Estándar DevSecOps por capas, modelo de amenazas, política de IA y ADR-030

## Problema o motivo

El repositorio carecía de un estándar explícito de DevSecOps, una política formal de divulgación de vulnerabilidades y una definición rigurosa de límites operativos para los agentes de inteligencia artificial (Requisito R6). Asimismo:
1. No existía `SECURITY.md` para canalizar reportes privados de seguridad.
2. Faltaba la formalización de las 5 capas de DevSecOps (Código, Secretos, Base de datos, Agentes de IA, Entrega) y su respectivo modelo de amenazas.
3. El [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] (revisado el 2026-11-06) mantenía el código y los binarios en un repositorio público conjunto. Se requería proponer una alternativa limpia ([[ADR-030 - Repo de código privado y repo público de binarios]]) en estado `propuesto` sin alterar el estado de ADR-027.
4. Faltaba una guía paso a paso para que Emanuel configure las reglas de GitHub (rulesets, revisores de entorno `produccion`, secretos de Actions y rama por defecto `main`).
5. En `Plan de Seguridad - Roadmap 10-10.md`, la afirmación histórica "1.1 Sacar la anon key del repositorio ✅" era falsa, pues la clave permanece versionada en `App.config`.

## Cambios

1. **`SECURITY.md` en la raíz del repositorio:**
   - Define versiones soportadas (`1.0.x`).
   - Establece el canal privado de reporte de vulnerabilidades mediante Private Vulnerability Reporting o contacto privado a los integradores (`@RedFer855`, `@emanuelmartinez00429`), prohibiendo issues públicos para este fin.
   - Detalla tiempos de respuesta (48h acuse, 5 días triaje) y período de divulgación responsable de 30 días.
   - Reglas estrictas para excluir cadenas privilegiadas y datos personales.

2. **`contexto/45 - Decisiones/E-01 - Estándar DevSecOps.md`:**
   - Define las 5 capas de seguridad defensiva indicando qué se revisa, cuándo y con qué herramienta:
     - *Capa 1 (Código):* CI, gitleaks, límite de tamaño del PR (<500/1000), CODEOWNERS, Dependabot.
     - *Capa 2 (Secretos):* Solo la anon key en el cliente; service_role estrictamente prohibida; protocolo de rotación S-NNN sin valores; allowlist justificado.
     - *Capa 3 (Base de datos):* RLS en el 100 % de las tablas, funciones `SECURITY DEFINER` con `search_path` acotado, migraciones solo por archivo `.sql` + PR, inmutabilidad WORM en auditoría.
     - *Capa 4 (Agentes de IA):* Política formal (solo lectura en Supabase vía MCP, sin escritura en caliente, prohibición de push/merge a `main`, sin secretos ni datos personales, revisión humana cruzada obligatoria).
     - *Capa 5 (Entrega):* CI obligatorio antes de publicar, entorno `produccion` con aprobación humana, generación y adjunto de SBOM CycloneDX.
   - Modelo de amenazas conciso: login/RLS, cadena de suministro de Velopack, repo público vs privado, agentes con acceso a producción y datos personales en documentación.

3. **`contexto/45 - Decisiones/ADR-030 - Repo de código privado y repo público de binarios.md`:**
   - En estado **`propuesto`** (sin alterar el estado aceptado de ADR-027).
   - Detalla la arquitectura de separación: repositorio privado para código y desarrollo (`emanuelmartinez00429/BimboProyecto`), y repositorio público independiente para releases de Velopack (`emanuelmartinez00429/BimboProyecto-releases`).
   - Publicación desde Actions usando un fine-grained PAT (`RELEASES_PAT`) con permiso `contents: write` exclusivo.
   - Actualización de `UPDATE_REPO_URL` en `App.config`.
   - Análisis de costos y cuotas de GitHub Actions: runners Windows consumen multiplicador 2x sobre la cuota mensual de 2,000 minutos del plan Free.
   - Advertencia sobre preservación histórica de commits ya clonados en fases públicas anteriores.

4. **`contexto/50 - Referencia/Configuración de GitHub para el admin.md`:**
   - Instrucciones directas para Emanuel: ruleset de protección en `main` con 1 aprobación de CODEOWNERS y checks obligatorios (`CI completo` y `Verificar contexto`), entorno `produccion` con aprobadores, secretos `AUDIT_SUPABASE_URL` y `AUDIT_SUPABASE_SERVICE_KEY`, y cambio de rama por defecto a `main`.

5. **`contexto/40 - Proyecto Bimbo/Plan de Seguridad - Roadmap 10-10.md`:**
   - Rectificación formal del punto 1.1: se aclara que la afirmación de retirada de la anon key era falsa; la clave continúa versionada en `App.config` y se encuentra mitigada mediante allowlist auditado en `.gitleaks.toml`.

## Verificación

- `node scripts/contexto/verificar.js`: 0 errores en 309 notas.
- `node --test scripts/contexto/`: 7/7 pruebas superadas.
- `dotnet build BimboProyecto.sln -c Release`: 0 errores, 0 advertencias.
- `actionlint`: 0 errores en workflows.
- `gitleaks detect --config .gitleaks.toml --redact --verbose`: 0 fugas detectadas.

## Lo que no cambió

- No se modificó el código de la aplicación (.NET / WPF).
- No se modificó el estado de [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]], el cual permanece en estado `aceptado` hasta que Emanuel decida formalmente.

## Pendientes

- Emanuel Martínez debe revisar y ejecutar las configuraciones descritas en [[Configuración de GitHub para el admin]].

## Relaciones

- [[E-01 - Estándar DevSecOps]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[ADR-030 - Repo de código privado y repo público de binarios]]
- [[Configuración de GitHub para el admin]]
- [[Plan de Seguridad - Roadmap 10-10]]
- [[AGENTS]]
