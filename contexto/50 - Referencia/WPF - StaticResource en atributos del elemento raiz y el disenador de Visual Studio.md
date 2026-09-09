---
title: "WPF — {StaticResource} en atributos del elemento raíz y el diseñador de Visual Studio"
tags:
  - referencia
  - wpf
  - xaml
  - diseñador
date: 2026-09-09
lifecycle: verified
---

# WPF — `{StaticResource}` en atributos del elemento raíz y el diseñador de Visual Studio

> [!info] Fuente
> Prueba propia (2026-09-09) con un arnés que reproduce la condición exacta del subrogado del diseñador: `new Application()` con `Resources` vacío + instanciar el `UserControl` por su constructor sin parámetros. La regla de fondo es la documentada por Microsoft para `StaticResource`: *"no debe hacer una referencia hacia adelante a un recurso definido léxicamente más adelante en el XAML"*.

## El hecho

Son **dos** hechos que se combinan, y la confusión viene de mezclarlos.

### 1. El diseñador no ejecuta `App.xaml`

El subrogado del diseñador (`WpfSurface.exe`) crea su **propia** `Application`. La clase `App` del proyecto nunca se instancia, así que **nada de lo que esté en `Application.Resources` existe en el lienzo**. Un `{StaticResource}` a una clave de `App.xaml` revienta el parseo, `InitializeComponent` lanza, el subrogado cae y en Salida aparece un `TaskCanceledException` — que es el síntoma, no la causa. La causa real es una `XamlParseException` que hay que ir a buscar.

Esto se arregla haciendo que el control **declare los diccionarios que usa** en su propio `UserControl.Resources`:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="/CapaUI;component/Resources/Styles.xaml"/>
</ResourceDictionary.MergedDictionaries>
```

> [!note] Sobre la forma de la URI, honestamente
> Se usó la forma corta (`/CapaUI;component/...`) porque es la que recomienda la documentación para el diseñador. Pero **no está probado que la larga (`pack://application:,,,/...`) fuera un problema**: se cambió en el mismo tramo en que se quitó el binding del raíz, y lo que arregló el lienzo fue el binding — eso sí está medido. La forma corta queda como convención, no como fix demostrado. Si alguien quiere zanjarlo, es un experimento de un minuto: volver a la larga y ver si el lienzo sigue bien.

Fuera del subrogado, las dos formas resuelven igual — verificado. Y el costo es despreciable: **0,4 ms** parsear `Styles.xaml` (1454 líneas, 86 claves de primer nivel) y 0,1 ms `PesajeModalStyles.xaml`. Instanciar `ProductosCargaModal` entero cuesta 4–5 ms. No hace falta ningún `SharedResourceDictionary` con caché.

### 2. Ese merge NO alcanza a los atributos del propio elemento raíz

Este es el que muerde y el que no es obvio.

El parser aplica los miembros **en orden del documento**. En el XML, los **atributos** del elemento raíz vienen antes que el elemento-propiedad `<UserControl.Resources>`, que es un hijo. Es decir:

```xml
<UserControl ...
             MaxWidth="{Binding ..., Converter={StaticResource RestarMargen}}">   <!-- (1) se aplica ACÁ -->
    <UserControl.Resources>                                                       <!-- (2) se puebla DESPUÉS -->
        <conv:RestarMargenConverter x:Key="RestarMargen"/>
    </UserControl.Resources>
```

Cuando se evalúa `(1)`, el diccionario de `(2)` **todavía está vacío**. Declarar la clave ahí es una **referencia hacia adelante** y `StaticResource` no la resuelve: sube por el árbol lógico, no encuentra ancestro (el control aún no está insertado en ninguno) y termina en `Application.Resources`. En la app corriendo eso funciona porque la clave está en `App.xaml`; en el diseñador no hay `App` y explota.

**Consecuencia práctica:** un merge local, por completo que sea, arregla los `{StaticResource}` del **contenido** del control pero **nunca** los de sus propios atributos raíz.

