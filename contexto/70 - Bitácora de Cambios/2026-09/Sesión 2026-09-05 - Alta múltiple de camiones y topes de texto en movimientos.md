---
title: "Sesión 2026-09-05 — Alta múltiple de camiones y topes de texto en movimientos"
tags:
  - sesion
  - pesaje
  - validacion
  - supabase
date: 2026-09-05
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
---

# Sesión 2026-09-05 — Alta múltiple de camiones y topes de texto en movimientos

> [!success] Resultado
> El alta de camiones pasó de un formulario de a uno a una tabla de hasta 5 en una sola pasada, con el proveedor elegido por fila. De paso Pesaje entró por fin al esquema de validación en tres capas: `ReglasCamion` en Dominio, `ValidadorFormulario` en la UI y una migración que le puso tope real a las dos columnas de texto de `movimientos` — [[Deuda Técnica - Pendientes|P-045]] queda parcialmente resuelta.

---

## Problema / motivo

En el andén los camiones llegan juntos. `CamionModal` da de alta **uno** por apertura, así que registrar la descarga de la mañana significaba abrir y cerrar el mismo modal cinco veces seguidas, reeligiendo el proveedor cada vez.

Fernando pidió el rediseño con un mockup: tabla numerada, filas que se ocupan con "Registrar otro camión", basurero por fila, contador "N de 5". Y explícitamente: *"coloca la validación del máximo de caracteres como en la base y con el servicio de validación del sistema"*.

Al ir a buscar ese "máximo como en la base" apareció que **no existía**: `movimientos.placa_vehiculo` era `character varying` sin longitud declarada y `observaciones` era `text`. Es exactamente lo que [[Deuda Técnica - Pendientes|P-045]] había registrado el 2026-08-20 y nadie había cerrado.

---

## Decisiones acordadas con Fernando

Tres puntos que el mockup no resolvía, consultados antes de escribir código:

| Pregunta | Decisión |
|---|---|
| El mockup no tiene Proveedor, pero `movimientos.id_proveedor` es NOT NULL | **Columna Proveedor por fila**, con lupa propia. Se descartó "un proveedor para todo el proceso" y "sin proveedor acá" (esta última exigía hacer la FK nullable) |
| No hay tope en la BD que reflejar | **Migración + reglas de dominio**: alinear la columna y el `ReglaCampo`, no inventar un número solo en la pantalla |
| ¿Reemplaza a `CamionModal`? | **Solo el alta.** Editar un camión sigue siendo de a uno |

---

## Cambios aplicados

### Base de datos

`supabase/migrations/20260905215247_limitar_texto_movimientos.sql`

```
movimientos.placa_vehiculo   varchar sin límite → varchar(20)
movimientos.observaciones    text               → varchar(500)
```

El 500 sigue el criterio de [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]] (`20260902184000_limitar_columnas_texto_a_varchar_500`). Verificado antes de aplicar sobre las 22 filas existentes: máximos reales 8 y 46 caracteres, cero filas en riesgo de truncarse.

> [!warning] El `ALTER` falló al primer intento por una vista
> Postgres rechaza `ALTER COLUMN … TYPE` sobre una columna que lee una vista, aunque el tipo nuevo sea compatible: `v_mov_productos_resumen` selecciona `m.placa_vehiculo`. Hubo que soltarla y recrearla **idéntica** dentro de la misma migración, conservando a mano las dos cosas que no viajan en un `CREATE VIEW` — `security_invoker = on` y los `GRANT`. El detalle completo, y el checklist para la próxima vez, quedó en [[Supabase - Vistas SQL, RLS y security_invoker]].

### Dominio

`CapaDominio/Reglas/ReglasEntidades.cs` — `ReglasCamion` nueva: `Proveedor` (obligatorio), `Placa` (obligatorio, 20) y `Descripcion` (500).

> [!note] Se llama `ReglasCamion`, no `ReglasPesaje`
> P-045 proponía una sola clase `ReglasPesaje`. Se prefirió `ReglasCamion` porque el archivo se organiza **por entidad** (`ReglasProducto`, `ReglasCategoria`, `ReglasProveedor`…) y las columnas que faltan pertenecen a otras dos tablas: `movimiento_productos` y `entradas_producto`. Cuando les toque, van como `ReglasProductoCamion` y `ReglasEntradaPesaje`, no amontonadas acá.

### UI

**`CapaUI/…/Pesaje/Modales/RegistroCamionesModal.xaml(.cs)`** — modal nuevo.

