---
title: ADR-017 — Catálogo real de unidad_medida con categoría (tipo_unidad) y su uso en Tara
type: adr
status: vigente
tags:
  - adr
  - decision
  - productos
  - tara
  - esquema
date: 2026-08-14
updated: 2026-08-14
summary: "Dos catálogos de unidad desconectados de la realidad, encontrados al investigar el pedido:"
scope: []
symbols:
  - CatalogoCache
  - CatalogoConfig
  - ComboBox
  - ComboBoxItem
  - PickerProductoRepository
  - ProductoSearchStrategy
  - SelectorCatalogoModal
  - SelectorProductosViewModel
estado: aceptado
---

# ADR-017 — Catálogo real de unidad_medida con categoría (tipo_unidad) y su uso en Tara

## Contexto

Dos catálogos de unidad desconectados de la realidad, encontrados al investigar el pedido:

- **`unidad_medida`** (`id_unidad`, `nombre_unidad`, `abreviatura`) ya existía en Supabase con 6 filas (kg, g, lb, t, oz, u) y su `CatalogoConfig`/`SelectorCatalogoModal` listos (`Catalogos.Unidades`), pero **nadie la usaba**. El combo "Unidad" del modal de Productos era un `ComboBox` de WPF con 5 `ComboBoxItem` fijos en el XAML (`g`, `kg`, `ml`, `l`, `oz`) — ni `ml` ni `l` existían en la tabla real.
- **`tara`** (`id_tara`, `peso_tara_envalaje`, `descripcion_tara`) no tenía ninguna columna de unidad — el "kg" era una convención implícita del nombre de columna, no un dato real.

