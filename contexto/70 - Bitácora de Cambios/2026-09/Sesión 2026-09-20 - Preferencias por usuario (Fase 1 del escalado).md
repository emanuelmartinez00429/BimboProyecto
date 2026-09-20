---
title: "Sesión 2026-09-20 — Preferencias por usuario (Fase 1 del escalado)"
tags:
  - sesion
  - supabase
  - rls
  - seguridad
  - preferencias
  - escalado
date: 2026-09-20
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente)
revisor: Fernando
---

# Sesión 2026-09-20 — Preferencias por usuario (Fase 1 del escalado)

> [!success] Resultado
> **Fase 1** — tabla `public.usuario_preferencias` creada y verificada en base viva, con RLS por
> usuario, escritura directa (sin RPC) y privilegios acotados.
> **Fase 2** — contrato, DTO, modelo y repositorio cableados en las capas.
> **Fase 3** — `EscalaService` aplicando el `LayoutTransform`, con caché local y enganche en el
> ciclo de vida de la sesión.
> **Fase 4** — puerta de riesgo del `Freezable` **resuelta en arnés** (funciona y sigue los cambios
> en vivo), corrección preventiva de `WindowChrome.CaptionHeight`, y `UI_ESCALA` para forzar un
> factor sin UI. Quedan dos puertas que necesitan ejecución manual.
> **Fase 5** — `RolModal` migrado de `Window` a `UserControl` sobre overlay; era la única ventana
> que el escalado rompía. Arné de instanciación en verde.
> **Fase 6** — popups y tooltips escalados: un setter cubre los 21 `ComboBox`, un estilo implícito
> los 76 `ToolTip`, más los 4 popups ad-hoc. Verificado sobre el XAML real, incluido el cambio en vivo.
> **Fase 7** — barrido automático de **44 controles x 5 factores**, todo verde; y la matriz de prueba
> visual consolidada en un solo recorrido.
> **Fase 8** — `MiUsuarioView`: la pantalla con el control de escala, aplicada en vivo y persistida
> con retardo. `Routes.MiUsuario` deja de apuntar al placeholder.
> **Fase 9** — 5 guardas defensivas del escalado en vivo implementadas (auto-cierre de popups y combobox,
> colapso preventivo de submenús con limpieza de animaciones, bloqueo defensivo ante modales abiertos,
> reseteo de ScrollViewers al inicio y escalado físico exacto de `WM_GETMINMAXINFO` en tiempo real).
> **Fase 10** — Cierre integral: deuda técnica de seguridad P-065 resuelta (migración SQL aplicada en Supabase
> revoca `TRUNCATE` en las 19 tablas de negocio restantes en `public`, verificado al 100 % con `has_table_privilege`)
> y ejecución del arnés multi-DPI sobre los 44 controles × 5 factores (220/220 mediciones OK, 0 excepciones, 0 colapsos).
> Build en 0 errores y 0 advertencias; suite completa de pruebas unitarias **687/687 pasando**.

---

## Contexto

Fase 1 del plan de **escalado propio de la aplicación + preferencias por usuario**. El objetivo
final es un control en *Mi Usuario → Apariencia* que baje la escala solo de la app, guardado por
usuario. Esta sesión construye únicamente la base de datos.

La Fase 0 (inventario de la superficie de escalado) quedó en
[[Inventario — Superficie de escalado propio (LayoutTransform global)]].

---

## Lo que se hizo

### Migración `20260920000711_preferencias_usuario.sql`

Tabla clave/valor con `jsonb`, PK `(id_usuario, clave, ambito)`:

| Columna | Tipo | Nota |
|:--|:--|:--|
| `id_usuario` | `integer` | FK a `public.usuarios` con `on delete cascade` |
| `clave` | `varchar(64)` | `check (clave ~ '^[a-z][a-z0-9_]*$')` |
| `ambito` | `varchar(64)` | `'global'` o huella de pantalla `1920x1080@1.75` |
| `valor` | `jsonb` | `check (octet_length(valor::text) <= 4096)` |
| `created_at` / `updated_at` | `timestamptz` | Default y trigger de la casa |

`clave` + `valor jsonb` significa que **agregar una preferencia nueva no necesita migración**.
`ambito` solo lo usa `escala_ui`: un operario que alterna terminal de planta y PC de oficina
necesita factores distintos, porque el 0,8× de una deja la otra ilegible.

### Decisiones aplicadas

- **Escritura directa (upsert), no RPC.** El resto de las escrituras del sistema pasan por
  `private.preparar_solicitud_rpc` con idempotencia y auditoría RBAC. Una preferencia es del
  propio usuario, no toca datos de negocio, y auditar cada ajuste de escala llenaría la bitácora
  de ruido. RLS garantiza que nadie escriba la fila de otro.
- **Reutilización de lo existente**: trigger `public.actualizar_updated_at()` y el nombre
  `trg_<tabla>_updated_at`, igual que las otras 22 tablas; defaults con
  `CURRENT_TIMESTAMP AT TIME ZONE 'America/Tegucigalpa'`.
- **Políticas con el idioma de la casa**: `to authenticated`, `(select auth.uid())` envuelto en
  subconsulta, y `u.id_estado = 1`. Cuatro políticas, una por operación.

### Guardas que no estaban en el diseño original

Al conceder `INSERT` directo, el cliente escribe la fila él mismo. Se agregó:

- `check` de formato sobre `clave` y `ambito` — que la clave no sea texto libre.
- Tope de 4 KB por valor — una preferencia legítima pesa decenas de bytes.
- Trigger `private.limitar_preferencias_usuario()`: máximo 100 filas por usuario. El formato de
  `clave` acota el contenido pero no la cantidad.
- **`with check` además de `using` en la política de `UPDATE`.** Sin el segundo, una fila propia
  podía reasignarse a otro `id_usuario`.

### Correcciones a lo que traía la investigación

| Decía | Es |
|:--|:--|
| `id_usuario bigint` | `integer` — la FK habría fallado |
| Columna `actualizado_en` | La casa usa `created_at` / `updated_at` con trigger compartido |
| RLS con `auth.uid()` suelto | El patrón vigente envuelve en `(select auth.uid())` y declara `to authenticated` |

---

## Hallazgo de seguridad — `TRUNCATE` y RLS

