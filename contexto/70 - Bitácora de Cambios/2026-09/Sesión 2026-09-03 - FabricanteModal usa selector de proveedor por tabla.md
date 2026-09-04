---
title: "Sesión 2026-09-03 — FabricanteModal usa el selector de proveedor por tabla"
tags:
  - sesion
  - ui
  - wpf
  - catalogos
  - selector
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando
---

# Sesión 2026-09-03 — FabricanteModal usa el selector de proveedor por tabla

> [!success] Resultado
> En el modal de fabricantes, el campo **Proveedor** dejó de ser un `ComboBox`
> cargado eager (todos los proveedores de una) y pasó al patrón lupa + tabla
> paginada con buscador, hospedando `SelectorCatalogoModal` — el mismo que usan
> ProductoModal y CamionModal. País se mantiene como `ComboBox` (catálogo chico).

---

## Problema

`FabricanteModal` cargaba todos los proveedores en `CmbProveedor` al abrir
(`_repo.GetProveedoresAsync()` → `foreach` → `ComboBoxItem`). Con muchos
proveedores el combo es inmanejable y desalineado con el resto de los modales,
que ya eligen catálogos por tabla.

## Cambios

Reutiliza el patrón existente ([[Selector de Catálogo - Selector genérico y multiselección]]),
sin código nuevo de infraestructura:

### `FabricanteModal.xaml`
- El `Grid` raíz pasa a `x:Name="RootGrid"` y el `ScrollViewer` a `x:Name="FormHost"`.
- El campo Proveedor: `ComboBox` → `TextBox` de solo lectura (`x:Name="TxtProveedor"`)
  + botón lupa (`BtnBuscarProveedor`, estilo `LupaBtnOscuro`), con
  `PreviewMouseDoubleClick`/`KeyDown` para abrir con mouse o teclado.
- Nuevo `<ContentControl x:Name="SelectorHost" Visibility="Collapsed"/>` como
  hermano dentro del `RootGrid` — hospeda la tabla "chromeless".

### `FabricanteModal.xaml.cs`
- `_catalogos = App.Services.GetRequiredService<ICatalogoRepository>()` en el ctor
  (igual que ProductoModal/CamionModal; **no** cambia la firma del ctor ni
  `FabricantesView`).
- `BuscarProveedor_Click` → `AbrirSelector(Catalogos.Proveedores(_catalogos), item => { TxtProveedor.Text = item.Nombre; _idProveedor = item.Id; })`.
- `AbrirSelector` / `CerrarSelector`: swap `FormHost` ↔ `SelectorHost`, con
  `RootGrid.Height = 760` mientras la tabla está abierta y `double.NaN` al volver
  (versión ligera del "freeze de altura" de ProductoModal; misma idea que
  `CamionModal`). El selector cierra solo (evento `Cerrado`), el handler solo agrega.
- `TxtCatalogo_PreviewMouseDoubleClick` / `TxtCatalogo_KeyDown` copiados de ProductoModal.
- Edición: prefill con `_fabricante.NombreProveedor` + `_idProveedor = _fabricante.IdProveedor`.
- Guardar: `IdProveedor = _idProveedor` (antes leía `CmbProveedor.SelectedItem`).
- `BtnCerrar_Click`: con el selector abierto, "Volver"/✕ cierra la tabla, no el modal.
- Se quitó la carga eager de proveedores y su rama que deshabilitaba Guardar si
  fallaba (el selector maneja sus propios errores). País conserva esa guarda.

## Comportamiento de paginación / búsqueda

Igual que ProductoModal: `Catalogos.Proveedores` usa los defaults del selector —
modo memoria filtrable hasta 200 registros, y paginación server-side automática
(`PageSize = 15`) por encima. No se fuerza `PageSize = 50`/`ForzarPaginacion`
(eso es solo de Reportería).

## Verificación

- Compilación de `CapaUI` (markup + C#) sin errores propios; el build completo
  quedó bloqueado por la app en ejecución (solo `MSB3021`/`MSB3027` de copia).
- **Pendiente prueba manual** (app cerrada / reiniciada): crear y editar un
  fabricante eligiendo proveedor por la lupa; verificar prefill en edición,
  búsqueda, paginación si hay >200, Escape/Volver, y que Guardar persista el
  `id_proveedor` correcto.

## Pendiente / posible seguimiento

- Si se quiere, el campo **País** puede migrar al mismo patrón (`Catalogos.Paises`),
  pero el catálogo es chico y el usuario solo pidió Proveedor.
- `IFabricanteRepository.GetProveedoresAsync()` puede quedar sin uso; revisar antes
  de retirarlo.

## Relaciones

- [[Selector de Catálogo - Selector genérico y multiselección]] — patrón reutilizado
- [[Anatomía compartida de los modales]] — hosting chromeless / freeze de altura
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]
- [[Módulos de Catálogos Administrativos]]
