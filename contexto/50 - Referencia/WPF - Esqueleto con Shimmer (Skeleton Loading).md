---
title: "WPF — Esqueleto con Shimmer (Skeleton Loading)"
tags:
  - referencia
  - wpf
  - animacion
  - rendimiento
  - ux
  - skeleton
  - shimmer
date: 2026-08-11
lifecycle: draft
---

# WPF — Esqueleto con Shimmer (Skeleton Loading)

> [!abstract] Cómo lo llamamos
> - **Esqueleto** (*skeleton screen* / *skeleton loader*): las cajas grises que ocupan de antemano el espacio que va a tener el contenido real. Término acuñado por Luke Wroblewski (2013).
> - **Shimmer**: el brillo diagonal que barre esas cajas de izquierda a derecha en loop. Nombre popularizado por la librería `Shimmer` de Facebook (2015).
> - **Los dos juntos: "esqueleto con shimmer"** (en inglés *shimmer skeleton*). De ahora en adelante, ese es el nombre del efecto en este proyecto.
>
> **Implementado en `RolesView`** (2026-08-11): esqueleto en `RolesResources.xaml` → estilo `RolEsqueletoTarjeta`; shimmer en `RolesView.xaml` (`ShimmerTransform`) arrancado/frenado desde `RolesView.xaml.cs`. Es la primera pantalla del proyecto que lo usa.

---

## ¿Se puede en WPF?

Sí, y sale barato **si se elige bien qué propiedad animar**. La trampa es que WPF no tiene un hilo compositor independiente como CSS: el `TimeManager` tickea en el **hilo de UI** con prioridad `Render`. Entonces el costo no está en "animar" sino en **qué invalida** cada frame.

| Propiedad animada | Qué invalida | Costo |
|---|---|---|
| `Width` / `Height` / `Margin` | `AffectsMeasure` → **pasada de layout completa** por frame | 🔴 Carísimo |
| `GradientStop.Offset` | Regenera la rampa del degradado | 🟠 Medio |
| `Opacity`, `RenderTransform` | Solo re-composición | 🟢 Barato |
| **`Brush.RelativeTransform`** | Matriz del espacio del brush, sin regenerar la rampa | 🟢 **El más barato** |

> [!danger] Nunca hacer el shimmer moviendo `Margin` o `Width`
> Es la implementación "obvia" y es la peor: dispara `Measure`/`Arrange` de todo el subárbol 60 veces por segundo.

---

## Las 4 palancas de optimización (en orden de impacto)

### 1. Un solo shimmer para todo el esqueleto, no uno por caja

El esqueleto de Roles tiene 6 tarjetas × ~4 barras = **~24 cajas**. Un shimmer por caja son 24 animaciones y 24 brushes vivos. Un único `Rectangle` superpuesto sobre toda el área, con el padre en `ClipToBounds="True"`, es **1 animación**. Diferencia de ~24×.

### 2. `Timeline.DesiredFrameRate` — la palanca que casi nadie usa

Por defecto WPF apunta a ~60 fps. Un shimmer es un movimiento **lento y difuso**: a 20–25 fps se ve idéntico y el TimeManager hace un tercio del trabajo.

```xml
<DoubleAnimation Timeline.DesiredFrameRate="20" ... />
```

```csharp
Timeline.SetDesiredFrameRate(animacion, 20);
```

Es propiedad adjunta de `Timeline`, así que funciona sobre la animación individual o sobre el `Storyboard` entero.

### 3. Parar el `Storyboard` cuando el esqueleto se oculta

**WPF no detiene las animaciones de elementos `Collapsed`.** El reloj sigue tickeando e invalidando aunque no se dibuje nada. Peor: un `RepeatBehavior.Forever` sin parar mantiene viva la referencia al elemento → **fuga de memoria**.

El proyecto ya tiene ese patrón resuelto en `CategoriasView.DetenerSpinner()` — hay que replicarlo: `Stop()` + `Remove()` + `Children.Clear()` al ocultar el esqueleto y en `Unloaded`.

### 4. Cero `Effect` sobre el área animada

`DropShadowEffect` / `BlurEffect` son pixel shaders y se re-ejecutan en cada frame del shimmer. Combinarlos multiplica el costo — es exactamente el problema documentado en [[WPF - Rendimiento de Efectos y Niveles de Renderizado]]. Las tarjetas del esqueleto no deben llevar sombra.

---

---

## Duración mínima: que no parpadee

Con el catálogo cacheado, la segunda visita a Roles responde en milisegundos. El esqueleto igual se muestra, pero dura tan poco que **parpadea** en vez de leerse como una carga.

La solución es un piso de tiempo (`RolesViewModel.DuracionMinimaEsqueletoMs = 1000`): si la consulta volvió antes, se espera la diferencia. Se empareja con la duración del barrido (también 1000 ms) para que se alcance a ver un shimmer entero.

```csharp
private async Task SostenerEsqueletoAsync(long transcurridoMs)
{
    long restante = DuracionMinimaEsqueletoMs - transcurridoMs;
    if (restante <= 0) return;
    try { await Task.Delay((int)restante, _cts.Token); }
    catch (OperationCanceledException) { }
}
```

