---
title: "Configuración de GitHub para el administrador"
type: referencia
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
summary: "Guía paso a paso para que el administrador del repositorio (Emanuel) configure branch rulesets, entorno de producción con aprobaciones, secretos de Actions y la rama principal."
tags:
  - github
  - devsecops
  - administracion
  - gobernanza
---

# Configuración de GitHub para el Administrador

> **Destinatario:** Emanuel Martínez (`@emanuelmartinez00429`), dueño del repositorio y administrador de GitHub.  
> **Objetivo:** Aplicar las configuraciones de seguridad, gobernanza y despliegue continuo en el repositorio `emanuelmartinez00429/BimboProyecto` para activar los portones de calidad de DevSecOps.

---

## 1. Cambio de Rama por Defecto a `main`

La rama de integración estándar del proyecto se ha unificado en `main` (sustituyendo la histórica `master`).

1. En GitHub, navegar a **Settings** > **General**.
2. En la sección **Default branch**, hacer clic en el botón de cambio de rama (ícono de dos flechas).
3. Seleccionar **`main`** y confirmar haciendo clic en **Update** > **I understand, update the default branch**.

> [!warning] Impacto crítico en el pipeline de publicación
> GitHub Actions solo dispara eventos `workflow_run` si el archivo del workflow existe físicamente en la **rama por defecto** del repositorio. Hasta que la rama por defecto sea cambiada de `master` a `main`, la publicación automática tras el CI en `main` **no se activará**; únicamente responderá al disparo manual (`workflow_dispatch`). Por tanto, este paso es prerrequisito indispensable.

---

## 2. Configuración del Ruleset de Protección para `main`

Para evitar pushes accidentales y forzar que todo cambio sea auditado mediante Pull Request y CI:

1. Navegar a **Settings** > **Rules** > **Rulesets**.
2. Hacer clic en **New ruleset** > **New branch ruleset**.
3. Configurar los siguientes campos:
   - **Ruleset Name:** `Protección de main`
   - **Enforcement status:** `Active`
   - **Target branches:** Hacer clic en **Add target** > **Include default branch** (o `main`).
4. En la sección **Branch rules**, activar:
   - **Restrict deletions:** Marcado.
   - **Block force pushes:** Marcado.
   - **Require a pull request before merging:**
     - **Required approvals:** `1`
     - **Dismiss stale pull request approvals when new commits are pushed:** Marcado (garantiza que nuevos commits re-validen la aprobación).
     - **Require review from Code Owners:** Marcado (exige visto bueno de dueños según `.github/CODEOWNERS`).
     - **Require approval of the most recent reviewable push:** Marcado.
   - **Require status checks to pass:**
     - **Require branches to be up to date before merging:** Marcado.
     - **Status checks that are required:** Añadir los siguientes dos checks exactos:
       1. `CI completo` (Job agregador de `.github/workflows/ci.yml`)
       2. `Verificar contexto` (Job de `.github/workflows/contexto.yml`)
5. Hacer clic en **Save changes**.

---

## 3. Configuración del Entorno `produccion` con Revisores

El pipeline de publicación de releases de Velopack (`publish-velopack-release.yml`) requiere autorización explícita antes de compilar y subir ejecutables a producción:

1. Navegar a **Settings** > **Environments**.
2. Hacer clic en **New environment** y nombrar: `produccion`.
3. En la sección **Deployment protection rules**:
   - Activar **Required reviewers**.
   - Añadir a **`emanuelmartinez00429`** (y opcionalmente a **`RedFer855`**).
   - *Prevent self-review:* Opcional según la política deseada.
4. En **Deployment branches**:
   - Seleccionar **Selected branches** y agregar únicamente la regla `main`.
5. Hacer clic en **Save protection rules**.

> [!danger] Crear el entorno ANTES de fusionar
> El entorno `produccion` debe ser creado en GitHub con sus revisores configurados **antes** de fusionar a `main`. Si se realiza un merge o se ejecuta el pipeline sin que el entorno exista previamente, GitHub Actions creará automáticamente el entorno `produccion` **vacío y sin ninguna regla de aprobación**, permitiendo publicaciones automáticas desatendidas.

---

## 4. Configuración de Secretos de GitHub Actions

Para la automatización del archivo semanal de auditoría WORM en Supabase (`.github/workflows/archivar-bitacora.yml`):

1. Navegar a **Settings** > **Secrets and variables** > **Actions**.
2. En la pestaña **Repository secrets**, hacer clic en **New repository secret** y registrar los dos secretos del proyecto independiente de auditoría (`bimbo-auditoria-dev`):
   - **`AUDIT_SUPABASE_URL`**: URL HTTPS del proyecto de Supabase para auditoría.
   - **`AUDIT_SUPABASE_SERVICE_KEY`**: Clave privada con rol `service_role` del proyecto de auditoría (necesaria para operaciones WORM administrativas desde el runner).
3. *(Opcional si se aprueba el ADR-030):*
   - **`RELEASES_PAT`**: Fine-grained Personal Access Token con permiso `contents: write` exclusivo sobre `emanuelmartinez00429/BimboProyecto-releases`.

---

## 5. Verificación de la Configuración

Una vez aplicados los pasos anteriores:
1. Intentar hacer un push directo a `main` desde la terminal debe ser rechazado por GitHub con error `remote: error: GH006: Protected branch update failed`.
2. Al abrir un Pull Request hacia `main`, los checks `CI completo` y `Verificar contexto` se ejecutarán automáticamente y deben figurar como obligatorios (*Required*).
3. Al aprobarse un merge en `main`, el workflow de publicación solo se activará tras la confirmación manual de Emanuel en la interfaz de Actions.

---

## 6. Relaciones

- [[E-01 - Estándar DevSecOps]]
- [[ADR-030 - Repo de código privado y repo público de binarios]]
- [[AGENTS]]
