---
title: "Sesión 2026-09-08 — Cierre de P-054 y P-055"
tags:
  - sesion
  - rendimiento
  - auth
  - wpf
  - refactor
  - auditoria
date: 2026-09-08
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente)
revisor: pendiente — falta la prueba de logout sin red
---

# Sesión 2026-09-08 — Cierre de P-054 y P-055

> [!success] Resultado
> Se cerraron las dos deudas que habían quedado abiertas al arreglar la fuga de memoria del contenedor DI. **La investigación corrigió el enunciado de las dos**: ninguna era lo que yo había escrito. De paso aparecieron dos defectos concretos que nadie había registrado (un timeout decorativo en el logout y un proyecto huérfano que duplica el singleton de conexión) y una deuda nueva, P-056.

---

## 1. Lo primero: los dos enunciados estaban mal

Vale documentarlo porque el valor de esta sesión está tanto en lo corregido como en lo arreglado.

| Deuda | Como se registró | Lo que era en realidad |
|---|---|---|
| **P-054** | «El debounce abandona un `CancellationTokenSource` por evento» — planteado como fuga de memoria | **No es una fuga.** Un CTS sin `CancelAfter` y al que nunca se le pide el `WaitHandle` no reserva timer ni handle: es basura recolectable. Era **higiene y duplicación** |
| **P-055** | «`ResetAsync` no libera el `Auth`, el timer enraiza el cliente viejo» — planteado como si el timer sobreviviera siempre | **No sobrevive siempre.** `SignOut()` lo apaga. El defecto es **de qué depende** ese apagado: de una llamada de red |

---

## 2. P-054 — el debounce estaba escrito cuatro veces

La mecánica «esperá y cancelá la anterior» estaba copiada a mano en cuatro lugares. Lo llamativo: **`SuggestionDebouncer` tenía el defecto adentro** — la clase que nació de [[Deuda Técnica - Pendientes|P-026]] justamente para no repetir esta mecánica.

| Sitio | ¿Disponía el CTS anterior? |
|---|---|
| `GhostTextBox.xaml.cs:164` | ✅ **Sí** — la única correcta |
| `SuggestionDebouncer.cs:59` | ❌ No |
| `SelectorCatalogoModal.xaml.cs:326` | ❌ No |
| `NotificacionesViewModel.cs:287` | ❌ No |

Por eso el arreglo no fue parchar el ViewModel: se extrajo `CapaUI/Core/Controls/Debouncer.cs` —que **es** la implementación de `GhostTextBox`, la que ya estaba bien— y los otros tres pasaron a usarla.

Dos detalles que importan y quedaron comentados en el código:

- **`Cancel()` antes de `Dispose()`** es el orden que exige la documentación de `CancellationTokenSource`: `Cancel` corre las registraciones —lo que completa el `Task.Delay` en vuelo como cancelado— y recién entonces se puede liberar. Al revés se corre el riesgo de disponer un origen que todavía tiene una espera colgando.
- **`Interlocked.Exchange` y no `lock`**: `NotificacionesViewModel` recibe los eventos desde el hilo de Realtime, no del UI.

### Lo que NO se tocó, a propósito

- **La API pública de `SuggestionDebouncer`** (`DebounceMs`, `Cancelar()`, `EjecutarAsync(query, buscar, aplicar)`) quedó idéntica, así que **los 9 ViewModels que lo consumen no se tocaron**. Lo que cambió es su interior: ahora delega el debounce y se queda solo con lo suyo — el `Trim()`, el mínimo de 2 caracteres y el mapeo a `SuggestionItemData`.
- **`GhostTextBox`**. Ya era correcto y su `CancelGhostDebounce` se invoca desde varios puntos con semántica propia. Migrarlo era churn con riesgo y sin ganancia.
- **`_ctsVida` de `SelectorCatalogoModal`**. No es debounce: es la vida del modal, de la que cuelga la revalidación de fondo. Tiene que abortarse al cerrar la lupa, no cuando el usuario sigue escribiendo.

> [!note] Sin cobertura automatizada
> `BimboProyecto.Tests` referencia Aplicación, Datos y Dominio — **no `CapaUI`**. `Debouncer` no tiene pruebas y agregarle una implicaría meterle una referencia a un proyecto WPF al de tests. Se verifica a mano: buscadores, lupa de catálogo y campana.

---

## 3. P-055 — el apagado del auth dependía de que hubiera red

`SignOut()` **sí** apaga el timer de auto-refresh: emite `AuthState.SignedOut` y `TokenRefresh` lo detiene. Pero `SignOut()` es una **llamada de red** envuelta en `try/catch` dentro de `MainWindow.LimpiarRecursosAsync`.

