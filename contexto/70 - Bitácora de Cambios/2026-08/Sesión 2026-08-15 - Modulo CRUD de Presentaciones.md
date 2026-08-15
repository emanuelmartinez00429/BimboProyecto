---
title: "Sesión 2026-08-15 — Módulo CRUD de Presentaciones"
tags:
  - sesion
  - presentaciones
  - crud
  - catalogo
date: 2026-08-15
branch: feat/fase8-MaquetadodeRoles-B-Fernando
autor_cambios: Osmany (Claude Code)
---

# Sesión 2026-08-15 — Módulo CRUD de Presentaciones

> [!success] Resultado
> `presentacion_producto` dejó de ser un catálogo de solo lectura: ahora tiene pantalla propia con grilla, buscador, filtros de estado y orden, y modal de alta/edición. Resuelve la mitad de [[Deuda Técnica - Pendientes|P-036]] (queda pendiente la de Tara). Solo código — la migración SQL ya estaba aplicada de antes.

---

## Problema / motivo

Hasta hoy la única forma de tocar una presentación era entrar por SQL a Supabase. En la app existía apenas la lupa de solo lectura (`Catalogos.Presentaciones` + `SelectorCatalogoModal`) para *elegir* una presentación al editar un Producto. P-036 lo dejó registrado como hueco explícito, junto con el mismo problema en Tara.

## Lo que ya existía (y no hubo que crear)

Verificado contra la base viva antes de escribir nada, no de memoria:

- **RPC `contar_presentaciones(p_estado integer)`** — migración `20260728122408`, ya aplicada.
- **`presentacion_producto` publicada en Realtime** y su PK ya mapeada en `RealtimeService._pkColumns`.
- **`ProductosViewModel.TablasDeJoin`** ya observa la tabla e invalida la caché `"presentaciones"` → la grilla de Productos se refresca sola al renombrar una presentación. No hubo que tocar nada ahí.
- **`nombre_presentacion` tiene UNIQUE** desde la migración `20260815001710` (`add_unique_nombre_presentacion`) — dato que cambió el diseño del modal, ver abajo.
- La tabla tiene **`created_at` / `updated_at`** con trigger `trg_presentacion_updated_at`, que ningún modelo C# mapeaba.

## Cambios aplicados

### Contratos — `CapaAplicacion4/Presentaciones/`
- `Dtos/PresentacionDto.cs` — `Id`, `Nombre`, `Descripcion`, `IdEstado`, `CreatedAt`, `UpdatedAt`.
- `Queries/PresentacionFiltros.cs` — `IdEstado` + `enum OrdenPresentacion { IdAsc, NombreAsc, NombreDesc }`.
- `Interfaces/IPresentacionRepository.cs` — 6 métodos.

> [!important] `GetPaginaDeRegistroAsync` recibe el DTO, no el `int id`
> Categorías y Fabricantes la declaran como `(int id, …)` y cuentan los registros con PK menor. Eso solo funciona con orden por id: con A-Z/Z-A activo hay que comparar por **nombre**. Se copió la firma de `ProductoCrudRepository.GetPaginaDeProductoAsync`, que sí recibe el DTO. Copiar la de Fabricantes habría roto el salto de página del buscador apenas se ordenara alfabéticamente.

### Datos — `CapaDatos/`
- `Modelados/Productos/PresentacionCrud.cs` (nuevo) — `: BaseModel`, las 6 columnas. Se suma al trío que ya existía para Fabricante: `Presentacion` (join-only, lo usa `Productos.cs`) · `PresentacionConsulta` (lupa) · `PresentacionCrud` (escritura). Los dos primeros quedaron intactos.

  Las columnas de auditoría van con **`ignoreOnInsert: true, ignoreOnUpdate: true`**: sin eso el SDK las serializa (en `null` al crear) y pisaría el `DEFAULT` de `created_at` y el trigger de `updated_at`. `Supabase.Postgrest 4.0.3` soporta esos flags — verificado en el ensamblado antes de usarlos.

- `Repositories/Presentaciones/PresentacionCrudRepository.cs` (nuevo) — patrón `RepositorioBase` + `TryAsync` + `Result`. `DeleteAsync` es borrado lógico (`id_estado = 2`). `UpdateAsync` no toca `updated_at`: lo mueve el trigger.

> [!warning] Los conteos van SIN `p_estado` — Fabricantes y Categorías lo hacen mal
> Las pastillas TOTAL/ACTIVOS/INACTIVOS desglosan **por estado**. Si al RPC de conteos se le pasa el filtro de estado, las tres quedan acotadas al mismo subconjunto: filtrando "Deshabilitados" se ve ACTIVOS = 0 y TOTAL = INACTIVOS, o sea las tres dejan de informar.
>
> `ProductoCrudRepository` ya excluye el estado a propósito. `FabricanteCrudRepository` y `CategoriaCrudRepository` **sí** lo pasan y tienen el defecto. Acá se siguió el criterio de Productos; el defecto de los otros dos quedó registrado como **P-040**.

- `DependencyInjection.cs` — `IPresentacionRepository → PresentacionCrudRepository`.

