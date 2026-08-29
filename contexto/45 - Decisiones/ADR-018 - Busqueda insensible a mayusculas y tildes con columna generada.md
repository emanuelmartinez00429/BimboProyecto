---
title: ADR-018 — Búsqueda insensible a mayúsculas y tildes con columna generada
type: adr
status: vigente
tags:
  - adr
  - decision
  - busqueda
  - supabase
  - postgres
date: 2026-08-14
updated: 2026-08-14
summary: "Todos los buscadores del proyecto (Productos, picker de Pesaje, combos de filtro vía ComboFiltro) usaban ILike directo sobre las columnas crudas (nombreproducto,…"
scope:
  - CapaAplicacion4/Common
symbols:
  - ComboFiltro
  - EmpleadoRepository
  - Filter
  - FormD
  - ILike
  - ProductoSearchRepository
estado: aceptado
---

# ADR-018 — Búsqueda insensible a mayúsculas y tildes con columna generada

## Contexto

Todos los buscadores del proyecto (Productos, picker de Pesaje, combos de filtro vía `ComboFiltro`) usaban `ILike` directo sobre las columnas crudas (`nombre_producto`, `codigo_producto`) o `string.Contains(OrdinalIgnoreCase)` en memoria. Ninguno de los dos ignora tildes: buscar `azucar` no encontraba `AZÚCAR`. Pedido explícito del usuario: que el buscador funcione escribiendo la palabra con o sin tilde, en cualquier dirección.

La normalización tiene que vivir en **dos runtimes distintos** que no comparten proceso: el servidor (Postgres/PostgREST, para las queries `ILike` server-side) y el cliente (C#, para el filtrado en memoria de `ComboFiltro`). Si las dos normalizaciones divergen, un término que el cliente ya normalizó de una forma deja de corresponderse con lo que el servidor indexó de otra, y el buscador falla en silencio — sin excepción, solo con menos resultados de los que debería.

## Decisión

1. **Función SQL `public.sin_tildes(text)`** — minúsculas + `unaccent`, con la variante de **dos argumentos** de `unaccent` (diccionario explícito `'extensions.unaccent'::regdictionary`), no la de uno. Es la única forma `IMMUTABLE` — la de un argumento resuelve el diccionario vía `search_path`, lo que la vuelve `STABLE`, y una columna generada exige `IMMUTABLE`.
2. **Columna generada `STORED`** por tabla (`productos.busqueda_producto = sin_tildes(nombre_producto || ' ' || codigo_producto)`), con **índice GIN de trigramas** (`gin_trgm_ops`) encima — es el único tipo de índice que sirve para `ILIKE '%algo%'` (un `btree` no sirve porque el patrón no está anclado al inicio).
3. **`TextoBusqueda.Normalizar()`** en C# (`CapaAplicacion4/Common/TextoBusqueda.cs`) — descomposición Unicode (`FormD`) + descarte de marcas diacríticas + `ToLowerInvariant()`. Usado tanto para normalizar el término antes de mandarlo al `ILike` server-side como para el filtrado en memoria de `ComboFiltro` (`TextoBusqueda.Contiene`).

El único filtro pasa a ser un `ILike` contra la columna normalizada, con el término normalizado del mismo modo — reemplaza el `.Or()` de dos columnas que existía antes.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| Columna generada `STORED` + índice GIN de trigramas | Rápido (índice real), transparente para el repositorio — un `Filter` más | Una `ALTER TABLE` + un `CREATE INDEX` por tabla; espacio extra en disco | ✅ |
| Índice funcional sobre `sin_tildes(columna)` directo (sin columna nueva) | Sin `ALTER TABLE`, una sola sentencia de índice por tabla | El repositorio tiene que envolver la columna en `sin_tildes(...)` en cada query en vez de nombrar una columna — más ruido en el C#, y algunos SDKs de PostgREST no dejan filtrar sobre una expresión, solo sobre nombres de columna | ❌ (pendiente de confirmar si el SDK lo permite; si algún día se prueba y funciona, es la opción más liviana para las 7 tablas que faltan) |
| `unaccent()` en cada query, sin índice ni columna | Cero cambios de esquema | Sin índice usable → `seq scan` completo por búsqueda; inaceptable en tablas grandes | ❌ |
| Normalizar SOLO en C# (cliente), sin tocar la BD | Un solo lugar de verdad | El `ILike` server-side seguiría comparando contra la columna cruda con tildes — solo arreglaría el filtrado en memoria de los combos, no el buscador real contra la base | ❌ |
| `unaccent()` de un argumento (resuelve diccionario por `search_path`) | Sintaxis más corta | `STABLE`, no `IMMUTABLE` → Postgres rechaza usarla en una columna generada | ❌ |

## Consecuencias

- **Se gana:** búsqueda insensible a tildes en Productos (buscador con sugerencias, picker de Pesaje, los 4 combos de filtro vía `ComboFiltro`) con rendimiento de índice real, no `seq scan`.
- **Se sacrifica:** el patrón exige tocar cada tabla individualmente — no hay forma de centralizarlo en un solo lugar del esquema (una columna generada pertenece a una tabla; no existe el concepto de columna generada cross-tabla en Postgres). Ver hilo de esta pregunta con el usuario, sesión 2026-08-14.
- **Deuda explícita — P-039:** el patrón NO se aplicó todavía a Fabricantes, Proveedores, Categorías, Empleados, Usuarios, Bitácora, ni al buscador universal (`ProductoSearchRepository`, `EmpleadoRepository`). Cada uno necesita su propia migración (columna generada + índice) siguiendo exactamente este mismo molde. Ver [[Deuda Técnica - Pendientes]].
- **Riesgo a vigilar:** si `TextoBusqueda.Normalizar()` (C#) y `sin_tildes()` (SQL) alguna vez divergen — por ejemplo, si a uno se le agrega manejo de guiones y al otro no — el término que manda el cliente deja de corresponderse con la columna indexada y el buscador empieza a devolver de menos, sin ningún error visible. Tocar los dos archivos juntos siempre.

---

## Relaciones

- [[Módulo Productos]] — primer (y hasta ahora único) módulo con el patrón aplicado
- [[Deuda Técnica - Pendientes]] — P-039, rollout a las tablas restantes
- [[Bug - CREATE OR REPLACE FUNCTION con distinta cantidad de parametros duplica en vez de reemplazar (Postgres)]] — bug encontrado en la migración de `contar_productos` durante esta misma sesión, tabla distinta pero mismo día de trabajo
- [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]] — el filtro de Categoría (mismo día) fue lo que motivó tocar la barra de filtros completa
