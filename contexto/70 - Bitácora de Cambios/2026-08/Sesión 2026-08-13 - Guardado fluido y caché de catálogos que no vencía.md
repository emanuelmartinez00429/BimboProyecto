---
title: Sesión 2026-08-13 — Guardado fluido en los modales y caché de catálogos que no vencía
type: sesion
status: vigente
tags:
  - sesion
  - productos
  - modales
  - cache
  - realtime
  - rendimiento
date: 2026-08-13
updated: 2026-08-13
summary: "Se rediseñó el modal de Productos a tres columnas, se corrigieron cuatro bugs funcionales (tara con salto de línea, lupa de fabricantes sin acotar, guardado que…"
scope:
  - CapaAplicacion4/Productos/Dtos
  - CapaDatos/Modelados/Productos
  - CapaDatos/Repositories/Catalogos
  - CapaDatos/Repositories/Productos
  - CapaUI/Core/Catalogos
  - CapaUI/Core/Controls
symbols:
  - BaseOutputPath
  - BtnGuardar
  - CampoModal
  - CargarContactosAsync
  - CargarPaginaAsync
  - CargarPaginaSilenciosamenteAsync
  - CatalogoCache
  - Center
  - ColumnDefinition
  - Content
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Opus 5 (Claude Code)
---

# Sesión 2026-08-13 — Guardado fluido en los modales y caché de catálogos que no vencía

> [!success] Resultado
> Se rediseñó el modal de Productos a tres columnas, se corrigieron cuatro bugs funcionales (tara con salto de línea, lupa de fabricantes sin acotar, guardado que parecía colgar, catálogos congelados en memoria) y se replicó el guardado fluido en los ocho modales de edición. Build: `0 errores`.

---

## Problema / motivo

La sesión arrancó por un recorte visual en las lupas del modal de Productos y fue destapando cuatro problemas funcionales que no eran visibles a simple vista. Cada uno se atacó desde la causa raíz, no desde el síntoma.

---

## 1. Layout del modal de Productos

El formulario pasó de una columna (13 filas, etiqueta a la izquierda) a **tres columnas con la etiqueta arriba del campo**, 5 filas, ancho `620 → 1020`. Se agregaron estilos locales `EtiquetaCampo`, `CampoModal` y `ModalInputAuditoria` (Creado/Actualizado en gris apagado).

**El recorte de las lupas** tenía una causa concreta: el botón mide 38 px más 6 de margen = 44, pero su `ColumnDefinition` seguía en `Width="32"`. Se corrigieron las seis columnas y se agregó `HorizontalAlignment="Right"` al estilo `LupaBtn` para que no vuelva a desbordar si la fila se aprieta.

En `SelectorCatalogoModal` la tipografía se llevó al nivel de la tabla del formulario (celdas 15.5, encabezados 13) y del modal principal para todo lo demás. El **círculo indicador de fila** quedaba pegado al borde izquierdo porque el template de celda forzaba `HorizontalAlignment="Left"` en el `ContentPresenter`: la celda se encogía al ancho del contenido y el `Center` del círculo no tenía dónde centrarse. Pasó a `Stretch`, y ahora cada plantilla manda sobre su propia alineación.

> Archivos: `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml`, `CapaUI/Core/Controls/SelectorCatalogoModal.xaml`.

---

## 2. La tara se dibujaba fuera de su campo

**Síntoma:** el texto de Tara aparecía corrido hacia arriba, montado sobre el borde del `TextBox`.

**Causa raíz — es un dato sucio, no un problema de layout.** Consultado contra Supabase:

```sql
select descripcion_tara, encode(convert_to(descripcion_tara,'UTF8'),'hex') from public.tara;
-- "tara de 20 kg\n"  →  …3230206b67 0a
```

Ese `\n` hace que el `TextBox` trate el valor como dos líneas; con alto fijo de 38 px y contenido centrado, la primera línea sube y se monta sobre el borde. Es el **mismo dato** que ya había causado el recorte en la columna TARA de la grilla, donde se tapó con `TextWrapping="NoWrap"` sin tocar la causa (ver [[Sesión 2026-08-13 - Instalación de Diagram Design y Corrección Alineación Tara]]).

**Corrección en el origen**, para que lo reciban limpio todos los consumidores:

- `CapaDatos/Modelados/Productos/Productos.cs` — `descripcion_Tara` aplica `Trim()` y cae a `"Sin tara"` si queda vacío.
- `CapaDatos/Repositories/Catalogos/CatalogoRepository.cs` — el `Nombre` del ítem de tara del selector también se limpia, así elegir una tara por la lupa no reintroduce el `\n`.

El dato en la base **sigue sucio**; limpiarlo es un `update … set descripcion_tara = btrim(descripcion_tara)` que no se ejecutó por ser escritura sobre producción. Relacionado con [[Deuda Técnica - Pendientes]] P-023.

---

## 3. La lupa de fabricantes no respetaba el proveedor

**Síntoma:** al abrir un producto existente y tocar la lupa de Fabricante **por primera vez**, aparecían fabricantes de cualquier proveedor, aunque el formulario mostrara "Cisa" en Proveedor.

