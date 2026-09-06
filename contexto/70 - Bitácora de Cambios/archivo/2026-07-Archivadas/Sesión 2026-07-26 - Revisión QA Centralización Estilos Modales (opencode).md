---
title: "Sesión 2026-07-26 — Revisión QA Centralización Estilos Modales (opencode)"
tags:
  - sesion
  - qa
  - revision
  - estilos
date: 2026-07-26
branch: feat/fase7-GestióndeUsuarios
autor_cambios: opencode (gentle-orchestrator)
revisor: Claude Sonnet 5
---

# Sesión 2026-07-26 — Revisión QA: Centralización Estilos Modales (opencode)

Revisión del commit `e059c68 *opencode fixes en UI`, que centraliza `ModalInput`/`ModalCombo` en `CapaUI/Resources/Styles.xaml` eliminando 9 definiciones locales duplicadas en 6 modales CRUD. Documentado por opencode en [[Sesión 2026-07-26 - Centralización Estilos ComboBox y TextBox en Modales CRUD]].

## Veredicto: ✅ Aprobado, con una observación menor (no bloqueante)

## Qué se verificó

1. **Mecanismo de resolución de recursos** — correcto. `App.xaml:8` ya mergea `Resources/Styles.xaml` a nivel `Application`; WPF resuelve `StaticResource` buscando primero en el árbol lógico local y cayendo a `Application.Resources` si no lo encuentra ahí. Al borrar las definiciones locales, la resolución cae en la global sin romper nada.
2. **Ningún `StaticResource` quedó huérfano** — verifiqué el uso real de `ModalCombo`/`ModalInput` en los 6 modales (`grep` de `Style="{StaticResource ...}"`) contra lo que cada uno tenía definido localmente antes del cambio. Coincide exactamente con la tabla que documentó opencode: Fabricante/Producto/Usuario usan `ModalCombo` (lo tenían definido, ahora lo heredan); Categoria/Empleado/Proveedor solo usan `ModalInput` (no tienen ComboBox, consistente).
3. **Comparación propiedad por propiedad** — tomé el `ModalInput`/`ModalCombo` original de `FabricanteModal.xaml` (el que tenía más diferencias, FontSize=15) contra el centralizado. **Única diferencia real: `FontSize` 15 → 13.5.** Todo lo demás (Height, BorderBrush, Background, Template, trigger de foco) es idéntico.
4. **Build** — `dotnet build --no-incremental`: los "6 errores" que vi al principio eran `MSB3027`/`MSB3021` (archivo bloqueado por un proceso `CapaUI` en ejecución — la app corriendo en ese momento, probablemente para prueba visual). **Cero errores `CS`/`MC` reales.** 46 advertencias únicas, todas preexistentes (nullable en modelos de `CapaDatos`), ninguna nueva. El reporte de opencode ("0 errores, 47 warnings") es correcto.

## La única observación

opencode centralizó un valor donde había un **empate real 3-contra-3**: Producto/Proveedor/Usuario usaban FontSize 13.5, Fabricante/Categoria/Empleado usaban 15. Eligió 13.5 y lo documentó honestamente en su nota de sesión (a favor: la transparencia es exactamente lo que pide el protocolo de la bóveda) — pero **decidió el desempate sin preguntar**, y esto cambia visualmente 3 pantallas (Fabricante, Categoria, Empleado) un 10% más chico de lo que tenían.

No es un bug: no hay inconsistencia técnica, y 1.5px es sutil. Pero es exactamente el tipo de decisión de diseño/UX que conviene confirmar con la persona a cargo antes de commitear, no después — porque a diferencia de un refactor de código puro, un cambio de tamaño de fuente es una decisión visual que el usuario podría no compartir.

## Para opencode — guardar en memoria

> **Al centralizar/deduplicar estilos que tienen valores DIVERGENTES entre archivos (no solo duplicados idénticos), preguntar por cuál valor usar como estándar ANTES de commitear — especialmente en propiedades visuales (tamaños, colores, espaciados) — en vez de decidir el desempate y solo documentarlo después.**
>
> Por qué: centralizar código duplicado es refactor seguro (comportamiento idéntico garantizado). Centralizar valores que YA diferían entre archivos implica elegir cuál de los comportamientos existentes se descarta — eso es una decisión de producto/diseño, no solo de arquitectura, y el usuario podría preferir el otro valor o uno nuevo.
>
> Cómo aplicar: si al auditar duplicados encontrás que no son idénticos (como este caso: 3 archivos con 13.5, 3 con 15), pausar y preguntar "encontré esta discrepancia, ¿cuál va?" en vez de resolver el empate por criterio propio y avisar en la nota de sesión.

## Qué no hace falta arreglar

Nada bloqueante. Si Fernando ve las pantallas de Fabricante/Categoría/Empleado y el tamaño 13.5 le resulta bien, no hay nada más que tocar — el refactor queda como está.

## Relaciones

- [[Sesión 2026-07-26 - Centralización Estilos ComboBox y TextBox en Modales CRUD]] — sesión original de opencode
- [[Convenciones C#]]
- [[Arquitectura Actual]]
