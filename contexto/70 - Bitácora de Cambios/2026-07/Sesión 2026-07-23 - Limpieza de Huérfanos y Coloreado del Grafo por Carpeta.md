---
title: "Sesión 2026-07-23 — Limpieza de Huérfanos y Coloreado del Grafo por Carpeta"
tags:
  - sesion
  - mantenimiento
  - obsidian
  - grafo
date: 2026-07-23
autor_cambios: Claude
---

# Sesión 2026-07-23 — Limpieza de Huérfanos y Coloreado del Grafo

> [!success] Resultado
> Escaneada la bóveda completa (87 notas) buscando archivos sin enlaces entrantes y mal ubicados. Conectados los 2 huérfanos reales, eliminado 1 duplicado vacío, y coloreado el grafo de Obsidian por carpeta (versionado, viaja con el repo).

---

## Escaneo de huérfanos

Método: extraer todos los `[[wikilinks]]` de la bóveda (normalizando `#anclas` y `\|alias` escapados en tablas) y comparar contra el listado real de archivos.

**Falsos positivos descartados** (bugs del propio script de escaneo, no de la bóveda):
- `Convenciones C#` — el `#` se interpretaba como ancla de sección.
- `Recuperacion de Contrasenia con Supabase OTP` — el `\|` escapado dentro de una tabla markdown rompía la extracción.

**Huérfanos reales encontrados y conectados:**
- [[Detector-de-Conexion]] — no tenía ningún enlace entrante. Conectado desde [[Arquitectura Actual]] (tabla "Patrones implementados") y se le agregó sección `## Relaciones`.
- [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]] — no tenía ningún enlace entrante. Conectado desde [[Deuda Técnica - Pendientes]] (callout nuevo "Pendiente de diseño").

**Limpieza:**
- `_archivo/Caso 02 - Buscador Universal (duplicado raiz).md` — archivo de **0 bytes**. Eliminado junto con la carpeta `_archivo/` (quedó vacía).

**Decisión — huérfanos aceptados (no son un defecto):**
17 notas de `70 - Bitácora de Cambios/` sin enlace entrante individual. Es el comportamiento esperado de un log cronológico: el MOC enlaza solo la sesión más reciente, el resto se descubre navegando por carpeta/fecha, no por grafo. Con el coloreado por carpeta (ver abajo) igual se ven agrupadas visualmente, no como puntos sueltos.

---

## Coloreado del grafo por carpeta

Usé el skill `graph-colorize` como base mecánica (paleta, backup antes de escribir, merge sin tocar el resto de `graph.json`), pero **adaptado**: el skill trae una taxonomía genérica (`concepts/entities/skills/...`) que no es la de este vault. Se coloreó por las carpetas reales:

| Carpeta | Color |
|---|---|
| `00 - MOC` | azul |
| `10 - Arquitectura` | naranja |
| `20 - Patrones` | rojo |
| `30 - Casos de Uso` | teal |
| `40 - Proyecto Bimbo` | verde |
| `45 - Decisiones` | amarillo |
| `50 - Referencia` | morado |
| `70 - Bitácora de Cambios` | rosa |
| `_templates` | gris |

Backup previo: `.obsidian/graph.json.backup-20260723-2051` (gitignoreado, no se versiona).

**Por qué lo verán los demás usuarios:** `.obsidian/graph.json` se versiona a propósito (solo `workspace.json`/`cache` quedan gitignoreados por ser estado de ventana/caché local). Cualquiera que clone el repo y abra `contexto/` como bóveda ve el grafo coloreado sin configurar nada.

**Por qué otros agentes lo van a mantener bien:** documenté la convención (tabla de colores + cómo agregar una carpeta nueva) en [[AGENTS]] de la bóveda, sección "Colores del grafo". Es edición manual de una línea JSON — no depende del skill de Claude.

---

## Relaciones

- [[AGENTS]] — protocolo actualizado con la sección de colores del grafo
- [[Arquitectura Actual]] — recibió el enlace a Detector-de-Conexion
- [[Deuda Técnica - Pendientes]] — recibió el enlace al pendiente de diseño
- [[Sesión 2026-07-23 - Plan Preparar Bóveda Multi-Agente (AGENTS.md)]] — trabajo previo del mismo día
