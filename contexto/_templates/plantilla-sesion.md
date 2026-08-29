---
title: "Sesión AAAA-MM-DD — Título descriptivo"
type: sesion
status: vigente
tags:
  - sesion
date: AAAA-MM-DD
updated: AAAA-MM-DD
summary: "Una frase declarativa: qué se logró en esta sesión."
scope:
  - CapaUI/ruta/que/tocaste
symbols:
  - ClaseQueTocaste
branch: nombre-de-la-rama
autor_cambios: TuNombre (agente)
# revisor: TuNombre   ← solo si es una revisión QA
---

# Sesión AAAA-MM-DD — Título descriptivo

> [!success] Resultado
> Una o dos frases: qué se logró. **Esto es lo que sale en el índice** — escribilo bien.

---

## Problema / motivo

Qué se buscaba resolver.

## Cambios aplicados

Archivo por archivo o por área. Incluí rutas relativas (`CapaUI/…`) y, si aplica, el porqué.

## Verificación

Cómo se comprobó (`dotnet build …` → N errores; prueba manual; etc.).

## Lo que NO cambió

Para acotar el alcance y evitar suposiciones del próximo lector.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Deuda Técnica - Pendientes]] — si generaste ítems P-NNN