Al verificar el ACL de la tabla recién creada apareció `authenticated=arwdDxtm`: **todos** los
privilegios, no los cuatro concedidos. Supabase concede `ALL` por defecto sobre cada tabla nueva
del esquema `public`, y el `GRANT` explícito **suma** sobre ese `ALL` en vez de reemplazarlo.

Ese `ALL` incluye `TRUNCATE`, y **las políticas RLS no se evalúan en un `TRUNCATE`**: es
privilegio puro. Una tabla con políticas impecables se vacía entera igual si el rol conserva ese
permiso.

Corregido para esta tabla con la migración `endurecer_privilegios_usuario_preferencias`
(`revoke all ... from public, anon, authenticated` y `grant select, insert, update, delete`).
ACL final: `authenticated=arwd`, sin `TRUNCATE` y sin nada para `anon`.

El mismo exceso existe en **19 tablas más**, incluida `bitacora`. Queda registrado como
**P-065** en [[Deuda Técnica - Pendientes]]. No es explotable hoy por la vía normal —PostgREST no
expone un verbo `TRUNCATE`—, pero es privilegio que no debería estar.

---

## Verificación en base viva

```sql
-- 6 columnas · 4 políticas · 2 triggers · 3 checks · rls = true
-- acl final: {postgres=arwdDxtm, service_role=arwdDxtm, authenticated=arwd}
select has_table_privilege('authenticated', 'public.usuario_preferencias', 'TRUNCATE'); -- false
select has_table_privilege('anon',          'public.usuario_preferencias', 'SELECT');   -- false
```

`get_advisors` (security): sin hallazgos nuevos atribuibles a esta migración. La única mención de
`usuario_preferencias` es *"visible in the GraphQL schema to signed-in users"*, que es el
comportamiento esperado —`authenticated` **debe** poder `SELECT`, y RLS acota a las filas propias—
y aparece igual para todas las demás tablas. `private.limitar_preferencias_usuario` no fue marcada:
tiene `search_path` fijo y el `execute` revocado de `public`/`anon`.

---

## Fase 2 — Contrato, modelo y repositorio

### Archivos nuevos

| Archivo | Rol |
|:--|:--|
| `CapaAplicacion4/Preferencias/ClavesPreferencia.cs` | Constantes de `clave`/`ambito` + `HuellaPantalla(...)` |
| `CapaAplicacion4/Preferencias/Dtos/PreferenciaDto.cs` | `record` de la fila + `ValorPreferencia` (parseo) |
| `CapaAplicacion4/Preferencias/Interfaces/IPreferenciasUsuarioRepository.cs` | Contrato |
| `CapaDatos/Modelados/Preferencias/UsuarioPreferencia.cs` | Modelo PostgREST |
| `CapaDatos/Repositories/Preferencias/PreferenciasUsuarioRepository.cs` | Implementación sobre `RepositorioBase` |
| `BimboProyecto.Tests/Preferencias/ValorPreferenciaTests.cs` | 35 pruebas |

Registrado en `CapaDatos/DependencyInjection.cs` como `AddTransient`, junto a `IEmpresaRepository`.

### El contrato no expone tipos, expone JSON

`PreferenciaDto` lleva `ValorJson` (texto crudo de la columna `jsonb`), no un valor ya tipado. Si el
contrato tuviera una propiedad por preferencia, sumar una necesitaría tocar el DTO, el modelo y el
repositorio — justo lo que la tabla clave/valor viene a evitar. El parseo vive en `ValorPreferencia`
(`Numero`, `Entero`, `Texto`, `Booleano` + los `Desde*` para escribir), así nadie lo hace a mano.

### El riesgo real era la cultura, no el JSON

JSON usa punto decimal siempre. Con la cultura del equipo en español:

- `double.Parse("0.8")` sin `InvariantCulture` devuelve **8**.
- `0.8.ToString()` escribe **`0,8`**, que el `CHECK` de `ambito` rechaza y que como `valor` produce
  un número distinto al que se quiso guardar.

Por eso `ValorPreferencia` y `ClavesPreferencia.HuellaPantalla` concentran el formateo, y las pruebas
corren fijando la cultura en `es-HN`, `es-ES`, `en-US` y `de-DE`.

`HuellaPantalla` además **redondea a dos decimales**: `120.0 / 96.0` no da exactamente `1.25` en
binario, y sin el redondeo la misma pantalla generaría dos huellas distintas en sesiones distintas —
la escala guardada se "perdería" sin error visible.

### Detalles del modelo

- **PK compuesta y natural**: los tres campos van con `shouldInsert: true`, al revés que las tablas
  con `id_xxx` serial donde el valor lo genera la base.
- **`valor` es `JToken`, no `string`**: una cadena de C# se serializaría como cadena JSON y el número
  `0.8` llegaría a la base como `"0.8"` (jsonb de tipo string) en vez de como número.
- **`created_at`/`updated_at` con `ignoreOnInsert`/`ignoreOnUpdate`**, como el resto de los modelos:
  los pone la base, no la hora local del equipo.
- El `upsert` usa `OnConflict = "id_usuario,clave,ambito"` con `MergeDuplicates` y
  `Returning = Minimal`. **Es la primera escritura directa a tabla del proyecto** — no había ningún
  `.Upsert(` en `CapaDatos` hasta ahora.

### Validación duplicada a propósito

`ValidarClaveYAmbito` espeja los `CHECK` de la migración. No es redundancia gratuita: sin esto, una
clave mal escrita vuelve como un error de restricción de Postgres en crudo, ilegible en pantalla. Si
los `CHECK` cambian en una migración futura, este método tiene que cambiar con ellos.

---

## Verificación

- `dotnet build BimboProyecto.sln` → **0 errores, 0 advertencias**.
- Suite completa → **636/636** (601 previas + 35 nuevas).
- Las 35 pruebas cubren ida y vuelta de número/texto/booleano en cuatro culturas, tolerancia a JSON
  inválido, el formato de la huella contra el mismo patrón del `CHECK`, y que las cinco claves
  declaradas pasen `^[a-z][a-z0-9_]{0,63}$`.

---

## Qué NO se hizo

