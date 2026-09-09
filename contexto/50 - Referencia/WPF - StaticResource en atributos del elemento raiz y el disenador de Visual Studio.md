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
    <ResourceDictionary Source="pack://application:,,,/CapaUI;component/Resources/Styles.xaml"/>
</ResourceDictionary.MergedDictionaries>
```

Las URIs `pack://` **sí** resuelven en el subrogado — verificado. Y el costo es despreciable: **0,4 ms** parsear `Styles.xaml` (1454 líneas, 86 claves de primer nivel) y 0,1 ms `PesajeModalStyles.xaml`. Instanciar `ProductosCargaModal` entero cuesta 4–5 ms. No hace falta ningún `SharedResourceDictionary` con caché.

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

## Por qué importa aquí

Los 16 modales del proyecto llevan en su elemento raíz:

```xml
MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border},
                   Converter={StaticResource RestarMargen}, ConverterParameter=48}"
```

`RestarMargen` vive en `App.xaml`. Por eso **ninguno** se previsualizaba, y por eso el intento de arreglarlo redeclarando la clave en el `UserControl.Resources` del modal no podía funcionar nunca.

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
