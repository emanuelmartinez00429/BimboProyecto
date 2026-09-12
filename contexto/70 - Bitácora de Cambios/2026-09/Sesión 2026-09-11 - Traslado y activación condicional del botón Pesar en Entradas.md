---
title: "Sesión 2026-09-11 — Traslado y activación condicional del botón Pesar en Entradas"
date: 2026-09-11
tags:
  - bitacora
  - sesion
  - wpf
  - ui
  - pesajes
  - ux
aliases:
  - Traslado botón Pesar a Entradas
  - Lógica condicional de pesaje
---

# Sesión 2026-09-11 — Traslado y activación condicional del botón Pesar en Entradas

## Resumen

Se reubicó el botón **«Pesar»** (`BtnProdPesar`) desde el panel de *Movimiento de Materia Prima* hacia el panel de *Entradas de M. Prima*, posicionándose como el primer botón de acción del pie de tabla (`[ Pesar ]` `[ Tara extra ]` `[ Editar ]`). Se implementó la regla de negocio que restringe su activación exclusivamente cuando el filtro está fijado en **«Producto actual»** y existe un producto seleccionado directamente cuyo estado esté abierto (y el camión no esté cerrado). En cualquier otra circunstancia (como en la vista «Todo el camión» o cuando no hay ningún producto seleccionado), el botón permanece deshabilitado en tono gris con un `ToolTip` contextual explicativo gracias a `ToolTipService.ShowOnDisabled="True"`.

## Intervenciones Realizadas

1. **Reorganización en XAML (`PesajeView.xaml`):**
   - Se removió `BtnProdPesar` del `WrapPanel` de *Movimiento de Materia Prima*. La barra del panel izquierdo ahora contiene únicamente `BtnProdAgregar` («Agregar»), `BtnProdEditar` («Editar») y `BtnProdCerrar` («Cerrar / Reabrir producto»).
   - Se insertó `BtnProdPesar` al inicio del `WrapPanel` de *Entradas de M. Prima*, antes de `BtnProdTaraExtra` y `BtnEntEditar`.
   - Se agregó la propiedad adjunta `ToolTipService.ShowOnDisabled="True"` para habilitar la inspección del motivo de inactividad al pasar el cursor sobre el botón deshabilitado.

2. **Sincronización en ViewModel (`PesajeViewModel.cs`):**
   - En el método `SeleccionarProducto(ProductoCamion? p)`, al seleccionar un producto (`p is not null`), se conmuta de forma automática `VistaEntradas = "producto"`. Esto asegura que al elegir un producto en la grilla izquierda, la vista de entradas filtre inmediatamente sus registros y reactive «Pesar» de forma natural.

3. **Lógica de Activación y Feedback Dinámico (`PesajeView.xaml.cs`):**
   - En `ActualizarUI()`, se actualizó la condición de habilitación:
     ```csharp
     bool puedePesar = porProducto && prodAbierto && !cerrado;
     BtnProdPesar.IsEnabled = puedePesar;
     ```
   - Se implementó la asignación dinámica de `ToolTip` en función del estado:
     - Sin camión seleccionado: *"Selecciona un camión para comenzar a pesar"*
     - Sin producto seleccionado: *"Selecciona un producto de la tabla para registrar una pesada"*
     - En vista «Todo el camión»: *"Cambia a la vista «Producto actual» para registrar una pesada"*
     - Con producto cerrado: *"El producto seleccionado está cerrado; reábrelo para seguir pesando"*
     - Con camión cerrado: *"El camión está cerrado"*
     - Activo: *"Registrar pesaje para {NombreProducto}"*
   - En `BtnProdPesar_Click`, se añadió la guarda `_vm.ModoEfectivo == "producto"` para garantizar consistencia operativa ante eventos de teclado o accesos directos.

## Verificación

- **Compilación de la solución:** `dotnet build BimboProyecto.sln` completado con 0 errores y 0 advertencias.
- **Suite de pruebas unitarias:** `dotnet test BimboProyecto.Tests.csproj` completado con 424/424 pruebas pasando exitosamente (100% de éxito).