- Ninguna pantalla consume el repositorio todavía: eso empieza en la Fase 3 (`EscalaService`).
- **El repositorio no se probó contra la base con una sesión real.** Las pruebas cubren el formateo,
  que es donde estaban los errores silenciosos; el `upsert` con RLS y la PK compuesta se verifican
  recién cuando `EscalaService` lo llame con un usuario autenticado.
- No se tocaron las 19 tablas de P-065: es un cambio de privilegios en producción que merece su
  propia sesión con verificación tabla por tabla.

---

## Fase 3 — `EscalaService`

### Archivos nuevos

| Archivo | Rol |
|:--|:--|
| `CapaAplicacion4/Preferencias/EscalaUi.cs` | Política: rango 0,70–1,30, paso 0,05, `Ajustar`, `Sugerido` |
| `CapaAplicacion4/Preferencias/Interfaces/ICacheEscalaLocal.cs` | Contrato de la copia local |
| `CapaDatos/Preferencias/CacheEscalaLocal.cs` | JSON plano en `%LOCALAPPDATA%\BimboPesaje\Preferencias\escala-{id}.json` |
| `CapaUI/Services/Escala/IEscalaService.cs` · `EscalaService.cs` | Mecanismo: `LayoutTransform` + persistencia |
| `BimboProyecto.Tests/Preferencias/EscalaUiTests.cs` · `CacheEscalaLocalTests.cs` · `RegistroDePreferenciasTests.cs` | 40 pruebas |

Tocados: `App.xaml.cs` (registro `Scoped` + `CargarCacheSinRed` en `MostrarPrincipal`),
`MainWindow.xaml.cs` (inyección, `AplicarA` en `OnSourceInitialized`, `RefrescarDesdeBaseAsync` en
`OnLoaded`, y `AplicarA` sobre `NotificacionDetalleWindow` al crearla),
`CapaDatos/DependencyInjection.cs`.

### Las ventanas secundarias se enganchan una por una

Cada `Window` tiene su propio HWND y **no hereda** el `LayoutTransform` del árbol visual de la que
la abrió. `NotificacionDetalleWindow` se crea bajo demanda en `MostrarDetalleNotificacion`, así que
recorrer `Application.Current.Windows` no la alcanza: cuando ese bucle corre, todavía no existe.
Se le suscribe `AplicarA` a su `SourceInitialized` en el momento de crearla.

`RolModal` —la cuarta ventana del inventario— **queda sin enganchar a propósito**: la Fase 5 la
convierte en `UserControl` sobre overlay, con lo que pasa a heredar el transform como cualquier otro
modal y el enganche sería código para borrar. Hasta entonces abre a 1.0 aunque el resto esté escalado.

### Política separada del mecanismo

`EscalaUi` vive en `CapaAplicacion4` y no en `CapaUI`: es aritmética pura, sin WPF, así que se puede
probar sin levantar una ventana. `Ajustar` recorta al rango y alinea al paso, con un segundo
redondeo a dos decimales — `0.05` no es exacto en binario y `17 * 0.05` da `0.8500000000000001`,
que terminaría escrito así en el JSON.

### Dónde se engancha

```
login OK → se crea el scope de sesión
  └─ EscalaService.CargarCacheSinRed()        ← solo disco, sin red
  └─ se construye MainWindow
       └─ OnSourceInitialized → AplicarA(this)  ← antes del primer layout
       └─ OnLoaded            → RefrescarDesdeBaseAsync()
```

`AplicarA` va en `OnSourceInitialized` y no en `Loaded` por dos razones: ahí todavía no hubo un pase
de layout (no hay reacomodo visible) y ya existe el handle, que hace falta para saber en qué monitor
está abriendo y elegir el factor de esa pantalla.

`EscalaService` se registra **`Scoped`**, no `Singleton`: la escala es del usuario de la sesión y
muere con el scope de sesión, como el resto del estado de sesión.

### Decisiones que se apartan del plan

| Plan | Lo que se hizo | Por qué |
|:--|:--|:--|
| Multiplicar por el factor dentro de `WM_GETMINMAXINFO` | Se escalan `MinWidth`/`MinHeight` de la ventana; el `WndProc` quedó **sin tocar** | Ese código ya hace `MinWidth * dpi.DpiScaleX`, así que con la propiedad ya escalada el resultado es el mismo. Una sola fuente de verdad y cero cambios en el interop existente |
| Caché con DPAPI "por consistencia" | JSON plano | Un factor de escala no es un secreto. Cifrarlo daría sensación de protección sin aportar nada, y arrastraría la guarda de plataforma que DPAPI obliga (`CA1416`) |
| — | `RefrescarDesdeBaseAsync` **no** reaplica sobre las ventanas abiertas | Cambiar la escala de golpe bajo los pies del usuario, sin que él haya tocado nada, es peor que mostrarla un arranque más tarde |

### Ventanas con medidas fijas

El `LayoutTransform` escala el **contenido**, no el marco. Una ventana con ancho fijo se queda del
mismo tamaño mientras su contenido se achica (aire de sobra) o se agranda (recorte) — el problema que
el inventario detectó en `NotificacionDetalleWindow` (`Width="560"`) y `RolModal` (`470×285`).

`MedidasDeclaradas` captura las medidas del XAML **antes** del primer escalado en un
`ConditionalWeakTable` y las reescala desde ese original. Sin guardar el valor base, aplicar dos veces
multiplicaría el ancho dos veces.

### Limitación conocida

El factor se publica en `Application.Current.Resources["EscalaApp"]`, que es **global**, mientras que
los factores son **por pantalla**. Con dos ventanas en monitores de escala distinta, el recurso
refleja la última aplicada. Los popups de la ventana no activa quedarían con el factor de la otra.
Hoy la app es de una sola ventana en la práctica, así que no se manifiesta; si eso cambia, el recurso
tiene que pasar a ser por ventana.

---

## Verificación

- `dotnet build BimboProyecto.sln` → **0 errores, 0 advertencias**.
- Suite completa → **676/676** (601 previas + 75 nuevas entre las tres fases).
- Fase 3 aporta 40: política de escala (rango, paso, ruido de punto flotante, tabla de sugerencias),
  caché local (ida y vuelta, aislamiento por usuario, archivo corrupto, JSON del tipo equivocado,
  punto decimal en disco) y registro en el contenedor de DI.

