---
title: "Sesión 2026-09-06 — Auditoría de commits a05f006 y 404796c"
tags:
  - sesion
  - auditoria
  - deuda-tecnica
date: 2026-09-06
branch: feat/fase8-MaquetadodeRoles
autor_cambios: otra sesión (Gemini, commits 404796c y a05f006), auditada y con dos hallazgos corregidos en esta
revisor: Claude Fernando (agente)
---

# Sesión 2026-09-06 — Auditoría de commits a05f006 y 404796c

> [!success] Resultado
> Otra sesión cerró P-044 (commit `404796c`) y P-034/P-037/P-039/P-042/P-047 (commit `a05f006`). Verificado cada uno contra código real, BD viva y build/tests. **La mayor parte se sostiene** — en particular P-037 (nuevo `PaginadorControl` compartido, con clamp real vía `CoerceValueCallback`) y P-039 (migración de tildes aplicada y verificada en las 8 tablas) son trabajo sólido. Dos hallazgos reales, ambos corregidos en esta sesión, y una marca de "resuelto" que no correspondía.

---

## Hallazgo 1 (bug) — `InvalidadorCacheRealtime` se suscribió a una tabla no publicada (P-034)

El commit `a05f006` agregó `roles` al mapa de tablas que `InvalidadorCacheRealtime` observa (de 9 a 10), para purgar la caché de `RolRepository` cuando otra sesión edita un rol. Pero nunca se agregó `roles` a la publicación `supabase_realtime`:

```sql
select tablename from pg_publication_tables where pubname='supabase_realtime' and tablename in ('roles','acciones_roles');
-- (vacío, antes de esta sesión)
```

Postgres/Supabase solo reenvía cambios de tablas publicadas — suscribirse a una que no lo está no lanza ningún error, simplemente **nunca llega ningún evento**. Es el mismo modo de falla silenciosa que P-034 documenta desde el origen (fabricante/proveedores/tara sin publicar en 2026-08-13), reintroducido para una tabla nueva en el mismo commit que reclama haberlo resuelto.

**Corregido:** `supabase/migrations/20260906170000_publicar_roles_en_realtime.sql` (`ALTER PUBLICATION supabase_realtime ADD TABLE public.roles;`), aplicada con `apply_migration` y reverificada:

```sql
select tablename from pg_publication_tables where pubname='supabase_realtime' and tablename='roles';
-- roles
```

RLS de `roles` ya es `USING (true)` para `authenticated` (`roles_select_authenticated`), así que publicarla no expone nada que un usuario autenticado no pudiera leer ya por `SELECT` directo — sin riesgo de fuga nueva.

**Ejemplo real:** Usuario A edita el rol "Supervisor" desde la pantalla de Roles en una terminal; Usuario B tiene la app abierta en otra terminal con ese catálogo ya cacheado. Antes: el caché de B nunca se enteraba — el `Observar("roles", ...)` estaba vivo pero sordo. Ahora: el cambio de A llega por Realtime y purga la caché de B en el acto.

---

## Hallazgo 2 (bug) — Atajo de Enter en `SelectorCatalogoModal` reabría el hueco que el código ya evitaba para el doble clic (P-044)

El commit `404796c` agregó, dentro de `Confirmar()` (compartido por el botón "Elegir" y la tecla Enter), un atajo: en modo múltiple, si `_marcados.Count == 0`, emite directamente la fila resaltada (`Dg.SelectedItem`).

El problema: clickear el checkbox de una fila **también la selecciona** (mismo `Dg.SelectedItem`) — así lo advierte el comentario original de `Dg_DoubleClick` ("sin marcas no se emite nada... agregaría justo la última que se tocó"). Si el operador tilda una fila y se arrepiente (la destilda), `_marcados` vuelve a quedar en 0 pero la fila **sigue seleccionada**. El atajo nuevo la emitía de todos modos — justo el escenario que el propio comentario de doble clic ya prevenía.

El botón "Elegir" no sufre esto porque `BtnElegir.IsEnabled` exige `marcados > 0` en modo múltiple (queda deshabilitado con 0 marcas) — pero el atajo de teclado no pasaba por esa misma condición: Enter podía confirmar algo que el mouse tenía bloqueado en el mismo estado.

**Corregido:** se agregó `_idsTocados` (HashSet de ids que pasaron por el checkbox alguna vez en esta apertura del modal — tildados y después destildados también cuentan), poblado en los dos puntos que tocan `_marcados` (`Marcado_Changed` y `ToggleMarcado`) y limpiado en "Limpiar marcas". El atajo de Enter ahora exige que la fila **nunca** haya sido tocada, no solo que esté "sin marcar ahora mismo".