### UI — `CapaUI/Formularios/Principal/Pantallas/Presentaciones/`
- `PresentacionesViewModel.cs` — `: RealtimeAwareViewModel`. Estructura de `CategoriasViewModel` más la propiedad `Orden`. `SuggestionDebouncer` por composición, refresco silencioso con `_refrescoSilencioso` + `_loadGeneration`, gate de permiso con `Permiso.ModificarConfiguracion`.

  La heurística de INSERT de `CargarPaginaSilenciosamenteAsync` se ajustó: la del checklist asume que los registros nuevos caen en la última página, lo cual **solo vale con orden por id**. Con orden alfabético una presentación nueva puede aparecer en cualquier página, así que ahí se refresca siempre.

- `PresentacionesView.xaml(.cs)` — grilla de 5 columnas (CÓDIGO · NOMBRE · DESCRIPCIÓN · ACTUALIZADO · ESTADO), `PanelFiltrosFluido` con dos grupos de pastillas (Estado y Orden), rótulos del header que colapsan bajo 690px vía `AnchoMinimoAVisibilidad`. Los dos `case` separados de `PageRows` / `TotalPages` del checklist.

- `PresentacionModal.xaml(.cs)` — 620px, una columna, **etiqueta arriba del campo** (lenguaje de `ProductoModal`, no el de `CategoriaModal` que la pone a la izquierda), `TabIndex` explícito, Ctrl+Enter vía `AtajoGuardar`, fila de auditoría Creado/Actualizado fuera del recorrido de Tab, guardado fluido.

  El foco visible verde y el tooltip+recorte **no se redefinieron**: ya vienen de los triggers de `ModalInput` en `Styles.xaml`.

> [!important] El UNIQUE obliga a traducir el error
> Repetir un nombre devuelve un `23505` de Postgres envuelto en texto de PostgREST, ilegible. `MostrarError` lo detecta y muestra *"Ya existe una presentación con ese nombre."*, devolviendo el foco al campo. Sin esto, la constraint agregada ayer se manifestaba como un volcado técnico.

### Cableado
`Routes.cs` · `App.xaml.cs` (using + `AddTransient`) · `MainViewModel.cs` (`_routes`, `_routePermissions`, `PresentacionesVM`) · `MainWindow.xaml` (xmlns, `DataTemplate`, botón del sidebar) · `MainWindow.xaml.cs` (`_subMap` y **`GetSubCount("productos")` de 6 a 7** — la altura del acordeón es hardcodeada; sin eso el ítem nuevo queda cortado).

## Verificación

- `dotnet build BimboProyecto.sln --no-incremental` → **0 errores**. Warnings: 1 nuevo `CS8603` en `PresentacionCrudRepository.cs:82`, exactamente el mismo idioma `.Set(...)` que ya emiten Productos (8), Proveedores (4), Fabricantes (3), Contactos (4) y Pesaje (3) — artefacto de la firma del SDK, no defecto nuevo.
- **Chequeo de `StaticResource` en runtime**: se instanciaron `PresentacionesView` y `PresentacionModal` en un host WPF real (`Application` + `Styles.xaml` + converters), con `Measure`/`Arrange`/`UpdateLayout` para forzar el pase de layout. Ambos → OK, sin `XamlParseException`. Esto es lo que el build **no** valida: WPF resuelve los recursos en runtime.
- Cruce estático de las 20 claves `StaticResource` de los dos XAML nuevos contra sus `Resources` locales, `Styles.xaml` y los converters de `App.xaml` — todas resuelven.

> [!warning] Sin verificación visual todavía
> No se corrió la app (requiere login contra Supabase). Falta probar el recorrido completo: sidebar → grilla → filtros → orden → alta → duplicado → edición → baja lógica, y que la lupa de Presentación en el modal de Producto refleje un rename sin reiniciar.

## Lo que NO cambió

- `Presentacion.cs` y `PresentacionConsulta.cs` — intactos; `Productos.cs:67` y la lupa dependen de ellos.
- `CatalogoRepository.GetPresentacionesAsync` — sigue con `id_estado = Activo` hardcodeado. Está bien: la lupa **debe** ofrecer solo activas. Por eso el CRUD tiene repositorio propio.
- Realtime — cero cambios, ya estaba todo.
- Permisos — se reusó `Modificar Configuración`; no se tocó la tabla `acciones`, ni el enum `Permiso`, ni la pantalla de Roles.
- El defecto de conteos de Fabricantes/Categorías — solo registrado (P-040), no corregido.
- Migraciones SQL — ninguna. Las que este módulo necesitaba ya estaban aplicadas.
- No se probó la paginación: la tabla tiene 2 filas y `PageSize` es 50.

---

## Relaciones

- [[Módulo Productos]] — patrón de referencia del que salió el diseño del modal
- [[Deuda Técnica - Pendientes]] — P-036 (mitad resuelta), P-040 (nuevo)
- [[Checklist - Replicar Módulo con Realtime]]
- [[ADR-015 - Cache de catalogos mostrar y revalidar]]
- [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]] — sesión que abrió P-036
