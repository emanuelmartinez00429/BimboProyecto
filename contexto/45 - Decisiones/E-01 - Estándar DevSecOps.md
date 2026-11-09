---
title: "E-01 — Estándar DevSecOps"
type: estandar
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
summary: "Estándar DevSecOps por capas para BimboProyecto: código, secretos, base de datos, agentes de IA y entrega continua con modelo de amenazas."
tags:
  - devsecops
  - seguridad
  - rls
  - gitleaks
  - agentes
---

# E-01 — Estándar DevSecOps

> **Propósito:** Definir el modelo integral de seguridad en cada etapa del ciclo de vida del software en BimboProyecto (.NET 10, WPF y Supabase). Adapta el estándar de seguridad probado en CITMec y FOE 360 Móvil al entorno de escritorio y base de datos relacional de Bimbo Honduras.

---

## 1. DevSecOps por Capas

La seguridad se evalúa y aplica en cada fase de desarrollo e integración, nunca como un trámite posterior. El modelo se estructura en **5 capas defensivas**:

| Capa | Qué se revisa | Cuándo | Herramienta / Mecanismo |
| :--- | :--- | :--- | :--- |
| **1. Código** | Límite de tamaño de PR (~2000 recomendado, tope 2500), compilación con 0 advertencias, pruebas xUnit con skips explícitos, revisión de dependencias vulnerables y revisión humana obligatoria. | En cada Pull Request y push a `main`. | GitHub Actions (`ci.yml`), `dotnet build` (`TreatWarningsAsErrors=true`), `dotnet test`, `dotnet list package --vulnerable`, `.github/CODEOWNERS`, Dependabot. |
| **2. Secretos** | Exclusión de claves `service_role`, credenciales de base de datos o tokens privados. Únicamente la clave pública `anon` está permitida en cliente (`CapaUI/App.config`), debidamente protegida por RLS. | Pre-commit y escaneo de todo el historial en CI. | Gitleaks 8.30.1 (`.gitleaks.toml`), `verificar.js`. |
| **3. Base de Datos** | Row Level Security (RLS) habilitado en el 100 % de las tablas. Funciones almacenadas `SECURITY DEFINER` con `search_path` explícito y seguro. Modificaciones DDL y DML operadas exclusivamente por migraciones versionadas. | Revisión de cada archivo `.sql` en PR antes de aplicar a producción. | Revisión de código en PR, scripts en `supabase/migrations/` y `auditoria/migrations/`, trigger inmutable WORM en auditoría. |
| **4. Agentes de IA** | Límites operativos y de permisos para agentes (Claude Code, Antigravity, Codex, etc.): solo lectura en BD de producción, prohibición de push/merge a `main`, sin secretos ni datos personales en notas o prompts. | En cada sesión multiagente y en revisión de PR. | Política de Agentes en `AGENTS.md` y este estándar, validador `verificar.js`. |
| **5. Entrega** | CI en verde requerido antes de empaquetar, aprobación humana obligatoria en entorno `produccion`, generación y adjunto de SBOM CycloneDX a cada release, y verificación de integridad. | En cada publicación de release hacia clientes de báscula. | GitHub Actions (`publish-velopack-release.yml`), GitHub Environments, CycloneDX CLI, Velopack (`vpk`). |

---

## 2. Detalle de las Capas