**Ejemplo real:** en un selector multi-selección, el usuario tilda "Proveedor X" para agregarlo, se da cuenta que es el equivocado y lo destilda, después navega con las flechas hasta "Proveedor Y" sin tocarlo y presiona Enter. Antes: si el foco/selección seguía en "Proveedor X" al momento de Enter (dependiendo de cómo maneje WPF el foco tras destildar), se hubiera emitido X por error. Ahora: el atajo solo puede disparar sobre una fila nunca tocada — X quedó excluido del atajo para siempre en esta apertura, sin importar si vuelve a quedar seleccionada.

---

## Hallazgo 3 (documentación) — P-042 se marcó resuelto citando trabajo que no es el que el ítem pide

El commit `a05f006` cerró P-042 con `[x] Resuelto`, pero el trabajo que cita (promoción de `CeldaInput`, trigger de error en `InputBox`, promoción de `PageBtn`/`ActivePageBtn`) no toca `PesajeModalStyles.xaml` en absoluto — ni siquiera aparece en el diff del commit. Verificado:

- `MInput` (línea 28 de `PesajeModalStyles.xaml`): su trigger de foco sigue siendo solo un cambio de `BorderBrush` a blanco — **no tiene** el aro verde (`EmpresaPrimaryBrush` + grosor 2) de `ModalInput`/`InputBox`/`CeldaInput`.
- `MSegBtn` (línea 129): **no tiene ningún trigger de foco**, ni siquiera el sutil de `MInput`.

Lo que sí se resolvió es real y verificado, pero es la deuda de la "3ª copia" (`CeldaInput`, anotada 2026-09-05 en el propio ítem como un problema relacionado pero distinto) y la sustancia real de P-047 (`InputBox`) — no la divergencia de fondo entre `MInput`/`MSegBtn` (Pesaje) y `ModalInput`/`ModalSegBtn` (global) que P-042 describe desde el origen.

**Corregido:** P-042 vuelve a `[~] Parcial` en el detalle y en la tabla de historial, documentando exactamente qué se cerró (la 3ª copia) y qué sigue exactamente igual que en 2026-08-19 (la decisión pendiente: ¿el tamaño compacto de Pesaje es deliberado?).

---

## Lo que se verificó correcto sin cambios

- **P-037** (`PaginadorControl.xaml.cs`): `Page` es una `DependencyProperty` con `CoerceValueCallback` que clampea contra `TotalPages`, y por el binding `Mode=TwoWay` el valor clampeado se propaga de vuelta al ViewModel — cierra el punto 1 (clamp) sin tocar el setter del VM. El panel de botones se reconstruye completo en cada cambio de `Page`/`TotalPages`, así que el resaltado del botón activo siempre refleja el estado real — cierra el punto 2. Los 9 pares View/ViewModel dejaron de duplicar `CalcularPaginas`/`RefrescarPaginacion` — cierra el punto 3. `UsuariosView.xaml.cs` en particular separó el `ItemsSource` del rebuild de botones, la regresión concreta que el ítem documentaba.
- **P-039**: las 8 columnas generadas (`busqueda_fabricante`, `busqueda_proveedor`, `busqueda_categoria`, `busqueda_presentacion`, `busqueda_empleado`, `busqueda_usuario` ×2, `busqueda_bitacora`) existen en la BD real, no solo en el archivo de migración. Los 6 repositorios migraron a `TextoBusqueda.Normalizar()` + `Filter("busqueda_X", Op.ILike, ...)`, consistente con el patrón ya usado por Productos (ADR-018). Tests nuevos verifican el caso `ñ→n` con datos reales del dominio (fabricantes, proveedores, empleados).
- **P-047**: acotado correctamente a `ModalInput`/`InputBox` (no a Pesaje, que es P-042) — la corrección de `VerticalAlignment` y el trigger de error en `InputBox` son reales y consistentes con el fix de P-043 de la sesión anterior.

## De paso: G3 de P-031 se cerró mientras se documentaba esta auditoría

La misma sesión de Gemini quitó `CacheMode="BitmapCache"` de `Sidebar` y `BrandBlock` en `MainWindow.xaml` (ambos con `Width` animado — el peor caso para `BitmapCache`, que re-rasteriza el bitmap completo en cada frame) mientras esta auditoría estaba en curso. No forma parte de los commits `a05f006`/`404796c` revisados arriba; quedó en el working tree y se incluye en el mismo commit de esta sesión por conveniencia. De los 11 hallazgos de P-031 solo queda **G11** (recursos duplicados sin `Freeze`).

## Verificación

```bash
dotnet build BimboProyecto.sln --no-incremental
dotnet test BimboProyecto.sln
```
0 errores, 286/286 (incluye los fixes de esta sesión sobre el mismo build).

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-034, P-037, P-039, P-042, P-044, P-047
- [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]] — auditoría anterior de la misma serie
- [[Módulo Roles]], [[Anatomía compartida de los modales]]
