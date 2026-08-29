---
title: Vista Descargada Durante un await (async void Loaded)
type: patron
status: vigente
tags:
  - patron
  - wpf
  - async
  - bug
  - rendimiento
  - mvvm
date: 2026-08-11
updated: 2026-08-11
summary: "await vm.CargarDatosAsync(); // ← el usuario cierra la pantalla ACÁ PoblarRoles(); // ← corre igual, con vm ya en null → 💥 }"
scope: []
symbols:
  - CancellationTokenSource
  - Loaded
  - OnVmPropertyChanged
  - ReferenceEquals
  - Unloaded
lifecycle: verified
---

# Vista Descargada Durante un `await` (async void Loaded)

> [!danger] La regla en una frase
> **Después de cada `await` en un handler de UI, la vista puede haberse descargado.** Todo lo que venga después tiene que verificarlo antes de tocar `_vm` o los controles.

---

## El síntoma

`System.NullReferenceException` al **abrir y cerrar una pantalla rápido**. Reportado en `UsuariosView.PoblarRoles()` el 2026-08-11.

## El mecanismo

```csharp
private async void UserControl_Loaded(object sender, RoutedEventArgs e)
{
    _vm = App.Services.GetRequiredService<UsuariosViewModel>();
    DataContext = _vm;

    await _vm.CargarDatosAsync();   // ← el usuario cierra la pantalla ACÁ
    PoblarRoles();                  // ← corre igual, con _vm ya en null → 💥
}

private void UserControl_Unloaded(object sender, RoutedEventArgs e)
{
    _vm.Dispose();
    _vm = null!;                    // ← esto pasa mientras el await está en vuelo
}
```

La secuencia real:

1. `Loaded` arranca la carga y **cede el control** en el `await`.
2. El usuario cierra → `Unloaded` corre completo → `_vm = null!`.
3. La consulta termina → la continuación del `await` vuelve al hilo de UI.
4. `PoblarRoles()` hace `_vm.Roles` → **NullReferenceException**.

No es una condición rara: basta con cerrar antes de que responda la BD.

---

## La solución

**Capturar la instancia antes del `await` y comparar por referencia después.**

```csharp
var vm = _vm;
await vm.CargarDatosAsync();
if (!ReferenceEquals(_vm, vm)) return;   // se descargó (o se recargó) mientras tanto

PoblarRoles();
```

> [!tip] Por qué `ReferenceEquals` y no `if (_vm == null)`
> El chequeo contra null se le escapa el caso **abrir → cerrar → abrir rápido**: ahí `_vm` no es null, pero es **otro** ViewModel. La continuación vieja poblaría la UI con datos del VM anterior. Comparar por referencia cubre las dos situaciones con una sola línea.

**Segunda línea de defensa** en los handlers que el VM puede disparar:

```csharp
private void OnVmPropertyChanged(object? s, PropertyChangedEventArgs ev)
{
    if (_vm == null) return;   // un PropertyChanged emitido justo durante Unloaded
    ...
}
```

---

## El otro lado del problema: la consulta sigue viva

Arreglar el NRE evita el crash, pero **no evita el trabajo desperdiciado**. Si el ViewModel no cancela, cerrar la pantalla deja la petición HTTP en vuelo hasta que termine sola. Abrir y cerrar varias veces seguidas acumula consultas simultáneas compitiendo — que es exactamente la "lentitud" que se percibe.

```csharp
private readonly CancellationTokenSource _cts = new();

public async Task CargarDatosAsync()
{
    var r = await _repo.ObtenerTodosAsync(_cts.Token);
    if (_disposed) return;
    ...
}

public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _cts.Cancel();
    _cts.Dispose();
}
```

**Los repositorios del proyecto ya aceptan `CancellationToken` en todos sus métodos.** El problema no era que faltara el parámetro: era que nadie se lo pasaba.

### El timer fantasma del timeout

Este patrón está en **9 ViewModels** y tiene una fuga sutil:

```csharp
// ❌ El Task.Delay NO se cancela cuando gana la consulta.
// Cada carga de página deja un timer de 10 s vivo en el TimerQueue,
// aunque la consulta haya vuelto en 200 ms.
if (await Task.WhenAny(task, Task.Delay(TimeoutMs)) != task) { ... }
```

```csharp
// ✅ El timer se libera apenas gana la consulta.
var relojTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
var demora  = Task.Delay(TimeoutMs, relojTimeout.Token);
var ganador = await Task.WhenAny(task, demora);
relojTimeout.Cancel();
relojTimeout.Dispose();

if (ganador != task) { /* timeout */ }
```

---

## Checklist para pantallas nuevas

- [ ] ¿Hay código **después** de un `await` en `Loaded`? → capturar el VM antes y comparar con `ReferenceEquals`.
- [ ] ¿El ViewModel tiene `CancellationTokenSource` y lo cancela en `Dispose()`?
- [ ] ¿Se le pasa el token a **todas** las llamadas a repositorio?
- [ ] ¿Cada `await` dentro del VM verifica `_disposed` antes de escribir propiedades?
- [ ] ¿Los `Task.Delay` de timeout se cancelan cuando gana la consulta?
- [ ] ¿`OnVmPropertyChanged` empieza con `if (_vm == null) return;`?

---

## Estado en el proyecto

| Pantalla | NRE del `await` | Cancelación real |
|---|---|---|
| Usuarios | ✅ Corregido | ✅ Corregido |
| Bitácora | ✅ Corregido | ⛔ Ver **P-029** |
| Productos · Proveedores · Fabricantes · Categorías · Empleados · Contactos ×2 | No aplica (no tienen código después del `await`) | ⛔ Ver **P-029** |

Los 7 restantes **no crashean** — ninguno tiene código después del `await` en `Loaded`. Lo que les falta es la cancelación y el arreglo del timer del timeout: registrado como **P-029** en [[Deuda Técnica - Pendientes]].

---

## Relaciones

- [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]] — donde se detectó
- [[Deuda Técnica - Pendientes]] — P-029
- [[Base Repository con TryAsync]] — los repos ya exponen el `CancellationToken`
- [[Módulo Usuarios]]