## `{StaticResource}` vs `{DynamicResource}` vs `{x:Static}`

| Mecanismo | Cuándo resuelve | Degrada sin excepción si falta | Sirve en un atributo del raíz |
|---|---|---|---|
| `{StaticResource}` | Al parsear | ❌ lanza | ❌ referencia hacia adelante |
| `{DynamicResource}` | En tiempo de ejecución, y re-evalúa | ✅ queda sin valor | ⚠️ **solo si el destino es una `DependencyProperty`** |
| `{x:Static}` | Al parsear, contra el **tipo CLR** | n/a — no usa diccionario | ✅ siempre |

> [!warning] `{DynamicResource}` no sirve para un `Converter`
> `Binding` **no** es un `DependencyObject`, así que su propiedad `Converter` no es una `DependencyProperty` y no acepta referencias diferidas. `Converter={DynamicResource X}` no compila como se espera. Lo mismo vale para cualquier propiedad de una extensión de marcado, para las claves (`x:Key`) y para tipos congelados (`Freezable` ya sellado).

Dónde `{DynamicResource}` **sí** es la herramienta correcta: `Style`, `Background`, `Foreground`, `Fill` y demás propiedades de dependencia — es lo que hace `PaginadorControl.xaml`, que por eso siempre se previsualizó bien.

## Corolario: un merge local eclipsa `Application.Resources`

`{DynamicResource}` busca hacia arriba: **recursos del elemento → ancestros → `Application` → tema del sistema**. El diccionario que el control mergea en su propio `UserControl.Resources` está al principio de esa cadena, así que **le gana a `Application.Resources`**.

Verificado el 2026-09-09: con `Application.Resources["EmpresaPrimaryBrush"]` en `#B1002E` y un diccionario mergeado en el control con `#1E3A8A`, el `DynamicResource` resuelve a **`#1E3A8A`**.

> [!danger] Nunca mover los colores `Empresa*` a `Styles.xaml`
> `EmpresaThemeService.Aplicar()` escribe el tema por empresa directamente en `Application.Current.Resources`. Si `Styles.xaml` definiera esas claves, cualquier control que lo mergee localmente quedaría pintado con el color por defecto **ignorando el de la empresa, en silencio y sin error**. Los `Style` y las `Geometry` de `Styles.xaml` no tienen este problema porque nadie los reescribe en runtime.

**Consecuencia para el lienzo:** un control que se previsualiza bien igual se dibuja **sin los colores de empresa**, porque esas claves solo existen en `Application.Resources` y el diseñador no instancia `App`. Pero degrada sin excepción — la estructura, los estilos compartidos y el layout se ven correctos; solo falta el color. Si se quisiera el color fiel, el único lugar sano para esas seis claves es un `DesignTimeResources.xaml` (ámbito `Application`, solo en diseño), nunca `Styles.xaml`.

## Por qué importa aquí

Los 16 modales del proyecto llevan en su elemento raíz:

```xml
MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border},
                   Converter={StaticResource RestarMargen}, ConverterParameter=48}"
```

`RestarMargen` vive en `App.xaml`. Por eso **ninguno** se previsualizaba, y por eso el intento de arreglarlo redeclarando la clave en el `UserControl.Resources` del modal no podía funcionar nunca.

## Corolario 2: `RelativeSource AncestorType` también se escapa del control

Más peligroso que el anterior, porque **no lanza nada**.

En el diseñador, el `UserControl` no es la raíz de nada: cuelga del árbol visual **de Visual Studio**, que tiene sus propios `Border`, `Grid` y demás. Un `RelativeSource={RelativeSource AncestorType=Border}` atraviesa la frontera del control y engancha un elemento de la infraestructura del IDE.

Las consecuencias, en orden:

1. El binding **resuelve**. No falla. Por lo tanto **`FallbackValue` nunca se activa** — que es justo lo contrario de lo que uno asume al ponerlo «por las dudas».
2. En el primer `MeasurePass` ese elemento del IDE todavía tiene `ActualWidth = 0`.
3. Si hay un converter que resta (`0 - 48`, acotado a 0 con `Math.Max`), el resultado es `0`.
4. `MaxWidth = 0` → la regla de layout `min(max(MinWidth, Width), MaxWidth)` fuerza el ancho a 0. **El control colapsa a 0×0 y no se recupera.**

Verificado el 2026-09-09 con el mismo binario y una `Application` vacía:

```
sin ancestro Border  -> MaxWidth=880   880x620
con ancestro Border  -> MaxWidth=0       0x0
idem, 5 ciclos       -> MaxWidth=0       0x0
```

> [!tip] Regla
> Un `UserControl` no debe calcular su propio tamaño mirando hacia arriba. El tamaño lo decide **quien lo hospeda**, por referencia directa (`Source = MiOverlay`, o `ElementName` si están en el mismo XAML). Además de arreglar el diseñador, es el contrato de layout natural de WPF.

> [!warning] Al reproducirlo, cuidado con el arnés
> La condición a reproducir **no** es «sin ancestro `Border`» — eso da falso verde, porque ahí sí entra el `FallbackValue`. Es «**con** ancestro todavía sin medir».

## El diseñador no ejecuta el code-behind del documento raíz

Comprobado el 2026-09-09: con el control ya renderizando en el lienzo, todo lo que asigna el constructor (textos, `ItemsSource`) sale **vacío**. El diseñador arma el árbol parseando el XAML y no corre el code-behind del documento que se está editando.

Corolario práctico: **sembrar datos de muestra en el constructor no sirve** para la vista previa. Para que una tabla muestre filas en el lienzo hay que hacerlo declarativo (`ItemsSource` bindeado más datos de diseño), no imperativo.

## La excepción real (para reconocerla la próxima vez)

```
XamlParseException: 'Se produjo una excepción al proporcionar un valor en
'System.Windows.StaticResourceExtension'.' (número de línea: '14'; posición de línea: '14').
  -> Exception: No se puede encontrar el recurso con el nombre 'RestarMargen'.
     Los nombres de recursos distinguen mayúsculas de minúsculas.
  en System.Windows.StaticResourceExtension.ProvideValueInternal(IServiceProvider, Boolean)
```

`XamlParseException.LineNumber` / `LinePosition` apuntan al `.xaml` **fuente**, así que dan el atributo exacto. Salida de VS solo muestra el `TaskCanceledException` del subrogado: para ver esto hay que instanciar el control a mano.

## Ejemplo / workaround

Exponer el converter como instancia compartida y referenciarlo con `{x:Static}` — sin diccionario de recursos de por medio:

```csharp
public sealed class RestarMargenConverter : IValueConverter
{
    public static readonly RestarMargenConverter Instancia = new();
    // …
}
```

```xml
xmlns:conv="clr-namespace:CapaUI.Converters"
...
MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border},
                   Converter={x:Static conv:RestarMargenConverter.Instancia},
                   ConverterParameter=48, FallbackValue=880}"
```

El `FallbackValue` es lo que hace que en el lienzo el modal se dibuje a su tamaño real: en el diseñador no hay `Border` ancestro, el `Binding` no produce valor y `MaxWidth` cae al fallback.

### Cómo diagnosticar esto sin abrir Visual Studio

```csharp
[STAThread]
static void Main()
{
    var app = new Application();          // Resources vacío = condición del diseñador
    try   { new MiUserControl(); }        // el ctor sin parámetros, como el subrogado
    catch (Exception ex) { /* volcar ex y sus InnerException */ }
}
```

Es más rápido y más preciso que el log del diseñador, y da el número de línea.

---

## Relaciones

- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] — la decisión y las alternativas descartadas
- [[Anatomia compartida de los modales]] — cómo se aplica a los modales del proyecto
- [[Deuda Técnica - Pendientes]] — P-057, los 15 modales que faltan migrar
- [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]]
- [[WPF - DPI Awareness y Escalado Multi-Resolución]]
