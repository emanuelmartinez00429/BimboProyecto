---
title: "ADR-030 — Repo de código privado y repo público de binarios"
type: adr
status: borrador
estado: propuesto
date: 2026-11-09
updated: 2026-11-09
autor: fernando
summary: "Propuesta arquitectónica para separar el código fuente en un repositorio privado de GitHub y distribuir instaladores/deltas de Velopack desde un repositorio público independiente."
tags:
  - adr
  - seguridad
  - velopack
  - devsecops
  - gobernanza
---

# ADR-030 — Repo de código privado y repo público de binarios

> **Estado:** `propuesto`  
> **Fecha:** 2026-11-09  
> **Autor:** Fernando (con revisión de Emanuel Martínez)  
> **Reemplazaría a:** [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] (actualmente vigente en estado aceptado)

---

## 1. Contexto y Problema

Actualmente, conforme al [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] (revisado el 2026-11-06), el código fuente de BimboProyecto y sus paquetes de distribución residen juntos en el repositorio público de GitHub `emanuelmartinez00429/BimboProyecto`.

Si bien el acceso a la base de datos de producción está protegido por autenticación y Row Level Security (RLS) en Supabase, mantener el código fuente en un repositorio abierto presenta riesgos y desventajas operativas:
1. **Exposición de Lógica de Negocio:** La estructura de modelos, reglas de pesaje de báscula, tarifas de productos y nombres de funciones RPC son visibles públicamente.
2. **Superficie de Ataque para Ingeniería Inversa:** Facilita la auditoría estática externa de posibles vulnerabilidades antes de que sean detectadas internamente.
3. **Distribución de Velopack:** Velopack requiere un endpoint público HTTPS sin autenticación en el cliente para que las computadoras de planta puedan descargar `releases.win.json`, instaladores y deltas sin portar tokens embebidos.

---

## 2. Decisión Propuesta

Se propone desacoplar el entorno de desarrollo y código fuente del canal de distribución de binarios mediante una arquitectura de **dos repositorios**:

```
[ Repo Privado: BimboProyecto ]
      │ (Código fuente, CI, tests, documentación)
      ▼ (GitHub Actions en main / tags)
  Genera SBOM + vpk pack
      │ (Publica usando fine-grained PAT)
      ▼
[ Repo Público: BimboProyecto-releases ]
      │ (Solo releases de GitHub: Setup.exe, .nupkg, releases.win.json, sbom.json)
      ▼
[ Clientes de Planta (WPF / Velopack) ] (Consumen UPDATE_REPO_URL sin secretos)
```

### Componentes de la Arquitectura:

1. **Repositorio de Código Fuente Privado (`emanuelmartinez00429/BimboProyecto`):**
   - Se cambia la visibilidad del repositorio actual a **Privado**.
   - Contiene todo el código C# (.NET 10, WPF), capas de aplicación y datos, pruebas xUnit, scripts de automatización y la bóveda de contexto/documentación.
   - El acceso queda restringido exclusivamente a colaboradores autorizados (`@emanuelmartinez00429`, `@RedFer855`, `@sanchezcarlosvalle-arch`).

2. **Repositorio Público de Distribución (`emanuelmartinez00429/BimboProyecto-releases`):**
   - Repositorio público vacío, dedicado exclusivamente a alojar **GitHub Releases**.
   - No contiene código fuente ni ramas de desarrollo; únicamente actúa como almacén CDN de los artefactos generados por Velopack (`BimboPesaje-win-Setup.exe`, `BimboPesaje-x.x.x-full.nupkg`, deltas y `releases.win.json`) y el archivo SBOM (`sbom.json`).

3. **Publicación Automatizada con PAT Fine-Grained:**
   - En el repositorio privado, se configura un secreto en GitHub Actions (`RELEASES_PAT`).
   - Este token es un **Fine-Grained Personal Access Token** de GitHub con vigencia acotada y permiso exclusivo `contents: write` limitado únicamente sobre el repositorio `emanuelmartinez00429/BimboProyecto-releases`.
   - El workflow `publish-velopack-release.yml` del repositorio privado compila la solución, ejecuta Velopack y publica directamente en el repositorio de releases usando el token.

4. **Configuración del Cliente (`UPDATE_REPO_URL`):**
   - En `CapaUI/App.config`, la clave `UPDATE_REPO_URL` se actualiza para apuntar al repositorio de distribución:
     ```xml
     <add key="UPDATE_REPO_URL" value="https://github.com/emanuelmartinez00429/BimboProyecto-releases" />
     ```
   - Los clientes de planta descargan las actualizaciones de forma nativa por HTTPS sin necesidad de ninguna clave o credencial embebida.

---

## 3. Análisis de Costos y Recursos (GitHub Actions)

Un factor determinante para esta decisión es el consumo de cuotas en GitHub:

- **Repositorios Públicos:** Minutos de GitHub Actions ilimitados y gratuitos.
- **Repositorios Privados (Plan GitHub Free):** Cuenta con una cuota mensual gratuita de **2,000 minutos**.
- **Multiplicador de Windows Runners:** Los ejecutables y pruebas de WPF requieren `windows-latest`. En GitHub Actions, los runners de Windows tienen un **multiplicador de consumo de 2x** (1 minuto de ejecución en Windows descuenta 2 minutos de la cuota mensual).
- **Proyección de Consumo:**
  - Corrida de CI (`dotnet` + pruebas): ~1.5 minutos de Windows = 3 minutos de cuota.
  - Con ~20 PRs/pushes mensuales en `main`, el consumo ronda los 60 a 100 minutos al mes, muy por debajo del límite de 2,000 minutos.
  - No obstante, la suite de CI debe mantenerse ágil mediante el job `cambios` en Linux (multiplicador 1x) para saltar compilaciones cuando solo se toquen notas o documentación.

---

## 4. Preservación y Realidad del Historial Previo

Es imperativo reconocer una limitación técnica y de seguridad:
- El repositorio ha sido público durante fases anteriores.
- Cualquier persona u organización que haya realizado un fork o clon local antes de privatizar conserva el historial de commits previo.
- La privatización del repositorio **protege estrictamente el código, features y correcciones que se desarrollen a partir de la fecha de privatización**, pero no revierte retroactivamente copias históricas ya distribuidas.
- Por esta razón, la política de no commitear secretos reales (y la rotación inmediata mediante hallazgos `S-NNN` si ocurriese) sigue siendo obligatoria.

---

## 5. Gobernanza

La adopción de este ADR es una decisión estratégica que corresponde a **Emanuel Martínez** como dueño del repositorio y responsable del despliegue en Bimbo Honduras.

Este documento permanece en estado **`propuesto`** y no sustituye al [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] hasta que Emanuel complete la configuración administrativa y formalice su aceptación.

---

## 6. Relaciones

- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[E-01 - Estándar DevSecOps]]
- [[Configuración de GitHub para el admin]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
