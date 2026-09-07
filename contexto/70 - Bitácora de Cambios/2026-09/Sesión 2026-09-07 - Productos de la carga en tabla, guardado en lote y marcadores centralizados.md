---
title: "Sesión 2026-09-07 — Productos de la carga en tabla, guardado en lote y marcadores centralizados"
tags:
  - sesion
  - pesaje
  - ux
  - wpf
  - supabase
date: 2026-09-07
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
revisor: Fernando (prueba visual aprobada)
---

# Sesión 2026-09-07 — Productos de la carga en tabla, guardado en lote y marcadores centralizados

> [!success] Resultado
> El alta de productos de una recepción pasó de un formulario de **un producto por vez** a una **tabla que gestiona toda la carga** y se guarda con un **RPC transaccional**. De paso se cerró el último camino de escritura sin auditar del módulo, se sacó el parpadeo al entrar a Pesaje, se le puso spinner al alta de camiones y se centralizó el marcador («placeholder») de los campos de texto.

---

## 1. `ProductosCargaModal` — la carga entera en una tabla

**Archivos nuevos:** `CapaUI/.../Pesaje/Modales/ProductosCargaModal.xaml` + `.xaml.cs`
**Eliminado:** `ProductoCamionModal.xaml` / `.xaml.cs`

### Qué estaba mal

`ProductoCamionModal` era un formulario de **un producto**: Proveedor, Producto, Peso, Bultos y Observaciones, con un botón «Guardar y agregar otro» para repetir. Tres problemas concretos:

1. **El pie se pisaba.** Era un `Grid` sin `ColumnDefinitions`, así que `TxtPie` («Se agregará al camión 1213») y la fila de botones compartían la misma celda y se superponían. Solo se veía en modo alta, donde «Guardar y agregar otro» ensancha la fila de botones.
2. **Cada producto era un viaje de red + una recarga completa.** `AgregarProductoAsync` terminaba en `RecargarCamionesAsync()`, que recarga *todos* los camiones con *todos* sus productos. Cinco productos = cinco recargas de la pantalla.
3. **Era el único camino de escritura del módulo sin auditar**: `Insert`/`Update` crudos de PostgREST, sin RPC, sin `p_id_solicitud`, sin bitácora y sin validación en servidor — a diferencia de todo el resto de Pesaje. Además el ViewModel resolvía placa+proveedor y creaba la recepción hermana en el cliente, lógica que ya existía —atómica y con lock— en un RPC desplegado desde 2026-08-24 que nunca se llamó desde C#.

### Qué se hizo

Una tabla con la carga completa de la recepción seleccionada: los productos que ya tiene (peso, bultos y observaciones editables en la fila) más los que se agreguen, marcados con una barrita verde mientras no estén guardados. Columnas `CÓDIGO · PRODUCTO · PESO MAN. (KG) · BULTOS · OBSERVACIONES · basurero`.

Decisiones que tomó Fernando durante el diseño:

- **El proveedor es contexto, no campo.** La recepción (placa + proveedor) ya viene elegida de la tabla de camiones de la pantalla, así que arriba hay una franja de solo lectura con ambos datos (estilo `MInputDisplay`, que existe justamente para eso). Se pierde el atajo de redirigir un producto a otro proveedor desde el modal: para eso se selecciona la otra recepción en la lista.
- **Sin nada del wizard viejo**: no hay chip de paso, ni «Paso 2 de 2», ni botón «Atrás». El pie es `Cancelar` + `✓ Finalizar`, y usa `DockPanel LastChildFill="False"` — que es lo que impide que vuelva a pasar el solape del punto 1.
- **Se mantienen los dos botones** de la barra: «Agregar producto» abre el modal listo para sumar filas y «Editar producto» lo abre con el foco puesto en la fila del producto seleccionado.

**Nada se escribe hasta «Finalizar».** Tocar el basurero o corregir un peso solo mueve estado en memoria, así que «Cancelar» descarta todo — y por eso el basurero ya no pide confirmación con `MessageBox` como hacía el modal viejo.

**«Agregar producto» abre el catálogo en multiselección** (`Catalogos.Productos(..., permiteMultiple: true)`), acotado al proveedor de la recepción y excluyendo los que ya están en la tabla. Es el **primer consumidor real** de la multiselección de `SelectorCatalogoModal`: existía desde 2026-08-19 pero ningún llamador pasaba `permiteMultiple: true`.

**Estructura:** `ItemsControl` dentro de un `ScrollViewer`, no un `DataGrid` — es el patrón de `RegistroCamionesModal`, y es el que hay que usar acá porque los `TextBox` de cada fila se capturan por `Loaded` para armarles su `ValidadorFormulario`; con la virtualización de un `DataGrid` los contenedores se reciclan y esa captura se vuelve frágil.