**Causa raíz:** `ProductoDto` traía `Proveedor` (el nombre, para mostrar) pero **no `IdProveedor`**. `ProductoModal.OnLoaded` llenaba `_idPresentacion`, `_idFabricante`, `_idCategoria`, `_idPais` y `_idTara`, y `_idProveedor` quedaba en `null`. Con eso, `Catalogos.Fabricantes(_catalogos, null)` arma la clave `"fabricantes"` a secas y consulta sin filtro. Recién al elegir un proveedor con su lupa el acote empezaba a funcionar.

**Corrección:** el id viaja en el DTO de punta a punta.

- `CapaAplicacion4/Productos/Dtos/ProductoDto.cs` — campo `IdProveedor`.
- `CapaDatos/Modelados/Productos/Productos.cs` — `id_Proveedor => Fabricante?.idProveedor` (el dato ya venía en la consulta, `fabricante(*, proveedores(*))` incluye `id_proveedor`; solo no se leía).
- `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs` — el `Map` lo pasa al DTO.
- `ProductoModal.xaml.cs` — `_idProveedor = _producto.IdProveedor` al cargar.

**Efecto secundario corregido:** `BuscarProveedor_Click` decide `bool cambio = _idProveedor != item.Id` para limpiar el fabricante. Con `_idProveedor` en `null`, *cualquier* elección contaba como cambio, así que reelegir el mismo proveedor que ya mostraba el formulario borraba el fabricante sin motivo.

---

## 4. Guardar parecía colgar la aplicación

**Síntoma:** al guardar un producto la aplicación no respondía ~1 s y después "recargaba el formulario".

**No era un cuelgue: eran tres esperas de red encadenadas sin nada que las tapara.**

1. `await UpdateAsync(dto)` — PATCH a Supabase. El modal seguía en pantalla y lo único que cambiaba era el botón apagado.
2. `Guardado` → `CerrarModal()` + `RefrescarDatos()` → `CargarPaginaAsync()` levanta `IsLoading`, o sea la grilla se vacía y vuelve (el "parpadeo de recarga"), y dispara `GetPagedAsync` = 2 requests.
3. El **eco de Realtime** del mismo UPDATE llegaba por el websocket y disparaba `CargarPaginaSilenciosamenteAsync` = 2 requests más, para traer lo que se acababa de traer.

Total: hasta 5 requests y 3 esperas en serie para un cambio ya conocido.

**Corrección, replicada en los ocho modales de edición** (Productos, Categorías, Fabricantes, Proveedores, Empleados, Usuarios, Contactos de fabricante y de proveedor):

- **El botón avisa.** `BtnGuardar` muestra `"Guardando..."` mientras dura el request. Para eso su `ControlTemplate` pasó a leer `Content` (`{Binding Content, RelativeSource={RelativeSource TemplatedParent}}`) en vez de un texto fijo. Los modales de Contactos ya pintaban la etiqueta desde `Tag` y la arman según alta o edición (`"Agregar"` / `"Guardar cambios"`), así que ahí se guarda y se restaura el valor previo en vez de hardcodear `"Guardar"`.
- **Refresco sin parpadeo.** `OnXGuardado` llama a `RefrescarTrasGuardar()`, que va por la vía silenciosa: las filas viejas se quedan en pantalla y se reemplazan recién cuando llegan las nuevas.
  - Productos, Categorías, Fabricantes y Proveedores ya tenían `CargarPaginaSilenciosamenteAsync`; se les enganchó.
  - Empleados y Usuarios no lo tenían: se le agregó un parámetro `bool silencioso = false` a `CargarPaginaAsync` que saltea el `IsLoading`.
  - Contactos: mismo parámetro sobre `CargarContactosAsync`.
- **Sin consulta duplicada.** Bandera `_refrescoSilencioso` en los cuatro VM que tienen el refresco silencioso: colapsa el eco de Realtime contra el refresco que ya está en vuelo. Descartar el eco es seguro porque esa consulta arrancó *después* de la escritura, así que ya trae el cambio. Baja de 5 requests a 3.

**Lo que no desaparece:** el viaje de red del UPDATE (~300-600 ms hasta `us-west-2`). Solo se esconde con UI optimista —cerrar el modal y parchear la fila en memoria antes de que el servidor confirme—, que se evaluó y se descartó por ahora: mostraría como guardado algo que todavía puede fallar.

---

## 5. Los catálogos quedaban congelados hasta reiniciar

**Síntoma:** se cambiaron las presentaciones en la base y la lupa nunca mostró el cambio, ni recargando.

**Causa raíz:** `CatalogoCache` es un `static ConcurrentDictionary` a nivel de proceso **sin vencimiento**. Todo catálogo que entra entero en la primera página (`Total <= 200`) queda guardado para siempre. A la escala real del proyecto eso es *todos*: presentaciones (3 filas), taras (1), categorías (5), fabricantes (5), proveedores (11), países y unidades. Recargar la grilla no toca esa memoria; solo se limpiaba cerrando la aplicación.