Tabla de 5 filas **fijas** sobre tarjeta blanca, con el cromo azul del resto de los modales de Pesaje. Las filas no se crean ni se destruyen: se *ocupan*. Las libres muestran "Espacio libre · usá «Registrar otro camión» para ocuparlo".

Ese detalle no es cosmético — es lo que hace que el número de cada fila **nunca cambie**, y por lo tanto que las etiquetas que el validador capturó al construirse ("La placa del camión 3…") sigan siendo ciertas después de quitar una fila del medio. Al borrar, se mueven los *valores* hacia arriba, no las filas.

Validación por fila, cada una con su propio `ValidadorFormulario`:

```csharp
Validador = ValidadorFormulario.Nuevo()
    .Campo(CajaPlaca, $"La placa del camión {Numero}").Segun(ReglasCamion.Placa)
    .Catalogo(CajaProveedor, $"El proveedor del camión {Numero}", () => IdProveedor.HasValue)
        .Segun(ReglasCamion.Proveedor)
    .Campo(CajaDescripcion, $"La descripción del camión {Numero}").Segun(ReglasCamion.Descripcion)
    .ValidarAlSalirDelCampo();
```

El número de fila va **dentro de la etiqueta** a propósito: en una tabla de cinco camiones, "La placa es obligatoria" no dice cuál. Como `Segun` aplica `TopePreventivo`, el `MaxLength` del TextBox queda puesto solo — el operario no puede ni escribir de más.

Las cajas nacen dentro del `DataTemplate`, así que el code-behind no las ve por nombre: cada una se presenta en su `Loaded` y la fila arma el validador cuando ya tiene las tres. Se arma **una sola vez** — `ValidarAlSalirDelCampo()` suscribe handlers y rearmarlo sobre las mismas cajas los duplicaría.

Tres reglas de tabla que el validador de campo no cubre, y que viven en el modal:

1. **Cupo del andén** — contado por placa distinta, no por fila: dos filas con la misma placa (un camión con carga de dos proveedores) ocupan un solo lugar de los 5.
2. **Duplicado interno** — misma placa + mismo proveedor en dos filas.
3. **Duplicado contra lo abierto** — la placa ya tiene recepción abierta con ese proveedor.

El aviso ámbar de "placa abierta con otro proveedor" se mantiene igual que en `CamionModal`: informa, no bloquea.

**`CeldaInput`, estilo local del modal.** No reusa `MInput` por dos razones: el borde `#80FFFFFF` de `MInput` es invisible sobre la tarjeta blanca, y `MInput` **no reacciona** a `validacion:Validacion.TieneError` ([[Deuda Técnica - Pendientes|P-042]]). En una tabla de 5 filas el borde rojo es lo único que dice *cuál* fila está mal sin desarmar el layout.

**`PesajeViewModel.RegistrarCamionesAsync`** — persiste el lote. Son N INSERT sueltos sin transacción; si uno falla se corta ahí y se informa cuántos entraron. Lo que ya entró **no** se deshace: anular movimientos que quizá ya se están pesando es peor que dejar un camión de más, que se quita con el basurero de su fila. Registrado como P-053.

**`PesajeView`** — `BtnNuevoProceso` abre el modal nuevo; `BtnEditarProceso` sigue yendo a `CamionModal`. Si el lote se guarda a medias el modal queda abierto pero se le descuentan las filas ya persistidas y se le refresca la lista de recepciones abiertas (`AplicarGuardadoParcial`) — sin eso, volver a tocar "Guardar" las registraría dos veces.

---

## Bug encontrado y corregido en la misma sesión

Fernando probó y reportó: *"genero un tercero pero no quería generarlo, lo borro y me borra ese y el anterior que escribí también"*.

Era real, y el mecanismo vale recordarlo porque es fácil de reintroducir. Al borrar una fila, los valores de las de abajo suben un lugar y hay que vaciar la del final. El código elegía cuál vaciar así:

```csharp
var ultima = _filas.Last(f => f.Activa);   // ← el bug
```

El bucle que sube los valores **también copia el flag de ocupada**. Con los camiones 1, 2 y 3 cargados y borrando el 3:

1. La fila 3 copia de la fila 4, que está libre → la fila 3 ya queda libre sola.
2. Recién ahí se preguntaba cuál era la última ocupada → como la 3 ya no lo era, respondía **la fila 2**.
3. Y la vaciaba.

