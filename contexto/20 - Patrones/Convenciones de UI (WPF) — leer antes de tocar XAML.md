---
title: "Convenciones de UI (WPF) — leer antes de tocar XAML"
tags:
  - patron
  - wpf
  - xaml
  - ui
  - convenciones
date: 2026-09-10
lifecycle: verified
---

# Convenciones de UI (WPF) — leer antes de tocar XAML

> [!abstract]
> Nodo único de convenciones de UI del proyecto. **Cualquier agente que vaya a crear o editar un `UserControl`, `Window`, `ResourceDictionary` o cualquier `.xaml` lee esto primero.** Cada regla acá tiene detrás una sesión, un ADR o una prueba — no son preferencias de estilo. Se va sumando a medida que aparecen casos.

## Cómo usar este nodo

1. Antes de tocar XAML: leé las reglas de abajo que apliquen a tu cambio.
2. Al terminar: **build limpio (0/0)** + **arné de instanciación** (§8) + prueba visual manual del flujo tocado.
3. Si descubrís una regla nueva o un gotcha, agregalo acá con su evidencia (link a sesión/ADR/referencia).

---

## 1. Un control debe poder instanciarse solo

El diseñador de Visual Studio (y una prueba, y otro host) crea el control **por su constructor sin parámetros** y **no ejecuta `App.xaml` ni el code-behind del documento raíz**. Un control que depende de que "alguien ya cargó algo" no se previsualiza — lienzo en blanco + `TaskCanceledException` en Salida.

**Reglas:**

- **Constructor sin parámetros** en todo `UserControl`/`Window` con `x:Class`. Si el ctor real recibe dependencias, el sin-parámetros las deja en `null!` y llama `InitializeComponent()`. Nada más.
  ```csharp
  /// <summary>Constructor de diseño. Ver ADR-028.</summary>
  public MiModal()
  {
      _repo = null!;
      InitializeComponent();
  }
  ```
- **Nada de DI en el constructor** (regla 9 de `AGENTS.md`). Las dependencias entran por el ctor real, desde quien abre el control. Prohibido `App.Services.GetRequiredService<T>()` dentro del `.ctor`. El parche `if (DesignerProperties.GetIsInDesignMode(this)) return;` **no sirve** para esto — el diseñador no llama a ese ctor.
- **El control mergea los diccionarios que usa** en su `<UserControl.Resources>`, en forma **corta** de URI:
  ```xml
  <ResourceDictionary Source="/CapaUI;component/Resources/Styles.xaml"/>
  ```
  Mergear ≠ redefinir: sigue prohibido escribir un `<Style x:Key="...">` que ya existe en `Styles.xaml`.
- **`mc:Ignorable="d"` + `d:DesignWidth`/`d:DesignHeight`** siempre. Sin el tamaño de diseño, un control con contenido centrado colapsa a alto 0 en el lienzo.
- **Un `Loaded` que hable con `App.Services`** va detrás de un guard de modo diseño:
  ```csharp
  if (DesignerProperties.GetIsInDesignMode(this)) return;
  Loaded += (_, _) => Init();
  ```

Detalle y evidencia: [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]], [[Anatomia compartida de los modales]], [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]].

## 2. El contenedor decide el tamaño del hijo, no al revés

**Prohibido** que un control calcule su propio tamaño mirando hacia arriba:

```xml
<!-- ❌ NUNCA -->
MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border}, Converter=...}"
```

`RelativeSource AncestorType` se escapa del control: en el diseñador engancha un `Border` de la infraestructura de VS, que mide 0 → el control colapsa a 0×0 **en silencio** (`FallbackValue` nunca entra porque el binding *sí* resuelve). Es el contrato de layout invertido: en WPF el tamaño lo decide **quien hospeda**.

**Cómo se hace:** el host ata el límite por **referencia directa** al overlay, con `CapaUI.Core.ModalLayout.LimitarAlOverlay(modal, ModalOverlay)` al mostrarlo. Si el overlay está en el mismo XAML, `{Binding ActualWidth, ElementName=ModalOverlay, ...}` también sirve (lo usa `ReporteriaView`). El `AncestorType` solo es válido **dentro de un `ControlTemplate`**, nunca para que un `UserControl` se dimensione.

