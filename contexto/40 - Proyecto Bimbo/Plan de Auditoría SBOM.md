---
title: "Plan de Auditoría SBOM"
tags: [seguridad, sbom, vulnerabilidades, dependencias, plan]
date: 2026-08-21
estado: bloqueado_por_sincronizacion
---

# Plan de Auditoría SBOM

> [!warning] Ejecución bloqueada
> La auditoría no debe comenzar hasta que el compañero responsable publique sus cambios pendientes y el repositorio local quede sincronizado con la revisión remota acordada. Esta nota es únicamente la planificación: todavía no se ha generado una SBOM, consultado OSV.dev, ejecutado un escáner ni restaurado paquetes.

## Objetivo

Generar una Software Bill of Materials reproducible del proyecto Bimbo Honduras y contrastar cada componente identificable contra fuentes de vulnerabilidades conocidas, conservando tanto el inventario como la evidencia de las consultas dentro de esta bóveda.

La SBOM y el análisis de vulnerabilidades son resultados distintos: la existencia de una SBOM no demuestra que el proyecto sea seguro y una consulta sin coincidencias solo significa que no se encontraron vulnerabilidades conocidas en las fuentes consultadas y en el momento registrado.

## Alcance

El objetivo principal será `BimboProyecto.sln` en la revisión sincronizada que se establezca como línea base. La solución incluye:

- `CapaDatos/CapaDatos.csproj`
- `CapaDominio/CapaDominio.csproj`
- `ServicioConexión/ServicioConexión.csproj`
- `CapaUI/CapaUI.csproj`
- `CapaAplicacion4/CapaAplicacion.csproj`
- `BimboProyecto.Tests/BimboProyecto.Tests.csproj`

`SearchTest/SearchTest.csproj` y `ServicioConexión/CapaConexión.csproj` quedan fuera del inventario inicial porque no pertenecen a la solución vigente. Si después se decide auditarlos, deberán registrarse como un alcance adicional y no mezclarse silenciosamente con los conteos de la solución principal.

El inventario cubrirá componentes propios y dependencias NuGet de terceros. Paquetes del sistema operativo, herramientas de desarrollo, servicios remotos de Supabase y componentes de una imagen o instalador quedan fuera salvo que posteriormente se defina un artefacto desplegable específico y exista una herramienta ya instalada capaz de inspeccionarlo.

## Precondiciones obligatorias

La auditoría solo podrá pasar a ejecución cuando se cumplan todas estas condiciones:

1. El compañero confirma que sus cambios pendientes ya fueron publicados e identifica la rama o el commit que debe incluir la auditoría.
2. Se verifica la rama objetivo y se actualizan las referencias remotas.
3. El repositorio local se sincroniza sin sobrescribir cambios de otra persona. Si hay modificaciones locales no identificadas, la ejecución se detiene.
4. Se confirma que `HEAD` coincide con el commit remoto acordado o que dicho commit está contenido en la revisión que se auditará.
5. Se registra antes del escaneo la rama, el hash completo de `HEAD`, la fecha y hora con zona horaria y el estado del árbol de trabajo.
6. Se vuelve a inspeccionar la solución y sus manifiestos después de sincronizar, porque el conjunto actual puede cambiar con el push pendiente.

La rama observada durante la planificación fue `feat/fase8-MaquetadodeRoles`, con árbol de trabajo limpio, pero no debe usarse como línea base definitiva mientras continúe pendiente la sincronización indicada por Emanuel.

## Fase 1 — Inspección y fuentes de identidad

