---
title: Sesión 2026-08-12 — Contadores TOTAL/ACTIVOS/INACTIVOS en Productos y Roles
type: sesion
status: vigente
tags:
  - sesion
  - bimbo
  - wpf
  - rbac
  - productos
date: 2026-08-12
updated: 2026-08-12
summary: "Corregido el conteo TOTAL de Productos, que quedaba contaminado por el filtro de estado, y reconstruidas las pastillas TOTAL/ACTIVOS/INACTIVOS de Roles con el…"
scope:
  - CapaDatos/Repositories/Productos
  - CapaUI/Formularios/Principal/Pantallas/Roles
symbols:
  - Activos
  - CargarAsync
  - CargarPaginaAsync
  - CargarPaginaSilenciosamenteAsync
  - ControlTemplate
  - DataContext
  - GetConteosRpcAsync
  - GetPagedInternal
  - HeaderedContentControl
  - IdEstado
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude
---

# Sesión 2026-08-12 — Contadores TOTAL/ACTIVOS/INACTIVOS en Productos y Roles

> [!success] Resultado
> Corregido el conteo TOTAL de Productos, que quedaba contaminado por el filtro de estado, y reconstruidas las pastillas TOTAL/ACTIVOS/INACTIVOS de Roles con el mismo patrón de binding directo que usa Productos — antes no mostraban ningún número.

---

## Problema / motivo

El usuario reportó dos síntomas relacionados en la misma sesión:

1. **Productos**: la pastilla **TOTAL** del encabezado deja de mostrar el total real
   del catálogo y pasa a coincidir con **ACTIVOS** (o **INACTIVOS**) cuando el filtro
   segmentado de estado no está en "Todos" — debería mantenerse fijo sin importar el
   filtro.
2. **Roles**: las pastillas **TOTAL/ACTIVOS/INACTIVOS** del encabezado nunca
   mostraban ningún número (quedaban en blanco), pese a que el `RolesViewModel` sí
   calculaba y actualizaba esos valores correctamente — el badge chico junto al
   buscador, que bindea las mismas propiedades con un `Run` directo, sí funcionaba.

## Causa raíz

### A) Productos — RPC de conteo contaminado por el filtro de estado

`ProductoCrudRepository.GetPagedInternal` reutilizaba la misma instancia de
`ProductoFiltros` (con `IdEstado` incluido cuando el filtro no es "Todos") tanto para
la query paginada de filas como para `GetConteosRpcAsync`, que reenvía `IdEstado`
como `p_estado` al RPC `contar_productos`. Al filtrar por "Activos", el servidor
termina acotando el conteo total al mismo subconjunto — de ahí que TOTAL y ACTIVOS
coincidieran.

> [!warning] No se pudo confirmar la función SQL en vivo
> El proyecto Supabase accesible por MCP en esta sesión no es el de Bimbo (la app
> apunta a `bzmmrifjgzlvsphctais.supabase.co`; el único proyecto listado por el MCP
> era otro, `mxarlisuueovxvttytcm` / "Restaurante"). El fix se aplicó igual desde el
> lado C#, que es correcto independientemente del comportamiento actual del RPC:
> los conteos de desglose por estado no deben filtrarse a su vez por estado.

### B) Roles — indirección `HeaderedContentControl` + `TemplateBinding` que no reflejaba el valor

Las pastillas de Roles usaban un `HeaderedContentControl` con estilo
`RolPastillaStat`, cuyo `ControlTemplate` mostraba el valor con
`Text="{TemplateBinding Content}"` sobre un `TextBlock`. El ViewModel calculaba bien
`TotalAcciones`/`Activos`/`Inactivos` (confirmado leyendo `CargarAsync`,
`OnRolSeleccionadoChanged` y `Recalcular`) — el problema era la ruta
`Content (object, boxeado desde int)` → `TemplateBinding` → `TextBlock.Text`, que no
terminaba de renderizar el número en pantalla. Productos nunca tuvo este problema
porque arma sus 3 pastillas a mano (`Border`/`StackPanel`/`TextBlock`) con
`Text="{Binding TotalCount}"` directo contra el `DataContext`, sin control templado
de por medio.

## Cambios aplicados

- **`CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`** —
  `GetPagedInternal` arma ahora un `ProductoFiltros` separado para
  `GetConteosRpcAsync` (mismo `IdFabricante`/`IdPais`, `IdEstado` siempre `null`), sin
  tocar el filtro que usa la query paginada. Cubre los tres call-sites del
  ViewModel (`CargarPaginaAsync`, `CargarPaginaSilenciosamenteAsync`,
  `RefrescarConteosAsync`), que pasan todos por este método.
- **`CapaUI/Formularios/Principal/Pantallas/Roles/RolesView.xaml`** — las 3
  pastillas del encabezado se reconstruyeron como `Border > StackPanel > TextBlock`
  con `Text="{Binding TotalAcciones}"` / `Activos` / `Inactivos` directos, igual que
  `ProductosView.xaml`. Se mantuvo la paleta ya usada en Roles (no es un cambio
  visual, solo de mecanismo de binding).
- **`CapaUI/Formularios/Principal/Pantallas/Roles/RolesResources.xaml`** — se
  eliminó el estilo `RolPastillaStat`, que quedó sin ningún otro uso.

La semántica de las etiquetas de Roles no cambió: TOTAL = tamaño del catálogo de
acciones, ACTIVOS/INACTIVOS = asignadas/no asignadas al **rol seleccionado en el
combo**. No existe un estado "activo/inactivo" propio de la acción a nivel de
catálogo — solo existe `id_estado` en la tabla puente `accion_rol` (confirmado en
`RolPermisoRepository.cs`) — así que replicar el concepto de Productos 1:1 (estado
propio de la entidad) requeriría un cambio de esquema, no solo de UI; lo que se
corrigió acá es que el número aparezca, no el significado.

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**. Advertencias sin cambios
  respecto a antes de esta sesión (todas preexistentes, nada nuevo en los archivos
  tocados).
- Pendiente: prueba manual en runtime por parte del usuario (cambiar el filtro
  Activos/Inactivos/Todos en Productos y confirmar que TOTAL no se mueve; abrir
  Roles y confirmar que las 3 pastillas muestran número desde el primer frame).

## Lo que NO cambió

- No se tocó la función SQL `contar_productos` — no se pudo inspeccionar en esta
  sesión, ver advertencia arriba.
- No se tocó el esquema de la tabla `accion` ni `accion_rol`.
- No se agregó un estado "activo/inactivo" propio a las acciones del catálogo de
  Roles.

---

## Relaciones

- [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] — sesión previa del mismo día, mismo módulo
- [[Módulo Productos]]
- [[Arquitectura Actual]]
- [[Deuda Técnica - Pendientes]]