Evidencia: [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] (Addendum 2), y confirmado con documentación de WPF (`RelativeSource.AncestorType`, comportamiento de `ActualWidth` durante Measure/Arrange).

## 3. Converters: `{x:Static}` a un singleton, no `{StaticResource}` a `App.xaml`

Un `IValueConverter` **stateless** (todos los del proyecto lo son) se referencia por su instancia estática:

```csharp
public sealed class MiConverter : IValueConverter
{
    public static readonly MiConverter Instancia = new();
    // ...
}
```
```xml
xmlns:conv="clr-namespace:CapaUI.Converters"
...
Converter={x:Static conv:MiConverter.Instancia}
```

Por qué, no solo "lo dice el ADR":

- `{x:Static}` resuelve contra el **tipo CLR**, sin diccionario de por medio → funciona igual en runtime, en el diseñador y en tests.
- `{StaticResource}` a una clave de `App.xaml` **revienta en el diseñador** (no ejecuta `App.xaml`). Si va en un atributo del elemento raíz, además es una referencia hacia adelante y ni siquiera un merge local lo salva.
- Declarar converters en `App.xaml`/`ResourceDictionary` **no evita instancias duplicadas** — WPF crea instancias separadas al mergear diccionarios. El singleton garantiza una sola.
- `App.xaml` con decenas de `<conv:...>` es *"akin to declaring multiple classes in a single C# file"*.

Estado en el repo (desde 2026-09-10): **cero** `{StaticResource <converter>}` — los 6 converters (`BoolToVisibility`, `InverseBoolToVisibility`, `RestarMargen`, `AnchoMinimoAVisibilidad`, `VacioAVisibilidad`, `TextoVacioConverter`) tienen `.Instancia` y ya no están en `App.xaml`.