Logout sin conexión, endpoint lento o token ya inválido ⇒ no hay evento `SignedOut` ⇒ **el timer sobrevive a la sesión y se queda pidiendo tokens de una sesión que ya no existe**. No había ningún apagado local determinista.

`IGotrueClient.Shutdown()` es exactamente eso, y su documentación lo dice literal: detiene el hilo de fondo que refresca el token. No toca la red. Ahora `ResetAsync()` lo llama, en su propio `try` para que un fallo ahí no impida disponer el socket.

### Defecto encontrado de paso: el timeout del logout era decorativo

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var client = await ConexionSupabase.GetClientAsync();
await client.Auth.SignOut();          // ← el cts nunca se le pasa a nada
```

`IGotrueClient.SignOut(SignOutScope)` **no acepta `CancellationToken`** (verificado en la interfaz de `supabase.gotrue 6.0.3`). El `catch (OperationCanceledException)` con el mensaje «SignOut timeout — continuando» **no podía dispararse por timeout**. Con la red colgada, el cierre se iba hasta el timeout por defecto de `HttpClient` —**100 segundos**— con la UI congelada detrás de un `await` en un `async void`.

Ahora el tope se impone por fuera con `Task.WhenAny(signOut, Task.Delay(5s))`, se observa la excepción del `SignOut` abandonado para que no quede sin manejar, y los `Debug.WriteLine` de ese bloque pasaron a Serilog como el resto del cierre. Se mantiene el `SignOut` porque invalida el refresh token en el servidor —importa en terminal compartida— pero ya no puede bloquear el cierre.

### El comentario de `ResetAsync` prometía de más

Decía que liberaba «el cliente Supabase completo». **`Supabase.Client` no implementa `IDisposable`**: la lista de miembros de `M:Supabase.Client.*` en el XML de doc de `Supabase 1.1.1` es `#ctor`, `AdminAuth`, `From`, `GetAuthHeaders`, `InitializeAsync` y `Rpc`. No existe tal liberación. Las palancas reales son dos —el socket, que ya estaba, y `Auth.Shutdown()`, que faltaba— y el resto queda para el GC cuando se suelta la última referencia, que es lo que hace el `_client = null`. El comentario se corrigió para que diga lo que realmente apaga.

---

## 4. Deuda nueva: P-056, el gemelo muerto

Buscando dónde aplicar el fix apareció que hay **dos** clases `ConexionSupabase` en el **mismo namespace** (`ServicioConexión.Conexion`), en ensamblados distintos:

- `CapaDatos/Conexion.cs` — **el vivo**. `CapaUI` referencia solo `CapaDominio`, `CapaAplicacion` y `CapaDatos`.
- `ServicioConexión/Conexion/ConexionSupabase.cs` — **huérfano**: está en la solución, compila, y **ningún `.csproj` lo referencia**.

No es código inerte, es una trampa: el nombre del proyecto y el namespace lo hacen parecer el archivo correcto, y quien lo edite va a ver que su arreglo no cambia nada en ejecución.

**Decisión de Fernando:** aplicar el fix en **ambos** para que no divergan (como pide el comentario del propio archivo vivo), **no borrar ahora**, y dejarlo anotado como P-056 para borrarlo más adelante — hay otros agentes trabajando sobre el mismo árbol y sacar un proyecto de la solución es un cambio estructural.

---

## 5. Verificación

Corridas: `dotnet build --no-incremental` → **0 errores, 0 advertencias**. `dotnet test` → **286/286**.

> [!warning] Lo que el compilador no cubre
> **Buscadores** — teclear en Productos, Proveedores y Usuarios. Es la regresión más probable de tocar `SuggestionDebouncer`: las sugerencias tienen que aparecer, el popup no debe reabrirse solo al elegir una, y menos de 2 caracteres no dispara consulta.
> **Lupa de catálogo** — con más de 200 filas (la rama servidor, la única con debounce): teclear rápido tiene que dar una sola consulta al final.
> **Campana** — varios cambios seguidos deben refrescarla una vez, no una por evento.
> **P-055, la prueba que importa** — cerrar sesión **con la red cortada**: el logout no debe colgarse (antes se iba hasta 100 s), debe avisar por log que `SignOut` no respondió y volver al login.

Y sigue pendiente de la sesión anterior: abrir **las 14 pantallas** al menos una vez. `ActivatorUtilities.CreateInstance` resolviendo los ViewModels falla en ejecución, no al compilar.

---

## Relacionado

- [[Sesión 2026-09-08 - Fuga de memoria por contenedor DI y scope de sesión]] — de donde salieron P-054 y P-055
- [[Deuda Técnica - Pendientes]] — P-054 y P-055 cerrados acá; P-056 nace acá
- [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]] — origen de `SuggestionDebouncer`, la clase que traía el defecto adentro
