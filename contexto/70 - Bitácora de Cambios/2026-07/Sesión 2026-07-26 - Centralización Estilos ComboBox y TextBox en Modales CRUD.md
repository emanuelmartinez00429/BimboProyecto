---
title: "Sesión 2026-07-26 — Centralización Estilos ComboBox y TextBox en Modales CRUD"
tags:
  - sesion
  - refactor
  - estilos
date: 2026-07-26
branch: main
autor_cambios: opencode (gentle-orchestrator)
---

# Sesión 2026-07-26 — Centralización Estilos ComboBox y TextBox en Modales CRUD

> [!success] Resultado
> Se centralizaron los estilos `ModalCombo` y `ModalInput` en `Resources/Styles.xaml` y se eliminaron las 9 definiciones locales duplicadas de los 6 modales CRUD, unificando FontSize, Height, BorderBrush y FocusVisualStyle en una sola fuente de verdad.

---

## Problema / motivo

El formulario de productos (`ProductoModal.xaml`) definía un estilo `ModalCombo` con FontSize=13.5. Al revisar el resto de modales CRUD se encontró:

- **ModalCombo inconsistente**: `FabricanteModal` usaba FontSize=15 vs ProductoModal y UsuarioModal que usaban 13.5
- **ModalInput igualmente fragmentado**: FontSize 13.5 en Producto/Usuario/Proveedor, pero FontSize 15 en Fabricante/Categoria/Empleado
- **Sin fuente de verdad única**: cada modal redefinía los mismos estilos con el mismo Template, duplicando ~35 líneas por archivo
- **Riesgo de deriva**: cualquier modal nuevo copiaría otro existente y podría heredar valores incorrectos

## Cambios aplicados

### `CapaUI/Resources/Styles.xaml`
- Se agregaron dos nuevos estilos globales: `ModalCombo` (TargetType="ComboBox") y `ModalInput` (TargetType="TextBox")
- Ambos usan FontSize=13.5, Height=34, BorderBrush=#7FFFFFFF, Foreground=#1A1F2E
- `ModalInput` incluye Template con CornerRadius=4 y trigger de foco a borde blanco (idéntico al que estaba duplicado en cada modal)

### Modales que perdieron definiciones locales

| Archivo | Eliminado |
|---|---|
| `CapaUI/.../Productos/ProductoModal.xaml` | `ModalCombo` + `ModalInput` |
| `CapaUI/.../Usuarios/UsuarioModal.xaml` | `ModalCombo` + `ModalInput` |
| `CapaUI/.../Fabricantes/FabricanteModal.xaml` | `ModalCombo` (FontSize=15 → hereda 13.5) + `ModalInput` (FontSize=15 → hereda 13.5) |
| `CapaUI/.../Categorias/CategoriaModal.xaml` | `ModalInput` (FontSize=15 → hereda 13.5) |
| `CapaUI/.../Proveedores/ProveedorModal.xaml` | `ModalInput` (coincidía, pero duplicado innecesario) |
| `CapaUI/.../Empleados/EmpleadoModal.xaml` | `ModalInput` (FontSize=15 → hereda 13.5) |

El mecanismo de resolución de WPF busca `StaticResource` primero en `UserControl.Resources`, luego en `Application.Resources` (donde está `Styles.xaml` vía merged dictionary en `App.xaml`). Al eliminar las definiciones locales, la resolución cae en los globales automáticamente.

### Lo que NO se tocó
- **Toolbars** (ComboBox inline en ProductosView, UsuariosView, FabricantesView, BitacoraView): 26px, borde #CBD5E1, contexto visual diferente (fondo blanco, compactos). Siguen siendo inline.
- **Pesaje modals** (PesajeModalStyles.xaml → `MCombo`): 36px, BorderBrush=#80FFFFFF, Cursor=Hand. Contexto visual diferente, intencional.
- `ModalSegBtn` (RadioButton): consistente en todos los modales, no se tocó.
- Iconos geométricos (`IconBoxM`, `IconUserM`, etc.): propios de cada modal, no se tocaron.

## Verificación

```
dotnet build BimboProyecto.sln → 0 errores, 47 warnings (solo los preexistentes de nullable en CapaDatos)
```

No hay harness de tests de UI; la verificación es build + prueba visual manual del flujo de creación/edición en cada modal que usa ComboBox (Producto, Usuario, Fabricante).

---

## Relaciones

- [[Arquitectura Actual]]
- [[CLAUDE]]
- [[Convenciones C#]]