**Un caso que apareció armándolo:** un producto **cerrado** puede estar en la carga, y el servidor rechaza modificar un producto que no está abierto. Si su fila quedaba editable, corregirle el peso abortaba el lote **entero** al guardar — incluidas las filas buenas. Sus celdas se muestran deshabilitadas con un ToolTip que dice que hay que reabrirlo primero (`FilaProducto.EsEditable`).

---

## 2. El RPC transaccional

**Migración:** `supabase/migrations/20260906180000_rpc_productos_lote.sql`
**Función:** `public.registrar_productos_lote_seguro(p_id_movimiento, p_altas, p_cambios, p_bajas, p_id_solicitud, p_id_operacion)`

Calcada de `registrar_camiones_lote_seguro` (P-053). Hace altas, correcciones y bajas **en una sola transacción**: si una fila viola una restricción, no persiste ninguna. Valida del lado del servidor lo mismo que la rama `ingresar_producto_recepcion_pesaje_tabla_bitacora` del despachador (`peso > 0`, `bultos > 0`, producto activo y del proveedor de la recepción) y, para las bajas, el mismo guard de `cambiar_estado_producto`: un producto con pesajes activos no se quita.

**Permisos por tipo de operación.** `preparar_solicitud_pesaje` chequea *un* permiso, pero un lote puede traer tres clases de operación. El permiso base es el de la operación principal presente (`Registrar Entrada` si hay altas; si no, `Modificar Pesaje`; si no, `Cancelar Pesaje`) y los otros se exigen aparte solo si esa clase viene en el payload, resolviendo además su `id_accion` para que **cada renglón de bitácora quede con su acción real** y no con la del alta. Así, un lote que solo corrige pesos lo puede ejecutar alguien con `Modificar Pesaje` y nada más.

### Verificación contra la base real

Las dos pruebas corrieron dentro de transacciones revertidas, así que producción quedó intacta (confirmado después: 0 filas, 0 bitácora, 0 solicitudes).

**Atomicidad** — dos altas donde la segunda tenía `peso_manifestado = 0`:
```
error=[Peso manifestado y bultos deben ser positivos (producto 538)]
filas_antes=0  filas_despues=0
```
La primera alta era **válida** y tampoco se guardó. Eso es exactamente lo que se buscaba: el manifiesto a medio guardar es peor que el manifiesto sin guardar.

**Camino feliz** — altas, cambio y baja encadenados:
```
altas={"creados": 2, "ids_creados": [37, 38]}   bitacora_prods=2
cambio={"actualizados": 1}                       peso_final=333.300
baja={"anulados": 1}                             est=9
```

---

## 3. Capas de datos y ViewModel

- `IPesajeRepository`: nuevos `ProductoCargaAlta`, `ProductoCargaCambio`, `ResultadoLoteProductos` y `GuardarProductosLoteAsync`.
- `PesajeViewModel.GuardarProductosCargaAsync`: genera el `Guid` **una vez por intención del usuario**, fuera de cualquier reintento, y al terminar recarga **solo la recepción tocada** (`CargarProductosAsync`), no todos los camiones.
- **Código muerto eliminado** (precedente P-025): `AgregarProductoAsync`, `ActualizarProductoAsync` y `AnularProductoAsync` del repositorio e interfaz, y `ActualizarProductoAsync`, `QuitarProductoAsync` y `RecepcionesDePlaca` del ViewModel. Eran los tres escritores crudos y sus llamadores. Se verificó con `grep` que ningún test ni pantalla los usaba. **`SetEstadoProductoAsync` se conserva**: lo usa «Cerrar/Reabrir producto», que no es parte de este flujo.

---

## 4. Parpadeo al entrar a Pesaje

**Archivo:** `PesajeView.xaml.cs`

Al entrar a la pantalla se veía un instante la grilla completa (camiones, productos, pesajes) y recién después el estado correcto. La pantalla **ya tenía** el overlay `CargandoInicial` pensado para eso, pero quien lo activa corre por `PedirActualizarUI()`, que difiere el barrido a `DispatcherPriority.Background` — una prioridad **más baja que el renderizado**. Resultado: WPF alcanzaba a pintar un frame con las `Visibility` por defecto antes de que el overlay se encendiera.

Se muestra el overlay de forma **sincrónica, antes del `await`** de la carga. El barrido diferido sigue igual para todo lo demás; lo único que se adelanta es la primera pintada.

---

## 5. Spinner al registrar camiones

**Archivos:** `RegistroCamionesModal.xaml[.cs]`, `PesajeView.xaml.cs`