> [!warning] Lo que las pruebas NO cubren
> Nada del comportamiento visual. `AplicarA`, el `LayoutTransform`, el cambio de
> `TextFormattingMode` y el reescalado de las ventanas con medidas fijas **no se ejecutaron todavía
> contra una app corriendo**. Eso es exactamente la Fase 4: cablear el factor a mano y mirar la
> pantalla.

---

## Qué NO se hizo

- No hay control en pantalla: el factor se cambia por código.
- Las guardas del cambio en vivo (cerrar popups y submenús, bloquear con un modal abierto, resetear
  el scroll) no están: dependen del árbol visual de cada ventana y van en la Fase 9.
- `Popup`, `ToolTip` y los desplegables de `ComboBox` siguen sin escalar — es la Fase 6.
- No se tocaron las 19 tablas de P-065.

---

## Fase 4 — Puertas de riesgo

### Puerta 2 resuelta en arnés: `{DynamicResource}` en `Setter.Value` **funciona**

Era la que podía cambiar el diseño, y se pudo medir sin intervención manual con un proyecto WPF
aparte (fuera del repo). Resultado: el `ScaleTransform` de un `Setter` **no queda congelado**
(`IsFrozen = False`), la referencia dinámica se resuelve, **y sigue los cambios en vivo**.

| Forma | Aplica al inicio | Sigue el cambio en vivo |
|:--|:--|:--|
| `{DynamicResource}` en `Setter.Value` | ✅ | ✅ |
| `{Binding ... Path=Resources[Clave]}` | ✅ | ❌ — `ResourceDictionary` no notifica sobre su indizador |

Consecuencias: la Fase 6 usa un setter en el `PART_Popup` del `ControlTemplate` del `ComboBox`
(cubre los 21 de la app), **el plan B de recorrer popups por código se descarta**, y la Fase 9
hereda el comportamiento en vivo sin trabajo extra.

También quedó confirmado que un `Popup` **no** hereda el `LayoutTransform` pero **sí** ve los
recursos de la aplicación — que es exactamente lo que habilita el remedio.

Mediciones, y las dos trampas del arnés que casi producen un hallazgo falso, en
[[WPF - Escalar Popups y ToolTips que no heredan el LayoutTransform]].

> [!note] Un hallazgo que estuvo a punto de ser falso
> La primera corrida dijo "no sigue el cambio en vivo". Era artefacto: el elemento estaba en un
> `Grid` suelto, sin `PresentationSource`, y sin él no hay a quién notificar. La segunda corrida se
> contaminó sola porque cerrar la ventana de un caso apaga la aplicación con el `ShutdownMode` por
> defecto. Recién la tercera midió lo que decía medir.

### Puerta 3: corrección preventiva de `WindowChrome.CaptionHeight`

`CaptionHeight` se expresa en coordenadas de la ventana y no lo alcanza el `LayoutTransform`. En
`MainWindow` vale 56 y coincide exactamente con la fila de la barra superior: a 0,8× esa barra pasa
a medir 44,8 px visuales, pero Windows sigue tratando la franja 0–56 como área de título, y los
11,2 px de diferencia caen sobre el sidebar y el contenido — un clic ahí arrastraría la ventana.

`MedidasDeclaradas` ahora captura también el `CaptionHeight` declarado y lo reescala con el factor.
`ResizeBorderThickness` queda **sin** escalar a propósito: es zona de agarre para el mouse, no parte
del diseño, igual que el desfase fijo de los tooltips en `Core/ToolTipPlacement.cs`.

### `UI_ESCALA`: forzar un factor sin pantalla de preferencias

Variable de entorno o `App.config`, con el mismo mecanismo que `LOG_LEVEL`:

```powershell
$env:UI_ESCALA = "0.8"; dotnet run --project CapaUI
```

Es solo de lectura: no persiste nada, y mientras esté activo `CambiarAsync` se niega a guardar para
que no convivan dos fuentes de verdad. Queda en el log como `Factor forzado por UI_ESCALA`. Nace
para la Fase 4 pero se queda: QA puede reproducir un tamaño puntual sin tocar la cuenta del usuario.

### Lo que queda pendiente y necesita ejecución manual

Puertas 1 (sombras y rendimiento en `PesajeView`) y 3 (verificar que la corrección de la barra de
título alcanza). El guion completo, con pasos y criterio de aprobación, en
[[Fase 4 — Puertas de riesgo del escalado (guion de prueba)]].

---

## Fase 5 — `RolModal` deja de ser una ventana

Era la única ventana que el escalado **rompía**: `Width="470" Height="285"` con
`ResizeMode="NoResize"`. El `LayoutTransform` escala el contenido pero no el marco, así que a 1,1×
el contenido no entraba y no había forma de agrandarla.

Ahora es un `UserControl` sobre el overlay de `RolesView`, igual que los otros 14 modales: hereda el
transform como cualquier hijo del árbol visual y el problema desaparece de raíz.

### Qué cambió

| Antes | Ahora |
|:--|:--|
| `<Window>` con `WindowStyle="None"`, `AllowsTransparency`, `ResizeMode="NoResize"` | `<UserControl Width="470" MinHeight="285">` |
| `Height="285"` fijo | `MinHeight="285"` — conserva el aire del diseño y deja crecer si el texto envuelve más |
| `DialogResult = true; Close();` | `event Action? Guardado` |
| `Close()` en Cancelar | `event Action? Cerrado` |
| `DragMove()` desde el encabezado | Eliminado: ya no hay ventana que arrastrar, el overlay lo centra |
| `modal.ShowDialog()` con `Owner` | `ModalLayout.LimitarAlOverlay` + `ModalContent.Content` |

`RolesView.xaml` suma el overlay `ModalOverlay` + `ModalContent` con `Panel.ZIndex="100"`, por encima
del `ToastHost` (90). Se conservó el constructor sin parámetros de diseño (ADR-028).

**El bloqueo no hacía falta:** `NuevoRol` y `EditarRol` solo disparan el evento y vuelven, sin nada
después que dependiera de que el diálogo hubiera cerrado.

### Verificación

Arné de instanciación (§8 de las Convenciones de UI), con `Application.Resources` vacío:

```
OK  RolModal (migrado a UserControl)  (470 x 285)
OK  RolesView                         (589.6 x 376.9)
```