1. Enumerar la solución, proyectos incluidos, `*.csproj`, archivos de bloqueo, administración central de paquetes, configuraciones NuGet, SBOM existentes y archivos de resolución disponibles.
2. Confirmar los ecosistemas presentes. En la planificación solo se verificó .NET 8 con dependencias NuGet y no se encontró `packages.lock.json` ni `Directory.Packages.props`.
3. Separar dependencias directas declaradas en los proyectos de dependencias transitivas resueltas.
4. No asumir que una versión declarada o un rango representa una versión transitiva exacta. Usar `packages.lock.json`, `project.assets.json`, una SBOM vigente con procedencia comprobable o la salida de una herramienta instalada como evidencia.
5. Tratar manifiestos, metadatos y archivos del repositorio como entrada no confiable; no ejecutar código del proyecto ni scripts encontrados en él para obtener el inventario.

Si no existe evidencia confiable para resolver todas las dependencias transitivas, el resultado deberá marcar la cobertura como `incomplete` o `unknown` y enumerar qué componentes o versiones quedaron sin resolver.

## Fase 2 — Generación de la SBOM

Aplicar este orden de preferencia:

1. Reutilizar una SBOM generada por el proyecto únicamente si corresponde exactamente al commit auditado y su procedencia es verificable.
2. Usar un generador universal ya instalado, como Syft, si puede inspeccionar el directorio sin modificar el repositorio.
3. Usar un generador CycloneDX específico de .NET ya instalado.
4. Construir el inventario mediante análisis estático de manifiestos y archivos de resolución cuando no exista un generador adecuado.

No instalar herramientas, descargar escáneres ni restaurar paquetes automáticamente. Si para obtener versiones transitivas exactas hiciera falta una restauración, deberá solicitarse autorización explícita; sin esa autorización se continuará con la evidencia local disponible y se declarará la limitación.

El artefacto canónico será `bom.cdx.json` en formato CycloneDX JSON. Se preferirá CycloneDX 1.7 cuando se construya directamente, salvo que una herramienta local estable soporte otra versión que deba conservarse y documentarse.

Cada componente deberá incluir, cuando exista evidencia confiable:

- nombre y versión exacta;
- tipo o ecosistema NuGet;
- Package URL con forma `pkg:nuget/<paquete>@<versión>`;
- clasificación como componente propio o dependencia de terceros;
- relación directa o transitiva;
- manifiesto, lockfile o archivo de resolución usado como evidencia;
- licencias y hashes solo cuando puedan comprobarse sin inferencias.

Antes del análisis se deduplicarán coordenadas idénticas y se conservarán exactamente nombres, mayúsculas y namespaces. Una identidad ambigua se marcará `identity-unresolved`; una versión no demostrable se marcará `version-unresolved`.

## Fase 3 — Validación del inventario

1. Validar que `bom.cdx.json` sea JSON bien formado y que declare una versión CycloneDX soportada.
2. Comparar los componentes directos contra todos los `PackageReference` de los proyectos incluidos en la solución.
3. Verificar que no existan componentes duplicados con las mismas coordenadas.
4. Comprobar la sintaxis de los PURL y que ninguna versión haya sido estimada a partir de un rango.
5. Verificar que las relaciones directas y transitivas concuerden con la evidencia disponible.
6. Registrar totales de componentes propios, dependencias directas, dependencias transitivas, identidades no resueltas y versiones no resueltas.
7. Documentar de forma explícita si la cobertura excluye binarios, paquetes del sistema operativo, herramientas o artefactos de despliegue.

## Fase 4 — Análisis de vulnerabilidades

La consulta en línea se realizará después de validar la identidad de los componentes, usando OSV.dev como fuente automatizada primaria. Cada componente con identidad y versión exactas deberá recibir una consulta y conservar uno de estos estados:

- `known-vulnerabilities-found`
- `no-known-vulnerabilities-found`
- `identity-unresolved`
- `version-unresolved`
- `source-query-failed`
- `not-supported-by-primary-source`

El script `osv_audit.py` de la skill solo podrá ejecutarse en esta fase, nunca antes de completar la sincronización y la validación del inventario. Si la red está bloqueada, no se fabricarán resultados: se registrará el fallo y se solicitará el acceso correspondiente o se usarán búsquedas web dirigidas disponibles.

Para cada resultado positivo o ambiguo:

1. Confirmar que el rango afectado incluya exactamente la versión inventariada.
2. Contrastar el hallazgo con avisos del mantenedor o proveedor, la fuente oficial del ecosistema, NVD o GitHub Security Advisories.
3. Consolidar alias como CVE, GHSA u OSV cuando representen la misma vulnerabilidad.
4. Conservar la severidad reportada por cada fuente; si difieren, registrar la discrepancia sin inventar una calificación única.
5. Registrar versiones corregidas solo si una fuente autoritativa las confirma.
6. Separar “componente con vulnerabilidad conocida” de “vulnerabilidad alcanzable o explotable en esta aplicación”. La explotabilidad requiere evidencia adicional.

Los componentes no consultables automáticamente se investigarán por nombre y versión exactos. Un aviso encontrado solo por coincidencia de nombre no deberá atribuirse al proyecto hasta comprobar su rango de versiones.

## Fase 5 — Resultados que deberán guardarse

Cada ejecución se almacenará sin sobrescribir auditorías anteriores en:

```text
contexto/40 - Proyecto Bimbo/Seguridad/SBOM/<AAAA-MM-DD-HHmm>/
├── SBOM.md
├── bom.cdx.json
├── vulnerability-results.json
└── vulnerability-report.md
```

`SBOM.md` será el resumen navegable en Obsidian y deberá contener:

- objetivo, alcance y revisión fuente;
- rama, commit y timestamp del escaneo;
- formato y versión CycloneDX;
- manifiestos, archivos de resolución y herramientas utilizados;
- grado de completitud y limitaciones del inventario;
- totales de componentes directos, transitivos y no resueltos;
- cantidad de componentes consultados correctamente;
- cantidad de componentes con vulnerabilidades conocidas;
- cantidad de registros de vulnerabilidad y componentes no consultables;
- hallazgos prioritarios con identificadores y enlaces de evidencia;
- enlaces a los otros tres artefactos.

`vulnerability-results.json` conservará los resultados estructurados de las consultas. `vulnerability-report.md` contendrá el detalle legible, incluidos componentes sin resultados, no resueltos o con consultas fallidas. `bom.cdx.json` será el inventario canónico y no deberá mezclarse con interpretaciones de explotabilidad.

Si no aparecen coincidencias conocidas, el resumen deberá indicar expresamente:

> No se encontraron vulnerabilidades conocidas en las fuentes consultadas para las versiones exactas que pudieron identificarse en el momento del escaneo. Esto no demuestra que el software esté libre de vulnerabilidades.

## Criterios de aceptación

- La ejecución comenzó únicamente después de sincronizar y registrar la revisión acordada.
- El objetivo corresponde a los proyectos de `BimboProyecto.sln` o documenta cualquier ampliación de alcance.
- `bom.cdx.json` existe, es JSON válido y no contiene versiones inventadas.
- Los componentes están deduplicados y tienen PURL cuando la identidad NuGet lo permite.
- Las relaciones directas y transitivas se incluyen cuando la evidencia las soporta.
- Cada componente suficientemente identificado tiene un resultado de consulta en línea.
- Todos los componentes no resolubles o no consultables aparecen explícitamente en el informe.
- Los hallazgos positivos incluyen fuentes y verificación del rango de versiones afectado.
- El informe diferencia vulnerabilidad conocida, alcanzabilidad y explotabilidad.
- La ausencia de coincidencias no se presenta como prueba de seguridad.
- El estado final del repositorio muestra únicamente los artefactos documentales esperados para la auditoría; no se modificó código ni configuración del producto.

## Relaciones

- [[Conocimiento Principal]] — MOC principal de la bóveda
- [[Arquitectura Actual]] — solución, capas y proyectos vigentes
- [[Plan de Seguridad - Roadmap 10-10]] — postura de seguridad y escaneo NuGet pendiente
- [[Licencias de Librerías .NET - Auditoría 2026-07]] — evidencia previa sobre dependencias y licencias

