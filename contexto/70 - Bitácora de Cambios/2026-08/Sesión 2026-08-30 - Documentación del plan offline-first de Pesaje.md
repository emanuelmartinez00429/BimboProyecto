---
title: "Sesión 2026-08-30 — Documentación del plan offline-first de Pesaje"
tags:
  - sesion
  - documentacion
  - plan
  - offline-first
date: 2026-08-30
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-08-30 — Documentación del plan offline-first de Pesaje

> [!success] Resultado
> Se incorporó a la vault el plan arquitectónico revisado de funcionamiento offline-first, con estado `propuesto`, métricas de avance, decisiones pendientes, ADR y enlaces de navegación. No se implementó ninguna parte funcional.

---

## Problema / motivo

El plan existía fuera de la vault y necesitaba convertirse en documentación navegable sin confundirlo con la arquitectura vigente. Debía conservar las correcciones de idempotencia, snapshot/Realtime, multiusuario, alcance, SQLCipher, seguridad, recuperación y pruebas.

## Cambios aplicados

- `40 - Proyecto Bimbo/Plan Offline-First de Pesaje.md`
  - Plan completo con hechos verificados, contradicciones, decisiones pendientes, fases, puertas de avance, métricas y criterios de aceptación.
- `45 - Decisiones/ADR-022 - Persistencia local-first con SQLCipher y sincronización por outbox.md`
  - Dirección arquitectónica registrada como `propuesto`, no aceptada.
- `00 - MOC/Conocimiento Principal.md`
  - Enlace al plan y al ADR dentro de navegación rápida.
- `40 - Proyecto Bimbo/Módulo Pesaje.md`
  - Relación hacia el plan futuro, sin cambiar el estado vigente del módulo.

## Verificación

- Se leyó `contexto/AGENTS.md` y se aplicaron frontmatter, taxonomía, anti-duplicados y `## Relaciones`.
- Se comprobó que no existía otra nota offline-first/SQLCipher.
- El plan quedó diferenciado explícitamente del estado ejecutable.
- No se creó deuda P-NNN porque las brechas están registradas como prerrequisitos de una iniciativa todavía no aprobada, no como regresiones introducidas.
- No se ejecutó build: solo cambiaron archivos Markdown.

## Lo que NO cambió

- Ningún archivo C# o XAML.
- Ningún `.csproj` ni paquete.
- Ninguna migración local o Supabase.
- Ninguna tabla, RPC, política, trigger, grant o publicación Realtime.
- `Arquitectura Actual.md` continúa describiendo exclusivamente el sistema implementado.
- El ADR no pasa a `aceptado`.

## Pendientes de decisión

- Modelo de sucursales.
- Forma real de inicio de Windows.
- Vigencia offline.
- Escala de equipos.
- PKI, escrow y firma.
- Copia cifrada fuera del equipo.
- Matriz de operaciones offline.
- Retención histórica local.
- Credencial autoritativa del dispositivo.

## Relaciones

- [[Plan Offline-First de Pesaje]]
- [[ADR-022 - Persistencia local-first con SQLCipher y sincronización por outbox]]
- [[Arquitectura Actual]]
- [[Módulo Pesaje]]

---

## Ampliación — planificación independiente de las 27 rutas RPC

### Objetivo

Documentar por separado la migración de las 27 rutas activas que todavía ejecutan DML directo, sin modificar el plan arquitectónico offline-first principal y sin implementar código o cambios de Supabase.

### Trabajo realizado

- Se creó `40 - Proyecto Bimbo/Plan de Migración de Mutaciones Directas a RPC.md`.
- Se clasificaron 6 rutas de Pesaje, 14 de catálogos y 7 administrativas o técnicas.
- Cada ruta quedó asociada a una RPC/estrategia objetivo, política online/offline, fase y criterio de aceptación.
- Se documentaron contrato común, idempotencia, versionado, pruebas, despliegue gradual y reversión sin dual-write.
- Se añadió la nueva nota al MOC principal.

### Verificación

- El inventario suma 27 rutas activas.
- `Plan Offline-First de Pesaje.md` no fue modificado.
- No se modificó código, paquetes, migraciones ni Supabase.
- No se ejecutó build porque los cambios son exclusivamente Markdown.

### Relaciones adicionales

- [[Plan de Migración de Mutaciones Directas a RPC]]
