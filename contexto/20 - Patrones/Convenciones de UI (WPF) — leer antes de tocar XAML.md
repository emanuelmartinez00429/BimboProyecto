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
2. Al terminar: **build limpio (0/0)** + **arné de instanciación** (§7) + prueba visual manual del flujo tocado.
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

## 7. Verificación — el arné de instanciación

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

- Proyecto `net8.0-windows` + `<UseWPF>true</UseWPF>`, referencia a `CapaUI`. Se arma en el scratchpad, se corre, se borra.
- Si la app o VS tienen el DLL bloqueado: `dotnet build -p:UseAppHost=false` y referenciar `CapaUI/obj/Debug/net8.0-windows/CapaUI.dll`.
- **Punto ciego conocido:** un `{StaticResource}` dentro de un `ControlTemplate`/`DataTemplate`/`DataTrigger` diferido **no se evalúa** en un `Measure`/`Arrange` sin datos. El arné da falso verde ahí. Mitigación: `grep -rn "StaticResource <clave>"` en todo el repo, no confiar solo en el arné.
- El arné verifica *carga y layout*, no comportamiento ni bindings con datos reales → **sigue haciendo falta la prueba visual manual**.

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
| Literal de otro framework como atributo XAML | equivalente WPF verificado en la API |

---

## Relaciones

- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] — la decisión y las capas del bug
- [[Anatomia compartida de los modales]] — la receta de 4 pasos y las tablas de réplica
- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] — el hecho de WPF y el arné de diagnóstico
- [[Deuda Técnica - Pendientes]] — P-057 (previsualización), P-042 (familia paralela de estilos)
- [[Arquitectura Actual]]
- [[CLAUDE]] — convenciones de código C#
