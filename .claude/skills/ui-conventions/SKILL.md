---
name: ui-conventions
description: Convenciones de UI/WPF del Proyecto Bimbo — leer y aplicar antes de crear o editar cualquier .xaml (UserControl, Window, ResourceDictionary), y verificar con el arné de instanciación
tags: [wpf, xaml, ui, convenciones, disenador, verificacion]
---

# Skill: ui-conventions

## Descripción

Recetas obligatorias para tocar la capa visual (`CapaUI/**/*.xaml` y su code-behind). Encapsula lo que se aprendió cerrando P-057 (previsualización en el diseñador de Visual Studio) y las buenas prácticas de WPF verificadas contra documentación oficial y fuentes de la comunidad.

**Fuente de verdad completa:** [`contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md`](../../contexto/20%20-%20Patrones/Convenciones%20de%20UI%20(WPF)%20%E2%80%94%20leer%20antes%20de%20tocar%20XAML.md). Esta skill es el resumen accionable; ante cualquier duda, ese nodo manda.

---

## Cuándo usar esta skill

Automáticamente, antes de:
- Crear un `UserControl`, `Window`, `Page` o `ResourceDictionary`.
- Editar cualquier `.xaml` o su `.xaml.cs` en `CapaUI/`.
- Agregar un `IValueConverter`, un `Style`, un binding de tamaño, un overlay de modal.
- Diagnosticar "no se ve en el diseñador" / lienzo en blanco / `TaskCanceledException`.

Palabras clave: *modal, XAML, UserControl, diseñador, designer, previsualización, converter, Style, DataGrid, overlay, MaxWidth, RelativeSource, ResourceDictionary, Styles.xaml, lienzo en blanco*.

---

## Checklist antes de escribir XAML

1. **¿El control se instancia solo?**
   - Constructor sin parámetros (servicios en `null!`, solo `InitializeComponent()`).
   - Sin `App.Services.GetRequiredService<T>()` en el `.ctor` (regla 9 de `AGENTS.md`). Dependencias por el ctor real.
   - `mc:Ignorable="d"` + `d:DesignWidth`/`d:DesignHeight`.
   - `Loaded` que toque `App.Services` → detrás de `if (DesignerProperties.GetIsInDesignMode(this)) return;`.

2. **¿Usa recursos de `Styles.xaml`?** Mergealo en `<UserControl.Resources>`, forma corta:
   `<ResourceDictionary Source="/CapaUI;component/Resources/Styles.xaml"/>`. No copies estilos que ya existen ahí.

3. **¿Necesita un converter?** `{x:Static conv:MiConverter.Instancia}` con `xmlns:conv="clr-namespace:CapaUI.Converters"`.
   **Nunca** `{StaticResource X}` a una clave de `App.xaml`. Si el converter no tiene `public static readonly MiConverter Instancia = new();`, agregáselo.

4. **¿Necesita limitar su tamaño al contenedor?** El **host** lo hace:
   `CapaUI.Core.ModalLayout.LimitarAlOverlay(modal, ModalOverlay);` al mostrarlo.
   **Nunca** `MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border}, ...}"` en el raíz.

5. **¿Colores de empresa?** `{DynamicResource EmpresaPrimary*}` — ya resueltos por `App.xaml` (runtime) y `DesignTimeResources.xaml` (diseño). **Nunca** los muevas a `Styles.xaml`.

6. **¿El requisito vino en términos de otro framework?** (WinForms `DisplayedCells`, CSS `flex`, Android). Traducir al idioma WPF nativo y validado, nunca literal. Verificar la API si el enum/propiedad no existe.

---

## Verificación obligatoria al terminar

1. `dotnet build BimboProyecto.sln` → **0 errores, 0 advertencias**.
   Si `bin` está bloqueado (app o VS abiertos): `dotnet build CapaUI/CapaUI.csproj -p:UseAppHost=false` y revisar que no haya errores `MC####` / `CS####` (los `MSB3021/3026/3027` son solo el copy a `bin`, se ignoran).
2. **Arné de instanciación** — reproduce la condición del diseñador sin abrir VS:
   - Crear un proyecto `net8.0-windows` + `<UseWPF>true</UseWPF>` en el scratchpad, referencia a `CapaUI` (o `HintPath` a `CapaUI/obj/Debug/net8.0-windows/CapaUI.dll` si hay lock).
   - `new Application();` (Resources vacío) → por cada control: `Activator.CreateInstance` por el ctor sin parámetros → `Measure(new Size(1400,900))` → `Arrange(...)` → comprobar `ActualWidth/Height >= 5` y que no tiró excepción.
   - Plantilla completa en el nodo de convenciones, §7.
   - Correr, leer el reporte (`OK` / `COLAPSA` / `EXCEPCION`), **borrar el proyecto del scratchpad**.
   - **Punto ciego:** `{StaticResource}` dentro de `ControlTemplate`/`DataTemplate`/`DataTrigger` diferido no se evalúa. Complementar con `grep -rn "StaticResource <clave>" --include=*.xaml`.
3. **Prueba visual manual** del flujo tocado — el arné no cubre bindings con datos reales ni comportamiento.

---

## Antipatrones (rechazar en revisión)

| ❌ | ✅ |
|---|---|
| `UserControl` sin ctor sin parámetros | ctor de diseño con servicios en `null!` |
| DI en el `.ctor` | inyección por el ctor real |
| `GetIsInDesignMode` como guard del ctor con parámetros | ctor sin parámetros separado |
| `MaxWidth`/`MaxHeight` del raíz con `AncestorType` | `ModalLayout.LimitarAlOverlay` en el host |
| `{StaticResource}` a converter de `App.xaml` | `{x:Static conv:XConverter.Instancia}` |
| `<Style>` copiado de `Styles.xaml` | mergear y usar el global |
| `Empresa*` en `Styles.xaml` | solo `App.xaml` + `DesignTimeResources.xaml` |
| literal de otro framework en XAML | equivalente WPF verificado |

---

## Cómo se amplía

Cuando aparezca una regla o gotcha nuevo de UI:
1. Agregarlo al nodo `contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md` con su evidencia (link a sesión / ADR / referencia).
2. Si es accionable en cada cambio, sumarlo al checklist de esta skill.
3. Commitear ambos junto al código que lo motivó.
