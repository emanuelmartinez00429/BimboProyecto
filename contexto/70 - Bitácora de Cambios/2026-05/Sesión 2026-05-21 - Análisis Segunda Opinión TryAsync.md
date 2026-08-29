---
title: "Sesión 2026-05-21 — Análisis Segunda Opinión: RepositorioBase + TryAsync"
type: sesion
status: vigente
tags:
  - bitácora
  - análisis
  - decisión-técnica
  - manejo-errores
date: 2026-05-21
updated: 2026-05-21
summary: "La afirmación técnica es falsa. En C#, los miembros protected static de una clase base sí son accesibles desde métodos static de clases derivadas. Esto compila y…"
scope:
  - CapaDatos/Repositorios
symbols:
  - BuscarSugerenciasAsync
  - GetFabricantesAsync
  - GetPagedAsync
  - GetPaisesAsync
  - IProductoRepository
  - List<T>
  - OperationCanceledException
  - RepositorioBase
  - Result<T>
  - SupabaseRepository
estado: documentado
fecha: 2026-05-21
---

# Sesión 2026-05-21 — Análisis Segunda Opinión: RepositorioBase + TryAsync

> Revisión técnica de 4 observaciones sobre el plan de implementación del [[Base Repository con TryAsync]]. Verificado contra documentación oficial de Microsoft.

---

## Punto 1 — "Legacy repos NO pueden heredar por tener métodos static"

**Veredicto: Razonamiento incorrecto. Conclusión correcta.**

La afirmación técnica es falsa. En C#, los miembros `protected static` de una clase base **sí son accesibles** desde métodos `static` de clases derivadas. Esto compila y funciona:

```csharp
public abstract class RepositorioBase {
    protected static async Task<Result<T>> TryAsync<T>(...) { }
}
public class RepositorioProducto : RepositorioBase {
    public static async Task<List<Productos>> ObtenerTodos() {
        return await TryAsync(...); // ✅ compila
    }
}
```

**Motivo real para no migrar los legacy:** Si heredan y usan `TryAsync`, sus tipos de retorno deben cambiar de `List<T>` → `Result<T>`. Ese cambio rompe todos los formularios WinForms que los llaman hoy. La herencia sin ese cambio no aporta nada.

**Decisión:** Los repos legacy (`CapaDatos/Repositorios/`) quedan intactos. Se migran cuando el formulario WinForms correspondiente migre a WPF.

---

## Punto 2 — "TryAsync debe re-lanzar OperationCanceledException + logging"

**Veredicto: Correcto. Crítico.**

`OperationCanceledException` (que incluye `TaskCanceledException`) no es un error — es señal de cancelación intencional. Convertirla a `Result.Fail` haría que el buscador mostrara "Error de red" cada vez que el usuario deja de escribir.

Práctica estándar .NET (Microsoft docs): usar exception filter:

```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Debug.WriteLine($"[{contexto}] {ex}");
    return Result<T>.Fail($"{contexto}: {ex.Message}");
}
```

`Debug.WriteLine` es obligatorio: sin él, si la UI no chequea `Result.Success`, el error desaparece en silencio.

**Decisión:** Incorporado en `RepositorioBase` como se describe.

---

## Punto 3 — "ViewModel se llena de if (!r.Success) — usar ref bool ok"

**Veredicto: Preocupación válida. Solución propuesta descartada.**

El `ref bool ok` no es un patrón idiomático en C#. Es stateful y confuso. El patrón estándar .NET para este problema es `TryXxx(out T value)` (como `int.TryParse`, `Dictionary.TryGetValue`).

Sin embargo, en este ViewModel específico solo hay **4 llamadas** con Result:
- `GetFabricantesAsync`
- `GetPaisesAsync`
- `GetPagedAsync`
- `BuscarSugerenciasAsync`

A esa escala, el `if (!r.Success)` explícito es **más legible** que cualquier helper. Un helper solo tiene sentido con 10+ llamadas encadenadas.

**Decisión:** No se agrega helper. Se usa `if (!r.Success)` explícito en los 4 casos.

---

## Punto 4 — Lo que está correcto

Confirmado sin cambios:
- ✅ `TryAsync` centralizado en una clase base
- ✅ Métodos de lectura en `IProductoRepository` → `Result<T>`
- ✅ Eliminar `catch { }` vacíos en `ProductoModal.OnLoaded`
- ✅ `SupabaseRepository` hereda `RepositorioBase`
- ✅ Orden de fases: Base → Interface → Impl → ViewModel → Modal

---

## Resumen de decisiones

| Observación | Acción |
|---|---|
| Repos legacy no heredan | ✅ Fase 7 eliminada del plan |
| OperationCanceledException + logging | ✅ Incorporado en RepositorioBase |
| No usar `ref bool ok` | ✅ `if (!r.Success)` explícito |
| Fases 1-6 correctas | ✅ Sin cambio |

---

*Relacionado: [[Base Repository con TryAsync]] · [[Result Pattern]] · [[Plan de Refactor - Estado y Fases]]*