Dos camiones desaparecían de un clic. La fila a vaciar es siempre la **última de la tabla**, no la última ocupada: es la única que el bucle no alcanza a pisar, porque no tiene una fila siguiente de donde copiar.

```csharp
var ultima = _filas[^1];
```

> [!tip] La lección general
> Cuando una operación de "compactar" copia el estado *además* de los datos, el estado ya quedó resuelto por la propia copia. Volver a consultarlo después para decidir sobre qué actuar lee un mundo que la compactación ya cambió. Se decide por **posición** (la última de la colección), no por **estado** (la última ocupada).

De paso quedó redundante una rama de `AplicarGuardadoParcial` que existía solo para tapar este mismo caso; se sacó.

---

## Verificación

| Qué | Cómo | Resultado |
|---|---|---|
| Compilación | `dotnet build CapaUI.csproj` y `BimboProyecto.Tests.csproj` | 0 errores, 0 advertencias |
| Migración | `apply_migration` vía MCP de Supabase | Aplicada como versión `20260905215247` |
| Columnas | `information_schema.columns` | `placa_vehiculo` varchar(20), `observaciones` varchar(500) |
| Vista recreada | `pg_options_to_table` + `role_table_grants` + `SELECT` | `security_invoker = on`, 21 grants restaurados, devuelve 17 filas |
| Datos | `count(*)` + `max(length(...))` | 22 filas intactas, máximos sin cambio (8 y 46) — nada truncado |
| Advisors de seguridad | `get_advisors` | Sin hallazgos nuevos: solo los dos genéricos de exposición en GraphQL que la vista ya tenía. **No** aparece `security_definer_view`, la señal de que el `security_invoker` se preservó |
| Dependencias de la vista | `pg_depend` | Nada más colgaba de `v_mov_productos_resumen` |

> [!warning] Falta la prueba manual del fix del basurero
> El bug de borrado se corrigió y compila, pero Fernando todavía no lo verificó corriendo la app. Los cinco casos (borrar la del medio, la primera, la última, con la tabla llena, y con un solo camión) se validaron por razonamiento sobre el algoritmo, no ejecutando.

Los builds se hicieron redirigiendo la salida (`-p:BaseOutputPath`) porque la app estaba corriendo y bloqueaba los DLL de `bin`. **El `bin` del proyecto sigue con los binarios viejos**: hace falta rebuild y reinicio para probar.

---

## Lo que NO cambió

- **`CamionModal` sigue vivo y en uso** — es la edición de un camión existente. No se retiró.
- **`ProductoCamionModal`, `PesajeModal`, `TaraExtraTotalModal`, `ReporteModal`** — intactos.
- **Las otras dos columnas de texto de Pesaje** — `movimiento_productos.observaciones` y `entradas_producto.observaciones` siguen siendo `text` sin tope, y sus modales siguen sin validador. Por eso P-045 queda `[~]` y no `[x]`.
- **`MInput`** — no se le agregó el trigger de error; se resolvió con un estilo local en el modal nuevo. P-042 sigue abierta y ahora con una divergencia más.
- **Permisos ni RLS** — la vista se recreó con exactamente los grants que tenía. El endurecimiento sigue siendo tarea del cierre de desarrollo.
- **`movimientos.peso_tara_extra`** — la columna congelada del flujo viejo no se tocó.

---

## Corrección de documentación previa

`Módulo Pesaje` describía el alta como `ProcesoDescargaModal` (wizard + megamodal, marco 720×720). **Ese componente ya no existe**: se había partido en `CamionModal` + `ProductoCamionModal` en una sesión anterior (el doc-comment de `CamionModal` cita el commit `bf1117f`) y la nota nunca se actualizó. Se corrigió al documentar esta sesión — el drift es previo, no de este cambio.

---

## Relaciones

- [[Módulo Pesaje]] — nota del módulo, actualizada con el flujo de alta nuevo
- [[Deuda Técnica - Pendientes]] — P-045 parcialmente resuelta, P-042 actualizada, P-053 nueva
- [[Supabase - Vistas SQL, RLS y security_invoker]] — dónde quedó el gotcha del `ALTER TYPE` bloqueado por una vista
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — el esquema al que Pesaje por fin se sumó
- [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]] — la sesión que fijó el criterio de varchar(500)
- [[Selector de Catálogo - Selector genérico y multiselección]] — el selector de proveedor que usa cada fila
- [[Anatomia compartida de los modales]] — estructura que comparte el modal nuevo
- [[Arquitectura Actual]]