**La red de seguridad prevista estaba muerta.** `ProductosViewModel.cs:238` hace `Observar("fabricante", _ => CatalogoCache.Invalidar("fabricantes"))`, pero la publicación `supabase_realtime` **no incluye** `fabricante`. Verificado:

```sql
select tablename from pg_publication_tables where pubname = 'supabase_realtime';
-- categoria, empleados, entradas_producto, movimiento_productos,
-- movimientos, paises, productos, usuarios
```

Ni `presentacion_producto`, ni `fabricante`, ni `proveedores`, ni `tara`. Ese handler no se ejecuta nunca → **P-034**.

**Corrección: mostrar y revalidar** (*stale-while-revalidate*). Se descartó un vencimiento por tiempo porque deja una ventana en la que lo mostrado sigue siendo viejo. El razonamiento completo y las alternativas están en [[ADR-015 - Cache de catalogos mostrar y revalidar]].

- `CapaUI/Core/Catalogos/CatalogoCache.cs` — `ObtenerCompletoAsync` y `ObtenerParaComboAsync` aceptan un `alRevalidar` opcional: devuelven lo cacheado al instante y lanzan `RevalidarAsync` por detrás. Esa revalidación es silenciosa (si falla no muestra error: en pantalla hay algo válido), invalida la entrada si el catálogo creció más allá del umbral, y **solo avisa si la lista cambió de verdad** — `SonIguales` compara campo por campo porque `FiltroItem` es `class` y no tiene igualdad por valor. Ese cortocircuito es lo que evita que la lista parpadee en el caso normal.
- `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs` — el pintado en memoria salió a `PintarEnMemoria(items, preservarSeleccion)`, reusado al abrir y al repintar; en el repintado conserva el texto buscado (el filtro lee el campo `_query`) y vuelve a marcar la fila por `Id`. Se agregó `_ctsVida`, un token que vive lo que vive el modal —aparte del `_cts`, que se recrea en cada tecla del buscador— para abortar una revalidación en vuelo al cerrar la lupa.
- `ProductosViewModel.cs` — los tres combos de filtro también revalidan, **pero el repoblado se saltea cuando ese filtro está en uso**: `ComboFiltro.Poblar` vuelve a "(Todos)" y limpia su id *sin notificar*, así que repoblar por detrás dejaría el combo diciendo "(Todos)" con la grilla todavía filtrada. La lista nueva igual queda cacheada y entra en el próximo poblado.

Detalle de orden verificado: en el hit de caché `ObtenerCompletoAsync` no tiene ningún `await` antes de devolver, así que completa de forma síncrona y el pintado inicial siempre ocurre antes de que el hilo de UI pueda procesar la continuación de la revalidación. No hay carrera entre los dos pintados.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**, 49 advertencias (todas preexistentes, `CS8618` de modelados).
- Durante buena parte de la sesión la aplicación estuvo abierta y bloqueaba `CapaUI.exe`; se compiló contra un `BaseOutputPath` alterno para verificar sin cerrarla. Ojo con esto: `dotnet msbuild -t:Compile` **no sirve** como sustituto — saltea el markup compile de XAML y produce errores falsos de `InitializeComponent`.
- **Verificación en runtime pendiente**, en particular:
  1. Editar una presentación por SQL y reabrir la lupa sin reiniciar: debe abrir al instante y corregirse sola.
  2. Reabrir la lupa varias veces sin tocar la base: no debe parpadear ni perder la fila marcada.
  3. Guardar en cada uno de los ocho modales: el botón debe decir "Guardando..." y la grilla no debe vaciarse.

---

## Lo que NO cambió

- **No se tocó la publicación `supabase_realtime`** (sería un `ALTER PUBLICATION` sobre producción) → P-034.
- **No se limpió el dato sucio de `tara.descripcion_tara`** en la base; se mitigó en código.
- **No se aplicó UI optimista** al guardado: el viaje de red del UPDATE se sigue esperando.
- **No se tocaron los modales del flujo de Pesaje** (`PesajeModal`, `TaraExtraTotalModal`, `ProcesoDescargaModal`): no son CRUD con grilla detrás, tienen otro ciclo.
- `Observar("fabricante", …)` se dejó en su lugar aunque hoy no se ejecute: si algún día se publica la tabla, vuelve a servir.

## Estado en git

- Commit [`e4ec139`](https://github.com/warthunderlover/BimboProyecto/commit/e4ec139) — layout del modal, lupas, tara, tipografía del selector y guardado fluido en los ocho modales. Pusheado a `feat/fase8-MaquetadodeRoles`.
- **Sin commitear al cierre de esta nota:** el círculo centrado del selector, el fix de `IdProveedor` y la revalidación de `CatalogoCache`.

---

## Relaciones

- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — la decisión de fondo de la sección 5
- [[Módulo Productos]]
- [[Arquitectura Actual]]
- [[Deuda Técnica - Pendientes]] — origen de P-034
- [[Sesión 2026-08-13 - Instalación de Diagram Design y Corrección Alineación Tara]] — donde se tapó el síntoma del `\n` de la tara
- [[Sesión 2026-08-13 - Campos completos en Productos]]