**Hallazgo que cambió el alcance real de la implementación:** al inspeccionar el esquema real (no solo el modelo C#), `productos.id_unidad` **ya existía** en la base con una FK activa a `unidad_medida` — pero el modelo `Productos.cs` lo tenía comentado, y nada en la UI lo usaba. No hizo falta agregar una columna nueva en `productos`.

Pedido: convertir `unidad_medida` en un catálogo con una categoría controlada (Masa / Volumen / Conteo), conectarlo al combo real de "contenido" del producto, y agregar la unidad a `tara` — dejando la posibilidad de filtrar por categoría (solo Masa) para cuando exista una pantalla de edición de tara.

## Decisión

### 1. `tipo_unidad` es una tabla propia, no una columna de texto

```sql
create table tipo_unidad (id_tipo_unidad serial primary key, nombre_tipo_unidad varchar(30) not null unique);
insert into tipo_unidad (nombre_tipo_unidad) values ('Masa'), ('Volumen'), ('Conteo');
```

Sigue el mismo patrón que ya usa el proyecto para `categoria`, `paises`, `fabricante`: agregar una categoría nueva mañana (ej. "Longitud") es una fila, no una migración de esquema ni un `CHECK` a reescribir.

### 2. `unidad_medida` gana categoría + estado real (patrón `estado_general`, no un booleano local)

```sql
alter table unidad_medida
  add column id_tipo_unidad    integer references tipo_unidad(id_tipo_unidad),
  add column factor_conversion numeric,        -- sin uso todavía, listo para convertir dentro de una categoría
  add column id_estado         integer references estado_general(id_estado) default 1;
```

El primer intento de este ADR asumía un `id_estado integer` suelto con convención 1/2 hardcodeada. Al inspeccionar el esquema real se encontró que el proyecto **ya tiene** una tabla compartida `estado_general` (`id_estado`, `tipo_estado`: 1=Activo, 2=Inactivo) y que los catálogos chicos comparables (`fabricante`, `presentacion_producto`) ya la referencian por FK — se corrigió para seguir ese patrón real en vez de inventar uno nuevo.

Se sembraron además `Mililitro (ml)` y `Litro (l)`, que no existían en `unidad_medida` pero sí aparecen en datos reales de `productos.contenido` (`"485 ml"`, `"900 ml"`).

### 3. `productos.id_unidad` pasa a ser la unidad de `contenido` — sin tocar `peso_teorico`

`productos.contenido` sigue siendo texto libre (`"500 g"`), sin cambios, porque lo consumen `ProductoSearchStrategy`, `PickerProductoRepository`, `SelectorProductosViewModel` y `CapaDominio.Entities.Producto` como texto de búsqueda/visualización — reemplazarlo hubiera roto esa cadena. El cambio es **aditivo**: se descomentó `Productos.idUnidad` (la columna y su FK ya existían), se sumó como fuente estructurada, y el modal la mantiene sincronizada con el texto al guardar.

Se decidió explícitamente **no** usar esta unidad para `peso_teorico` (aunque el ejemplo que dio el usuario en la conversación usaba justo `peso_teorico`/`unidad_peso_id`): `precio_por_kg` asume que `peso_teorico` está en kg para el cálculo de precio, y dejar esa unidad seleccionable introduciría un riesgo de cálculo silenciosamente incorrecto. `contenido` no tiene ningún cálculo aguas abajo — es descriptivo — así que es el lugar seguro para la unidad elegible.

### 4. `tara.id_unidad` se agrega en la base, sin pantalla propia

```sql
alter table tara add column id_unidad integer references unidad_medida(id_unidad);
update tara set id_unidad = (select id_unidad from unidad_medida where abreviatura='kg');
alter table tara alter column id_unidad set not null;
```

Hoy no existe ninguna pantalla para crear/editar `tara` en la app — solo una lupa de solo lectura (`Catalogos.Taras` + `SelectorCatalogoModal`) para *elegir* una fila ya existente al editar un Producto. Por eso el combobox de unidad filtrado a Masa (`Catalogos.Unidades(r, idTipoUnidad)`, ya implementado y listo) **no tiene, hoy, ningún formulario donde vivir**. Se decidió no construir esa pantalla en este cambio — el usuario ya lo sabía y señaló que Presentaciones tiene el mismo hueco. Queda como **P-036**.

### 5. Backfill de datos: solo lo que se puede inferir con confianza

`productos.contenido` tiene 505 filas, de las cuales solo 11 terminan en una unidad reconocible (el resto son placeholders de prueba tipo `"Contenido 10"`, `"Contenido 100"`...). El backfill de `productos.id_unidad` corrió únicamente sobre filas con `id_unidad is null` **y** `contenido` terminado en una abreviatura real — no se adivinó nada. Al ejecutar se encontró que **502 de 505 productos ya traían `id_unidad = 1` (kg)** desde antes de esta migración — dato preexistente, no introducido por este cambio, y del mismo tipo de dato de prueba que ya fichaba P-023 para `tara`. No se sobrescribió: tocar 502 filas ya pobladas para "corregirlas" hubiera sido una escritura mucho más grande y riesgosa que el backfill acotado que se pidió.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| **Tabla `tipo_unidad` propia** | Ampliable sin migrar, mismo patrón que el resto del proyecto | Una tabla más para 3 valores | ✅ |
| Columna de texto con `CHECK` | Menos tablas | El usuario explícitamente pidió que no fuera texto libre ("categoría controlada") | ❌ |
| `productos.id_unidad` para `peso_teorico` | Coincide con el ejemplo que dio el usuario en la conversación | `precio_por_kg` asume kg implícito — la unidad seleccionable ahí es riesgo de cálculo, no de catálogo | ❌ |
| Reemplazar `productos.contenido` por columna numérica pura | Esquema "limpio" | Rompe el buscador universal, el picker de Pesaje y el dominio, que leen `contenido` como texto opaco | ❌ |
| Construir ya una pantalla CRUD de Tara | El combo filtrado a Masa tendría un lugar real donde probarse hoy | Feature nueva y grande, no pedida para este cambio; mismo hueco existe en Presentaciones y se deja para después, a propósito | ❌ |

## Consecuencias

- **Se gana:** el combo "Unidad" del modal de Productos consume el catálogo real (con `ml`/`l` incluidos), `tara` tiene un dato de unidad real en vez de una convención de nombre de columna, y queda lista la infraestructura (`Catalogos.Unidades(r, idTipoUnidad)`) para un futuro combo de tara filtrado a Masa sin tocar nada más el día que se construya esa pantalla.
- **Se sacrifica:** `productos.contenido` sigue siendo la fuente de verdad para búsqueda/visualización — la fuente estructurada (`id_unidad`) puede desincronizarse del texto si algo escribe `contenido` por fuera del modal (no se identificó ningún otro escritor hoy).
- **Pendiente real:** P-036 — sin pantalla de Tara (ni de Presentaciones), el combo filtrado a Masa no tiene dónde mostrarse todavía.
- **Dato preexistente sin resolver:** 502 productos con `id_unidad=1` (kg) que puede no corresponder a su `contenido` real — mismo espíritu que P-023, no se tocó por ser una corrección mucho más grande que el backfill pedido.

---

## Relaciones

- [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]] — implementación
- [[Módulo Productos]]
- [[Deuda Técnica - Pendientes]] — P-036, y P-023 (mismo tipo de dato de prueba en `tara`)
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — el combo de unidad reusa `CatalogoCache`
- [[Arquitectura Actual]]
