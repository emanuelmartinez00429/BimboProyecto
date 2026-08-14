---
title: "Sesión 2026-08-14 — Catálogo real de unidad_medida con categoría (tipo_unidad), conectado a contenido y tara"
tags:
  - sesion
  - productos
  - tara
  - esquema
  - supabase
date: 2026-08-14
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Catálogo real de unidad_medida con categoría, conectado a contenido y tara

> [!success] Resultado
> `unidad_medida` pasó de ser una tabla sembrada pero sin usar a un catálogo real con categoría (`tipo_unidad`: Masa/Volumen/Conteo) y estado. El combo "Unidad" del modal de Productos ya no es una lista fija en el XAML — consume la tabla real. `tara` tiene ahora una unidad explícita en vez de una convención de nombre de columna. Migración aplicada contra Supabase (`Bimbo_Pesaje`, project_id `bzmmrifjgzlvsphctais`). Build: `CapaDatos`/`CapaAplicacion` 0 errores 0 advertencias; `CapaUI` 0 errores `CS*` (falló solo la copia de DLL por tener `CapaUI.exe` abierto).

---

## Problema / motivo

El usuario pidió una tabla de tipos de contenido (kg, ml, l, lb...) con una categoría controlada, para poder filtrar a "solo masa" el combo de unidad que se use en Tara. La decisión de fondo y las alternativas descartadas quedaron en [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]] — acá el detalle de implementación.

Antes de tocar código se corrió una exploración (`Explore` agent) del estado real: `unidad_medida` existía con 6 filas (kg, g, lb, t, oz, u) pero **nadie la consumía** — el combo "Unidad" del modal de Productos era un `ComboBox` con `ComboBoxItem` fijos en el XAML (`g/kg/ml/l/oz`), sin relación con la tabla, y `tara` no tenía ninguna columna de unidad (el "kg" era la convención del nombre `peso_tara_envalaje`).

---

## Hallazgo que cambió el alcance planeado

El plan original (aprobado en modo plan) asumía que había que agregar `productos.id_unidad_contenido` como columna nueva. Al inspeccionar el esquema real de Supabase con el MCP de Supabase (`list_tables` verbose), apareció que **`productos.id_unidad` ya existía**, con una FK activa a `unidad_medida` — el modelo `Productos.cs` la tenía comentada:

```csharp
//[Column("id_unidad")]
//public int idUnidad { get; set; }
```

No hizo falta ninguna columna nueva en `productos`. Se descomentó, se le agregó la navegación, y se usó como la unidad de `contenido` — **no** de `peso_teorico` (que el propio ejemplo del usuario en la conversación usaba como analogía, `peso_teorico`/`unidad_peso_id`): `precio_por_kg` asume que `peso_teorico` está en kg para calcular precio, y hacerla seleccionable ahí sería un riesgo de cálculo, no de catálogo.

También se corrigió sobre la marcha el patrón de "estado": el plan original asumía un `id_estado integer` suelto con convención 1/2; la inspección real mostró que el proyecto ya tiene una tabla compartida `estado_general` (1=Activo, 2=Inactivo) y que catálogos comparables (`fabricante`, `presentacion_producto`) la referencian por FK — se siguió ese patrón real.

---

## Migración aplicada (Supabase, con confirmación explícita antes de correrla)

Cuatro migraciones (`apply_migration`), en orden:

1. **`crear_tipo_unidad`** — tabla nueva, sembrada con Masa/Volumen/Conteo.
2. **`extender_unidad_medida_con_tipo_y_estado`** — sembró `Mililitro (ml)` y `Litro (l)` (faltaban; aparecen en datos reales de `contenido` como `"485 ml"`), agregó `id_tipo_unidad` (FK, `not null`), `factor_conversion` (nullable, sin uso todavía), `id_estado` (FK a `estado_general`, default 1), y clasificó las 8 filas resultantes.
3. **`agregar_unidad_a_tara`** — `tara.id_unidad` (FK, `not null`), backfill de la única fila existente a `kg`.
4. **`backfill_id_unidad_en_productos_con_contenido_reconocible`** — `UPDATE ... WHERE id_unidad IS NULL AND contenido ~ '(^|\s)(abreviatura)$'`.

**Verificado antes de correr:** de 505 productos, solo 11 tienen `contenido` con una unidad reconocible al final (el resto son placeholders de prueba `"Contenido N"`). El backfill solo tocó esas filas que además estaban `NULL` — y de esas 11, la mayoría **ya tenía `id_unidad = 1` (kg)** puesto de antes (502 de 505 productos en total), dato preexistente no introducido por esta sesión, mismo tipo de dato de prueba que ya fichaba P-023 para `tara`. No se sobrescribió: corregir esas 502 filas hubiera sido una escritura mucho más grande que el backfill acotado que se pidió.

---

## Cambios en C#

