---
title: "Sesión 2026-07-26 — Refactor Visual de ComboBox y TextBox (Modales + Toolbars)"
tags:
  - sesion
  - refactor
  - estilos
date: 2026-07-26
branch: main
autor_cambios: opencode (gentle-orchestrator)
revisor: Emanuel Castellanos
---

# Sesión 2026-07-26 — Refactor Visual de ComboBox y TextBox (Modales + Toolbars)

> [!success] Resultado
> Ronda 1: Centralizados estilos `ModalCombo`/`ModalInput` en `Resources/Styles.xaml`, eliminadas 9 definiciones locales duplicadas en 6 modales CRUD.  
> Ronda 2: Homogeneizados ComboBox de toolbar en UsuariosView y BitacoraView agregando `IsEditable=True` para que tengan el mismo fondo blanco que ProductosView y FabricantesView.

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

### Ronda 2 — Toolbar ComboBox: `IsEditable=True` para fondo blanco

Los ComboBox de filtro en `UsuariosView` y `BitacoraView` se veían con fondo gris nativo de WPF porque **no tenían `IsEditable="True"`**. En cambio, `ProductosView` y `FabricantesView` sí lo tenían y se veían blancos.

Archivos modificados:

| Archivo | ComboBox | Propiedades |
|---|---|---|
| `CapaUI/.../Usuarios/UsuariosView.xaml` | `CmbRol` | `IsEditable="True" IsTextSearchEnabled="True" StaysOpenOnEdit="True"` |
| `CapaUI/.../Bitacora/BitacoraView.xaml` | `CmbModulo` | ídem |
| `CapaUI/.../Bitacora/BitacoraView.xaml` | `CmbAccion` | ídem |
| `CapaUI/.../Bitacora/BitacoraView.xaml` | `CmbUsuario` | ídem |

No se agregó `DisplayMemberPath` porque estos ComboBox se llenan con `ComboBoxItem` desde code-behind (el `Content` se muestra automáticamente).

### Ronda 3 — `IsTextSearchEnabled=True` para búsqueda escribiendo

Se activó `IsTextSearchEnabled="True"` en los ComboBox editables de UsuariosView y BitacoraView. Esto permite que al escribir en el ComboBox, WPF busque automáticamente la coincidencia más cercana en la lista desplegable y la seleccione — funciona contra la propiedad `Content` de los `ComboBoxItem`.

Propiedad nativa de WPF: `ComboBox.IsTextSearchEnabled` — cuando está en `True` (default), el control busca el primer elemento cuyo texto empiece con lo que el usuario teclea. Combinado con `IsEditable=True` y `StaysOpenOnEdit=True`, la experiencia es tipo autocomplete: el dropdown se mantiene abierto mientras se escribe y el item más cercano se resalta/selecciona automáticamente.

### Lo que NO se tocó (Ronda 2)
- **Pesaje modals** (PesajeModalStyles.xaml → `MCombo`): 36px, BorderBrush=#80FFFFFF, Cursor=Hand. Contexto diferente, intencional.
- **FabricantesView**: ya tenía `IsEditable=True` (idéntico a ProductosView).
- `ModalSegBtn` (RadioButton): consistente en todos los modales, no se tocó.
- Iconos geométricos (`IconBoxM`, `IconUserM`, etc.): propios de cada modal, no se tocaron.

## Verificación

```
dotnet build BimboProyecto.sln → 0 errores (warnings preexistentes de nullable en CapaDatos)
```

No hay harness de tests de UI; la verificación es build + prueba visual manual: al escribir en los ComboBox de filtro (Usuarios → Rol; Bitácora → Módulo, Acción, Usuario), el dropdown debe mostrar el item más cercano al texto escrito.

---

## Relaciones

- [[Arquitectura Actual]]
- [[CLAUDE]]
- [[Convenciones C#]]
