---
title: "Sesión 2026-09-06 — Enter abre el modal de edición en las grillas CRUD"
tags:
  - sesion
  - ux
  - wpf
date: 2026-09-06
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
revisor: Claude Fernando (agente)
---

# Sesión 2026-09-06 — Enter abre el modal de edición en las grillas CRUD

> [!success] Resultado
> `Enter`, con una fila seleccionada y el foco en la grilla, ahora abre el modal de edición en las 9 pantallas CRUD estándar — el mismo efecto que ya tenía el doble clic. Antes `Enter` solo disparaba la navegación de celda por defecto de WPF (mover el "cursor" de celda una fila hacia abajo), sin ningún efecto útil porque las grillas son `IsReadOnly="True"`.

---

## Origen

El usuario mostró una captura de la grilla de Proveedores y preguntó si el salto de foco de celda a celda al presionar Enter era el comportamiento esperado. No lo era — es el default nativo de `DataGrid` en WPF (nadie lo programó a propósito) y no hacía nada útil en una grilla de solo lectura. El doble clic sí tenía una acción real (abrir el modal de edición), así que Enter quedaba inconsistente con el mouse. Pidió el fix.

---

## Cambio

En cada `DataGrid` de las 9 pantallas CRUD (11 grillas contando las dos vistas de Contactos, que tienen una grilla maestra + una de detalle), se agregó `PreviewKeyDown` con un handler que:

1. Si la tecla no es `Enter`, no hace nada (deja pasar la navegación nativa para las demás teclas — flechas, Tab, etc. siguen intactas).
2. Si es `Enter` y hay una fila seleccionada, marca `e.Handled = true` (evita el salto de celda nativo) y llama exactamente al mismo método que ya usa el doble clic.

No se tocó el mecanismo de doble clic ni se centralizó en una clase base — cada `View` sigue siendo autónoma, siguiendo el patrón ya establecido en el repo (cada pantalla CRUD es independiente, sin una base compartida para code-behind de grillas).

### Archivos tocados

| Pantalla | Grilla(s) | Acción que reutiliza |
|---|---|---|
| Proveedores | `DgProveedores` | `AbrirModalEditar(_vm.Seleccionado)` |
| Categorías | `DgCategorias` | `AbrirModalEditar(_vm.Seleccionado)` |
| Fabricantes | `DgFabricantes` | `AbrirModalEditar(_vm.Seleccionado)` |
| Presentaciones | `DgPresentaciones` | `AbrirModalEditar(_vm.Seleccionado)` |
| Empleados | `DgEmpleados` | `AbrirModalEditar(_vm.Seleccionado)` |
| Productos | `DgProductos` | `AbrirModalEditar(_vm.Seleccionado)` |
| Usuarios | `DgUsuarios` | `_vm.EditarCommand.Execute(null)` (este módulo ya editaba vía comando, no llamada directa) |
| Contactos de Fabricantes | `DgFabricantes` (maestra) | `_vm.AbrirFabricanteAsync(fab)` |
| | `DgContactos` (detalle) | `AbrirModalEditar(_vm.ContactoSeleccionado)` |
| Contactos de Proveedores | `DgProveedores` (maestra) | `_vm.AbrirProveedorAsync(prov)` |
| | `DgContactos` (detalle) | `AbrirModalEditar(_vm.ContactoSeleccionado)` |

**Bitácora se dejó afuera a propósito**: no tiene modal de edición (es un log de auditoría, de solo lectura por diseño) — no hay ninguna acción sensata que Enter deba disparar ahí.

---

## Por qué `PreviewKeyDown` y no `KeyDown`

`PreviewKeyDown` es un evento de *tunneling* (baja de la raíz hacia la hoja) y llega **antes** que el manejo interno del `DataGrid` procese la tecla. Si se usara `KeyDown` (evento de *bubbling*), el `DataGrid` ya habría movido la celda internamente antes de que el handler corriera — habría que revertir ese movimiento además de abrir el modal. Con `PreviewKeyDown` y `e.Handled = true`, el salto de celda nunca llega a ocurrir.

---

## Verificación

```bash
dotnet build BimboProyecto.sln --no-incremental
dotnet test BimboProyecto.sln
```
0 errores, 286/286 (sin tests nuevos — es un cambio de UI de code-behind, no hay lógica de negocio que testear unitariamente).

**Pendiente de prueba visual/manual** (según protocolo del proyecto): confirmar en cada una de las 9 pantallas que Enter con una fila resaltada abre el modal correcto, y que las flechas arriba/abajo y Tab siguen navegando la grilla con normalidad (no deberían verse afectadas — el handler solo intercepta `Key.Enter`).

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — mejora de UX, no ligada a un ítem de deuda existente
- [[Anatomía compartida de los modales]]
- [[Sesión 2026-09-06 - Auditoría de commits a05f006 y 404796c]] — donde se revisó el mismo patrón de teclado en `SelectorCatalogoModal`