### Capa 1: Seguridad del Código y Dependencias
- **Tamaño de PRs:** Los Pull Requests deben ser pequeños y enfocados (objetivo: ~2000 líneas de código; tope estricto: 2500, igual que CITMec desde 2026-10-25). PRs de más de 2500 líneas fallan el CI automáticamente en el job `cambios`. No cuentan `contexto/`, `*.md`, lockfiles ni migraciones. Un apilado de PR se fusiona en orden con merge commit (no squash), para que cada PR mida solo su propio cambio.
- **Cero Advertencias:** Compilación en modo Release con `/p:TreatWarningsAsErrors=true`. No se admiten advertencias de compilación ni analizadores omitidos.
- **Auditoría de Vulnerabilidades NuGet:** El CI ejecuta `dotnet list package --vulnerable --include-transitive` y falla inmediatamente si detecta vulnerabilidades con severidad `High` o `Critical`.
- **Pruebas de Integración Confiables:** Las pruebas que requieren base de datos viva (`BIMBO_POSTGRES_CONNECTION_STRING`) deben usar `[SkippableFact]` con motivo visible cuando no haya conexión, erradicando los falsos positivos por retornos silenciosos.
- **Gobernanza:** Rutas críticas (`.github/`, `scripts/`, `auditoria/`, `supabase/migrations/`, `AGENTS.md`, `.gitleaks.toml`) requieren la revisión y visto bueno de al menos un propietario en `.github/CODEOWNERS` (`@RedFer855` o `@emanuelmartinez00429`).
- **Dependabot:** Actualizaciones semanales agrupadas para dependencias de `nuget` y acciones de `github-actions`.

### Capa 2: Detección y Gestión de Secretos
- **Regla Fundamental:** Las cadenas de conexión con privilegios administrativos (`service_role`, `postgres`, `sb_secret_`) jamás entran al código, archivos de configuración, notas de bitácora ni prompts de agentes.
- **Protocolo de Rotación (S-NNN):** Si un secreto real llega a exponerse en Git:
  1. Se **rota de inmediato** en la consola del proveedor (Supabase / GitHub / Azure).
  2. Se purga del historial de Git con herramientas adecuadas (`git filter-repo` / BFG).
  3. Se registra el incidente en `contexto/` como hallazgo `S-NNN`, documentando la causa y mitigación **sin incluir el valor del secreto**.
- **Allowlist Justificado:** La clave `anon` de `CapaUI/App.config` figura en el allowlist de `.gitleaks.toml` porque es pública por diseño en la arquitectura de Supabase y sus permisos están estrictamente gobernados por autenticación y políticas RLS.

### Capa 3: Seguridad de la Base de Datos
- **Row Level Security (RLS):** Cada tabla en el esquema `public` o `auditoria` debe tener RLS habilitado (`ALTER TABLE ... ENABLE ROW LEVEL SECURITY`).
- **Principio de Menor Privilegio:** El rol `anon` no tiene acceso directo a tablas operativas; solo usuarios autenticados (`authenticated`) con roles de negocio válidos en RBAC pueden interactuar con los datos.
- **Funciones SECURITY DEFINER Seguras:** Toda función `SECURITY DEFINER` debe fijar su esquema de búsqueda explícitamente (`SET search_path = public, pg_temp;` o `SET search_path = ''`) para impedir ataques de secuestro de ruta (*search_path hijacking*).
- **Inmutabilidad en Auditoría (WORM):** Las tablas de auditoría histórica (`auditoria.sesiones` y `auditoria.deuda_resuelta`) cuentan con triggers estrictos `BEFORE UPDATE OR DELETE` que cancelan incondicionalmente cualquier intento de alteración o borrado de registros.
- **Migraciones Exclusivas:** Ninguna persona ni agente aplica cambios DDL directamente en caliente en producción; toda modificación se versiona en `supabase/migrations/` o `auditoria/migrations/` y es revisada en un Pull Request.

### Capa 4: Política de Seguridad para Agentes de IA
Los agentes de inteligencia artificial (Claude Code, Antigravity, Codex, Gemini, etc.) asisten al equipo bajo límites no negociables:
1. **Acceso de Solo Lectura a Producción:** Las herramientas de base de datos (MCP de Supabase) configuradas para agentes en producción solo permiten operaciones `SELECT`. Queda terminantemente prohibido ejecutar sentencias DDL (`CREATE`, `ALTER`, `DROP`) o DML de escritura (`INSERT`, `UPDATE`, `DELETE`) en la base productiva mediante el MCP.
2. **Migraciones por Archivo:** Toda modificación al esquema de datos debe redactarse como archivo `.sql` en el directorio de migraciones y someterse a revisión por Pull Request. Solo una persona integradora humana aplica las migraciones a producción.
3. **Prohibición de Privilegios de Git:** Ningún agente realiza `git push` directo a `main` o `master`, `force push`, `merge` de Pull Requests, ni modificación de la configuración de GitHub (rulesets, secretos, entornos o ramas por defecto).
4. **Protección de Datos Personales y Secretos:** Prohibido incorporar nombres completos ajenos, correos reales, teléfonos, cédulas o contraseñas en prompts, memoria persistente (Engram), notas de sesión o commits.
5. **Revisión Cruzada Obligatoria:** Todo Pull Request originado por un agente requiere la revisión y aprobación de una persona distinta de quien lanzó o supervisó al agente.
6. **Aislamiento Multiagente:** Las carpetas de coordinación interna entre agentes (`.agents/teamwork/`, `auditor_*`, `worker_*`) nunca se versionan en Git y deben permanecer ignoradas en `.gitignore`.

