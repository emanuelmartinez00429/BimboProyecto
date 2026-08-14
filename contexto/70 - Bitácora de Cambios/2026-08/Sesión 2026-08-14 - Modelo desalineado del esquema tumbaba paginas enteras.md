---
title: "Sesión 2026-08-14 — El modelo C# desalineado del esquema tumbaba páginas enteras de Productos"
tags:
  - sesion
  - productos
  - paginacion
  - supabase
  - bugfix
  - modelado
date: 2026-08-14
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Opus 5 (Claude Code)
---

# Sesión 2026-08-14 — El modelo C# desalineado del esquema tumbaba páginas enteras de Productos

> [!success] Resultado
> Las cuatro FK de `productos` que son NULLABLE en la base (`id_presentacion`, `id_fabricante`, `id_categoria`, `id_pais`) pasaron a ser `int?` en el modelo C#, el DTO y el modal. Antes, cualquier fila con NULL en alguna de ellas hacía fallar la deserialización de **la consulta entera**, y la página no cargaba nunca. Build: `0 errores`, `0 advertencias`.

---

## Problema reportado

Reportado en tres rondas, cada vez con más precisión:

1. *"las paginaciones al navegar me están mostrando páginas erróneas o repetidas, más que todo en las de final"*
2. *"sale el número correcto pero la página es la misma que la anterior"*
3. *"sigue pasando lo mismo, entre la página 11-14 se muestra la página 1 repetida"* + *"no actualiza, hay más de 600 productos"*

El tercer reporte fue el que permitió cerrar el diagnóstico: el rango exacto (11–14) y el dato de que había más de 600 productos.

---

## Diagnóstico

### El dato que lo destrabó

La tabla había crecido de 505 a **680 productos** (el usuario venía insertando para probar Realtime). 680 ÷ 50 = **14 páginas** — o sea, los botones estaban bien; el conteo no era el problema.

Correlación exacta entre páginas afectadas y filas con FK en NULL:

```sql
with orden as (
  select id_producto, id_presentacion, id_fabricante, id_categoria, id_pais, id_estado,
         row_number() over (order by id_producto) as fila
  from productos
)
select ((fila - 1) / 50) + 1 as pagina,
       count(*) as filas,
       count(*) filter (where id_presentacion is null or id_fabricante is null
                          or id_categoria is null or id_pais is null
                          or id_estado is null) as filas_con_fk_null
from orden group by 1 order by 1;
```

| Página | Filas | Con FK NULL |
|---|---|---|
| 1–10 | 50 c/u | **0** ✅ |
| 11 | 50 | **45** ❌ |
| 12–13 | 50 c/u | **50** ❌ |
| 14 | 30 | **30** ❌ |

Las 175 filas nuevas tienen `id_presentacion` (y `contenido`) en NULL. Al ordenarse por `id_producto` quedan al final → páginas 11 a 14. Exactamente el rango reportado.

### La causa

En la base, cuatro FK de `productos` son nullable:

```sql
select column_name, is_nullable from information_schema.columns
where table_schema='public' and table_name='productos';
-- id_presentacion YES · id_fabricante YES · id_categoria YES · id_pais YES
-- (id_estado NO, codigo_producto NO, nombre_producto NO)
```

Pero `CapaDatos/Modelados/Productos/Productos.cs` las declaraba como `int` a secas. Newtonsoft no puede meter `null` en un `System.Int32` → lanza al deserializar → **falla la consulta completa, no solo la fila con el NULL** → `TryAsync` la captura y devuelve `r.Success = false`.

`CargarPaginaAsync` (`ProductosViewModel.cs:355-360`) sale entonces por su return temprano de error, que **no reasigna `PageRows`**:

```csharp
if (!r.Success)
{
    ErrorCarga = r.Error;
    IsLoading  = false;
    return;              // ← PageRows queda intacto
}
```

La grilla se queda con la última página que sí cargó — la 1, si el usuario venía de ahí.

### Por qué el número salía bien y el contenido no

Son **dos consultas distintas**, lanzadas en paralelo desde `GetPagedInternal`:

- Las **filas** salen de `query.Range(from,to).Get()` — deserializa a objetos C# → **es la que falla**.
- Los **conteos** salen de la RPC `contar_productos`, que solo hace `count(*)` y devuelve tres enteros → **nunca falla**, no deserializa filas.

