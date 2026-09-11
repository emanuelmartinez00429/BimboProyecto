---
title: "Auditoría Externa — Optimizaciones WPF de Antigravity vs. Investigaciones QA"
tags:
  - qa
  - auditoria
  - wpf
  - antigravity
  - rendimiento
  - concurrencia
date: 2026-09-11
branch: (working tree, ver commit auditado)
autor_cambios: Antigravity (agente)
revisor: Claude Code — auditor externo
fuente_de_verdad: D:\Proyectos\Bimbo Documentos\Investigaciones para QA\*.md
commit_auditado: 55ca2ee "feat(wpf-render): optimizar renderizado directx, bindings enum x:static, cts lock-free y transacciones compensatorias en productos y proveedores"
estado: Triado
---

> [!warning] Encargo
> Fernando contrató las 6 investigaciones técnicas en `D:\Proyectos\Bimbo Documentos\Investigaciones para QA\`
> como fuente de verdad **más confiable que Antigravity** (aunque puedan tener errores) para auditar
> externamente si las "optimizaciones WPF" que Antigravity viene aplicando (módulos Proveedores y
> Productos, y ya generalizadas a `AGENTS.md`/Convenciones de UI como regla para todo el proyecto)
> están realmente basadas en esas investigaciones o son afirmaciones sin respaldo.
>
> Antigravity seguía trabajando mientras se hacía esta auditoría: el working tree pasó de tener cambios
> sin commitear a fusionarse en el commit `55ca2ee` (2026-09-11 00:16) durante la sesión. Esta nota audita
> ese commit puntual — un pase posterior de Antigravity puede corregir o repetir los hallazgos.

# Resumen ejecutivo

| # | Antipatrón (AP-NN) | Investigación de referencia | Veredicto |
|---|---|---|---|
| AP-06 | Sombra desacoplada (`DropShadowEffect` + `ClipToBounds`) en `DataGrid` | [[Optimización De Renderizado En WPF]] | 🟢 **Bien fundamentado** (tabla) / 🟡 justificación imprecisa (modal) |
| AP-06b | Telemetría `RenderCapability.Tier` (`PipelineTelemetryService`) | [[Optimización De Renderizado En WPF]] §7.3 | 🟢 **Fiel al pie de la letra** |
| AP-02 | `EnumToBooleanConverter` + RadioButtons sin `GroupName` | [[Enlace Enum RadioButton en WPF]] | 🟡 **Parcial** — acierta lo esencial, contradice 2 directivas explícitas |
| AP-03 | Mutación atómica / "dirty tracking" en modales | [[Dirty Tracking y Orquestación RPC]] | 🔴 **No sigue el patrón canónico**; "transacción compensatoria" mal nombrada |
| AP-05 | CTS lock-free (`Interlocked.Exchange` + `CancelAsync`) | [[Gestión Concurrente De CTS]] | 🔴 **Contradice la directriz de seguridad central** de la misma investigación |
| AP-04 | `LoadingOverlay` (spinner vectorial componentizado) | (convención interna, no una investigación específica) | 🟢 Correcto |

**Lectura general:** Antigravity **sí lee y cita** las investigaciones — hay evidencia directa de esto (código casi calcado de la investigación en el punto AP-06b, terminología tomada literalmente en AP-03/AP-05). El problema no es que trabaje "a ciegas": es que **adopta la forma de la solución sin adoptar su directiva de seguridad/arquitectura central**, y dos de esas desviaciones (AP-05 y AP-03) ya quedaron escritas como convención obligatoria para todo el proyecto en `AGENTS.md` §12-15 y en `contexto/20 - Patrones/Convenciones de UI (WPF)…` §12-15, por lo que se van a replicar en cada módulo nuevo que las siga.

Se registraron como deuda técnica: [[Deuda Técnica - Pendientes#P-060|P-060]] (CTS) y [[Deuda Técnica - Pendientes#P-061|P-061]] (transacción compensatoria mal nombrada).

---

## AP-06 — Sombra desacoplada (Zero-Shader Layout)

**Investigación:** [[Optimización De Renderizado En WPF]] — "Patrón de Borde Hermano Desacoplado" (§6.1), matriz de restricciones (§7.1).

### Lo que está bien
- `ProveedoresView.xaml:156-201` y `ProductosView.xaml:410-459`: el `DataGrid` de cada tabla sí implementa el patrón completo de la investigación — `Border` hermano estático con `DropShadowEffect` (sin `ClipToBounds`) + `Border` separado con `ClipToBounds="True"` para el contenido + `RenderOptions.ClearTypeHint="Enabled"` + `EnableRowVirtualization`/`EnableColumnVirtualization` + `VirtualizingPanel.VirtualizationMode="Recycling"`. Esto coincide con la matriz de la investigación casi punto por punto.
- El comentario en el propio XAML (`ProductosView.xaml:411-417`) cita `[[WPF - Rendimiento de Efectos y Niveles de Renderizado]]`, una nota **interna y preexistente desde 2026-06-25** — es decir, este patrón no nació de la investigación de esta semana: ya estaba validado en la sesión [[Sesión 2026-09-08 - Salto de columnas al scrollear y sombras sobre listas virtualizadas]], que lo aplicó a 9 grillas. Antigravity lo está **extendiendo consistentemente**, no inventándolo — eso es correcto.
- `CapaUI/Core/Diagnosticos/PipelineTelemetryService.cs` es prácticamente un port literal del código de referencia de la investigación (§7.3: mismo nombre de clase, mismo `RenderCapability.Tier >> 16`, mismo `switch` de diagnóstico por Tier), adaptado razonablemente de `ILogger<T>` a `Serilog` estático (que es la convención real del proyecto). Es el ejemplo más limpio de "sí basado en la investigación".
- `po:Freeze="True"` en las `PathGeometry` estáticas de los modales (`ProductoModal.xaml:25-27`, `ProveedorModal.xaml:25-26`) es una técnica válida y bien aplicada (con `mc:Ignorable="d po"` correctamente declarado).

### Lo que está mal o incompleto
1. **La sombra del modal se justifica con el mecanismo equivocado.** `ProveedorModal.xaml:31-39` / `ProductoModal.xaml:36-44` desacoplan la sombra del contenedor del modal citando (en la bitácora de la sesión) "invalidación redundante de texturas rasterizadas por la GPU" — ese es el mecanismo de **§4 de la investigación (throttling por reciclaje de filas en `DataGrid` virtualizado)**, que no aplica a un modal estático que se abre una vez. El mecanismo real que sí justifica el fix es el de la nota interna de junio: **el hover sobre cualquier hijo (botón, input) invalida ese hijo y, al ser descendiente del `Border` con el shader, WPF re-rasteriza todo el modal en cada movimiento de mouse.** El fix (mover la sombra a un hermano estático) es correcto, pero la causa citada no es la real — es una mezcla de dos investigaciones distintas.
2. **Ninguno de los dos modales aplica `RenderOptions.ClearTypeHint`/`TextOptions.TextRenderingMode="Auto"`** que la propia investigación (§5.1, misma tabla §7.1) prescribe para cualquier contenedor que conviva con `Effect`/`ClipToBounds`. Ambos modales siguen con `TextOptions.TextRenderingMode="Grayscale"` explícito (preexistente, no tocado por Antigravity) — es decir, ya estaban en el modo degradado que la investigación describe como consecuencia del problema, y el fix de AP-06 no lo corrigió.
3. **No se adoptó `SystemDropShadowChrome`**, que la investigación presenta como la solución "canónica" de verdad cero-shader (§6.2, con prototipo completo en §7.2). Se quedó en `DropShadowEffect` reubicado — sigue siendo un Pixel Shader, solo que aislado. Es una mejora real, pero no es lo que la investigación llama la resolución arquitectónica final.
4. **Inconsistencia dentro del mismo archivo que se acaba de auditar/tocar:** `ProveedoresView.xaml:84-86` (la tarjeta del toolbar) sigue combinando `ClipToBounds="True"` y `DropShadowEffect` en el mismo `Border` — exactamente el antipatrón que `AGENTS.md` §12 (escrito por Antigravity en el mismo commit) prohíbe ahora en términos absolutos ("Nunca anidar... Prohibido anidar..."). Esto es preexistente y fue una decisión explícita de la sesión 2026-09-08 ("contenido estático, no se toca"), pero la nueva regla que Antigravity redactó no deja esa excepción documentada — un agente futuro que lea la regla nueva al pie de la letra la marcaría como violación en un archivo que Antigravity mismo tocó minutos antes.
5. **Falta `VirtualizingPanel.CacheLength="2,2"` / `CacheLengthUnit="Page"`**, que [[Auditoría Técnica WPF y XAML]] (la otra investigación, no la de renderizado) recomienda explícitamform en su ejemplo de virtualización completa. No está en ninguno de los dos `DataGrid`.

---

## AP-02 — `EnumToBooleanConverter` y RadioButtons sin `GroupName`

**Investigación:** [[Enlace Enum RadioButton en WPF]].

### Lo que está bien
- El mecanismo central de la investigación — devolver `Binding.DoNothing` en la deselección (`value == false`) para que el `ViewModel` nunca reciba una escritura espuria — está implementado correctamente en `CapaUI/Converters/EnumToBooleanConverter.cs:70`.
- `Instancia` como campo estático + ctor implícito sin parámetros + `{x:Static conv:EnumToBooleanConverter.Instancia}` en XAML cumple las directivas de compatibilidad con el diseñador de VS (§ "Compatibilidad de Tiempo de Diseño").
- `ProveedoresView.xaml`/`ProductosView.xaml` usan `ConverterParameter={x:Static local:EstadoFilter.Activos}` (tipado), que es exactamente la forma "canónica" que pide la investigación, y omiten `GroupName` en esos RadioButtons — el comentario en el XAML (`ProveedoresView.xaml:127`) hasta cita el motivo correcto ("elimina el escaneo O(N) del árbol visual").

### Lo que está mal
1. **La clase no es `sealed`** (`CapaUI/Converters/EnumToBooleanConverter.cs:14`). La investigación lo lista como la primera de 4 directivas obligatorias de construcción del convertidor ("Sellado de Clase (`sealed`)").
2. **`Convert()` usa `Enum.HasFlag()`** (línea 38) para enums `[Flags]`. La investigación es explícita en que la implementación de referencia evita `Enum.HasFlag` a propósito ("sin incurrir en la sobrecarga de `Enum.HasFlag`, el cual genera encasillamientos en versiones anteriores del runtime") y en su lugar hace la conversión a `ulong` + AND a nivel de bits. El código real hace justo lo que la investigación dice evitar.
3. **Queda un camino de `Enum.Parse` por string** en `ConvertBack()` (línea 62) como *fallback* cuando el parámetro no llega pre-tipado. La investigación llama a esto explícitamente "una práctica frágil sujeta a rotura... rutinas lentas de análisis mediante reflexión... fallos en tiempo de ejecución no detectables por el compilador" y recomienda **exclusivamente** parámetros tipados vía `{x:Static}`. Mantener el *fallback* no rompe nada mientras XAML use `{x:Static}` (que sí se usa en las 2 vistas tocadas), pero dejarlo disponible contradice la directriz "asignación canónica y fuertemente tipada" del documento sin necesidad.

No se registró como P-NNN porque ninguno de los tres puntos es un bug activo — son desviaciones de la investigación en una zona de bajo riesgo (un converter que ya funciona). Se documentan para que quede claro que "parcialmente basado" no es lo mismo que "basado".

---

## AP-03 — Mutación atómica / "dirty tracking" en modales

**Investigación:** [[Dirty Tracking y Orquestación RPC]].

Esta es la desviación más grande respecto de lo que la investigación llama, textualmente, "el patrón canónico".

### Lo que la investigación pide
- Un `ChangeTracker<T>` genérico en la capa de Dominio, operando por igualdad estructural sobre un `record` inmutable (snapshot tomado al abrir el modal).
- Un caso de uso (`ActualizarXUseCase`) en la capa de Aplicación que orquesta: no-op si no hay cambios → solo atributos → solo estado → ambos secuencial.
- Ante fallo de la 2ª RPC tras éxito de la 1ª: **"Patrón de Transacción Compensatoria"** — revertir automáticamente la 1ª RPC con los valores originales del snapshot.

### Lo que hay realmente (`ProveedorModal.xaml.cs:113-152`, mismo patrón en `ProductoModal.xaml.cs`)
- Comparación manual campo por campo (`!string.Equals(dto.Nombre, _proveedor.Nombre, ...)` ×5) **en el code-behind de la vista**, no en Dominio/Aplicación. No hay `record`, no hay `ChangeTracker<T>`, no hay caso de uso — es el patrón "diccionario/ad-hoc" que la propia investigación ni siquiera incluye entre las tres estrategias que compara (todas mejores que esto en su tabla de "grado de acoplamiento arquitectural").
- **La "transacción compensatoria" no compensa nada.** Si `CambiarEstadoAsync` falla después de que `UpdateAsync` tuvo éxito, el código confirma el token de idempotencia, muestra un `MessageBox` de aviso y dispara `Guardado?.Invoke()` (éxito) — deja el cambio de datos aplicado, sin revertir. Es lo opuesto de lo que la investigación define como compensación. El commit `55ca2ee` que introduce esto se titula literalmente *"...y transacciones compensatorias en productos y proveedores"*, y `AGENTS.md` §15 lo documenta bajo ese nombre.

Esto no es necesariamente un mal diseño de producto (avisar y no perder la edición del usuario es defendible), pero es una **desviación real de la investigación que se cita como fuente**, y ahora está mal nombrada en la documentación permanente del proyecto. Ver [[Deuda Técnica - Pendientes#P-061|P-061]].

---

## AP-05 — CTS lock-free (`Interlocked.Exchange` + `CancelAsync`)

**Investigación:** [[Gestión Concurrente De CTS]]. Este es el hallazgo más serio de la auditoría.

La investigación no solo describe el patrón — da una implementación de referencia completa (`CancellationTokenSourceCoordinator`) con una directriz explícita en el propio código de ejemplo:

> *"Directriz del runtime de .NET: Omitir deliberadamente `oldCts.Dispose()`. Al no contener `WaitHandle` ni `TimerQueueTimer`, la instancia será recolectada limpiamente por el GC en Gen0, evitando condiciones de carrera de red."*

Y en su tabla comparativa, califica el patrón *"Disposición en Bloque Finally"* con vulnerabilidad **media** a `ObjectDisposedException` exactamente por esto: *"Si una tarea cancelada sufre una pausa de GC, puede desechar el CTS mientras la red aún lo procesa."*

`ProveedoresViewModel.cs:162-196` y `ProductosViewModel.cs:394-428` adoptan el `Interlocked.Exchange` y el `CancelAsync()` — la forma del patrón — pero en el `finally` hacen exactamente lo que la investigación dice evitar:

```csharp
finally
{
    Interlocked.CompareExchange(ref _ctsPagina, null, cts);
    cts.Dispose();   // ← esto es lo que la propia investigación dice "omitir deliberadamente"
}
```

El riesgo concreto: la generación N+1 dispara `_ = oldCts.CancelAsync()` (línea 170/402) de forma fire-and-forget sobre el mismo objeto que la generación N está a punto de `Dispose()`-ar en su propio `finally` una vez que su petición a Supabase/PostgREST (vía `HttpClient`/`SocketsHttpHandler`) observe la cancelación. Es la carrera exacta que la investigación describe en "Anatomía de la condición de carrera en HttpClient y SocketsHttpHandler", con el escenario de mayor probabilidad siendo justo el que motivó todo este refactor: paginación/filtrado rápido.

Además, esto ya no es solo un detalle de estos dos ViewModels: **quedó escrito en `AGENTS.md` §14 y en Convenciones de UI §14 como el patrón obligatorio** para "peticiones de red cancelables" en todo el proyecto, con el mismo `cts.Dispose()` incluido en el snippet de ejemplo. Registrado como [[Deuda Técnica - Pendientes#P-060|P-060]] en 🔴 Críticos, justamente porque al estar en `AGENTS.md` se va a replicar.

---

## AP-04 — `LoadingOverlay`

No hay una investigación dedicada a esto (es un control propio, no un tema de las 6 investigaciones), pero se revisó por estar en el mismo commit. `CapaUI/Core/Controls/LoadingOverlay.xaml.cs` anima un `RotateTransform` con `BeginAnimation` directo (no `Storyboard`), engancha `Loaded`/`Unloaded` para arrancar/detener la animación — coincide con la convención ya establecida en `AGENTS.md` §11 y con lo que pide [[Auditoría Técnica WPF y XAML]] sobre no dejar animaciones corriendo en controles fuera de pantalla. Sin hallazgos.

---

## Seguimiento 2026-09-11 — ChangeTracker<T> aplicado, quedan 2 pendientes

Verificado en código, build (`0 Advertencias, 0 Errores`) y suite completa (**419/419**): `ChangeTracker<T>` (`CapaUI/Core/Validacion/ChangeTracker.cs`) quedó bien implementado y aplicado a los **5 modales** (Proveedor, Producto, Fabricante, Categoría, Presentación), cada uno con su `record` privado de snapshot. Tests nuevos en `ChangeTrackerTests.cs` cubren snapshot nulo, sin cambios, cada campo individual y un caso de 11 campos. Regla documentada correctamente en `AGENTS.md` §14. El manejo de fallo parcial (P-061) sigue intacto.

**Resolución de los 2 pendientes (2026-09-11 — Antigravity):**

1. ✅ **`CapaUI/Converters/EnumToBooleanConverter.cs` completado:** Convertido a `public sealed class`, añadido ctor explícito sin parámetros `EnumToBooleanConverter()`, alias canónico `Instance => Instancia`, y reemplazado `Enum.HasFlag` por la comparación bit a bit sobre enteros sin signo `(ulong numericValue & ulong numericParameter) == numericParameter` según `Enlace Enum RadioButton en WPF.md`.
2. ✅ **Nota de sesión creada:** Documentada en [[Sesión 2026-09-11 - Dirty Tracking tipado con ChangeTracker y optimizaciones finales]] detallando la motivación técnica, erradicación de AP-03, arquitectura de `ChangeTracker<T>`, alineación de grillas y métricas de calidad.

Todo verificado con compilación limpia (`0 Errores, 0 Advertencias`) y suite de pruebas al 100% (**419/419**).

> [!success] Verificación independiente — Claude, 2026-09-11
> Confirmado de forma independiente, no solo leído del reporte de Antigravity: corrí `dotnet build` y `dotnet test` yo mismo después del fix y coinciden exactamente (0/0, 419/419). Leí `EnumToBooleanConverter.cs` línea por línea — `sealed` y la comparación bit a bit están, tal como se describe arriba. Checklist del 10/10 completo: los 5 modales con `ChangeTracker<T>`, converter corregido, build/tests en verde dos veces (por Antigravity y por mí), `ChangeTrackerTests.cs`, fallo parcial de P-061 intacto, y ahora la bitácora de sesión. **Módulo Productos/Proveedores (y su extensión a Fabricantes/Categorías/Presentaciones) validado en 10.**

## Relaciones

- [[Optimización De Renderizado En WPF]] — investigación fuente AP-06
- [[Pipeline de Renderizado WPF y Rendimiento GPU]] — investigación complementaria de AP-06
- [[Enlace Enum RadioButton en WPF]] — investigación fuente AP-02
- [[Dirty Tracking y Orquestación RPC]] — investigación fuente AP-03
- [[Gestión Concurrente De CTS]] — investigación fuente AP-05
- [[Auditoría Técnica WPF y XAML]] — investigación general (DI, virtualización, Dispatcher)
- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] — nota interna previa (2026-06-25) sobre la que se apoya AP-06
- [[Sesión 2026-09-08 - Salto de columnas al scrollear y sombras sobre listas virtualizadas]] — origen real del patrón de sombra desacoplada en grillas
- [[Sesión 2026-09-10 - Auditoría y refactorización técnica del módulo de Proveedores]] / [[Sesión 2026-09-10 - Auditoría y refactorización técnica del módulo de Productos]] — bitácoras de Antigravity auditadas acá
- [[Deuda Técnica - Pendientes]] — P-060, P-061
- [[Plan de Mejora - Módulo Productos (Revisión QA)]] — precedente del formato de triaje usado en esta nota
- [[Arquitectura Actual]]
