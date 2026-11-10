---
title: "Resumen 2026-08"
type: resumen
status: vigente
date: 2026-08-31
updated: 2026-11-09
summary: "Motor de reportes PDF/Excel, RPC idempotentes auditadas en pesajes, rediseño de Roles y configuración de empresa."
autor: fernando
agente: antigravity
tags: [resumen, bitacora, hitos]
---

# Resumen 2026-08

## Qué se hizo

- **Motor de Reportería Operativo:** Generación y exportación de 4 reportes en PDF/Excel con previsualización paginada y auditoría obligatoria.
- **RPC Idempotentes en Pesajes:** Migración de operaciones críticas a funciones Postgres `_seguro` con verificación de idempotencia y bitácora atómica.
- **Rediseño de Gestión de Roles:** Panel visual interactivo con `SpanningGridPanel` y control fino de acciones por rol.
- **Configuración de Empresa:** Formulario de ajustes institucionales con paleta de colores reactiva y almacenamiento de logotipo.

## Decisiones del mes

- `[[ADR-011 - Motor de reportes PDF y Excel]]` — Generación local con PDFsharp y ClosedXML.
- `[[ADR-012 - RPC seguras con bitácora e idempotencia en Pesajes]]` — Integridad transaccional y prevención de duplicados en báscula.

## Qué quedó abierto

- `P-028` y `P-030` (verificación de Roles en runtime), `P-035` (trigger de updated_at en empresa) y `P-036` (CRUD de taras).

## Relaciones

- [[Módulo Reportería]]
- [[Módulo Configuración de Empresa]]
- [[Deuda - Abierta]]
- [[INDEX-bitacora]]
