# ADR-003 — Disolución de CapaServicios en CapaDominio

**Fecha:** 2026-05-29  
**Estado:** Aceptado e implementado

---

## Contexto

`CapaServicios` era un proyecto separado que contenía 4 clases: `SesionActual`, `servicioSesionActual`, `PesoCalculator` y `ServicioBuscador`. Al auditar la arquitectura se encontró que:

1. Las 4 clases ya estaban en el namespace `CapaDominio` — vivían en el proyecto equivocado desde el origen.
2. `CapaUI` tenía una referencia directa a `CapaServicios`, creando una capa extra en el grafo de dependencias sin valor real.
3. `CapaServicios` no tenía lógica propia: era un contenedor de clases que pertenecían a otro proyecto.

---

## Opciones consideradas

1. **Mantener CapaServicios** — sin cambios. Deuda técnica acumulada.
2. **Mover a CapaAplicacion** — incorrecto: son entidades de dominio puro, no lógica de aplicación.
3. **Mover físicamente a CapaDominio** — correcto: sus namespaces ya decían `CapaDominio`. Solo hay que mover los archivos y eliminar el proyecto.

---

## Decisión

**Opción 3: absorber en CapaDominio.**

Las 4 clases se movieron al proyecto `CapaDominio`. La referencia de `CapaUI` a `CapaServicios` fue eliminada. El proyecto `CapaServicios` fue removido de la solución.

---

## Consecuencias

- **Positivo:** un proyecto menos en la solución.
- **Positivo:** el grafo de dependencias es más limpio: `CapaUI → CapaDominio` ya existía.
- **Positivo:** las clases viven donde su namespace siempre dijo que vivían.
- **Negativo:** ninguno relevante — el cambio fue puramente estructural sin impacto en runtime.

---

## Regla resultante

> Si una clase tiene namespace `CapaDominio`, debe vivir físicamente en el proyecto `CapaDominio`. No crear proyectos intermedios para alojar clases que pertenecen a una capa ya existente.
