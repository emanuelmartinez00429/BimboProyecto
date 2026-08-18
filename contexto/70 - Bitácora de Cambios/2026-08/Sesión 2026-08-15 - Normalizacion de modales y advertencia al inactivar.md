---
title: "Sesión 2026-08-15 — Normalización de modales y advertencia al inactivar"
tags:
  - sesion
  - wpf
  - modales
  - ux
  - refactor
date: 2026-08-15
branch: feat/fase8-MaquetadodeRoles-B-Fernando
autor_cambios: Fernando
---

# Sesión 2026-08-15 — Normalización de modales y advertencia al inactivar

Siete entregables. El pedido original era "que todos los modales queden como el de Productos", pero la exploración mostró que copiar el XAML de referencia a cinco archivos más habría multiplicado un problema que ya existía.

## El hallazgo que definió el enfoque

**Nada estaba centralizado.** `EtiquetaCampo`, `CampoModal` y `ModalInputAuditoria` estaban duplicados en `PresentacionModal` y `ProductoModal` con `Margin` distinto entre sí; `ModalSegBtn` estaba duplicado en **seis** archivos y solo la copia de Presentación tenía trigger de foco; el patrón "relleno + borde verde" estaba clonado en **cinco** lugares; y los títulos usaban seis valores distintos de `FontSize` (19–24). `Styles.xaml` solo aportaba `ModalCombo` y `ModalInput`.

Por eso el orden fue **centralizar primero, migrar después**: así los puntos transversales (foco, tamaño de etiqueta, tooltip) se arreglaron en un solo lugar y se propagaron solos a los siete modales.

## Qué se hizo

1. **Estilos centralizados** en `CapaUI/Resources/Styles.xaml` — contrato completo en [[Anatomia compartida de los modales]]. Se agregó también `ModalPassword`, `ModalTitulo` y `ModalContexto`, y se borraron todos los duplicados locales.

2. **Foco sin relleno verde.** Se eliminó el `Background="#C2F2E0"` de los cinco lugares donde estaba clonado; queda solo el aro `#34D399`. En `ModalSegBtn` el relleno era el único indicador, así que ahí se **reemplazó** por borde en vez de solo quitarlo.

3. **Cinco modales migrados** a etiqueta-arriba: Categoría, Fabricante, Proveedor, Empleado, Usuario. Con `TabIndex` explícito y el hint `Ctrl+Enter para guardar` — que **ya funcionaba** en los cinco vía `AtajoGuardar.Boton`, simplemente no se anunciaba.

4. **Lupa operable por teclado.** Los TextBox de catálogo solo tenían `PreviewMouseLeftButtonUp`; al llegar por Tab, Enter no hacía nada. Se agregó `TxtCatalogo_KeyDown` reusando el mismo mecanismo `Tag` + `RaiseEvent` que ya usaba el mouse.

5. **Tooltip con `…`** donde faltaba. Ver la deuda cerrada abajo.

6. **Renombre `RbHabilitados`/`RbDeshabilitados` → `RbActivos`/`RbInactivos`** en las 5 vistas que faltaban (Productos, Presentaciones, Categorías, Proveedores, Fabricantes), más los miembros del enum `EstadoFilter` en los 5 namespaces. Usuarios y Empleados ya usaban la convención correcta y fueron el modelo. Las etiquetas visibles ya estaban en "Activos/Inactivos" — lo que faltaba era la nomenclatura del código.

7. **Diálogo de confirmación reusable** al inactivar — ver [[Dialogo de confirmacion reusable]]. Conectado en los 7 modales con toggle.

## Bugs reales encontrados y corregidos

**`SelectorCatalogoModal.xaml.cs` — Enter se tragaba sobre los botones del pie.** El `case Key.Enter` del `PreviewKeyDown` marcaba `e.Handled = true` **incondicionalmente**. Como es un handler de tunneling a nivel `UserControl`, veía el Enter de cualquier hijo — incluidos `BtnElegir` y `BtnCerrar`, que por eso nunca recibían su `Click` y solo respondían a Espacio. Ahora `Handled` se marca solo cuando el selector realmente hizo algo, y se agregó un guard `EsBotonDelPie` para que Enter sobre un botón siga su camino normal.

**`UsuarioModal` — dos hijos superpuestos en una celda.** `RowEmpleado` tenía el `ComboBox` y el `TextBlock` del nombre en la misma celda del `Grid`, alternándose según alta/edición. Convertir el `Grid` a `StackPanel` los habría **apilado** (se verían los dos). Se envolvieron en un `Grid` interno. Anotado como advertencia en la nota de patrón, porque le puede pasar a cualquiera que migre un modal.

**`UsuarioModal` — el `PasswordBox` se veía más chico.** Estaba en `Height=34, FontSize=15.5` contra los `38 / 16.5` del resto de los campos, sin ninguna razón. Al pasarlo a `ModalPassword` quedó alineado.

## Deuda cerrada

- `ContactoFabricanteModal` y `ContactoProveedorModal` **redefinían `ModalInput` localmente**, sombreando el compartido: tenían el Setter de `TextoResponsivo` pero su template no incluía `PART_Recorte`, así que mostraban tooltip sin el `…` visual. Se resolvió al borrar el estilo local.
- `SelectorCatalogoModal` tenía `TextTrimming` **sin ToolTip** en las celdas: un nombre largo de catálogo quedaba cortado sin forma de leerlo. Se agregó `TextoResponsivo.ToolTipSiRecorta`, una variante nueva del helper para `TextBlock`.
- El XML-doc de `TextoResponsivo.cs` decía que se activaba "por un `Trigger` sobre `IsReadOnly`", pero es un `Setter` plano y la distinción se resuelve en runtime. Corregido.

## Deuda detectada y NO resuelta

Los modales de la familia Pesaje (`SelectorProductosModal`, `ProcesoDescargaModal`) tienen el mismo defecto de `TextTrimming` sin ToolTip. Quedaron fuera de alcance por decisión explícita: usan su propio diccionario `PesajeModalStyles.xaml` y migrarlos es un trabajo aparte.

## Verificación

`dotnet build BimboProyecto.sln` en **0 errores**. Falta la prueba visual manual de los siete modales.

> [!warning] Build con la app corriendo
> Si Visual Studio tiene la app en depuración, el build falla con `MSB3027`/`MSB3021` por bloqueo de archivo — **no** son errores de código. La compilación de C#/XAML ya pasó cuando aparecen. Cerrar la app antes de compilar.

## Relaciones

- [[Anatomia compartida de los modales]] — el contrato de estilos que salió de acá
- [[Dialogo de confirmacion reusable]] — el componente nuevo
- [[TextoResponsivo]]
- [[Módulo Productos]] · [[Módulo Usuarios]]
