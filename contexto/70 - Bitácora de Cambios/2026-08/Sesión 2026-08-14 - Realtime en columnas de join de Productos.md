---
title: Sesión 2026-08-14 — Realtime en las columnas de join de Productos
type: sesion
status: vigente
tags:
  - sesion
  - productos
  - realtime
  - cache
date: 2026-08-14
updated: 2026-08-14
summary: "La grilla de Productos ahora refleja en vivo los cambios de fabricante, proveedor, categoría, país, presentación, tara y unidad — antes solo escuchaba la tabla…"
scope:
  - CapaDatos/Realtime
  - CapaUI/Formularios/Principal/Pantallas/Productos
symbols:
  - BaseOutputPath
  - CargarDatosAsync
  - CargarPaginaSilenciosamenteAsync
  - CatalogoCache
  - Observar
  - ProductoDto
  - ProductosViewModel
  - SelectPara
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Opus 5 / Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Realtime en las columnas de join de Productos

> [!success] Resultado
> La grilla de Productos ahora refleja en vivo los cambios de fabricante, proveedor, categoría, país, presentación, tara y unidad — antes solo escuchaba la tabla `productos` y esas siete columnas, que salen de un join, se quedaban con el nombre viejo hasta recargar la pantalla. Se publicaron cinco tablas nuevas en Realtime y se conectó la suscripción existente. Build: `0 errores`.

---

## Problema / motivo

Reportado por el usuario: *"el realtime funciona en los campos de la tabla productos pero cuando hay un campo que realmente es un join de otra tabla ese no escucha cambios — ejemplo si cambio el nombre del fabricante [...] dentro del modal o de la propia tabla de productos nunca actualiza"*.

## Diagnóstico

`ProductosViewModel` solo suscribía `Observar("productos", OnCambioProducto)` más un `Observar("fabricante", …)` que ya estaba muerto (ver [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]], P-034). Pero `SelectPara` arma la fila con:

```csharp
$"*, presentacion_producto(*), {fabricante}, categoria(*), paises(*), tara(*, unidad_medida(*)), unidad_medida(*)"
```

TARA, FABRICANTE, PROVEEDOR, PAÍS, PRESENTACIÓN, CATEGORÍA y la unidad salen de ahí, no de `productos`. Renombrar un fabricante no modifica ninguna fila de `productos` → no se emite ningún evento → la grilla y el modal (que hereda el `ProductoDto` que la grilla tenía capturado) se quedan con el nombre anterior.

Segunda capa del problema, verificada contra la base antes de tocar nada:

```sql
select tablename from pg_publication_tables where pubname = 'supabase_realtime';
-- categoria, empleados, entradas_producto, movimiento_productos,
-- movimientos, paises, productos, usuarios
```

**Cuatro de las tablas del join no estaban publicadas**: `fabricante`, `proveedores`, `presentacion_producto`, `tara`. Suscribirse a ellas no habría servido de nada sin este paso.

Se confirmó que las cinco candidatas (`fabricante`, `proveedores`, `presentacion_producto`, `tara`, `unidad_medida`) tienen exactamente la misma forma que las tres que ya funcionan — RLS activo, política de `SELECT` para `authenticated`/`PUBLIC`, `replica identity` default — así que publicarlas es el mismo cambio ya probado, no una apuesta.

## Decisión de diseño

Se evaluó un suscriptor global de vida larga (lo que le faltaba a la mitigación de Realtime descartada en [[ADR-015 - Cache de catalogos mostrar y revalidar]]) contra una suscripción por pantalla. Se eligió **por pantalla**: a diferencia de `CatalogoCache`, que es `static` y sobrevive a que la pantalla se cierre, acá alcanza con que `ProductosViewModel` escuche mientras está vivo — si el cambio ocurre con Productos cerrado, `CargarDatosAsync` reconsulta al volver a entrar. No hacía falta construir infraestructura nueva.

```
UPDATE fabricante ──websocket──> Observar("fabricante")
                                      ↓
                        CatalogoCache.Invalidar("fabricantes")
                                      ↓
                     CargarPaginaSilenciosamenteAsync()  ← sin spinner, sin parpadeo
```

## Cambios aplicados

**Migración `publicar_catalogos_en_realtime`** (Supabase, `bzmmrifjgzlvsphctais`):

