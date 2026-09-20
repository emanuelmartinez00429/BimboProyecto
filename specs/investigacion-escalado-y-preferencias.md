---
title: "Escalado de la app y preferencias por usuario — investigación consolidada"
type: plan
status: vigente
date: 2026-09-20
updated: 2026-09-20
summary: "Consolidación verificada de I-01, I-02 e I-01-R1: mecanismo de escalado con LayoutTransform aplicado en vivo, preferencias por usuario en Supabase con RLS y caché local, y el catálogo de remedios por área."
summary_fijo: true
scope:
  - CapaUI/Formularios/Principal
  - CapaUI/Resources
  - CapaUI/Services
  - CapaAplicacion4/Preferencias
  - CapaDatos/Repositories/Preferencias
symbols:
  - EscalaService
  - LayoutTransform
  - MiUsuarioViewModel
  - ScaleTransform
  - TextFormattingMode
---

# Escalado de la app y preferencias por usuario

> Consolida **I-01** (fallos bajo `ScaleTransform`), **I-02** (escalado en caliente) e
> **I-01-R1** (complemento), con las correcciones de la revisión. Todo dato marcado
> *Confirmado* fue verificado contra la fuente; lo que no se pudo verificar está en
> "Pendientes", no en las tablas.

---

## 1. Resumen y recomendación

Un solo `LayoutTransform` con `ScaleTransform` en el raíz del contenido de cada ventana,
con el factor **aplicado en vivo** desde la pantalla de Mi Usuario. La preferencia vive en
Supabase con RLS y se cachea localmente para arrancar sin red, igual que ya hace
`EmpresaThemeService` con el color de empresa.

Tres cosas hacen que esto sea más barato de lo que parecía:

- `LayoutTransform` **se auto-invalida**: no hace falta `InvalidateMeasure` en la raíz.
- **No genera rasterización intermedia**: opera a nivel vectorial durante el layout.
- **No hay doble escalado** con Per-Monitor V2: WPF compone el factor del monitor con el
  propio multiplicativamente.

Y una cosa la encarece: `TextFormattingMode="Display"`, que la app fija globalmente hoy,
**es el peor modo posible bajo un transform de zoom**. Hay que condicionarlo.

### Cambio de recomendación

En la primera vuelta recomendé aplicar la escala **al iniciar sesión**. La evidencia lo
movió: **Fork**, un cliente Git comercial para Windows, aplica zoom de toda su interfaz en
vivo por atajo de teclado, sin reiniciar y sin panel de vista previa. El miedo de fondo
—"nadie hace zoom en vivo en WPF porque es un pantano"— no tiene sustento.

La idea de **vista previa acotada a un panel de muestra**, que propuse como punto medio,
**no tiene precedente documentado** y el único caso que parecía respaldarla resultó
inventado. Queda descartada por innecesaria: si el cambio es en vivo, la app entera es la
vista previa.

---

## 2. Qué decide este documento y qué no

**Decide:** el mecanismo de escalado, el momento de aplicación, qué propiedades de
renderizado se tocan y cuáles no, dónde viven las preferencias, la forma de la tabla y sus
políticas RLS, y dónde engancha todo en el ciclo de vida.

**No decide, y no puede:** si funciona en *este* código. El precedente de Fork prueba
viabilidad en WPF, no que los submenús que animan `MaxHeight` medido y los modales atados
al overlay sobrevivan un cambio en caliente. Eso solo lo dice la matriz de pruebas de la
sección 9.

**No entra en alcance:** migrar los 633 `FontSize` literales. Con `LayoutTransform` no hace
falta, y el enfoque incremental de escalar solo el `FontSize` heredado no sirve acá
justamente porque esos 633 son los que no heredan.

---

## 3. Decisiones tomadas