De ahí el síntoma: `TotalPages` correcto (14 botones bien dibujados) con las filas de otra página.

---

## Corrección

El modelo tiene que reflejar el esquema real. No hay decisión de diseño acá: si la columna admite NULL, el modelo también.

| Archivo | Cambio |
|---|---|
| `CapaDatos/Modelados/Productos/Productos.cs` | `idPresentacion`, `idFabricante`, `idCategoria`, `idPais` → `int?` |
| `CapaAplicacion4/Productos/Dtos/ProductoDto.cs` | `IdPresentacion`, `IdFabricante`, `IdCategoria`, `IdPais` → `int?` |
| `CapaUI/.../Productos/ProductoModal.xaml.cs` | Campos `_idPresentacion`/`_idFabricante`/`_idCategoria`/`_idPais` → `int?`; se quitaron los `?? 0` de las lupas (inventaban un id 0 inexistente) y el guard pasó de `_idFabricante != 0` a `.HasValue` |
| `CapaDatos/Repositorios/productos_movimientos/RepositorioProducto.cs` | El armado de filtros ahora descarta explícitamente los ids nulos (`.Where(p => ... && p.idFabricante.HasValue)`) en vez de asumir que no los hay |

`ProductoCrudRepository` no necesitó cambios: `Map`, `CreateAsync` y `UpdateAsync` ya pasaban los valores directo, y `int? → int?` es la misma asignación.

Nota sobre `contenido`: 175 filas lo tienen en NULL, pero **no** causaba el crash — es `string`, un reference type, y en runtime acepta null (la anotación nullable de C# es solo de compilación). Además `Map` ya hacía `Contenido = p.contenidoProducto ?? string.Empty`.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores, 0 advertencias**.
- Correlación página↔NULLs verificada con SQL contra la base real (tabla de arriba).
- Esquema de nullabilidad verificado contra `information_schema.columns`.
- **Pendiente en runtime**: reiniciar la app, abrir Productos y navegar a las páginas 11–14; deben cargar sus filas reales en vez de repetir la 1.

---

## Lecciones

**1. El diagnóstico anterior fue incompleto y costó dos rondas de más.** La sesión previa ([[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]]) encontró y corrigió una regresión real —`RefrescarPaginacion()` repintando la grilla antes de tener datos— pero **esa no era la causa de lo que el usuario venía reportando**. El error fue dar por cerrado el diagnóstico sin verificar los datos contra la base: se razonó sobre un conteo de 505 productos que ya estaba viejo en contexto, cuando la tabla tenía 680. Con el `select count(*)` hecho al principio, el rango 11–14 se explicaba solo.

**2. Un modelo desalineado del esquema falla de la peor manera posible: en bloque y en silencio.** No falla la fila mala — falla la consulta entera, y el `catch` genérico de `TryAsync` la convierte en un `Result` fallido indistinguible de un error de red. Toda una pantalla queda inutilizable por una columna.

**3. El error nunca llegó al usuario.** `CargarPaginaAsync` asigna `ErrorCarga = r.Error`, pero eso no se tradujo en nada visible en la pantalla. Si hubiera aparecido el mensaje de deserialización, el diagnóstico habría sido inmediato. Ver **P-038**.

---

## Lo que NO se hizo

- **`ProductosInsertar.cs` sigue desalineado** (mismas cuatro columnas como `int`, más `id_tara` y `peso_teorico`). Solo lo usa `RepositorioProducto.ingresarProducto`, un método estático legacy sin llamadores activos — no se tocó para no ampliar el diff, pero es la misma bomba si algún día se usa. Anotado en P-038.
- **No se limpiaron las 175 filas con NULL.** Son datos de prueba del usuario; decidir si esas columnas deberían ser `NOT NULL` en la base es una decisión de negocio, no de este fix.
- **No se auditaron las otras tablas** por el mismo desalineo modelo↔esquema. Anotado en P-038.

## Estado en git

Sin commitear al cierre de esta nota. El árbol acumula varios trabajos del mismo día (catálogo de `unidad_medida`, logo de empresa, Realtime en columnas de join, dos fixes de paginación y este). Separar los commits queda a criterio del usuario.

---

## Relaciones

- [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]] — la regresión corregida antes de encontrar esta causa real
- [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]] — el primer fix de la cadena
- [[Deuda Técnica - Pendientes]] — origen de P-038
- [[Módulo Productos]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Supabase .NET]] — referencia del SDK