```sql
alter publication supabase_realtime
  add table public.fabricante,
            public.proveedores,
            public.presentacion_producto,
            public.tara,
            public.unidad_medida;
```

Verificado después: la publicación pasó de 8 a 13 tablas.

**`CapaDatos/Realtime/RealtimeService.cs`** — `_pkColumns` tenía la clave `"taras"` para una tabla que se llama `tara` (desalineo detectado en la sesión anterior, P-034). Corregido, y agregado `["unidad_medida"] = "id_unidad"`. Sin esto cada evento de esas tablas iba a loguear el warning de PK desconocida.

**`CapaUI/Formularios/Principal/Pantallas/Productos/ProductosViewModel.cs`** — el `Observar("fabricante", …)` muerto se reemplazó por:

```csharp
private static readonly (string Tabla, string ClaveCache)[] TablasDeJoin =
{
    ("fabricante",            "fabricantes"),
    ("proveedores",           "proveedores"),
    ("categoria",             "categorias"),
    ("paises",                "paises"),
    ("presentacion_producto", "presentaciones"),
    ("tara",                  "taras"),
    ("unidad_medida",         "unidades"),
};

private void OnCambioCatalogo(string claveCache)
{
    if (Disposed) return;
    CatalogoCache.Invalidar(claveCache);
    _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);
}
```

En `CargarDatosAsync`, junto al `Observar("productos", …)` existente:

```csharp
foreach (var (tabla, clave) in TablasDeJoin)
    Observar(tabla, _ => OnCambioCatalogo(clave));
```

No se escribió infraestructura nueva: `Observar` ya es idempotente por tabla y se da de baja solo en `Dispose()`; `CargarPaginaSilenciosamenteAsync` ya evita el spinner; y su bandera interna `_refrescoSilencioso` (de la sesión anterior) colapsa la ráfaga si un mismo cambio dispara varios eventos seguidos.

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores** (compilado contra un `BaseOutputPath` alterno porque la app y Visual Studio tenían los DLL bloqueados; ninguna de las 50 advertencias cae en los archivos tocados).
- `select tablename from pg_publication_tables where pubname='supabase_realtime'` → confirma las 13 tablas, incluidas las 5 nuevas.
- **Pendiente en runtime** (requiere reiniciar la app con el build nuevo): abrir Productos, renombrar un fabricante desde otra sesión o desde SQL, y confirmar que la columna FABRICANTE cambia sola sin spinner ni recarga de pantalla. Repetir con categoría, país, presentación, tara y unidad. Confirmar que un solo cambio produce un solo refresco.

## Lo que NO cambió

- **El modal abierto en el momento del cambio** sigue mostrando la foto del `ProductoDto` que tenía al abrirse; se corrige recién al reabrirlo, no en vivo.
- **Otras pantallas con el mismo patrón** (Fabricantes muestra PROVEEDOR por join y solo observaba `fabricante`) no se tocaron. Con `proveedores` ya publicada, cerrar ese caso es una línea si se pide.
- **P-034 no se cierra del todo.** Se resolvieron los dos primeros pasos de su "solución de fondo" (publicar las tablas, corregir `_pkColumns`); el tercero —un suscriptor de vida larga a nivel de aplicación, necesario si algún día `CatalogoCache` u otra caché global depende de Realtime sin una pantalla activa que la sostenga— sigue sin existir. No hacía falta para este caso porque la suscripción vive con la pantalla.

## Estado en git

Sin commitear al cierre de esta nota. El árbol de trabajo mezcla estos cambios con otro trabajo en curso (catálogo de `unidad_medida` con categoría, logo de empresa dinámico — ver [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]] y [[Sesión 2026-08-14 - Logo de empresa dinamico en login]]) hecho en paralelo; separar los commits queda a criterio del usuario.

---

## Relaciones

- [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]] — origen de P-034 y de `CatalogoCache`/`CargarPaginaSilenciosamenteAsync`, reusados acá
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — la alternativa de Realtime que se descartó ahí para la caché; acá sí se aplica, pero acotada a la pantalla
- [[Módulo Productos]]
- [[Deuda Técnica - Pendientes]] — actualiza P-034
- [[Gestor Realtime - Diseño Arquitectónico]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]
