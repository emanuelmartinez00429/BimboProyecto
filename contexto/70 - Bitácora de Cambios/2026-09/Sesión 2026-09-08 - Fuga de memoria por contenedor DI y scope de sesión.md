---
title: "Sesión 2026-09-08 — Fuga de memoria por el contenedor DI y scope de sesión"
tags:
  - sesion
  - rendimiento
  - memoria
  - wpf
  - di
  - auditoria
date: 2026-09-08
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Agente Antigravity (ejecución) sobre plan de Claude (investigación)
revisor: Claude (auditoría de código) — falta prueba de runtime de Fernando
---

# Sesión 2026-09-08 — Fuga de memoria por el contenedor DI y scope de sesión

> [!success] Resultado
> La app dejaba de liberar memoria al cerrar sesión y acumulaba al volver a entrar (~280 MB contra ~150 MB normales). La causa no eran los datos: era que **el contenedor de DI retiene toda instancia `Transient` que implemente `IDisposable`**, y `App.Services` es un proveedor raíz estático que nunca se dispone. Se introdujo un **scope por sesión** y las 14 vistas pasaron a crear su ViewModel con `App.CrearVm<T>()`.

---

## 1. El síntoma que Fernando reportó

Una sesión larga: ~120 pesajes creados, **128 notificaciones seguidas**. Al cerrar sesión la RAM **nunca bajó**, y al volver a entrar subió lo normal pero **encima de lo que ya había**.

Ese detalle —que no baje al cerrar sesión— es el que descarta las explicaciones fáciles. Un caché grande baja cuando se limpia; una lista larga baja cuando se destruye la vista. Algo estaba **enraizado en un objeto que sobrevive al logout**.

---

## 2. Lo que NO era

Se midió antes de teorizar:

| Hipótesis | Medición | Veredicto |
|---|---|---|
| Volumen de notificaciones en memoria | **330 filas = 192 kB** en la base | Descartado — tres órdenes de magnitud por debajo |
| Los 120 pesajes | ~25 kB | Descartado |
| Caché de catálogos | acotado y con TTL | Descartado |

---

## 3. La causa raíz — y por qué no es obvia

`App.Services` es un `IServiceProvider` **estático** creado una sola vez y **nunca dispuesto** (0 llamadas a `Dispose()` sobre él en todo el repo).

`Microsoft.Extensions.DependencyInjection` **rastrea toda instancia que implemente `IDisposable`** en la lista de descartables del scope que la resolvió, para poder liberarla cuando ese scope muera. Sobre el proveedor raíz eso significa: **para siempre**.

De los 20 `AddTransient` registrados, **15 implementan `IDisposable`** (casi todos por heredar de `RealtimeAwareViewModel`). Y cada vista resolvía el suyo del raíz en cada `Loaded`:

```csharp
_vm = App.Services.GetRequiredService<ProductosViewModel>();   // ← antes
```

> [!danger] Llamar `Dispose()` a mano NO lo saca de esa lista
> Las vistas ya llamaban `_vm.Dispose()` en su `Unloaded`, y estaba bien hecho: cerraba suscripciones de Realtime, cancelaba tokens, paraba timers. Pero **la referencia del contenedor sobrevive al `Dispose()`**. El objeto queda disposed y vivo. Por eso el arreglo correcto era el ciclo de vida, no más limpieza.

Resultado exacto del síntoma: **un ViewModel inmortal por cada navegación** y un `MainViewModel` + `NotificacionesViewModel` inmortales **por cada login**.

> [!note] Ironía documentada
> `PesajeViewModel` se hizo `IDisposable` en [[Sesión 2026-09-06 - Auditoría de commits a05f006 y 404796c]] justamente para corregir un `Dispose()` inerte. Ese arreglo era correcto — y es lo que hizo que el contenedor empezara a capturarlo. La deuda no la creó ese cambio: la reveló.

---

## 4. El amplificador: las 128 notificaciones

Dos defectos que solos son tolerables y juntos multiplican:

- **Sin debounce** — `OnCambioRealtime` disparaba 3 RPC + un `Clear()` completo de la colección por **cada** notificación entrante. 128 notificaciones = 384 llamadas y 128 reconstrucciones de lista.
- **Sin virtualización** — la lista era un `ItemsControl` **dentro de un `ScrollViewer` externo**, que es la forma clásica de anular la virtualización: el `ScrollViewer` le da altura infinita al panel, así que el panel materializa **todos** los items. ~50 elementos visuales por tarjeta × 128 items, creados y destruidos 128 veces.

---

## 5. Lo que se cambió

### Scope de sesión (`CapaUI/App.xaml.cs`)

```csharp
private static IServiceScope? _scopeSesion;

public static T CrearVm<T>() where T : class =>
    ActivatorUtilities.CreateInstance<T>(_scopeSesion?.ServiceProvider ?? Services);
```

- El scope se crea en `MostrarPrincipal()` y se dispone en `OnSesionCerrada` y en `OnExit`.
- `ActivatorUtilities.CreateInstance` **no registra la instancia en el contenedor**: resuelve las dependencias del constructor desde el proveedor, pero el dueño del objeto es quien lo crea. Es el mecanismo correcto para algo cuyo ciclo de vida lo maneja la vista.
- Las **14 vistas** migraron a `App.CrearVm<T>()`. **Cero** `GetRequiredService<...ViewModel>` quedan en el repo.

### Notificaciones

- **Debounce de 300 ms** en `OnCambioRealtime`; además dejó de ser `async void`.
- La lista pasó a `ListBox` con `IsVirtualizing`, `VirtualizationMode="Recycling"` y `ScrollUnit="Pixel"`, **y se eliminó el `ScrollViewer` externo** — que era el punto que realmente importaba.
- Se borraron **4 tarjetas mock hardcodeadas** (120 líneas) de `MainWindow.xaml`.

### Modales de Pesaje

`SelectorCatalogoModal` ahora **se auto-dispone en su `Unloaded`**, y `ProductosCargaModal`, `RegistroCamionesModal` y `CamionModal` implementan `IDisposable` para cascadear el cierre. `PesajeView` cierra el modal activo al descargarse (`CerrarModalActivo`).

---

## 6. Auditoría del cambio

Verificado leyendo el código, no asumido:

- **El doble `Dispose()` es seguro.** `MainViewModel` lo recibe dos veces (de `LimpiarRecursosAsync` y del scope); ambos `Dispose()` abren con guarda `if (_disposed) return;`.
- **Las dos vistas que no disponen su VM son benignas**: `ReporteriaViewModel` no es `IDisposable` (nada que liberar, nada que capturar), y el `MainViewModel` lo dispone `MainWindow.xaml.cs:738` más el scope.
- `dotnet build --no-incremental` → **0 errores**. Suite completa → **286/286**.

> [!warning] La verificación que falta es de runtime
> El compilador **no** cubre que `ActivatorUtilities.CreateInstance` resuelva bien los 14 ViewModels: eso falla en ejecución, no al compilar. Hay que **abrir las 14 pantallas al menos una vez**. Y la prueba del arreglo en sí: entrar, navegar, crear pesajes, cerrar sesión y volver a entrar — la RAM debe volver aproximadamente al valor de la primera sesión en vez de acumular.

---

## 7. Lo que quedó pendiente

- **P-054** — el debounce nuevo abandona un `CancellationTokenSource` sin `Dispose()` por cada notificación, y está hecho a mano existiendo `SuggestionDebouncer`.
- **P-055** — `ConexionSupabase.ResetAsync` no libera el `Auth` de Gotrue; su timer de auto-refresh mantiene enraizado el `Supabase.Client` de la sesión anterior. Se dejó fuera de alcance a propósito: tocar el ciclo de vida de la autenticación es riesgoso y merece medición previa.

---

## Relacionado

- [[Deuda Técnica - Pendientes]] — P-054 y P-055 nacen acá
- [[Sesión 2026-09-06 - Cierre integral de Deuda Tecnica P-031 y P-042]] — los frenos de rendimiento de UI, otra familia de problema
- [[Sesión 2026-09-06 - Auditoría de commits a05f006 y 404796c]] — donde `PesajeViewModel` pasó a ser `IDisposable`
- [[Informe de Optimización de DataGrids y Bug de Salto de Columnas]] — informe paralelo del agente sobre scroll y virtualización en las grillas