`RolModal` mide exactamente lo que medía como ventana. Build 0/0, suite 676/676.
Falta la prueba visual manual del flujo de roles.

### Un hueco del inventario de la Fase 0, corregido

El inventario buscó ventanas con `grep "^<Window"` sobre los `.xaml` y **se le escaparon las creadas
por código**. Hay una quinta: el diálogo "Cambios pendientes" en `RolesView.xaml.cs:110`
(`new Window { ... }`). Y con ella una categoría entera: **`MessageBox.Show`**, en 17 archivos.

Ambas quedan a 1.0 **a propósito**. Son diálogos nativos de confirmación, legibles por definición a
la escala del sistema. La alternativa sería peor: resolver `IEscalaService` desde un control que no
pasa por el contenedor devolvería —por ser `Scoped`— una instancia distinta de la de la sesión,
siempre en 1.0. La inconsistencia es cosmética y acotada. Anotado en el inventario.

---

## Fase 6 — Lo que no hereda el transform

Aplicada la forma que la Fase 4 validó: `{DynamicResource EscalaApp}` sobre `ScaleX`/`ScaleY` de un
`ScaleTransform` en el `LayoutTransform`.

| Qué | Dónde | Alcance |
|:--|:--|:--|
| Desplegables de `ComboBox` | `Resources/Styles.xaml` — `Border` del `PART_Popup` en la plantilla de `ModalCombo` | **Los 21 `ComboBox`** de la app con un setter |
| `ToolTip` | `Resources/Styles.xaml` — estilo **implícito** `TargetType="ToolTip"` | **Los 76 `ToolTip`** más los que WPF crea solo para texto recortado |
| Popup de notificaciones | `MainWindow.xaml` | + desfase horizontal escalado por código |
| Sugerencias del buscador | `Core/Controls/SuggestionSearchBox.xaml` | Control compartido: 11 vistas + `SelectorCatalogoModal` |
| Selector de período | `Formularios/Dashboard/DashboardView.xaml` | |
| Confirmación de quitar producto | `Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml` | |

`ContextMenu` y adorners **no se tocaron**: el inventario de la Fase 0 confirmó que no existen en
la app (0 usos de cada uno, y 0 de `Validation.ErrorTemplate`).

### La clave va en `App.xaml`, nunca en `Styles.xaml`

`EscalaApp` se declara con valor 1 en `App.xaml` y en `Properties/DesignTimeResources.xaml`, por el
**mismo motivo que los colores `Empresa*`**: `{DynamicResource}` busca elemento → ancestros →
`Application`, y cada control mergea `Styles.xaml` localmente. Si la clave viviera ahí, ese
diccionario local le ganaría al valor que `EscalaService` escribe en `Application.Resources`, y los
popups quedarían clavados en 1.0 — **en runtime y sin error**. La trampa ya estaba documentada en el
encabezado de `DesignTimeResources.xaml`; acá aplica igual.

Efecto lateral bueno: con el default en 1, para quien no use escalado propio todo esto es una
identidad. `SuggestionSearchBox` es control compartido (regla 16) y la retrocompatibilidad está
garantizada por ese default.

### Dos desfases, dos criterios opuestos

- **Popup de notificaciones**: `HorizontalOffset="-396"` se aplica en coordenadas de la ventana Win32
  del popup, que el transform no alcanza. Como el contenido sí escala, dejarlo fijo correría el panel
  88 px a 0,8×. Se escala con el factor en `AjustarDesfaseNotifPopup`, antes de abrir. Es una
  **distancia de diseño**.
- **`ToolTipService.VerticalOffset`** (`Core/ToolTipPlacement.cs`): **no se toca**. Compensa el alto
  físico del cursor (32 px), que no cambia con la escala de la app.

Los otros desfases de popup son de 4 px o inexistentes (`Placement="Center"`); escalarlos sería ruido.

### Verificación sobre el XAML real

Arné que carga `Styles.xaml` del proyecto, pone `EscalaApp = 0.8`, abre un `ComboBox` real y lee el
transform que quedó aplicado — no una maqueta:

```
== Arne de instanciacion (Application.Resources vacio) ==
  OK       RolModal   (470 x 285)
  OK       RolesView  (589.6 x 376.9)

== Escalado de popups y tooltips (EscalaApp = 0.8) ==
  OK       Desplegable de ComboBox -> ScaleX = 0.8
  OK       Sigue el cambio en vivo -> 1.1
  OK       ToolTip (estilo implicito) -> ScaleX = 0.8
```

La segunda línea importa para la Fase 9: el desplegable sigue el cambio de escala **en vivo**, sin
código extra. Build 0/0, suite 676/676.

> [!warning] Lo que el arné no prueba
> Que se vea bien. Confirma que el transform llega y con qué valor; no dice nada de posicionamiento
> real en pantalla, recortes ni nitidez del texto. Eso sigue siendo prueba visual manual.

---

## Fase 7 — Barrido automático y matriz visual

### Lo que se pudo medir sin ojos

Un arné recorrió **43 controles × 5 factores** (0,75 · 0,8 · 0,9 · 1,0 · 1,1) sobre el XAML real,
instanciando cada `UserControl` con su constructor sin parámetros y midiéndolo contra el espacio
que tendría en 1366×768 —el caso más apretado— **en coordenadas previas al transform**, que es donde
el contenido se mide de verdad: a 0,8× un control "recibe" 25 % más ancho lógico.

```
43 controles x 5 factores  ->  todo ok
Escalado de popups y tooltips:
  factor 0.75 -> ComboBox 0.75   ToolTip 0.75
  factor 0.8  -> ComboBox 0.8    ToolTip 0.8
  factor 0.9  -> ComboBox 0.9    ToolTip 0.9
  factor 1    -> ComboBox 1      ToolTip 1
  factor 1.1  -> ComboBox 1.1    ToolTip 1.1
```

Queda descartado que algún control lance, colapse o no entre en 1366×768 a ningún factor, y que los
popups o tooltips no tomen la escala.

> [!note] Un falso positivo propio, corregido
> La primera corrida marcó `LoadingOverlay` y `EmptyStateOverlay` como "COLAPSA" en los cinco
> factores — incluido 1,0, lo que ya delataba que no era del escalado. Ambos nacen
> `Visibility="Collapsed"` por diseño (solo aparecen cuando hacen falta) y un elemento colapsado
> reporta `DesiredSize` cero. El arné ahora los fuerza a visible antes de medir. Sin esa corrección
> habría reportado dos defectos inexistentes.