> [!warning] Esto retrasa la app a propósito
> Es un intercambio deliberado: se cambia ~1 s de velocidad real por una transición legible. La guía clásica de UX dice lo contrario (si carga en <100 ms, no mostrar loader — un parpadeo molesta más que nada).
>
> Sirve cuando el esqueleto es parte de la identidad visual del producto. Si en algún momento la prioridad pasa a ser velocidad pura, **bajar la constante a 0 alcanza** para desactivarlo, sin tocar nada más.

**El `Task.Delay` debe respetar el `CancellationToken`.** Si no, al salir de la pantalla queda una espera colgada que despierta sobre un ViewModel ya descartado.

---

## Implementación recomendada

Un `Rectangle` que cubre el área, relleno con un degradado transparente → blanco → transparente, cuyo **`RelativeTransform`** se traslada de `X=-1` a `X=1`.

Usar `RelativeTransform` (espacio normalizado 0..1 del bounding box) en vez de `RenderTransform` tiene una ventaja concreta: **no hay que conocer el ancho del contenedor**, así que no hacen falta bindings a `ActualWidth` ni code-behind de medidas.

```xml
<!-- Va ENCIMA de las cajas del esqueleto, dentro de un contenedor con ClipToBounds -->
<Rectangle IsHitTestVisible="False">
    <Rectangle.Fill>
        <!-- OJO: este brush NO puede llevar po:Freeze="True" —
             un Freezable congelado no se puede animar. -->
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
            <GradientStop Offset="0.0"  Color="#00FFFFFF"/>
            <GradientStop Offset="0.5"  Color="#B3FFFFFF"/>
            <GradientStop Offset="1.0"  Color="#00FFFFFF"/>
            <LinearGradientBrush.RelativeTransform>
                <TranslateTransform x:Name="ShimmerTransform" X="-1"/>
            </LinearGradientBrush.RelativeTransform>
        </LinearGradientBrush>
    </Rectangle.Fill>

    <Rectangle.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard>
                <Storyboard x:Name="ShimmerStory">
                    <DoubleAnimation
                        Storyboard.TargetName="ShimmerTransform"
                        Storyboard.TargetProperty="X"
                        From="-1" To="1" Duration="0:0:1.4"
                        RepeatBehavior="Forever"
                        Timeline.DesiredFrameRate="20"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Rectangle.Triggers>
</Rectangle>
```

**Sin easing** (`Linear`): es un loop continuo, igual que el spinner de carga — ver [[Animaciones WPF - Referencia de Easings]]. Un `EaseInOut` haría que el brillo "frene" en los bordes y se note el corte del loop.

**Duración 1.2–1.6 s.** Más rápido se siente nervioso; más lento parece que se colgó.

> [!warning] El `EventTrigger` de XAML no se puede parar desde código
> Si se usa el `EventTrigger` de arriba, el Storyboard queda fuera de alcance para `Stop()`. Para poder frenarlo (palanca 3), hay que dispararlo desde el code-behind y guardar la referencia, como hace `CategoriasView`. El XAML de arriba sirve para prototipar; **en producción va desde code-behind**.

---

## Costo real esperado

Con las 4 palancas aplicadas, el shimmer es: **1 animación de un `double`, a 20 fps, que solo cambia una matriz de brush**. No hay layout, no hay shader, no hay texturas intermedias.

Es de un orden de magnitud más barato que el `DropShadowEffect` que ya causó problemas en la PC con iGPU UHD 630. Aun así, por ser una animación **continua**, la regla es: **solo mientras el esqueleto está en pantalla** (típicamente < 1 s, y con la caché de catálogo ni siquiera aparece en visitas posteriores).

### La cuenta concreta

| | Shimmer del esqueleto | `DropShadowEffect` del caso 2026-06-25 |
|---|---|---|
| Trabajo por frame | 1 `double` interpolado + 1 matriz de brush | Render a textura intermedia + convolución de desenfoque |
| Píxeles tocados | ~400 k (relleno de degradado, 1 blend c/u) | ~400 k × decenas de muestras por píxel |
| Frames por segundo | **20** (forzado) | ~60, atado al movimiento del mouse |
| Cuánto dura | **Solo mientras carga** (~1–3 s la primera visita) | Indefinido, mientras el usuario interactúe |
| **Total por visita** | **≈ 20–60 frames, una vez** | Cientos de frames por segundo de hover |

La diferencia de fondo no es el costo por frame (que ya es ~2 órdenes de magnitud menor): es que **el shimmer tiene fin y la sombra no**.

> [!note] Pendiente de medición
> Los números de arriba son **análisis técnico, no medición**. Falta comprobarlo en la PC con UHD 630 con el método de [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] (Administrador de tareas → GPU/CPU). Recién ahí pasa a `lifecycle: verified`.
>
> Qué mirar: al entrar a Roles, el pico de CPU/GPU debe durar lo que dura el esqueleto y **volver a línea base al aparecer las tarjetas**. Si queda elevado después, el shimmer no se frenó (palanca 3 rota).

---

## Relaciones

- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] — por qué los `Effect` no se mezclan con animación
- [[Animaciones WPF - Referencia de Easings]] — timings y por qué acá va `Linear`
- [[Módulo Usuarios]] — el subsistema RBAC del que cuelga la pantalla de Roles