### Capa 5: Cadena de Suministro y Entrega Continua
- **Check Único Obligatorio:** El job `ci-completo` en `.github/workflows/ci.yml` es el único check de estado requerido para fusionar a `main`. Un job omitido por falta de cambios en código se considera exitoso, permitiendo la integración ágil de documentación.
- **Acciones Pinned por SHA:** Todos los workflows de GitHub Actions consumen acciones fijadas por su hash SHA inmutable de 40 caracteres, con la versión semántica documentada en un comentario.
- **Aprobación de Despliegue:** El workflow `publish-velopack-release.yml` se ejecuta bajo el entorno `produccion`, el cual exige aprobación humana explícita antes de generar el release oficial.
- **Software Bill of Materials (SBOM):** Cada versión generada produce un SBOM estándar CycloneDX en formato JSON (`sbom.json`) que se adjunta como activo del release en GitHub para auditoría forense de componentes.

---

## 3. Modelo de Amenazas Conciso

A continuación se sintetizan los vectores de riesgo más relevantes para la operación de Bimbo Honduras y las defensas implementadas:

| Vector de Amenaza | Escenario de Riesgo | Mitigación Implementada |
| :--- | :--- | :--- |
| **Bypass de Catálogo y RLS** | Un usuario de báscula autenticado manipula peticiones HTTP a la API REST de Supabase para alterar pesajes o tarifas de otras líneas. | RLS activado en todas las tablas; validaciones de negocio en funciones RPC con `SECURITY DEFINER` e integridad referencial forzada por claves foráneas y triggers. |
| **Cadena de Suministro de Velopack** | Inyección de paquetes NuGet comprometidos o alteración de binarios durante el proceso de empaquetado y actualización automática. | Escaneo automatizado de vulnerabilidades en CI; generación de SBOM CycloneDX auditable; acciones fijadas por SHA; entorno de producción protegido con revisores. |
| **Repositorio Público vs Privado** | Exposición de metadatos internos, estructura de endpoints o historial histórico de desarrollo en un repositorio abierto. | Verificación de secretos con Gitleaks en todo el historial; purga de PII y rutas absolutas; propuesta arquitectónica [[ADR-030 - Repo de código privado y repo público de binarios]] para aislar el código fuente. |
| **Agente de IA fuera de control** | Un agente con privilegios elevados aplica cambios destructivos en la base de datos o fusiona código defectuoso en la rama principal. | MCP de Supabase limitado estrictamente a `SELECT`; prohibición de merge y force-push; PR en borrador obligatorio con revisión humana cruzada. |
| **Filtración de PII en Documentación** | Registro accidental de números telefónicos de operadores, correos o credenciales en bitácoras de cambios o notas Markdown. | Validador `verificar.js` ejecuta comprobaciones regex obligatorias de patrones de secretos, correos no autorizados y teléfonos, bloqueando el merge en CI si detecta fugas. |

---

## 4. Relaciones

- [[AGENTS]]
- [[ADR-027 - Distribución pública mediante Velopack]]
- [[ADR-030 - Repo de código privado y repo público de binarios]]
- [[Configuración de GitHub para el admin]]
- [[Plan de Seguridad - Roadmap 10-10]]
