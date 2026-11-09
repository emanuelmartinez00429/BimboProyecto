---
title: "Sesión 2026-11-09 — Pipeline CI/CD con DevSecOps, fijación SHA y auditoría de seguridad"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-3.8-flash-high
rama: ci/devsecops
commits: [da10cce, 3d6240d, 18d1c26]
  - 15899a5
tags:
  - ci
  - devsecops
  - gitleaks
  - xunit
  - velopack
  - sbom
  - dependabot
  - codeowners
summary: "Pipeline CI/CD DevSecOps completo: acciones fijadas por SHA, gitleaks 8.30.1 con sha256 verificada, límite de líneas de PR, skips explícitos en xUnit, CODEOWNERS, dependabot y SBOM CycloneDX."
deuda: []
adr: []
---

# Sesión 2026-11-09 — Pipeline CI/CD con DevSecOps, fijación SHA y auditoría de seguridad

## Problema o motivo

El repositorio carecía de un pipeline unificado de DevSecOps conforme a las prácticas establecidas en CITMec y FOE 360 Móvil. Específicamente:
1. Las acciones de GitHub no estaban fijadas por SHA de commit inmutable.
2. No existía detección automatizada de secretos (`gitleaks`) ni control de tamaño máximo por Pull Request.
3. Tres pruebas de integración en `BimboProyecto.Tests` contenían un `return` silencioso ante la ausencia de `BIMBO_POSTGRES_CONNECTION_STRING`, arrojando un verde falso sin alertar que la prueba fue omitida.
4. No se generaba SBOM (Software Bill of Materials) en la distribución de binarios vía Velopack.
5. Faltaban las reglas de gobernanza `.github/CODEOWNERS`, `.github/dependabot.yml` agrupado y la plantilla estándar de Pull Request con checklist de seguridad.

## Cambios

1. **Pipeline de CI (`.github/workflows/ci.yml`):**
   - Acciones fijadas por commit SHA con versión en comentario.
   - Permiso `contents: read` con control de concurrencia `group: ci-${{ github.ref }}`.
   - Job `cambios` en `ubuntu-latest`:
     - Filtro determinista de rutas tocadas y rango de commits.
     - Límite de tamaño de PR: advierte en >500 líneas y falla en >1000 líneas (excluyendo documentación, lockfiles y migraciones SQL).
     - Descarga determinista de Gitleaks 8.30.1 verificando suma de comprobación sha256 (`551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb`), ejecutando con `--redact` y configuración local.
   - Job `dotnet` en `windows-latest`:
     - Configuración de .NET 10 fijada por SHA.
     - Compilación Release con cero advertencias (`TreatWarningsAsErrors=true`).
     - Ejecución de pruebas unitarias xUnit con reporte detallado.
     - Detección de paquetes NuGet vulnerables (`dotnet list package --vulnerable --include-transitive`), fallando ante severidades High o Critical.
   - Job agregador `ci-completo` con `if: always()` como único check obligatorio para el ruleset de GitHub.

2. **Validación de contexto en CI (`.github/workflows/contexto.yml`):**
   - Ejecución automatizada en Node 20 (`ubuntu-latest`) de `node scripts/contexto/verificar.js` y `node --test scripts/contexto/`.

3. **Publicación y SBOM (`.github/workflows/publish-velopack-release.yml` y `scripts/publish-release.ps1`):**
   - Disparo condicionado exclusivamente a `workflow_run` de `ci` con `conclusion == 'success'`, sobre `head_branch == 'main'` y con `event == 'push'`, o `workflow_dispatch`. Esto garantiza que una corrida de CI originada por un PR nunca dispare una publicación accidental.
   - En el paso de checkout, se fija explícitamente `ref: ${{ github.event.workflow_run.head_sha }}` para compilar y empaquetar el commit exacto probado por el CI en lugar de la rama por defecto del repositorio (`master`).
   - Entorno `produccion` con aprobadores requeridos.
   - Generación de SBOM estándar CycloneDX fijando la versión de la herramienta en `6.3.1` (`dotnet tool install --global CycloneDX --version 6.3.1`).
   - `scripts/publish-release.ps1`: incorporación de carga automatizada del SBOM (`gh release upload "v$Version" $sbomPath --clobber`) al crear el release en GitHub.

4. **Reglas de secretos (`.gitleaks.toml`):**
   - Extensión de reglas base (`useDefault = true`).
   - Reglas estrictas para `supabase-service-role` y `supabase-secret-key` (`sb_secret_`).
   - Allowlist para la clave `anon` de `CapaUI/App.config` documentando su carácter público protegido por RLS.

5. **Gobernanza y plantillas:**
   - `.github/CODEOWNERS`: propietarios `@RedFer855` y `@emanuelmartinez00429` para rutas críticas (`.github/`, `supabase/migrations/`, `auditoria/`, `scripts/`, `AGENTS.md`, `.gitleaks.toml`, `contexto/.control/`).
   - `.github/dependabot.yml`: revisiones semanales para `github-actions` y `nuget`, agrupadas y limitadas a 3 PRs.
   - `.github/pull_request_template.md`: checklist completo (tamaño, secretos, RLS, contexto, verificador y build).

6. **Pruebas de integración xUnit (`BimboProyecto.Tests`):**
   - Incorporación de `Xunit.SkippableFact`.
   - En `ReglasEntidadesTests.cs` y `ContratoRbacTests.cs`, sustitución del `return` silencioso por `Skip.If(string.IsNullOrWhiteSpace(conexion), ...)` con mensaje descriptivo, reportando `Omitido: 3` en lugar de verde falso.

## Verificación

- `dotnet build BimboProyecto.sln -c Release /p:TreatWarningsAsErrors=true`: 0 errores, 0 advertencias.
- `dotnet test BimboProyecto.Tests\BimboProyecto.Tests.csproj -c Release`: 735 superadas, 3 omitidas explícitamente, 0 errores.
- `actionlint`: 0 errores en todos los workflows (`ci.yml`, `contexto.yml`, `publish-velopack-release.yml`, `archivar-bitacora.yml`).
- `gitleaks detect --config .gitleaks.toml --redact --verbose`: 393 commits analizados en todo el historial, 0 fugas encontradas.
- `node scripts/contexto/verificar.js`: 0 errores en 307 notas.
- `node --test scripts/contexto/`: 7/7 pruebas superadas.

## Lo que no cambió

- No se modificó el código productivo de la app (`CapaUI`, `CapaDatos`, `CapaDominio`, `CapaAplicacion4`).
- No se alteraron claves ni configuraciones en `CapaUI/App.config`.
- La lógica interna de empaquetado de `scripts/publish-release.ps1` se conservó intacta salvo la subida del archivo SBOM.

## Pendientes

- Configurar el Ruleset en GitHub (`main`) y los aprobadores del entorno `produccion` en la UI de administración por parte de Emanuel.

## Relaciones

- [[E-01 - Estándar DevSecOps]]
- [[ADR-027 - Distribución pública mediante Velopack]]
- [[ADR-030 - Repo de código privado y repo público de binarios]]
- [[Configuración de GitHub para el admin]]
