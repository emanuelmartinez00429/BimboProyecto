---
title: "Sesión 2026-09-07 — Borrados auditados por fila y estados honestos en Pesaje"
tags:
  - sesion
  - pesaje
  - ux
  - wpf
  - auditoria
date: 2026-09-07
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
revisor: Fernando (prueba visual aprobada)
---

# Sesión 2026-09-07 — Borrados auditados por fila y estados honestos en Pesaje

> [!success] Resultado
> Los borrados de producto y pesaje pasaron a RPC con bitácora (eran `Update` crudos), la acción de quitar se movió a un basurero en cada fila con sus reglas de bloqueo, y se corrigió un desfase entre lo que la tabla de entradas mostraba y lo que su encabezado decía. Además: numerador en vez de ID, estado vacío con icono propio, y el login dejó de sacar barra de scroll al mostrar un error.

---

## 1. Los borrados ahora se auditan

Fernando preguntó si los borrados ya usaban RPC. La respuesta era **no**, y solo parcialmente:

| Qué | Antes | Ahora |
|---|---|---|
| Camión | RPC `cambiar_estado_movimiento_pesaje_tabla_bitacora` | igual |
| Producto | `Update` crudo de PostgREST, sin bitácora | RPC `cambiar_estado_producto_pesaje_tabla_bitacora` |
| Pesaje | `Update` crudo de PostgREST, sin bitácora | RPC `cancelar_entrada_producto_pesaje_tabla_bitacora` |

Los dos RPC ya estaban desplegados desde [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] y **nunca se habían llamado desde C#**. No hizo falta ninguna migración.

Vale aclararlo porque el nombre engaña: **ninguno es un `DELETE`**. Los tres son anulación por estado (`id_estado = 9`); la fila y su historial quedan en la base.

Efecto secundario que salió gratis: `cambiar_estado_producto_pesaje_tabla_bitacora` **ya traía el guard** «No se puede anular un producto con pesajes activos», así que esa regla ahora la exige el servidor y no solo la pantalla.

> [!note] Las notificaciones ya estaban bien
> El pedido era que los borrados quedaran en bitácora **sin** generar notificación (son muchos y agobian la campana). No hubo que tocar nada: `private.bitacora_pesaje` solo crea notificación cuando el campo es «Registro de pesaje», y los borrados escriben «Estado del pesaje» y «Estado de producto».

---

## 2. Quitar vive en la fila, no en la barra

El basurero se agregó al final de cada fila en **Movimiento de Materia Prima** y en **Entradas de M. Prima**, y se eliminó el botón «Quitar» de la barra de entradas. El basurero del `ProductosCargaModal` **queda como estaba** — ahí la baja es parte del lote que se guarda al Finalizar, no una acción inmediata.

Reglas de bloqueo (botón gris + `ToolTipService.ShowOnDisabled` explicando el motivo):

- **Producto**: no se quita si tiene pesajes registrados. Exigida también en el servidor.
- **Camión**: no se quita si tiene productos agregados. **Esta NO la exige el servidor** — la RPC de anular recepción no mira los productos —, así que hay guard en el handler además del botón gris. Cerrarla en la base implicaría reemplazar `private.ejecutar_rpc_pesaje`, la función compartida de todo el módulo; quedó sin hacer a propósito.

### El conteo que no existía

`CamionPesaje.Productos` **solo se llena para el camión seleccionado** (`CargarProductosAsync` corre sobre uno). Un `Productos.Count == 0` habría mostrado el basurero habilitado en todos los demás camiones, incluidos los que sí tienen carga — es decir, la regla se veía aplicada pero no lo estaba.

Se agregó `ContarProductosPorCamionAsync`: **una** consulta para toda la lista (no una por camión), que llena `CamionPesaje.ProductosEnBase`. Ese es el valor que gobierna el basurero. Al cargar los productos del camión seleccionado se sincroniza con el dato exacto que ya está en memoria, sin volver a consultar.

