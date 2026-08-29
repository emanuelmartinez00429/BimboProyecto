---
title: Sesión 2026-05-26 — Realtime Silent Refresh en ProductosViewModel
type: sesion
status: vigente
tags:
  - sesion
  - realtime
  - ux
  - productos
  - fix
date: 2026-05-26
updated: 2026-08-14
summary: El spinner de carga ya no aparece para eventos Realtime. Los datos se actualizan silenciosamente en segundo plano. 0 errores de compilación.
scope:
  - CapaUI/Formularios/Principal/Pantallas/Productos
symbols:
  - CargarPaginaAsync
  - CargarPaginaSilenciosamenteAsync
  - ErrorCarga
  - GetPagedAsync
  - IsLoading
  - ItemsControl
  - OnCambioProducto
  - OnVmPropertyChanged
  - PageRows
  - PaginacionPanel
---

# Sesión 2026-05-26 — Realtime Silent Refresh en ProductosViewModel

> [!success] Resultado
> El spinner de carga ya no aparece para eventos Realtime. Los datos se actualizan silenciosamente en segundo plano. 0 errores de compilación.

---

## Problema reportado

Cuando Supabase enviaba un evento Realtime (INSERT o UPDATE), el handler `OnCambioProducto` llamaba directamente a `CargarPaginaAsync()`. Ese método siempre ejecuta `IsLoading = true` como primera instrucción, lo que:

1. Mostraba el spinner aunque el cambio fuera en otra página (el usuario ve la animación pero sus datos no cambian)
2. Deshabilitaba los botones de paginación temporalmente sin razón para el usuario
3. Interrumpía visualmente la navegación normal

El único caso ya correcto era UPDATE en otra página → `RefrescarConteosAsync()`, que es silencioso.

---

## Diagnóstico por caso (antes del fix)

| Evento | Comportamiento anterior | Problema |
|---|---|---|
| INSERT (cualquier página) | `CargarPaginaAsync()` → spinner siempre | Spinner aunque el registro esté en otra página |
| UPDATE en página actual | `CargarPaginaAsync()` → spinner | Spinner aunque los datos vayan a actualizarse sin acción del usuario |
| UPDATE en otra página | `RefrescarConteosAsync()` | ✅ Correcto, ya era silencioso |

---

## Solución: `CargarPaginaSilenciosamenteAsync`

Se decidió **un método separado** (no un parámetro booleano ni un flag de clase) por estas razones:

- Parámetro booleano → viola "un método hace una cosa"
- Flag de clase → introduce race condition con `_loadGeneration`
- Método separado → patrón ya existente (`RefrescarConteosAsync`), semántica clara, sin efectos secundarios sobre `IsLoading`

### Diferencias clave vs `CargarPaginaAsync`

| Aspecto | `CargarPaginaAsync` (usuario) | `CargarPaginaSilenciosamenteAsync` (Realtime) |
|---|---|---|
| `IsLoading` | `true` → `false` | Nunca toca |
| `ErrorCarga` | Muestra errores al usuario | Log Serilog silencioso |
| `_loadGeneration` | `++_loadGeneration` | Lee sin incrementar (`genCapturada = _loadGeneration`) |
| Timeout 10s | Sí | No |
| Guard `IsLoading` | No | Sí → sale inmediatamente si carga de usuario activa |
| Try/catch externo | No (errores manejados inline) | Sí → traga excepciones inesperadas |
| Preserva `Seleccionado` | Vía `_pendingSelectionId` | Captura Id antes de reconstruir `PageRows`, restaura por Id |

### Lógica de `_loadGeneration` en modo silencioso

El método silencioso **no incrementa** `_loadGeneration`. En cambio captura el valor actual al inicio:

```
genCapturada = _loadGeneration  (lectura sin ++, no es carga de usuario)
...await GetPagedAsync...
if (_loadGeneration != genCapturada) return;  // usuario navegó → descartar
```

Esto garantiza que si el usuario cambia de página mientras el silent refresh está en vuelo, el resultado se descarta sin corromper `IsLoading`.

### Lógica especial para INSERT

Para INSERT no se sabe en qué página aterrizó el nuevo registro sin una consulta extra. La heurística aplicada:

1. Hacer `GetPagedAsync` (ya necesaria para actualizar conteos)
2. Con el `Total` nuevo, calcular `nuevoTotalPages`
3. Si `_page == nuevoTotalPages` → el usuario está en la última página → aplicar filas + conteos (el INSERT probablemente está ahí)
4. Si `_page < nuevoTotalPages` → el INSERT creó una nueva página → solo aplicar conteos

> [!note] Supuesto de sort
> Esta heurística asume que los registros nuevos aparecen en la última página (orden por `id_producto ASC` o fecha). Si en el futuro se cambia el orden, revisar esta lógica.

---

## Archivos modificados

### `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosViewModel.cs`

**1. Nuevo `using` agregado:**
```csharp
using CapaAplicacion.Common;  // para Result<T>
```

**2. `OnCambioProducto` — reemplazadas las llamadas a `CargarPaginaAsync()`:**
```csharp
private void OnCambioProducto(CambioRealtime cambio)
{
    if (_disposed) return;

    bool afectaPaginaActual = cambio.IdRegistro.HasValue
        && PageRows.Any(p => p.Id == cambio.IdRegistro.Value);

    if (string.Equals(cambio.Operacion, "INSERT", StringComparison.OrdinalIgnoreCase))
    {
        _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: true);
    }
    else if (string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase))
    {
        if (afectaPaginaActual || !cambio.IdRegistro.HasValue)
            _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);
        else
            _ = RefrescarConteosAsync();
    }
}
```

