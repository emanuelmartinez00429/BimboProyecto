---
title: "Sesión 2026-09-08 — Plan de CI-CD y actualizaciones remotas"
tags: [sesion, plan, documentacion, cicd, despliegue]
date: 2026-09-08
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (agente)
estado: solo_documentacion
---

# Sesión 2026-09-08 — Plan de CI-CD y actualizaciones remotas

> [!success] Resultado documental
> Se guardó el plan acordado para distribuir el instalador y futuras actualizaciones por Internet. Solo se editaron notas dentro de `contexto/`; no se implementó el actualizador ni CI/CD.

## Motivo y alcance

Tras leer `guia.md` y `Plan_CICD_Actualizaciones_WPF_Supabase.docx`, contrastar el estado local y aclarar las restricciones, se solicitó guardar lo planificado exclusivamente en el contexto. La petición inicial de no guardar automáticamente quedó sustituida por esa autorización documental explícita; no se autorizó desarrollo ni publicación.

## Trabajo y decisiones registradas

- Windows 11 x64, dos estaciones sin instalaciones previas, previsión de diez en seis meses e instalación por máquina.
- Descarga por Internet, sin depender de LAN, mediante aviso y acciones dentro de WPF; el operador no debe navegar a GitHub para actualizar.
- Repositorio fuente privado y repositorio binario público separado; la respuesta inicial contraria a binarios públicos fue reconsiderada tras explicar las diferencias.
- Integración prevista de Velopack, conservando la arquitectura online. El instalador inicial ya deberá incorporar el mecanismo de actualización.
- Barrera de seguridad centrada en trabajo local, con coordinación entre instancias y sin bloquear todas las recepciones de la planta.
- Beta/stable, construcción única y promoción de los mismos artefactos, piloto de dos jornadas con una actualización real y retención mínima de tres estables y 90 días.
- Restricciones de presupuesto, firma, aprobación, instalación por máquina, observabilidad y compatibilidad de Supabase explicitadas como puertas pendientes, no como capacidades existentes.

## Archivos documentales

- [[Plan de CI-CD y Actualizaciones Remotas]]: inventario, acuerdos, pendientes, diseño, 13 fases, pruebas, recuperación y backlog.
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]: decisión de distribución aceptada, implementación no iniciada y límites de seguridad.
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]]: fuentes oficiales consultadas y validaciones que aún faltan.
- [[Arquitectura Actual]] y [[Conocimiento Principal]]: enlaces y señalización explícita de «planificado, no implementado».
- [[Deuda Técnica - Pendientes]]: enlace al backlog de implementación, sin cerrar ni alterar el estado de deudas existentes.
- Esta nota registra la sesión sin reescribir bitácoras anteriores.

## Evidencia y verificación

- Checkout inspeccionado: `162818d4237e96ecbd1a7c8a0a52d1479a12b2e2`, rama indicada en frontmatter, árbol limpio antes de escribir estas notas. No es la futura línea base autorizada de master.
- Inspección local de solución, proyectos, inicio/cierre, configuración, persistencia, pruebas y migraciones; documentación externa oficial consultada durante la planificación.
- La comprobación documental se limita a alcance de archivos, whitespace/diff y enlaces nuevos. No se ejecutaron build, restore, pruebas, instaladores ni cambios remotos para guardar el plan.
- Sin `fetch` ni consulta de ajustes remotos de GitHub/base viva: no se certifican protecciones, credenciales, políticas desplegadas ni salud productiva.
- Las cifras de tests aprobados de otras sesiones no son una ejecución nueva ni evidencia de que se haya probado el actualizador.

### Comprobación documental de cierre

- Siete archivos creados/modificados, todos dentro de `contexto/`; tres notas existentes recibieron únicamente enlaces/anotación y cuatro notas son nuevas.
- `git diff --check` sin errores de whitespace; Git solo avisó de normalización futura LF → CRLF en las tres notas existentes.
- Frontmatter y sección de relaciones presentes en las cuatro notas nuevas; todos sus enlaces Obsidian tienen destino en la bóveda.
- No se detectaron cambios fuera del contexto. No se hizo commit ni push.

## Pendientes y próximo paso

Antes de implementar: responsables, firma/confianza, permisos de IT, calendario .NET, versión/SDK, diagnóstico permitido, controles GitHub disponibles y autorización desde SHA fijo de master. La primera actividad técnica futura es revalidar esa línea base, no ejecutar automáticamente la fase 1 por encontrar este documento.

La propuesta de Supabase staging queda pendiente; la primera entrega conserva el backend online. El plan de auditoría SBOM y sus restricciones no se ejecutaron ni se dieron por resueltos. P-056 permanece pendiente y no se cerró ningún P-NNN.

## Lo que NO cambió

No se modificaron C#, XAML, proyectos, paquetes, configuración de ejecución, workflows, SQL, datos remotos ni ajustes GitHub. No se crearon repositorios, certificados o artefactos distribuibles. No se hicieron commits, push ni implementación offline.

## Relaciones

- [[Conocimiento Principal]]
- [[Arquitectura Actual]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]]
- [[Deuda Técnica - Pendientes]]