| Punto | Decisión | Por qué |
|:--|:--|:--|
| Mecanismo | `LayoutTransform` + `ScaleTransform` en el raíz del contenido | `Viewbox` escala el render sin reflow (texto borroso); los recursos `sys:Double` no alcanzan a los 633 `FontSize` literales |
| Momento | **En vivo**, incremental, con guardas | Precedente WPF confirmado; sin vista previa el usuario necesita 3-4 ciclos de login para calibrar y termina no usándolo |
| `UseLayoutRounding="True"` | **Se queda** | La única evidencia en contra resultó inventada |
| `TextRenderingMode="Grayscale"` | **Se queda** | Nadie lo cuestionó con evidencia |
| `TextFormattingMode` | **Condicionado**: `Display` con factor 1.0, `Ideal` con factor ≠ 1.0 | Microsoft: *"Zooming text is the worst of all 2D transforms for display mode text"* |
| Dónde viven las preferencias | Supabase con RLS (verdad) + caché local (arranque) | Terminales mixtas: la preferencia tiene que viajar con el usuario, no con la máquina |
| Clave de la escala | `(usuario, huella de pantalla)` | Sin la huella, el 0,8× del monitor de oficina deja la terminal de planta ilegible |
| Arrastre entre monitores | **No cambia la escala** hasta la próxima sesión | La huella elige el valor al abrir la ventana; recalcular al arrastrar es impredecible para el usuario |
| Permiso RBAC nuevo | **No hace falta** | Cada quien edita lo suyo y RLS lo garantiza; `Routes.MiUsuario` sigue sin entrada en `_routePermissions` |

---

## 4. Datos verificados

Certeza recalculada contra la fuente real. **La investigación original marcó 20 de 22 filas
como "Confirmado"; la tasa verificada está cerca del 50 %.**