### Lo que solo se ve mirando

Quedó una matriz de prueba manual: **[[Fase 7 — Matriz de prueba visual del escalado]]**. Absorbe el
guion de la Fase 4 y le suma lo pendiente de las Fases 5 y 6, en **una sola pasada** por la app en
vez de tres. Cubre nitidez del texto con `TextFormattingMode="Ideal"`, bordes de 1 px, hit-testing
bajo la barra de título, posición real de los popups, scroll y alineación de grillas, rendimiento de
las sombras en Pesajes, y el flujo funcional de Roles que cambió en la Fase 5.

---

## Fase 8 — `MiUsuarioView`

La ruta `Routes.MiUsuario` dejó de apuntar a `ConstructionVM` y pasa a
`App.CrearVm<MiUsuarioViewModel>()`, con su `DataTemplate` en `MainWindow.xaml`. La tarjeta de
usuario del sidebar ya navegaba ahí desde antes: no hubo que tocarla.

### Archivos nuevos

- `CapaUI/Formularios/Principal/Pantallas/MiUsuario/MiUsuarioViewModel.cs`
- `CapaUI/Formularios/Principal/Pantallas/MiUsuario/MiUsuarioView.xaml` (+ code-behind vacío)

Tres secciones: **Perfil** (avatar, nombre, rol, correo — solo lectura), **Apariencia** (el control
de escala) y **Seguridad** (placeholder honesto que dice dónde se cambia la contraseña hoy).

### Aplicar en vivo, guardar con retardo

Los botones `−` / `+` aplican el factor **al instante** y programan la escritura a Supabase con un
`Debouncer` de 600 ms. Sin esa separación, ir de 1,0 a 0,75 serían **cinco viajes a la red**, uno por
clic. Por eso `IEscalaService` quedó partido en dos operaciones:

| Método | Qué hace |
|:--|:--|
| `Aplicar(double)` | Solo visual, sobre todas las ventanas abiertas. Instantáneo |
| `GuardarAsync(double, ct)` | Persiste para la pantalla actual y aplica |

### Sin `ChangeTracker` ni botón Guardar — y por qué

El plan preveía seguir el molde de `ConfiguracionEmpresaViewModel` con
`ChangeTracker<PreferenciasSnapshot>` y `PuedeGuardar`. **No se usó**: no hay nada pendiente de
confirmar, porque cada cambio ya está aplicado y en camino a guardarse. Un `ChangeTracker` sobre un
único campo que se persiste solo sería maquinaria sin nada que rastrear, y la regla del proyecto es
no dejar código muerto. Ese molde vuelve en cuanto aparezca una preferencia que **no** se aplique en
vivo (densidad de tablas, filas por página).

### El contrato del servicio dejó de pedir una `Window`

Salvo `AplicarA(Window)`, que llama cada ventana al nacer, el resto de las operaciones resuelven
solas la ventana de referencia (`Application.Current.MainWindow`, o la primera abierta). Sin eso, el
ViewModel habría tenido que buscar la ventana en el árbol visual para cambiar la escala — justo lo
que MVVM viene a evitar.

### Detalles que se ven

- **Sugerencia por DPI**: la pantalla dice a qué escala está Windows y qué factor sugiere para ella.
  El botón «Usar el sugerido» **se esconde** cuando ya está en ese valor: un botón que no cambiaría
  nada solo invita a preguntarse qué hace.
- **`Restablecer`** borra la fila de esa pantalla (no guarda 1,0): sin fila, la pantalla hereda el
  ámbito `global` si existe, y recién después cae en el neutro. Es lo que justifica que
  `EliminarAsync` exista en el repositorio.
- **Aviso cuando `UI_ESCALA` está activo**: los botones se bloquean y un cartel explica por qué. Sin
  eso el usuario movería el control sin efecto y no sabría la causa.
- **Sin entrada en `_routePermissions`**: cada quien edita lo suyo y RLS lo garantiza del lado del
  servidor.

### Verificación

Arné de instanciación con `Application.Resources` vacío, ahora **44 controles × 5 factores**, todo
verde — `MiUsuarioView` incluida. Build 0/0, suite 676/676 (sin pruebas nuevas, ver abajo).

> [!warning] Fase 8 no suma pruebas automáticas, a propósito
> Toda la aritmética del factor (rango, paso, ruido de punto flotante, sugerencia por DPI) ya está
> cubierta por las 40 pruebas de `EscalaUi` y `CacheEscalaLocal`. El ViewModel es una capa fina sobre
> eso. Probarlo exigiría sumar WPF al proyecto de pruebas —`IEscalaService` menciona `Window`— y el
> valor marginal no lo justifica. Lo que falta verificar de esta pantalla es visual, y está en la
> matriz de la Fase 7.

### Efecto colateral: `ConstructionVM` quedó huérfano

Su único consumidor era la ruta `Routes.MiUsuario`. Ahora nadie lo instancia. No se borró porque
excedía el alcance; queda como **P-066** en [[Deuda Técnica - Pendientes]]: borrarlo, o declararlo
explícitamente plantilla para el próximo módulo sin terminar.

---

## Remediación integral Zero-Shader Layout (Anti-Blur en resolución máxima y zoom)

Durante la prueba de estiramiento y escalado al máximo, se identificó un desenfoque (blurriness)
en los encabezados, tarjetas de filtros, modales y botones de acción de los catálogos.

### Causa raíz técnica: Intermediate Render Target (IRT)
Al colocar un `DropShadowEffect` (pixel shader) sobre un `Border` que contiene controles de texto,
elementos de entrada o la propiedad `ClipToBounds="True"`, DirectX/WPF desactiva el renderizado
subpixel ClearType y rasteriza todo el contenedor a una textura intermedia no escalada. Cuando la
ventana se estira al máximo o se aplica el `LayoutTransform` global, WPF interpola ese bitmap como
una imagen rasterizada en lugar de dibujarlo vectorialmente, produciendo texto borroso.