- **`CapaDatos/Modelados/Productos/TipoUnidad.cs`** (nuevo) — `[Table("tipo_unidad")]`.
- **`UnidadMedida.cs`** — suma `IdTipoUnidad`, `FactorConversion`, `IdEstado`, navegación `TipoUnidad? tipo_unidad`.
- **`CapaDatos/Modelados/Pesajes/Tara.cs`** — suma `IdUnidad` (FK) y navegación `UnidadMedida? unidad_medida`; `abreviatura_Unidad` (fallback `"kg"` si no vino el join).
- **`Productos.cs`** — descomenta `idUnidad` (ahora `int?`), suma navegación `unidad_medida` y `abreviatura_Unidad`.
- **`ProductoDto.cs`** — suma `IdUnidad`, `Unidad`.
- **`ProductoCrudRepository.cs`** — `SelectPara` suma `tara(*, unidad_medida(*)), unidad_medida(*)` al join; `Map`, `CreateAsync` y `UpdateAsync` leen/escriben `IdUnidad`.
- **`CatalogoRepository.cs`**:
  - `GetUnidadesAsync` gana `int? idTipoUnidad = null` (mismo patrón que `GetFabricantesAsync(..., idProveedor)`) — sin valor trae todas las unidades activas, con valor acota a la categoría. Es lo que va a usar un futuro combo de tara filtrado a Masa.
  - `GetTarasAsync` ahora hace `q.Select("*, unidad_medida(*)")` y muestra `{peso_tara_envalaje} {abreviatura real}` en vez del `" kg"` hardcodeado de antes.
- **`ICatalogoRepository.cs`** / **`CatalogoConfig.Unidades`** — firma actualizada con `idTipoUnidad` opcional, mismo patrón que `Fabricantes(r, idProveedor)`.

### UI — `ProductoModal`

- **XAML**: `CmbUnidad` perdió sus 6 `ComboBoxItem` fijos; queda un `ComboBox` vacío, poblado en código.
- **`OnLoaded`** pasa a `async void` y llama `CargarUnidadesAsync()` antes de todo lo demás — único catálogo que se carga al abrir el modal en vez de recién al abrir su lupa, porque hace falta tener las opciones listas para poder preseleccionar la del producto.
- **`CargarUnidadesAsync()`** (nuevo) — puebla `CmbUnidad.Items` desde `CatalogoCache.ObtenerParaComboAsync(Catalogos.Unidades(_catalogos))` (todas las unidades activas, sin filtro de categoría — el contenido puede ser masa o volumen), reusando el mismo patrón "mostrar y revalidar" que el resto de las lupas (ADR-015). Cada `ComboBoxItem` guarda el id real en `Tag`.
- **`CargarContenido(contenido, idUnidad)`** — ahora recibe también `IdUnidad` del DTO. Si viene, selecciona el combo directo por id (dato estructurado). Si no (fila vieja o de prueba sin `id_unidad`), cae al comportamiento anterior: infiere la unidad del sufijo de texto al final de `contenido`.
- **`ObtenerContenido()`** — sigue componiendo `"valor unidad"` para no romper el buscador/Pesaje, pero además fija `_idUnidadContenido` desde el `Tag` de la selección — es lo que viaja en el DTO al guardar.

---

## Verificación

- `dotnet build CapaDatos/CapaDatos.csproj` → **0 errores, 0 advertencias** (build limpio, sin bloqueo de proceso).
- `dotnet build CapaUI/CapaUI.csproj` → **0 errores `CS*`**; falló solo la copia final de DLL (`MSB3027`/`MSB3021`) por tener `CapaUI.exe` corriendo — mismo caso que ya se documentó en la sesión del logo de login.
- Contra Supabase: confirmado con `execute_sql` que `unidad_medida` tiene 8 filas correctamente clasificadas, `tara.id_unidad = 1` (kg), y `503/505` productos con `id_unidad` (502 preexistentes + 1 nuevo del backfill).
- **Sin verificar en runtime la app** (requiere cerrar `CapaUI.exe`/Visual Studio primero): abrir `ProductoModal` con un producto existente y confirmar que el combo carga desde la base y preselecciona bien; guardar un producto nuevo y confirmar que persiste `id_unidad`.

---

## Lo que NO se hizo

- **No se construyó pantalla CRUD de Tara** (ni de Presentaciones, mismo hueco) — el combo de unidad filtrado a Masa (`Catalogos.Unidades(r, idTipoUnidad)`) queda listo en el backend sin ningún formulario donde mostrarse. **P-036**.
- **No se tocaron los 502 productos** que ya traían `id_unidad=1` (kg) desde antes de esta sesión, aunque su `contenido` real pueda no ser kg — dato de prueba preexistente, mismo espíritu que P-023.
- **No se hizo `peso_teorico` seleccionable en unidad** — se decidió explícitamente no tocarlo por el acople con `precio_por_kg` (ver ADR-017).
- **No se verificó en runtime** dentro de la app corriendo (`CapaUI.exe` estaba abierto durante toda la sesión).

## Estado en git

- Sin commitear al cierre de esta nota. Archivos tocados: `TipoUnidad.cs` (nuevo), `UnidadMedida.cs`, `Tara.cs`, `Productos.cs`, `ProductoDto.cs`, `ProductoCrudRepository.cs`, `CatalogoRepository.cs`, `ICatalogoRepository.cs`, `CatalogoConfig.cs`, `ProductoModal.xaml`, `ProductoModal.xaml.cs`.
- Migración ya aplicada contra Supabase (no requiere commit de código para tomar efecto en runtime, pero sí que el modelo C# esté commiteado para no volver a desincronizarse).

---

## Relaciones

- [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]] — decisión de fondo
- [[Módulo Productos]] — sección "Contenido y unidad de medida" actualizada
- [[Deuda Técnica - Pendientes]] — P-036 (nuevo), P-023 (mismo tipo de dato de prueba)
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — el combo de unidad reusa `CatalogoCache`
- [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]] — implementó el selector de tara que esta sesión extiende