| # | Hallazgo | Certeza | Fuente |
|:--|:--|:--|:--|
| 1 | `Display` cuantiza las métricas a píxeles enteros; aplicar transforms desalinea esa cuantización y el texto se vuelve borroso. El zoom es el peor caso de todos los transforms 2D para `Display` | **Confirmado** | [WPF 4.0 Text Stack Improvements](https://learn.microsoft.com/en-us/archive/blogs/text/wpf-4-0-text-stack-improvements) |
| 2 | Reproducción concreta con `ScaleTransform`: texto que desaparece al reducir, borroso severo al ampliar; se arregla pasando a `Ideal` | **Confirmado** | [New Venture Software](https://www.newventuresoftware.com/blog/wpf-text-rendering-quirks-scaletransform) |
| 3 | Cambiar un `LayoutTransform` invalida el ciclo de layout de todo el árbol: `InvalidateMeasure` en la raíz es redundante | **Confirmado** | comportamiento de `AffectsMeasure`; corroborado en I-02 |
| 4 | `LayoutTransform` no genera rasterización intermedia; opera vectorialmente durante la disposición | **Confirmado** | I-02 · 4.2 |
| 5 | Descendientes con `BitmapCache` se pixelan salvo que se suba `RenderAtScale` de forma sincrónica | **Confirmado** | [MS Learn — caching an element](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-improve-rendering-performance-by-caching-an-element) |
| 6 | Per-Monitor V2 y un `ScaleTransform` propio se componen multiplicativamente: **no hay doble escalado** | **Confirmado** | I-02 · 3.1; coincide con el razonamiento independiente |
| 7 | `Popup`, `ToolTip` y `ContextMenu` viven en HWND separados y no heredan el transform del árbol visual | **Probable** | [dotnet/wpf #9420](https://github.com/dotnet/wpf/issues/9420) — la frase está, pero es una mención lateral en un issue **abierto** sobre add-ins VSTO |
| 8 | `ToolTip` bajo `LayoutTransform` no toma el tamaño transformado | **Probable** | [SO #38001665](https://stackoverflow.com/questions/38001665/wpf-layouttransform-tooltip-size-not-modified) |
| 9 | Las ventanas secundarias no heredan el transform: cada `Window` tiene su propio `HwndSource` | **Confirmado** | arquitectura de WPF; no requiere cita |
| 10 | El `AdornerLayer` vive en el `AdornerDecorator` de la ventana, fuera del contenedor escalado | **Probable** | [SO #16468368](https://stackoverflow.com/questions/16468368/exempt-control-in-adornerlayer-from-viewbox-stretching) |
| 11 | `ptMinTrackSize` se evalúa en píxeles físicos e ignora la escala lógica interna: hay que **multiplicar** `MinWidth`/`MinHeight` por el factor | **Confirmado** | aritmética verificable (960 DIPs de contenido a 0,8× ocupan 768 DIPs de ventana) |
| 12 | No hay evidencia de que un `DataGrid` virtualizado falle bajo escala fraccionaria | **Confirmado (negativo)** | [dotnet/wpf #1962](https://github.com/dotnet/wpf/issues/1962) es un cuelgue de **TreeView** y no menciona escalado |
| 13 | Fork (cliente Git para Windows) tiene zoom de toda la interfaz con `Ctrl+=` / `Ctrl+-`, aplicado en vivo | **Confirmado** | [Fork — keyboard-shortcuts-windows.md](https://github.com/fork-dev/Docs/blob/master/keyboard-shortcuts-windows.md) |
| 14 | No existe patrón documentado de vista previa acotada de interfaz en WPF | **Confirmado (negativo)** | I-02 · 6.1 |
| 15 | No hay mediciones publicadas de tiempo de relayout en ms provocado por un transform de raíz | **Confirmado (negativo)** | I-02 · 4.1 |

---

## 5. Descartado, y por qué

| Afirmación | Motivo |
|:--|:--|
| `UseLayoutRounding="False"` en el `DataGrid` | La causa ("cancelación catastrófica al estimar offsets bajo escala fraccionaria") **está inventada** y colgada de [#1962](https://github.com/dotnet/wpf/issues/1962), que es un cuelgue de TreeView sin relación. Retractado por el propio complemento |
| `ScrollViewer.CanContentScroll="False"` como remedio del extent | Cierto que resuelve el extent, pero **desactiva la virtualización**. Con ~500 productos es una regresión seria, y contradice la fila del mismo documento que trata problemas de virtualización |
| Efectos "pixelados y muddy" bajo transform | Citado a una página de **Silverlight de 2011** para afirmar comportamiento de WPF en .NET 10. Además describe **ampliación**; acá se va a reducir (0,8×), donde hay submuestreo, no pixelado |
| ModernFlyouts como precedente (deslizador 50–200 % con vista previa) | La [wiki oficial de su ventana de Configuración](https://github.com/ModernFlyouts-Community/ModernFlyouts/wiki/The-Settings-window) **no documenta ningún control de escala**. Inventado |
| VS Code, Slack, Figma, Blender como precedente | Tres son Electron (reflow de motor de navegador) y Blender es OpenGL inmediato. **Ninguno es XAML**: el modelo de costo no transfiere a layout retenido |
| Telegram Desktop = "al reiniciar" | El issue citado se titula *"No fullscreen after interface scale changing"* — describe un bug **después** del cambio, lo que sugiere aplicación en vivo. La cita apunta en contra de lo que afirma |
| `RenderOptions.EdgeMode="Aliased"` global para los bordes de 1 px | Mata el anti-aliasing de todo el subárbol. Aceptable puntualmente en un separador, nunca global |

---

## 6. Diseño técnico

### 6.1 Dónde vive cada pieza

```
CapaAplicacion4/Preferencias/Interfaces/IPreferenciasUsuarioRepository.cs   contrato
CapaAplicacion4/Preferencias/Dtos/PreferenciaDto.cs
CapaDatos/Repositories/Preferencias/PreferenciasUsuarioRepository.cs        : RepositorioBase
CapaDatos/Preferencias/CacheEscalaLocal.cs                                  archivo local
CapaUI/Services/Escala/IEscalaService.cs
CapaUI/Services/Escala/EscalaService.cs                                     singleton
CapaUI/Formularios/Principal/Pantallas/MiUsuario/MiUsuarioView.xaml
CapaUI/Formularios/Principal/Pantallas/MiUsuario/MiUsuarioViewModel.cs
```

`EscalaService` vive en `CapaUI` porque toca `Application.Current` y `Window` — el mismo
lugar y el mismo motivo que `EmpresaThemeService`. La lectura y escritura del valor es dato
y baja por el contrato en `CapaAplicacion4`, respetando que `CapaAplicacion` nunca
referencie `CapaDatos`.

### 6.2 Cómo se aplica

El factor se publica como recurso de aplicación, igual que los colores `Empresa*`. Así una
sola escritura alcanza para la ventana y para todas las plantillas compartidas:

```csharp
// EscalaService.Aplicar(double factor)
Application.Current.Resources["EscalaApp"] = factor;

foreach (Window v in Application.Current.Windows)
{
    if (v.Content is not FrameworkElement raiz) continue;

    raiz.LayoutTransform = new ScaleTransform(factor, factor);

    // Display cuantiza a pixeles enteros y el transform rompe esa alineacion
    TextOptions.SetTextFormattingMode(raiz,
        factor == 1.0 ? TextFormattingMode.Display : TextFormattingMode.Ideal);
}
```

En XAML no hace falta tocar la estructura de `MainWindow.xaml`: el transform se asigna por
código al elemento raíz que ya existe. Eso cubre de una `MainWindow`, `LoginWindow` y
`NotificacionDetalleWindow` con la misma llamada.

### 6.3 Lo que no hereda el transform

| Qué | Remedio | Dónde |
|:--|:--|:--|
| Desplegables de `ComboBox` | `LayoutTransform` en el `Border` raíz **dentro del `ControlTemplate`**, con `ScaleX`/`ScaleY` por `{DynamicResource EscalaApp}` | `Styles.xaml` — una vez, cubre toda la app |
| `ToolTip`, `ContextMenu` | Estilos implícitos globales (`<Style TargetType="ToolTip">`) con el mismo setter | `Styles.xaml` |
| `Popup` del panel de notificaciones y los otros 4 archivos | Mismo setter, explícito por ser ad-hoc | cada archivo |
| `LoginWindow`, `NotificacionDetalleWindow` | `EscalaService.Aplicar` las alcanza por el bucle sobre `Application.Current.Windows` | — |
| Adorners | `<AdornerDecorator>` **local** dentro del contenedor escalado | la vista que lo use |

> **Verificar en implementación:** `{DynamicResource}` sobre `ScaleX`/`ScaleY` dentro de un
> `Setter.Value` puede fallar si WPF congela el `Freezable`. Si falla, la alternativa es que
> `EscalaService` recorra los `Popup` abiertos y asigne el transform por código.

### 6.4 El mínimo de la ventana

`MainWindow.xaml.cs:211` ya convierte `MinWidth`/`MinHeight` de DIPs a píxeles con
`VisualTreeHelper.GetDpi`. Hay que **multiplicar** por el factor:

```csharp
// 960 DIPs de contenido a 0,8x ocupan 768 DIPs de ventana
mmi.ptMinTrackSize.x = (int)(MinWidth  * factor * dpi.DpiScaleX);
mmi.ptMinTrackSize.y = (int)(MinHeight * factor * dpi.DpiScaleY);
```

*(En la revisión escribí "dividir" por error; la cuenta correcta es multiplicar.)*

Tras un cambio en vivo hay que forzar que Windows vuelva a consultar el mensaje, con un
`SetWindowPos` de tamaño cero y `SWP_FRAMECHANGED`.

### 6.5 Las guardas del cambio en vivo

Antes de asignar el nuevo factor, en este orden:

1. **Cerrar los popups abiertos.** No recalculan posición ni detectan el cambio del
   `PlacementTarget`; quedan desalineados hasta cerrarse y reabrirse.
2. **Cerrar los submenús del sidebar.** `AnimateSubMenu` fija `MaxHeight` desde un
   `DesiredSize` medido con la escala anterior: si hay una animación en curso, interpola
   hacia un objetivo obsoleto.
3. **Bloquear el cambio si hay un modal abierto.** `ModalLayout` ata `MaxWidth`/`MaxHeight`
   al overlay y solo reevalúa el binding al hacerse visible. Es una guarda natural: Mi
   Usuario es una pantalla, no un modal.
4. **Resetear los offsets de `ScrollViewer`.** El extent cambia pero `VerticalOffset` se
   conserva, así que el punto focal se corre. Lo simple y predecible es volver a 0.
5. **Forzar la re-consulta de `WM_GETMINMAXINFO`** (6.4).

No hace falta `InvalidateMeasure`: asignar el `LayoutTransform` ya invalida el árbol.

### 6.6 Ciclo de vida

```
OnStartup
  └─ EmpresaThemeService.CargarCacheSinRed()      ← ya existe, tema de empresa
  └─ MostrarLogin()                                ← LoginWindow, factor 1.0
       └─ login OK → se crea el scope de sesion
            └─ EscalaService.Inicializar(idUsuario, huellaPantalla)
                 ├─ lee la cache local  →  aplica de inmediato
                 └─ consulta Supabase   →  si difiere, reaplica y reescribe la cache
            └─ se construye MainWindow  ← ya nace con el factor puesto
```

La escala no necesita cargarse antes del login, a diferencia del tema de empresa: no hay
usuario todavía y `LoginWindow` va siempre a 1.0. Eso evita la parte más delicada del
arranque.

---

## 7. Esquema de datos

### 7.1 Tabla

```sql
create table public.usuario_preferencias (
  id_usuario      bigint      not null
                  references public.usuarios(id_usuario) on delete cascade,
  clave           text        not null,
  ambito          text        not null default 'global',
  valor           jsonb       not null,
  actualizado_en  timestamptz not null default now(),
  primary key (id_usuario, clave, ambito)
);

create index idx_up_usuario on public.usuario_preferencias(id_usuario);
```

`clave` + `valor jsonb` significa que **agregar una preferencia no necesita migración** —
que es exactamente lo que compra haber diseñado el contrato completo desde ya.

`ambito` es `'global'` para casi todo. Solo la escala lo usa, con la huella de pantalla:

| clave | ambito | valor |
|:--|:--|:--|
| `escala_ui` | `1920x1080@1.75` | `0.8` |
| `escala_ui` | `1366x768@1.0` | `1.0` |
| `escala_ui` | `global` | `0.9` (respaldo para pantallas desconocidas) |
| `filas_por_pagina` | `global` | `25` |
| `densidad_tablas` | `global` | `"compacta"` |
| `pantalla_inicio` | `global` | `"productos"` |
| `recordar_filtros` | `global` | `true` |

Huella de pantalla: `{ancho}x{alto}@{escalaDpi}`, tomada de `VisualTreeHelper.GetDpi` y de
las dimensiones del monitor donde se abre la ventana.

### 7.2 RLS

```sql
alter table public.usuario_preferencias enable row level security;

create policy up_select on public.usuario_preferencias for select
  using (id_usuario = (select id_usuario from public.usuarios
                       where uuid_usuario = auth.uid() and id_estado = 1));

create policy up_insert on public.usuario_preferencias for insert
  with check (id_usuario = (select id_usuario from public.usuarios
                            where uuid_usuario = auth.uid() and id_estado = 1));

create policy up_update on public.usuario_preferencias for update
  using      (id_usuario = (select id_usuario from public.usuarios
                            where uuid_usuario = auth.uid() and id_estado = 1))
  with check (id_usuario = (select id_usuario from public.usuarios
                            where uuid_usuario = auth.uid() and id_estado = 1));

create policy up_delete on public.usuario_preferencias for delete
  using (id_usuario = (select id_usuario from public.usuarios
                       where uuid_usuario = auth.uid() and id_estado = 1));
```

Mismo patrón `uuid_usuario = auth.uid() and id_estado = 1` que ya usan las demás políticas.
Un usuario inactivo pierde el acceso a sus propias preferencias, que es lo correcto.

### 7.3 Caché local

`%LOCALAPPDATA%\BimboPesaje\Preferencias\escala-{idUsuario}.bin`

**El archivo se nombra por id de usuario, no por usuario de Windows.** En una terminal de
planta varios operarios pueden compartir una sola cuenta de Windows, así que
`DataProtectionScope.CurrentUser` no los separa. DPAPI se puede usar igual por consistencia
con `PreferenciasInicioSesionService` —cuesta nada— pero **no es lo que provee el
aislamiento**: eso lo hace el nombre del archivo y, sobre todo, RLS del lado del servidor.

La caché es aceleración, no verdad. Si está vieja, la consulta a Supabase la pisa.

---

## 8. La pantalla

`MiUsuarioView` sobre el molde de `ConfiguracionEmpresaView`, que ya resuelve este problema
a nivel empresa:

- `MiUsuarioViewModel : ViewModelBase` (no `RealtimeAwareViewModel`: las preferencias no
  son realtime).
- `ChangeTracker<PreferenciasSnapshot>` con `record` posicional de los campos editables,
  para el dirty tracking. Prohibido comparar campos a mano.
- `TieneCambios` / `PuedeGuardar`, estados `Cargando` / `Guardando`, `Error` /
  `MensajeExito`.
- Secciones: **Perfil** (solo lectura por ahora), **Apariencia** (escala), **Seguridad**
  (cambio de contraseña, a futuro).
- `Routes.MiUsuario` deja de apuntar a `ConstructionVM` y pasa a `MiUsuarioViewModel`.

El control de escala: pasos discretos de 5 % entre 70 % y 130 %, con botones `−` / `+`,
valor actual visible, y un botón "Restablecer". Pasos discretos y no un slider continuo
porque el cambio es en vivo y cada paso dispara un relayout.

**Sugerencia inicial** según el DPI detectado de Windows — es una heurística, no una
fórmula, y el usuario la puede ignorar:

| Escala de Windows | Factor sugerido |
|:--|:--|
| 100 % | 1.00 |
| 125 % | 0.95 |
| 150 % | 0.85 |
| 175 % | 0.80 |
| 200 % | 0.75 |

---

## 9. Matriz de pruebas

No hay tests de UI automatizados: esto es verificación manual, más el arné de instanciación
de XAML y build limpio.

**Con el transform puesto** (factores 0,75 · 0,8 · 0,9 · 1,0 · 1,1):

1. El desplegable de un `ComboBox` toma la escala, no queda a 1.0.
2. `ToolTip` y `ContextMenu` toman la escala.
3. `LoginWindow` y `NotificacionDetalleWindow` toman la escala.
4. Texto corrido a 0,8× sin encabalgamiento de glifos ni espaciado irregular.
5. Bordes de 1 px visibles y parejos, sin engrosarse a 2 px.
6. Drag & drop con el puntero alineado; adorners en su lugar.
7. `ScrollViewer` llega al final, sin barras fantasma.
8. `DataGrid` de Productos: columnas alineadas con cabeceras, scroll sin saltos.
9. Redimensionar al límite: la ventana frena en el mínimo visual esperado.
10. Sombras de modales sin degradación visible.

**Al cambiar en vivo:**

11. Cambiar con un `ComboBox` desplegado: se cierra antes de aplicar.
12. Cambiar con un submenú del sidebar abierto o animando: no queda a medio medir.
13. Cambiar con un modal abierto: el control está bloqueado.
14. Cambiar con la grilla scrolleada a la mitad: vuelve arriba, sin contenido cortado.
15. Cambiar y después redimensionar al mínimo: el nuevo mínimo es el correcto.
16. Diez cambios seguidos: sin fugas de memoria ni degradación acumulada.

**Multi-monitor:**

17. Arrastrar entre monitores de distinto DPI: no hay doble escalado, la escala no cambia.
18. Abrir en el monitor secundario: toma la preferencia de esa huella, no la del primario.

**Preferencias:**

19. Loguearse en otra terminal: la escala de *esa* pantalla, no la de la anterior.
20. Sin red: arranca con la caché local y no se cuelga.
21. Dos usuarios distintos en la misma terminal: cada uno con su escala.
22. Usuario inactivado: RLS le niega sus propias preferencias.

---

## 10. Riesgos

| Riesgo | Mitigación |
|:--|:--|
| El precedente de Fork prueba viabilidad, **no** que funcione en este código | Puntos 11 a 16 de la matriz son precisamente eso; si fallan, se cae a aplicar al iniciar sesión, que ya estaba diseñado |
| `{DynamicResource}` en `Setter.Value` sobre un `Freezable` puede no actualizar | Alternativa lista: recorrer los popups por código desde `EscalaService` |
| Los `Width`/`Height` fijos (683 y 542 en XAML) pueden recortar contenido en algunos factores | Limitar el rango a 70–130 % y cubrir los extremos en la matriz |
| Los umbrales responsive (1060, 1150) se mueven | **No es un riesgo, es el comportamiento deseado**: el contenido se mide antes del transform, así que a 0,8× recibe 1/0,8 = 25 % más ancho lógico y los botones se ocultan menos |
| Cada paso de escala dispara un relayout completo y no hay mediciones publicadas de cuánto tarda | Pasos discretos en vez de slider continuo; medir en la pantalla de Productos, que es la más pesada |
| `TextFormattingMode="Ideal"` empeora el texto chico a 1.0 | Por eso está condicionado: a factor 1.0 se mantiene `Display` |

---

## 11. Pendientes sin verificar

Nada de esto bloquea la implementación, pero no está confirmado y no debe citarse como si lo estuviera:

- **Fork es WPF.** Se verificó la *función* (documentación oficial de atajos), no el
  framework. Es plausible y ampliamente reportado, sin confirmar en fuente oficial.
- **Fluent Search** como segundo precedente WPF: citado solo a su página de inicio, no
  verificado. Con Fork confirmado no cambiaría la decisión.
- **"La comunidad debatió `UseLayoutRounding` en #1962".** Mi lectura del issue dice que no
  lo menciona. No cambia la conclusión (no hay evidencia para el `DataGrid`).
- **Telegram Desktop**: inmediato o al reiniciar, sin resolver. La cita apunta en contra de
  lo que afirmaba la investigación.
- **Tiempo de relayout en milisegundos**: nadie lo publicó. Hay que medirlo acá.
- **Los 114 wikilinks rotos y los `Width`/`Height` fijos** no se auditaron uno por uno.

---

## 12. Próximos pasos

1. **Migración SQL**: tabla `usuario_preferencias` + las cuatro políticas RLS. Aislado, sin
   tocar la app.
2. **Contrato y repositorio**: `IPreferenciasUsuarioRepository` en `CapaAplicacion4`,
   implementación en `CapaDatos` con `RepositorioBase` y `Result<T>`.
3. **`EscalaService`** con la caché local y el enganche en el scope de sesión. Sin UI
   todavía: se prueba fijando el factor a mano.
4. **Puntos 1 a 10 de la matriz** con el factor cableado. Si algo de acá falla, se arregla
   antes de construir la pantalla.
5. **`MiUsuarioView`** con la sección Apariencia y el `ChangeTracker`.
6. **Puntos 11 a 22 de la matriz.**
7. **ADR** con la decisión del `TextFormattingMode` condicionado, que es la única que
   revierte algo ya decidido (sesión 2026-06-21).

Los pasos 1 a 3 no tocan nada visible y se pueden commitear por separado.

---

## Relaciones

- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — mismo
  problema de tildes resuelto del lado de los datos
- [[ADR-019 - Configuración de empresa y tema dinámico global]] — el patrón de preferencia
  aplicada en runtime que esto replica
- [[Sesión 2026-06-21 - Soporte Multi-Resolución y DPI Per-Monitor V2]] — donde se fijaron
  `UseLayoutRounding`, `TextRenderingMode` y `TextFormattingMode`
- [[Módulo Usuarios]] — la tabla `usuarios` a la que referencia la nueva tabla
- [[Convenciones C#]]