Fuentes: [Static WPF Converters — gregsdennis](https://gregsdennis.github.io/coding-for-smarties/2015/03/28/static-wpf-converters.html), [Dr. WPF — Making Value Converters More Accessible in Markup](http://drwpf.com/blog/2009/03/17/tips-and-tricks-making-value-converters-more-accessible-in-markup/), [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]].

## 4. Estilos compartidos van en `Styles.xaml`, no inline por control

Todo lo repetible (estilos de `DataGrid`, botones de modal, inputs, geometrías de íconos, celdas centradas) vive **una sola vez** en `CapaUI/Resources/Styles.xaml`. Un control nuevo lo **mergea y usa**, nunca copia el `<Style>`. Copiar un estilo local que ya existe global = deuda inmediata (pasó con `LupaBtn`, con `PesajeModalStyles.xaml`).

Ver [[Anatomia compartida de los modales]] y las sesiones de centralización de DataGrids (2026-09-03).

## 5. Los colores `Empresa*` NUNCA se mueven a `Styles.xaml`

`EmpresaThemeService.Aplicar()` escribe el tema por empresa **directamente en `Application.Current.Resources`**. `{DynamicResource}` busca elemento → ancestros → `Application`. Si `Styles.xaml` definiera `EmpresaPrimaryBrush` y un control lo mergeara localmente, la copia local **le gana a `Application`** y el control queda con el azul por defecto ignorando la empresa — en runtime, en silencio, sin error.

- Runtime: los colores `Empresa*` viven **solo en `App.xaml`**.
- Diseño: su copia (misma paleta) vive en `CapaUI/Properties/DesignTimeResources.xaml` (ámbito `Application`, solo lo mergea el diseñador vía `ContainsDesignTimeResources` en el `.csproj`).

Ver [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] y ADR-019.

## 6. Traducir conceptos a idioma WPF, no literal

Si el requisito viene en términos de WinForms (`DisplayedCells`), HTML/CSS (`flex`, `div`) o Android, **nunca** trasladar el término como atributo XAML o identificador C#. Interpretarlo y traducirlo al equivalente nativo y validado de WPF (ej. `DataGrid` → `Width="Auto"` + `MinWidth`, no `SizeToDisplayedCells`). Si un enum/propiedad no existe en WPF, verificar la API oficial antes de generar código. (Regla 8 de `AGENTS.md`.)

## 7. Un botón/estilo que "no reacciona" casi nunca es el estilo

Cuando un control con estilo compartido no cambia de aspecto al cambiar de estado (un basurero que no se pone gris al deshabilitarse, un badge que no se recolorea), **el sospechoso es la notificación, no el `Style`**. Antes de tocar el estilo o dibujar el control por fila:

1. Verificá que el `Style`/`ControlTemplate` **ya tiene el trigger** (`<Trigger Property="IsEnabled" Value="False">`, etc.). Casi siempre lo tiene.
2. Verificá que la propiedad del binding (`IsEnabled="{Binding PuedeQuitar}"`) **se está re-evaluando**. Si es un getter calculado (`PuedeQuitar => !TienePesajes`) sobre una colección, WPF no se entera de que cambió salvo que alguien dispare `PropertyChanged` para esa propiedad.

**Gotcha real (basurero de fila en pesaje, 2026-09-10):** `ProductoCamion.NotificarAgregados()` era una lista a mano de `OnPropertyChanged(nameof(...))` para las 12 props derivadas de `Entradas`. Faltaban `PuedeQuitar`/`TienePesajes`/`MotivoQuitar`, así que al pesar un producto la columna "% restante" se actualizaba pero el basurero seguía rojo. La clase hermana `CamionPesaje` lo hacía bien con `[NotifyPropertyChangedFor(nameof(PuedeQuitar))]`.

**Regla:** un método "recalcular todo lo derivado de X" **no lleva lista a mano** — usá `OnPropertyChanged(string.Empty)` (= *todas las propiedades cambiaron*). Una fila tiene ~15 bindings y esto corre en un clic humano: el costo de refrescar de más es nulo y ninguna propiedad derivada se puede volver a olvidar. La lista enumerada solo se justifica en un bucle caliente, que en UI no existe.

## 8. Verificación — el arné de instanciación

No hay tests de UI automatizados. La verificación de que un control **carga y hace layout** sin la app es un arné WPF de ~40 líneas:

```csharp
[STAThread] static void Main()
{
    var _ = new Application();                 // Resources vacío = condición del diseñador
    foreach (var (nombre, crear) in casos) {
        try {
            var c = (FrameworkElement)crear(); // ctor sin parámetros
            c.Measure(new Size(1400, 900));
            c.Arrange(new Rect(0, 0, c.DesiredSize.Width, c.DesiredSize.Height));
            bool colapsa = c.ActualWidth < 5 || c.ActualHeight < 5;
            // ... reportar OK / COLAPSA / EXCEPCION
        } catch (Exception ex) { /* volcar inner + mensaje */ }
    }
}
```

- Proyecto `net10.0-windows` + `<UseWPF>true</UseWPF>`, referencia a `CapaUI`. Se arma en el scratchpad, se corre, se borra.
- Si la app o VS tienen el DLL bloqueado: `dotnet build -p:UseAppHost=false` y referenciar `CapaUI/obj/Debug/net10.0-windows/CapaUI.dll`.
- **Punto ciego conocido:** un `{StaticResource}` dentro de un `ControlTemplate`/`DataTemplate`/`DataTrigger` diferido **no se evalúa** en un `Measure`/`Arrange` sin datos. El arné da falso verde ahí. Mitigación: `grep -rn "StaticResource <clave>"` en todo el repo, no confiar solo en el arné.
- El arné verifica *carga y layout*, no comportamiento ni bindings con datos reales → **sigue haciendo falta la prueba visual manual**.

## 9. Prohibido `{DynamicResource}` dentro de `TargetNullValue` o `FallbackValue` de un `Binding`

En WPF, `TargetNullValue` y `FallbackValue` en una expresión `{Binding}` son propiedades CLR de tipo `object`, **no son `DependencyProperty` de un `DependencyObject`**. 

Escribir:
```xml
<!-- ❌ NUNCA: Compila limpio pero detona XamlParseException fatal al abrir la vista -->
Stroke="{Binding MiBrocha, ElementName=Root, TargetNullValue={DynamicResource EmpresaPrimaryBrush}}"
```
arroja en runtime:
`System.Windows.Markup.XamlParseException: "DynamicResourceExtension" no se puede establecer en la propiedad "TargetNullValue" de tipo "Binding". "DynamicResourceExtension" solo se puede establecer en una DependencyProperty de un DependencyObject.`

**Cómo se hace correctamente:**
1. Asignar el recurso dinámico directamente en la propiedad del elemento visual en XAML:
   ```xml
   Stroke="{DynamicResource EmpresaPrimaryBrush}"
   ```
2. La `DependencyProperty` del control (`MiBrocha`) actúa como canal de personalización opcional: en su callback `PropertyChangedCallback` (`OnMiBrochaChanged`), si el valor no es nulo, se sobreescribe la brocha del elemento visual en C#.
3. En code-behind de controles reutilizables, usar siempre resolución defensiva con `FindName` al acceder a elementos por nombre en callbacks de inicialización o márgenes para tolerar cualquier orden del ciclo de vida del árbol visual y desincronizaciones de diseño en Visual Studio.

## 10. Desacoplamiento de campos XAML (`x:Name`) en code-behind de UserControls

Para evitar errores `CS0103: El nombre 'X' no existe en el contexto actual` provocados por desincronización de la caché de compilación de diseño de Visual Studio (`.g.i.cs`), no accedas directamente a campos de marcado en lógica de layout o callbacks:
```csharp
// ❌ Evitar depender de campos autogenerados en UserControls:
ContenedorPanel.Margin = new Thickness(0, HeaderOffset, 0, 0);

// ✅ Resolver con FindName y pattern matching:
if (FindName("ContenedorPanel") is FrameworkElement panel)
    panel.Margin = new Thickness(0, HeaderOffset, 0, 0);
```

## 11. Animaciones en `Freezable` / Transformaciones (Spinners e Indicadores)

`RotateTransform`, `ScaleTransform` y `TranslateTransform` son `Freezable`, no `FrameworkElement`.
1. **No usar `Storyboard` sin host explícito:** Llamar a `_storyboard.Begin()` sobre un `RotateTransform` descarta silenciosamente la animación porque el motor no puede anclar el reloj de despacho sin un elemento visual raíz.
2. **Usar `IAnimatable.BeginAnimation`:** Es directo, hardware-accelerated, consume menos recursos y no tiene dependencias de nombres:
   ```csharp
   rotate.BeginAnimation(RotateTransform.AngleProperty, anim); // Iniciar
   rotate.BeginAnimation(RotateTransform.AngleProperty, null); // Detener
   ```
3. **Enganchar en `Loaded` y `Unloaded`:** Asegurarse de que si el estado de carga cambia antes del montaje del control, el evento `Loaded` inicie la animación, y `Unloaded` la libere para evitar fugas de memoria.

## 12. Rendimiento de Renderizado: Zero-Shader Layout, ClearType y Freeze

El uso indebido de `DropShadowEffect` (Pixel Shaders compilados en HLSL) genera Superficies Intermedias de Renderizado (*Intermediate Render Targets* o IRT) que colapsan la tasa de relleno de la GPU y destruyen el antialiasing subpixel *ClearType*.

1. **Patrón de Borde Hermano Desacoplado (*Decoupled Sibling Pattern*):**
   - **Prohibido** anidar `DropShadowEffect` en el mismo `Border` que contiene un `DataGrid`, `ScrollViewer` o elementos con `ClipToBounds="True"`.
   - La sombra se ubica en un `Border` hermano en la capa inferior (Z-Index menor), mientras que el contenido vivo corre en un `Border` superior con fondo completamente opaco.
2. **Preservación de ClearType:**
   - Todo `DataGrid` debe tener `Background="White"`, `RowBackground="White"` y `RenderOptions.ClearTypeHint="Enabled"`.
   - El control contenedor debe declarar `TextOptions.TextRenderingMode="Auto"` para que DirectWrite optimice fuentes según DPI.
3. **Congelamiento de Recursos Vectoriales (`po:Freeze="True"`):**
   - Geometrías vectoriales fijas en recursos (`PathGeometry`) y pinceles estáticos deben declararse con `xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options"` y `po:Freeze="True"`. En el elemento raíz declarar `mc:Ignorable="d po"`.

## 13. RadioButtons enlazados a Enums: Sin `GroupName` y con `{x:Static}`

Cuando los RadioButtons se enlazan bidireccionalmente a una propiedad `Enum` en el ViewModel a través de `EnumToBooleanConverter`:

1. **Omitir `GroupName`:** El ViewModel y el conversor ya garantizan la exclusión mutua de forma determinista. Incluir `GroupName` fuerza a WPF a ejecutar `RadioButton.UpdateRadioButtonGroup()` recorriendo todo el árbol visual $O(N)$ en cada clic y provoca condiciones de carrera entre la deselección del anterior y la selección del nuevo.
2. **Usar `{x:Static}` en `ConverterParameter`:**
   ```xml
   <!-- ✅ Verificado en tiempo de compilación por Roslyn y cero asignaciones en Gen0 -->
   IsChecked="{Binding EstadoFiltro, Converter={x:Static conv:EnumToBooleanConverter.Instancia}, ConverterParameter={x:Static local:EstadoFilter.Activos}, Mode=TwoWay}"
   ```
   Evita cadenas mágicas (`ConverterParameter=Activos`) y permite a `EnumToBooleanConverter` ejecutar comparación directa de valores sin parseo en runtime ni asignación de strings.

## 14. Ciclo de Vida de CTS en .NET 10: Reemplazo Atómico y CancelAsync

1. **`CancelAsync()` en lugar de `Cancel()` sincrónico:** En .NET 10, `Cancel()` bloquea el hilo emisor mientras `SocketsHttpHandler` drena flujos HTTP. `CancelAsync()` retorna control de inmediato.
2. **Reemplazo Lock-Free con `Interlocked.Exchange`:**
   ```csharp
   var cts = new CancellationTokenSource(TimeoutMs);
   var oldCts = Interlocked.Exchange(ref _ctsPagina, cts);
   if (oldCts is not null)
   {
       try { _ = oldCts.CancelAsync(); } catch (ObjectDisposedException) { }
   }
   ```
3. **Limpieza en `finally` con `Interlocked.CompareExchange`:**
   ```csharp
   finally
   {
       Interlocked.CompareExchange(ref _ctsPagina, null, cts);
       // Omitir cts.Dispose() deliberadamente: el GC lo recolecta de forma segura evitando
       // ObjectDisposedException en SocketsHttpHandler al haber cancelaciones asíncronas concurrentes (P-060).
   }
   ```
   Evita que una carga anterior que finaliza tarde ponga a `null` el token de una carga más nueva que ya tomó el control.

## 15. Manejo de Fallo Parcial y Detección de Cambios en RPCs Particionadas

Cuando un caso de uso requiere múltiples operaciones RPC secuenciales (ej. `UpdateAsync` para datos generales seguido de `CambiarEstadoAsync` para el ciclo de vida):

1. **Orden Determinista:** Modificaciones comerciales en primer lugar; cambios de estado en segundo lugar.
2. **Detección Atómica de Cambios (Dirty Tracking):** Comparar contra el snapshot inmutable original y ejecutar únicamente las RPCs de las facetas que realmente mutaron. Si nada cambió, cerrar el modal limpiamente sin tocar la red.
3. **Manejo de Fallo Parcial con Notificación (sin rollback destructivo):** Si el paso 1 tiene éxito en base de datos pero el paso 2 falla:
   - Confirmar el token de idempotencia del paso 1 (`_solicitud.Confirmar()`).
   - Notificar explícitamente al operador mediante un aviso contextual (`"Los datos se actualizaron correctamente, pero no se pudo cambiar su estado: ..."`).
   - Invocar el evento de guardado para refrescar los datos consolidados en la grilla, preservando la edición exitosa y evitando estados desincronizados en el cliente.

## 16. Plantillas de `TextBox` y triggers de `ControlTemplate`: dos gotchas

1. **No repetir el `Padding` en `PART_ContentHost`.** `TextBoxBase` ya aplica el `Padding` dentro del host. Con `Margin="{TemplateBinding Padding}"` en el `ScrollViewer`, el texto queda corrido el doble. Si hay un marcador superpuesto, alinearlo con el texto real (Padding + ~2px del `TextBoxView`) y **verificarlo con un render**, no a ojo. Pendiente de auditar en los estilos compartidos: [[Deuda Técnica - Pendientes#P-064|P-064]].
2. **En `ControlTemplate.Triggers`, el control plantillado es `RelativeSource Self`.** Un `Binding` dentro de un `DataTrigger`/`MultiBinding` de los triggers de la plantilla **no** resuelve con `RelativeSource TemplatedParent`. Usar `Self` (ej. `CommandParameter` del botón para marcar la opción activa).

Evidencia: [[Sesión 2026-09-17 - Rediseño visual de Configuración de empresa]].

## Anti-patrones — lista negra rápida

| ❌ No hacer | ✅ En su lugar |
|---|---|
| `UserControl` sin ctor sin parámetros | ctor de diseño con servicios en `null!` |
| `App.Services.Get...` en el `.ctor` | inyección por el ctor real |
| `if (GetIsInDesignMode) return;` en el ctor con parámetros | ctor sin parámetros separado |
| `MaxWidth`/`MaxHeight` del raíz con `AncestorType` | `ModalLayout.LimitarAlOverlay` en el host |
| `Converter={StaticResource X}` (X de `App.xaml`) | `Converter={x:Static conv:XConverter.Instancia}` |
| `<Style x:Key="...">` copiado de `Styles.xaml` | mergear `Styles.xaml` y usar el global |
| `EmpresaPrimaryBrush` en `Styles.xaml` | solo `App.xaml` + `DesignTimeResources.xaml` |
| `{DynamicResource}` en `TargetNullValue`/`FallbackValue` | Asignar `{DynamicResource}` directo en la propiedad del elemento visual y sobreescribir por callback de DP si no es nulo |
| Campos `x:Name` acoplados en C# de UserControls | Resolver con `FindName(...) is FrameworkElement` |
| `Storyboard.Begin()` huérfano sobre `Freezable` | `rotate.BeginAnimation(RotateTransform.AngleProperty, anim)` directo |
| `DropShadowEffect` en contenedor con `ClipToBounds` o `DataGrid` | Borde hermano desacoplado (Zero-Shader Layout) |
| `GroupName` en RadioButtons enlazados a enums con ViewModel | Omitir `GroupName` y tipar `ConverterParameter={x:Static ...}` |
| `_cts?.Cancel()` sincrónico o `Task.WhenAny` con Delay | `Interlocked.Exchange` + `_ = oldCts.CancelAsync()` |
| Reintentos ciegos ignorando fallo en la 2da RPC | Manejo de fallo parcial y confirmación idempotente del 1er paso |
| `PART_ContentHost` con `Margin="{TemplateBinding Padding}"` | host sin `Margin` (el `Padding` ya lo aplica `TextBoxBase`) |
| `RelativeSource TemplatedParent` en `ControlTemplate.Triggers` | `RelativeSource Self` |
| Literal de otro framework como atributo XAML | equivalente WPF verificado en la API |

---

## Relaciones

- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] — la decisión y las capas del bug
- [[Anatomia compartida de los modales]] — la receta de 4 pasos y las tablas de réplica
- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] — el hecho de WPF y el arné de diagnóstico
- [[Deuda Técnica - Pendientes]] — P-057 (previsualización), P-042 (familia paralela de estilos)
- [[Arquitectura Actual]]
- [[CLAUDE]] — convenciones de código C#