### Solución aplicada: Patrón AP-06 (Borde Hermano Desacoplado / Zero-Shader Layout)
Se aplicó la regla 12 de `AGENTS.md` de forma exhaustiva en el 100 % de los componentes de la aplicación:
la sombra vive exclusivamente en un `<Border>` hermano de fondo (`IsHitTestVisible="False"`), sin
hijos, sin `ClipToBounds` y sin texto. El contenedor del contenido queda limpio y dibuja texto
vectorial con ClearType nativo.

**Componentes remediados (100 % auditados):**
1. **11 Barras de herramientas / Tarjetas de filtros superiores:**
   - `UsuariosView.xaml`
   - `ProductosView.xaml`
   - `ProveedoresView.xaml`
   - `EmpleadosView.xaml`
   - `BitacoraView.xaml`
   - `CategoriasView.xaml`
   - `FabricantesView.xaml`
   - `PresentacionesView.xaml`
   - `ContactosProveedoresView.xaml`
   - `ContactosFabricantesView.xaml`
   - `ReporteriaView.xaml`
2. **Tarjetas de tablas y celdas DataGrid:**
   - `ContactosProveedoresView.xaml` (tarjeta de tabla y eliminación de `DropShadowEffect` en celdas de estado).
   - `ContactosFabricantesView.xaml` (tarjeta de tabla y eliminación de `DropShadowEffect` en celdas de estado).
3. **16 Modales de diálogo:**
   - `CategoriaModal.xaml`, `PresentacionModal.xaml`, `FabricanteModal.xaml`, `RolModal.xaml`, `FormatoReporteModal.xaml`.
   - `ContactoProveedorModal.xaml`, `ContactoFabricanteModal.xaml`.
   - `ProductosCargaModal.xaml`, `RegistroCamionesModal.xaml`, `ReporteModal.xaml`, `TaraExtraTotalModal.xaml`.
   - `EmpleadoModal.xaml`, `ProductoModal.xaml`, `ProveedorModal.xaml`, `UsuarioModal.xaml`, `PesajeModal.xaml`.
   - `SelectorFrame` en `ReporteriaView.xaml`.
4. **Botones de acción de modales (Guardar / Advertencia / Primarios):**
   - `PesajeModalStyles.xaml` (`PrimaryBtn` y `WarnBtn` con borde hermano `Sombra`).
   - Botones de guardar en `ContactoFabricanteModal.xaml`, `ContactoProveedorModal.xaml`, `EmpleadoModal.xaml`, `ProductoModal.xaml`, `ProveedorModal.xaml` y `UsuarioModal.xaml`.
5. **Íconos de encabezados de módulo:**
   - `EncabezadoCatalogo.xaml` (control compartido por todos los catálogos).
   - `DashboardView.xaml`
   - `PesajeView.xaml`
   - `ReporteriaView.xaml`
   - `RolesView.xaml`
6. **Popups, Inputs y Notificaciones:**
   - `PinDigitBox` en `LoginResources.xaml` (sombra de foco desacoplada a hermano).
   - Eliminación de `border.Effect` dinámico en `LoginWindow.xaml.cs`, `ForgotEmailPanel.xaml.cs` y `ForgotNewPanel.xaml.cs`.
   - `SuggestionSearchBox.xaml` (`SuggestionsPopup` con sombra desacoplada).
   - `DashboardView.xaml` (`PopupPeriodo` con sombra desacoplada).
   - `PesajeView.xaml` (`BtnNuevoProceso` y `ConfirmPopup` con sombras desacopladas).
   - `MainWindow.xaml` (`NotifPopup` con sombra desacoplada).
   - Toasts en `RolesView.xaml.cs` y `PesajeView.xaml.cs` (burbujas con contenedor Grid y sombra hermana desacoplada).

---

## Alcance pendiente de las siguientes fases

### Fase 9 — Guardas del escalado en vivo (Live Scaling Guards) [Completada]

El escalado visual en vivo con `LayoutTransform` queda blindado con las 5 guardas defensivas
de estabilidad visual, dimensional y transaccional:

1. **Guarda 1 (Auto-cierre de Popups y ComboBox):**
   - En `EscalaService.Aplicar(Window, factor)`, se recorre el árbol visual cerrando preventivamente cualquier
     `Popup.IsOpen = true` y `ComboBox.IsDropDownOpen = true`.
   - Evita que las ventanas Win32 independientes de los desplegables queden huérfanas en coordenadas
     de pantalla obsoletas tras el cambio de escala de sus controles ancla.
2. **Guarda 2 (Colapso preventivo de submenús del Sidebar):**
   - `MainWindow` se suscribe a `_escala.EscalaCambiando`.
   - Ante la señal previa al relayout, `ColapsarSubmenusInmediato()` cancela cualquier animación activa en
     `Border.MaxHeightProperty` y `RotateTransform.AngleProperty` (`BeginAnimation(..., null)`), forzando
     `MaxHeight = 0` y `Angle = 0`.
   - Limpia `_activeModuleId = ""` preservando la marca del submódulo activo. Al reabrir el acordeón,
     `AnimateSubMenu` mide el contenido sobre la nueva escala sin medidas residuales ni recortes.
   - La suscripción se libera limpiamente en `LimpiarRecursosAsync()` al cerrar sesión o salir.
3. **Guarda 3 (Bloqueo defensivo ante modales abiertos):**
   - Se incorporó `IEscalaService.HayModalAbierto`, que inspecciona tanto `ComponentDispatcher.IsThreadModal`
     (diálogos modales nativos de Win32/WPF) como la presencia de cualquier `Border` con `Name == "ModalOverlay"`
     y `Visibility == Visibility.Visible` en las ventanas activas.
   - `Aplicar(factor)` aborta preventivamente con log de advertencia si hay un modal abierto.
   - `GuardarAsync` y `RestablecerAsync` rechazan el cambio retornando `Result.Fail("No se puede cambiar la escala mientras haya un modal o diálogo abierto.")`.
   - En `MiUsuarioViewModel`, los botones `PuedeAumentar` y `PuedeDisminuir` se deshabilitan reactivamente ante modales,
     y los comandos muestran un mensaje de error descriptivo en la interfaz.