«Guardar camiones» no daba ninguna señal mientras esperaba el RPC de alta en lote, y esos cientos de milisegundos se leían como que el modal se había colgado. Se agregó `MostrarGuardando(bool)`: spinner que reemplaza el check, texto «Guardando…» y botones bloqueados. El mismo método se replicó en `ProductosCargaModal`.

---

## 6. Marcadores («placeholders») dentro de la plantilla

**Archivo nuevo:** `CapaUI/Core/Controls/Placeholder.cs` (propiedad adjunta `Placeholder.Texto`)

Cada pantalla superponía su propio `TextBlock` gris sobre la caja y le acertaba el margen a ojo: **24px** en `CamionModal` sobre un `MInput` cuyo texto arranca en 11px, **12px** en los modales de tabla. Ninguno coincidía con el origen real del texto, que es `BorderThickness + Padding` del estilo — y encima cambia 1px al enfocar, porque el borde pasa de 1 a 2.

El marcador se movió **dentro** de las plantillas de `CeldaInput` y `MInput`, usando el mismo `{TemplateBinding Padding}` y el mismo `VerticalAlignment` que el `PART_ContentHost`, y heredando `FontFamily`/`FontSize` del propio input (antes los marcadores estaban en 13 y el texto en 13.5). Cada caja declara solo su texto:

```xml
<TextBox Style="{StaticResource CeldaInput}"
         controls:Placeholder.Texto="Sin observaciones"/>
```

**El marcador se oculta al enfocar** (`MultiTrigger`: vacío **y** sin foco), decisión de Fernando: así el cursor nunca queda encima del texto gris y el problema de alineación al pixel deja de existir en vez de quedar «casi bien». Se migraron los 7 marcadores que había (3 en `CamionModal`, 3 en `ProductosCargaModal`, 1 en `RegistroCamionesModal`).

> [!note] Ajuste final hecho a mano por Fernando
> Después de la migración bajó el `Padding` de `CeldaInput` de `10,0` a **`6,0`** y le corrió el texto al marcador de PESO. Ese `Padding` es el que posiciona tanto el texto real como el marcador, así que es el knob correcto — pero **también lo usa la tabla de `RegistroCamionesModal`**, donde el texto quedó 4px más cerca del borde.

En la celda de Observaciones apareció un choque: la capa blanca que recorta con «…» al perder el foco tapaba el marcador cuando el campo estaba vacío. Ahora esa capa también se oculta si no hay texto (no hay nada que recortar).

---

## 7. Estilos de tabla centralizados

`RegistroCamionesModal` definía en su `UserControl.Resources` tres estilos que el modal nuevo necesitaba igual. Copiarlos habría sido justo lo que prohíbe [[Anatomia compartida de los modales]] y lo que se acababa de cerrar en P-031 G11. Se promovieron a `Styles.xaml`, junto a `CeldaInput`, con nombres genéricos: **`BasureroCelda`**, **`CabeceraColTabla`**, **`BtnAgregarFilaTabla`**, más las geometrías `IcoTrashFila` e `IcoMasFila` congeladas con `po:Freeze`. Los dos modales las consumen; no quedó ninguna copia local.

A `BasureroCelda` se le agregó un trigger de `IsEnabled=False` que antes no tenía: en esta tabla hay filas que **no** se pueden quitar (producto con pesajes) y el botón tiene que decirlo sin desaparecer.

---

## Verificación

```bash
dotnet build BimboProyecto.sln --no-incremental
dotnet test BimboProyecto.sln
```
0 errores · 286/286.

Migración aplicada con `apply_migration` y confirmada contra la base viva (firma y permisos):
```sql
select proname, pg_get_function_identity_arguments(oid) from pg_proc
 where proname = 'registrar_productos_lote_seguro';
-- p_id_movimiento integer, p_altas jsonb, p_cambios jsonb, p_bajas jsonb,
-- p_id_solicitud uuid, p_id_operacion uuid
-- grants: postgres, authenticated, service_role  (sin anon, sin public)
```

**Prueba visual aprobada por Fernando** (2026-09-07): entrada a Pesaje sin parpadeo, alta de camiones con spinner, modal de productos con la franja de proveedor y la tabla, y los marcadores alineados.

---

## Relaciones

- [[Módulo Pesaje]] — `ProductosCargaModal`, `PesajeViewModel`
- [[Anatomia compartida de los modales]] — estilos promovidos y el marcador dentro de la plantilla
- [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] — el despachador `private.ejecutar_rpc_pesaje` que este RPC reusa
- [[Sesión 2026-09-06 - Auditoría de commits a05f006 y 404796c]] — sesión anterior, multiselección de `SelectorCatalogoModal`
- [[Deuda Técnica - Pendientes]] — P-025 (código muerto), P-032 y P-053 (misma familia de escritura en lote)