**Confirmación:** los tres borrados usan `MessageBox` nativo con «No» por defecto, decisión de Fernando — el `Popup` anclado al botón quedaba flotando torcido sobre el panel de al lado. `PedirConfirmacion` sigue existiendo, pero ya solo lo usa «Cerrar todos los camiones», donde el botón está en el pie y tiene lugar para desplegarse.

---

## 3. La tabla de entradas decía una cosa y mostraba otra

Fernando notó que el rótulo del producto no aparecía aunque la tabla ya estuviera mostrando pesadas. El rótulo no era el problema:

`ModoEfectivo` cae a `"camion"` cuando no hay producto seleccionado, así que la tabla mostraba las pesadas de **todo el camión** mientras el toggle seguía marcando «Producto actual». Con un solo producto las dos vistas se ven idénticas, y por eso la contradicción venía pasando desapercibida.

Se arregló haciendo que el encabezado describa siempre lo que hay en pantalla, **sin cambiar qué datos se muestran**:

| Situación | Rótulo | Toggle |
|---|---|---|
| Con producto seleccionado | `· NOMBRE DEL PRODUCTO` | Producto actual |
| Sin producto seleccionado | `· Todo el camión` | Todo el camión (se sincroniza solo) |

Y «Producto actual» queda deshabilitado mientras no haya producto: antes se podía elegir y no filtraba nada. Sincronizar el `RadioButton` por código vuelve a disparar `RbVista_Changed`, así que lleva la guarda `_sync` que ya usa el resto de la pantalla — sin ella, sincronizar pisaba la preferencia del usuario.

> Se descartó auto-seleccionar el primer producto: haría que al abrir un camión se vean solo las pesadas del producto #1 en vez del panorama completo.

---

## 4. Resto de la tanda

- **Numerador en vez de ID** en la tabla de entradas: `#` con la misma `PlantillaCeldaNumeroFila` de las grillas CRUD. El `NumeroFilaConverter` ya toleraba tablas sin paginación (cae a página 1), así que no hubo que tocarlo. El `id_pesaje` sigue en bitácora y en el reporte, que es donde hace falta.
- **Doble clic corregido**: sobre un producto abre la gestión de la carga (antes abría el de pesar, que ya tiene su propio botón); sobre un pesaje abre su edición, que antes no tenía doble clic.
- **Teclado**: `Enter` abre el mismo modal que el doble clic en las tres tablas (camiones, productos, entradas), igual que en las grillas CRUD. Las flechas ya las movía el propio control.
- **Estado vacío de entradas**: pasa a usar `EmptyStateOverlay` —el componente que ya usaba Productos— al que se le agregó la propiedad **`Icono`** (con el actual de default, así Productos no cambia). En Pesaje usa la balanza: acá el vacío no es «no hay datos», es «todavía no empezaste a pesar». La grilla ya **no se oculta**: el overlay va encima y los encabezados quedan a la vista, que es el patrón de [[Empty State en DataGrid - Overlay centrado con encabezados visibles]].
- **Login**: al mostrarse el error aparecía una barra de scroll pegada al borde, sin recorrido útil. Ahora `VerticalScrollBarVisibility="Disabled"`, el `ErrorContainer` pasó a `Hidden` (reserva su lugar desde que abre la ventana, así el formulario no salta) y se liberaron 24px de los márgenes verticales del panel (`44/24` → `28/16`) para que todo entre sin tocar el tamaño de la ventana.

---

## Verificación

```bash
dotnet build BimboProyecto.sln --no-incremental
dotnet test BimboProyecto.sln
```
0 errores · 286/286. Prueba visual aprobada por Fernando.

---

## Relaciones

- [[Módulo Pesaje]]
- [[Sesión 2026-09-07 - Productos de la carga en tabla, guardado en lote y marcadores centralizados]] — sesión anterior del mismo módulo
- [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] — origen de los dos RPC que acá se conectaron
- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]]