**3. Nuevo método `CargarPaginaSilenciosamenteAsync`:**
```csharp
private async Task CargarPaginaSilenciosamenteAsync(bool actualizarFilas, bool esInsert)
{
    try
    {
        if (IsLoading) return;

        int genCapturada = _loadGeneration;

        var filtros = BuildFiltros();
        Result<PagedResult<ProductoDto>> r;

        try
        {
            r = await _repo.GetPagedAsync(_page, PageSize, filtros);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ProductosVM: silent refresh — excepción en request");
            return;
        }

        if (_loadGeneration != genCapturada) return;

        if (!r.Success)
        {
            Serilog.Log.Warning("ProductosVM: silent refresh — error: {Error}", r.Error);
            return;
        }

        var pagina = r.Value!;

        int nuevoFilteredCount = filtros.IdEstado switch
        {
            1 => pagina.Activos,
            2 => pagina.Inactivos,
            _ => pagina.Total
        };
        int nuevoTotalPages = Math.Max(1, (int)Math.Ceiling(nuevoFilteredCount / (double)PageSize));

        bool debeActualizarFilas = actualizarFilas
            && (!esInsert || _page == nuevoTotalPages);

        TotalCount     = pagina.Total;
        ActivosCount   = pagina.Activos;
        InactivosCount = pagina.Inactivos;
        _filteredCount = nuevoFilteredCount;

        if (debeActualizarFilas)
        {
            int? idSeleccionadoAntes = Seleccionado?.Id;
            PageRows = new ObservableCollection<ProductoDto>(pagina.Items);
            if (idSeleccionadoAntes.HasValue)
                Seleccionado = PageRows.FirstOrDefault(x => x.Id == idSeleccionadoAntes.Value);
        }

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(NoResults));
        NotifyPaginationCanExecuteChanged();
    }
    catch (Exception ex)
    {
        Serilog.Log.Error(ex, "ProductosVM: error inesperado en silent refresh");
    }
}
```

---

## Tabla de comportamiento final

| Evento Realtime | Condición | Spinner | Filas | Conteos | Selección |
|---|---|---|---|---|---|
| INSERT | `_page < nuevoTotalPages` (INSERT creó nueva página) | ❌ | Sin cambio | ✅ | Preservada |
| INSERT | `_page == nuevoTotalPages` (en última página) | ❌ | ✅ Recargadas | ✅ | Restaurada por Id |
| UPDATE en página actual | — | ❌ | ✅ Recargadas | ✅ | Restaurada por Id |
| UPDATE en otra página | — | ❌ | Sin cambio | ✅ | Preservada (no se toca) |
| Acción del usuario | Filtro, paginación, carga inicial | ✅ | ✅ | ✅ | Via `_pendingSelectionId` |

> [!danger] Adenda 2026-08-14 — efecto colateral no visto en esta sesión
> La fila `INSERT | _page < nuevoTotalPages | Filas: Sin cambio` de la tabla de arriba es correcta en el ViewModel: `TotalPages` se recalcula y notifica bien. Pero el `ItemsControl` de números de página en el code-behind de la vista (`PaginacionPanel`/`RefrescarPaginacion()`) solo se reconstruía cuando cambiaba `PageRows` — nunca se agregó un `case` para `TotalPages` en el `switch` de `OnVmPropertyChanged`. Resultado: en ese caso exacto, los botones numerados quedaban con el árbol viejo hasta recargar el módulo, aunque el usuario reportó que "el número de página decía 12 pero ir a la última llevaba a la 1" — porque el árbol de botones seguía siendo el de 11 páginas. Corregido (y replicado a los 5 módulos gemelos) en [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]].

---

## Edge cases cubiertos

### Race condition con navegación de usuario
Si el usuario navega a otra página mientras el silent refresh está en vuelo, `_loadGeneration` cambia (`CargarPaginaAsync` lo incrementa). Al regresar el silent, `_loadGeneration != genCapturada` → descarta resultado sin tocar `IsLoading`. Correcto.

### Carga de usuario activa
Guard `if (IsLoading) return` al inicio del método silencioso. Si el usuario ya inició una carga, el silent se cancela inmediatamente — la carga del usuario traerá datos igualmente frescos.

### Selección perdida tras reconstrucción de `PageRows`
Si `Seleccionado` apuntaba a un objeto y `PageRows` se reconstruye, la referencia queda obsoleta. El método captura `Seleccionado?.Id` antes del reemplazo y lo restaura buscando por Id en la nueva colección. Si el item ya no pasa el filtro activo (ej. se deshabilitó y el filtro es "Habilitados"), `Seleccionado = null` automáticamente — comportamiento correcto.

### Errores de red en silent refresh
`try/catch` doble: el inner captura excepciones del `await GetPagedAsync`, el outer captura cualquier error lógico inesperado. Ambos loggean con Serilog y retornan sin mostrar nada al usuario.

---

## Relaciones

- [[Sesión 2026-05-24 - Implementación Gestor Realtime Completa]] — Implementación original del Realtime en ProductosViewModel
- [[Módulo Productos]] — Documentación actualizada del módulo
- [[Plan de Implementación - Gestor Realtime]] — Plan base del que deriva este refinamiento
- [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]] — corrige el efecto colateral en `PaginacionPanel` (adenda arriba)
