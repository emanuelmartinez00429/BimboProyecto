---
title: "WPF — Escalar Popups y ToolTips que no heredan el LayoutTransform"
tags:
  - wpf
  - escalado
  - popup
  - freezable
  - referencia
date: 2026-09-20
---

# WPF — Escalar Popups y ToolTips que no heredan el `LayoutTransform`

> [!abstract] Pregunta que resuelve
> Con un `ScaleTransform` en el `LayoutTransform` del contenido de la ventana, los `Popup`,
> `ToolTip` y desplegables de `ComboBox` quedan a 1.0 porque viven en otro HWND. ¿Se puede
> escalarlos con `{DynamicResource}` sobre `ScaleX`/`ScaleY` dentro de un `Setter.Value`,
> o el `Freezable` del setter se congela y rompe la referencia dinámica?

**Respuesta corta: se puede, y además sigue los cambios en vivo.** Verificado el 2026-09-20 con
un arnés WPF en .NET 10 (no es lectura de documentación: son valores medidos).

---

## El temor y por qué no se cumple

La preocupación era razonable: WPF **sella** el `Style` la primera vez que se aplica, y un
`Freezable` congelado no admite referencias dinámicas — `DynamicResource` necesita poder
reescribir la propiedad cuando el recurso cambia.

Medido: el `ScaleTransform` que sale de un `Setter.Value` **no queda congelado**
(`IsFrozen = False`), la referencia se resuelve, y al cambiar el recurso el valor se actualiza.

| Forma | Aplica al inicio | Sigue el cambio en vivo |
|:--|:--|:--|
| **A.** `{DynamicResource}` en `ScaleX`/`ScaleY` dentro de `Setter.Value` | ✅ 0.8 | ✅ pasa a 1.1 |
| **B.** Igual que A con `x:Shared="False"` | no evaluable fuera de un diccionario compilado | — |
| **C.** `{Binding Source={x:Static Application.Current}, Path=Resources[Clave]}` | ✅ 0.8 | ❌ se queda en 0.8 |

**A es la forma correcta.** C aplica pero no notifica: `ResourceDictionary` no implementa
`INotifyPropertyChanged` sobre su indizador, así que el binding nunca se entera de un cambio
posterior. B no se pudo evaluar con `XamlReader.Parse` (`x:Shared` solo vale en diccionarios
compilados), y como A funciona, no hace falta.

```xml
<!-- Dentro del ControlTemplate del ComboBox, en Styles.xaml -->
<Border x:Name="RaizDelPopup">
  <Border.LayoutTransform>
    <ScaleTransform ScaleX="{DynamicResource EscalaApp}"
                    ScaleY="{DynamicResource EscalaApp}"/>
  </Border.LayoutTransform>
  ...
</Border>
```

---

## Lo que sí se confirmó del `Popup`

| Medición | Resultado |
|:--|:--|
| ¿Hereda el `LayoutTransform` del contenedor? | **No.** Contenido declarado en 120 px → `ActualWidth = 120` con el contenedor a 0,8× |
| ¿Ve los recursos de la aplicación? | **Sí.** `TryFindResource("EscalaApp")` devuelve el valor |

La segunda fila es la que habilita el remedio: el popup está fuera del árbol visual para el
transform, pero **no** para la búsqueda de recursos, así que un `{DynamicResource}` declarado en
su plantilla llega igual.

---

## Dos trampas del arnés, no de WPF

Anotadas porque cualquiera que repita la medición las va a pisar:

1. **El elemento tiene que estar dentro de una `Window` mostrada.** Con un `Grid` suelto medido con
   `Measure`/`Arrange`, el valor inicial se resuelve pero el cambio posterior **no** se propaga:
   sin `PresentationSource` no hay a quién notificar. La primera corrida dio "no sigue el cambio en
   vivo" por esto y era mentira.
2. **`ShutdownMode = OnExplicitShutdown`.** Cerrar la ventana de un caso de prueba apaga la
   aplicación entera con el modo por defecto, y los casos siguientes miden basura — un popup que
   reporta `ActualWidth = 0` y no encuentra ningún recurso.

Bombear el `Dispatcher` (`PushFrame` con un `DispatcherFrame`) sí hace falta en un arnés de consola,
pero no era lo que faltaba acá.

---

## Consecuencia para el proyecto

- La **Fase 6** usa la forma A: un solo setter en el `PART_Popup` del `ControlTemplate` del
  `ComboBox` en `Styles.xaml` cubre los 21 `ComboBox` de la app.
- El **plan B** que estaba previsto —recorrer los popups abiertos por código desde
  `EscalaService`— **no hace falta**.
- Como A sigue los cambios en vivo, la **Fase 9** hereda el comportamiento sin trabajo extra: al
  reescribir `Application.Current.Resources["EscalaApp"]` los popups se reacomodan solos.

---

## Relaciones

- [[Inventario — Superficie de escalado propio (LayoutTransform global)]] — los 5 popups y los 21 `ComboBox`
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]] — regla 11 sobre `Freezable` y recursos dinámicos
- [[WPF - DPI Awareness y Escalado Multi-Resolución]]
- [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]]