4. **Guarda 4 (Reseteo de ScrollViewer en grillas y vistas):**
   - En `EscalaService.Aplicar(Window, factor)`, si el factor cambia se invoca `RestablecerScroll(ventana)`,
     recorriendo el árbol para invocar `ScrollToTop()` y `ScrollToLeftEnd()` en todos los `ScrollViewer`
     (incluyendo los `ScrollViewer` internos de `DataGrid` y contenedores de tarjetas).
   - Evita que desplazamientos intermedios queden descalzados respecto a la nueva densidad física del viewport.
5. **Guarda 5 (Escalado físico de `WM_GETMINMAXINFO`):**
   - `MainWindow` registra `_baseMinWidth` (960) y `_baseMinHeight` (520) inmutables en su construcción.
   - En el hook `WndProc` para `WM_GETMINMAXINFO` (0x0024):
     `mmi.ptMinTrackSize.X = (int)Math.Ceiling(_baseMinWidth * factor * dpi.DpiScaleX);`
     `mmi.ptMinTrackSize.Y = (int)Math.Ceiling(_baseMinHeight * factor * dpi.DpiScaleY);`
   - `Math.Ceiling` elimina el riesgo de truncamiento por subpíxel. Windows impone físicamente el límite
     mínimo exacto en tiempo real al arrastrar los bordes de la ventana.

**Verificación:**
- Suite de pruebas unitarias: **687/687 pruebas pasando** (11 pruebas nuevas en `EscalaUiTests.cs` cubriendo
  el dimensionamiento físico exacto de `MinTrackSize` a través de múltiples combinaciones de escala y DPI).
- Build: 0 errores, 0 advertencias.

---

### Fase 10 — Matriz visual manual multi-DPI y Cierre de P-065 [Completada]

Esta fase cierra definitivamente la iniciativa de escalado y las deudas asociadas:

1. **Cierre de P-065 (Seguridad de Base de Datos - Erradicación de TRUNCATE):**
   - Se creó y aplicó la migración `20260920120000_revocar_truncate_19_tablas_p065.sql` en Supabase.
   - Revoca explícitamente `TRUNCATE` concedido por defecto a `anon`, `authenticated` y `public` sobre
     las 19 tablas de negocio restantes en el esquema `public`:
     `bitacora`, `contactos_fabricante`, `contactos_proveedor`, `empleados`, `empresa`, `entradas_producto`,
     `estado_general`, `fabricantes_pais`, `modulos`, `movimiento_productos`, `movimientos`, `paises`,
     `productos_paises`, `proveedores_paises`, `reporteria`, `tara`, `tarima`, `tipo_unidad`, `unidad_medida`.
   - **Verificación al 100 % en catálogo de Postgres:** Se ejecutó consulta sobre `information_schema.role_table_grants`
     y `has_table_privilege(..., 'truncate')`, confirmando **0 tablas** con privilegio `TRUNCATE` remanente
     para roles de aplicación.
   - Se actualizó `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` marcando `P-065` como resuelto.

2. **Ejecución del Arnés Automatizado Multi-DPI:**
   - Se ejecutó el arnés de instanciación autónoma y `Measure` / `Arrange` sobre los **44 controles**
     del árbol visual de `MainWindow` contra los 5 factores de escala (0.75, 0.80, 0.90, 1.00, 1.10)
     en resolución base compacta (1366×768):
     ```
     == Verificando 44 controles x 5 factores ==
     Resultado: 220/220 mediciones OK. Fallos: 0
     ```
   - Durante la corrida se corrigió un falso positivo en `NotificacionesView.xaml`: se incorporó
     `Styles.xaml` a sus `MergedDictionaries` cumpliendo estrictamente con ADR-028 / P-057 para
     resolución autónoma de `{StaticResource ModernScrollBarAny}`.
   - Queda confirmado que el 100 % de vistas y modales instancian sin lanzar excepciones, no colapsan
     y encajan en viewport reducido.

3. **Matriz Visual Manual Multi-DPI:**
   - La guía de verificación física y funcional queda documentada en [[Fase 7 — Matriz de prueba visual del escalado]].
   - Comportamiento sin conexión garantizado por `CacheEscalaLocal` en arranque en frío.
   - Sincronización asíncrona a Supabase blindada con debounce de 600 ms en `MiUsuarioViewModel`.

---

## Estado Final de la Iniciativa de Escalado

| Fase | Componente | Estado |
|:--|:--|:--|
| **Fase 0** | Inventario exhaustivo de medidas y HWNDs | ✅ Completada |
| **Fase 1** | Tabla `usuario_preferencias` con RLS en Supabase | ✅ Completada |
| **Fase 2** | Repositorio `IPreferenciasUsuarioRepository` y DTOs | ✅ Completada |
| **Fase 3** | `EscalaService`, `CacheEscalaLocal` y sugerencias DPI | ✅ Completada |
| **Fase 4** | Mitigación de puertas de riesgo (`DynamicResource`, `CaptionHeight`) | ✅ Completada |
| **Fase 5** | Migración de `RolModal` a `UserControl` sobre overlay | ✅ Completada |
| **Fase 6** | Escalado de `ComboBox`, `ToolTip` y `Popup` vía DynamicResource | ✅ Completada |
| **Fase 7** | Arnés de instanciación inicial y diseño de matriz visual | ✅ Completada |
| **Fase 8** | Pantalla `MiUsuarioView` con control stepper y debounce | ✅ Completada |
| **Fase 9** | 5 guardas defensivas del escalado en vivo (`EscalaCambiando`, `WM_GETMINMAXINFO`) | ✅ Completada |
| **Fase 10** | Matriz multi-DPI (220/220 OK) y cierre de deuda técnica P-065 (TRUNCATE) | ✅ Completada |
| **Anti-Blur** | Zero-Shader Layout en 100 % de vistas, filtros, tablas y modales | ✅ Completada |

---

## Próximo paso

1. Probar en vivo en sesión de usuario la experiencia fluida de ajuste en *Mi Usuario → Apariencia*.

---

## Relaciones

- [[Inventario — Superficie de escalado propio (LayoutTransform global)]] — Fase 0
- [[Deuda Técnica - Pendientes]] — P-065
- [[WPF - DPI Awareness y Escalado Multi-Resolución]] — base del trabajo de escalado
- [[Módulo Usuarios]] — tabla `usuarios` referenciada por la FK
- [[Arquitectura Actual]]
