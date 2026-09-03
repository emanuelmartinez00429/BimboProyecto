# Análisis Técnico R1: Auditoría Adversarial y Contraste Profundo con el Código Vivo de BimboProyecto

**Fecha:** 2026-09-03  
**Autor:** Explorer 1 (`teamwork_preview_explorer`)  
**Alcance:** CapaUI, CapaDatos, CapaAplicacion4, Ciclo de Vida WPF y Supabase Realtime  
**Estado:** Hallazgos verificados empíricamente sobre el código fuente  

---

## 1. Resumen Ejecutivo

La auditoría adversarial del código vivo confirma la viabilidad arquitectónica de reemplazar la caché estática *stale-while-revalidate* (ADR-015) por una solución en memoria basada en **FusionCache L1** con invalidación reactiva por **Supabase Realtime** (ADR-026). Sin embargo, la inspección exhaustiva de `CatalogoCache.cs`, `CatalogoRepository.cs`, `RolPermisoRepository.cs`, `RealtimeService.cs`, `MainWindow.xaml.cs` y `App.xaml.cs` reveló **vulnerabilidades críticas de ciclo de vida y concurrencia**:
1. **Fuga concreta de datos entre usuarios (P-048):** En terminales físicas compartidas de planta, `MainWindow.LimpiarRecursosAsync()` nunca invoca `CatalogoCache.InvalidarTodo()` ni limpia el campo estático `RolPermisoRepository._catalogoCache`, mientras que el contenedor de dependencias `App.Services` persiste inmutable como Singleton de proceso. Un segundo operador hereda de inmediato en memoria los datos y catálogos cargados por el usuario previo.
2. **Despacho forzado al hilo de UI en `RealtimeService`:** Todos los suscriptores de `IRealtimeService` se ejecutan en el hilo de UI vía `SynchronizationContext.Post`. `InvalidadorCacheRealtime` debe realizar la invalidación en memoria (`RemoveByTag`) de forma inmediata, pero **debe desacoplar el procesamiento y logueo de eventos mediante un `Channel<T>` en segundo plano** para no degradar la fluidez visual de la interfaz gráfica.
3. **Peligro de cancelación en cascada:** `SelectorCatalogoModal` ata la vida de la petición de catálogo al `CancellationTokenSource` privado `_ctsVida`. Si el decorador de caché pasa este token a la fábrica de FusionCache, cerrar el modal rápidamente abortará la carga para cualquier otra pantalla que estuviera esperando en el mismo vuelo único (*single-flight*). La fábrica debe invocar el repositorio subyacente con `CancellationToken.None`.

---

## 2. Auditoría Detallada de Implementaciones Existentes

### 2.1. `CatalogoCache.cs` (`CapaUI/Core/Catalogos/CatalogoCache.cs`)

`CatalogoCache` es una clase utilitaria puramente estática (`public static class CatalogoCache`, línea 28) diseñada bajo ADR-015 para servir de caché en memoria de sesión para catálogos pequeños.

#### Estructuras de Datos y Estado
- **Línea 30:**
  ```csharp
  private static readonly ConcurrentDictionary<string, IReadOnlyList<FiltroItem>> _cache = new();
  ```
  - Almacén en memoria global de proceso basado en `ConcurrentDictionary`.
  - **No posee TTL ni políticas de desalojo temporal o por memoria.** Todo elemento ingresado permanece en la memoria RAM del proceso hasta que se cierre la aplicación o se llame explícitamente a `Invalidar()`.
- **Claves de catálogo soportadas:** Las claves provienen de `CatalogoConfig.Clave` (`CapaUI/Core/Catalogos/CatalogoConfig.cs`, líneas 81-141):
  - `"presentaciones"`, `"taras"`, `"categorias"`, `"paises"`, `"proveedores"`
  - Catálogos con ámbito/filtro: `"unidades"` o `"unidades:{idTipoUnidad}"`, `"productos"` o `"productos:{idProveedor}"`, `"fabricantes"` o `"fabricantes:{idProveedor}"`.

#### Métodos de Acceso y Patrón *Stale-While-Revalidate* (ADR-015)
- **`ObtenerCompletoAsync` (líneas 46-71):**
  ```csharp
  public static async Task<Result<IReadOnlyList<FiltroItem>?>> ObtenerCompletoAsync(
      CatalogoConfig cfg, int umbral,
      Action<IReadOnlyList<FiltroItem>>? alRevalidar = null,
      CancellationToken ct = default)
  {
      if (_cache.TryGetValue(cfg.Clave, out var cacheado))
      {
          if (alRevalidar is not null)
              _ = RevalidarAsync(cfg, umbral, cacheado, alRevalidar, ct);

          return Result<IReadOnlyList<FiltroItem>?>.Ok(cacheado);
      }

      var r = await cfg.Cargar(string.Empty, 1, umbral, ct);
      if (!r.Success)
          return Result<IReadOnlyList<FiltroItem>?>.Fail(r.Error);

      var pagina = r.Value!;
      if (pagina.Total > umbral)
          return Result<IReadOnlyList<FiltroItem>?>.Ok(null);

      _cache[cfg.Clave] = pagina.Items;
      return Result<IReadOnlyList<FiltroItem>?>.Ok(pagina.Items);
  }
  ```
  - **Líneas 51-57:** Ante un cache-hit, devuelve la lista inmediatamente (0 ms). Si `alRevalidar != null`, dispara una tarea en segundo plano *fire-and-forget* (`_ = RevalidarAsync(...)`) pasando el `CancellationToken ct` del llamador.
  - **Líneas 59-70:** Ante un cache-miss, invoca el delegado `cfg.Cargar` pidiendo la primera página con tamaño igual a `umbral` (200). Si `pagina.Total > umbral`, no cachea y devuelve `Ok(null)` para indicar que el modal debe paginar contra el servidor.
- **`RevalidarAsync` (líneas 110-143):**
  - Consulta nuevamente la base de datos con `await cfg.Cargar(string.Empty, 1, umbral, ct)`.
  - Compara la lista previa contra la nueva mediante `SonIguales(previo, pagina.Items)` (líneas 149-164), comparando campo por campo (`Id`, `Nombre`, `Descripcion`, `Activo`, `IdPadre`).
  - Si la lista difiere, sobreescribe `_cache[cfg.Clave] = pagina.Items` e invoca `alRevalidar(pagina.Items)` para repintar la UI.
  - Si el catálogo creció más allá del umbral (`pagina.Total > umbral`), remueve la entrada con `Invalidar(cfg.Clave)`.
- **`Invalidar(string clave)` (líneas 170-175):**
  ```csharp
  public static void Invalidar(string clave)
  {
      foreach (var k in _cache.Keys)
          if (k == clave || k.StartsWith(clave + ":", StringComparison.Ordinal))
              _cache.TryRemove(k, out _);
  }
  ```
  Invalida la clave base y cualquier variante jerárquica con prefijo `clave + ":"`.
- **`InvalidarTodo()` (línea 177):** Ejecuta `_cache.Clear()`.

#### Deficiencias Técnicas Demostradas de `CatalogoCache`
1. **Cero ahorro de round-trips a la base de datos:** Como la revalidación se lanza en cada llamada (línea 54), cada apertura de lupa ejecuta una consulta HTTP a Supabase.
2. **Vulnerabilidad de cancelación de revalidación:** El token `ct` de `SelectorCatalogoModal._ctsVida.Token` se pasa a `RevalidarAsync` (línea 54). Si el usuario selecciona un item rápidamente y el modal se cierra (`Dispose()` cancela `_ctsVida`), la revalidación se aborta en `OperationCanceledException` (líneas 119, 138), perdiendo la oportunidad de corregir la caché.
3. **Ausencia de invocación en cierre de sesión:** Ningún componente del ciclo de vida de la aplicación invoca `CatalogoCache.InvalidarTodo()`.

---

### 2.2. `CatalogoRepository.cs` (`CapaDatos/Repositories/Catalogos/CatalogoRepository.cs`)

#### Contrato y Registro
- Implementa `ICatalogoRepository` (`CapaAplicacion4/Common/Catalogos/ICatalogoRepository.cs`, líneas 18-59).
- Registrado en `CapaDatos/DependencyInjection.cs` (líneas 69-70):
  ```csharp
  services.AddTransient<CapaAplicacion.Common.Catalogos.ICatalogoRepository,
                        Repositories.Catalogos.CatalogoRepository>();
  ```
- Hereda de `RepositorioBase` y recibe `IConexionMonitor` en su constructor (línea 27).

#### Motor Común de Paginación y Sonda (`PagedInternalAsync<T>`, líneas 230-279)
- Optimización de conteo barato: Pide `from` a `to = from + size` (sonda de `size + 1` filas).
- Si `filas.Count <= size`, se asume que se obtuvo el catálogo completo en una sola pasada y el conteo total exacto es `from + filas.Count`, **evitando una segunda consulta SQL `COUNT(*)`**.
- Solo cuando `filas.Count > size` se ejecuta la consulta de conteo exacto: `await Construir().Count(Count.Exact, ct)`.

#### Particularidades de los 8 Catálogos
1. `GetPresentacionesAsync` (líneas 31-45): Filtra por `id_estado = 1`, ordena por `nombre_presentacion`.
2. `GetTarasAsync` (líneas 51-64): No tiene columna de estado. Hace join con `unidad_medida(*)` para mostrar abreviatura.
3. `GetCategoriasAsync` (líneas 67-81): Usa `estado_categoria = true` (booleano, a diferencia de `id_estado` entero de las demás tablas).
4. `GetUnidadesAsync` (líneas 89-109): Soporta filtro opcional `id_tipo_unidad`.
5. `GetPaisesAsync` (líneas 111-124): No tiene columna de estado; ordena por `nombre_pais`.
6. `GetProveedoresAsync` (líneas 126-140): Filtra `id_estado = 1`, ordena por `nombre_proveedor`.
7. `GetProductosAsync` (líneas 152-181): No tiene `id_proveedor` directo en la tabla `productos`. Resuelve el encadenamiento en 2 pasos mediante `FabricantesDeProveedor(idProveedor, ct)` (líneas 183-194) ejecutando `id_fabricante IN (...)`.
8. `GetFabricantesAsync` (líneas 196-217): Soporta filtro opcional `id_proveedor`.

#### Trampa de Registro en DI para el Decorador FusionCache
Si se decora `ICatalogoRepository` con una clase `CachedCatalogoRepository` que envuelva a `CatalogoRepository`:
- ⚠️ **Riesgo:** Si en DI se registra `CatalogoRepository` por su interfaz `ICatalogoRepository`:
  ```csharp
  // INCORRECTO:
  services.AddTransient<ICatalogoRepository, CatalogoRepository>();
  services.AddTransient<ICatalogoRepository>(sp =>
      new CachedCatalogoRepository(sp.GetRequiredService<ICatalogoRepository>(), ...));
  ```
  La llamada `sp.GetRequiredService<ICatalogoRepository>()` se resuelve a sí misma recursivamente, provocando `StackOverflowException`.
- ✅ **Mitigación obligatoria:** Debe registrarse `CatalogoRepository` por su tipo concreto:
  ```csharp
  services.AddTransient<CatalogoRepository>();
  services.AddTransient<ICatalogoRepository>(sp =>
      new CachedCatalogoRepository(sp.GetRequiredService<CatalogoRepository>(), ...));
  ```

---

### 2.3. `SelectorCatalogoModal.xaml.cs` (`CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`)

Control de usuario (`UserControl`) genérico que aloja la visualización de catálogos en memoria o paginados.

#### Ciclo de Vida y Tokens de Cancelación
- **Línea 74:**
  ```csharp
  private readonly CancellationTokenSource _ctsVida = new();
  ```
  Token que abarca toda la vida útil del modal desde su creación hasta su cierre.
- **Línea 50:**
  ```csharp
  private CancellationTokenSource? _cts;
  ```
  Token de rebote (*debounce* de 300 ms) recreado en cada tecla digitada en el buscador `SearchBox`.
- **`OnLoaded` y `CargarInicialAsync` (líneas 125-179):**
  ```csharp
  var r = await CatalogoCache.ObtenerCompletoAsync(
      _cfg, UmbralMemoria,
      alRevalidar: lista => { if (!_dispuesto) PintarEnMemoria(lista, preservarSeleccion: true); },
      _ctsVida.Token);
  ```
  Pasa `_ctsVida.Token` como token de cancelación.
- **`Dispose()` (líneas 575-591):**
  ```csharp
  public void Dispose()
  {
      if (_dispuesto) return;
      _dispuesto = true;

      DependencyPropertyDescriptor
          .FromProperty(SuggestionSearchBox.QueryProperty, typeof(SuggestionSearchBox))
          .RemoveValueChanged(SearchBox, OnQueryChanged);

      _cts?.Cancel();
      _cts?.Dispose();
      _cts = null;

      _ctsVida.Cancel();
      _ctsVida.Dispose();
  }
  ```
  Al cerrarse el selector, se dispara inmediatamente `_ctsVida.Cancel()`.

#### Análisis del Retiro de `alRevalidar`
- En el diseño actual (ADR-015), `alRevalidar` recibe la lista revalidada y llama a `PintarEnMemoria(lista, preservarSeleccion: true)`. Esto preserva la selección de fila si el usuario ya había seleccionado un elemento mientras ocurría la revalidación.
- **Consecuencia de adoptar FusionCache + Realtime:**
  - Las tablas de catálogo están 100% publicadas en `supabase_realtime` (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`).
  - La frescura de la caché L1 está garantizada de forma reactiva: cuando ocurre un cambio en la base de datos, el evento Realtime invalida la etiqueta en FusionCache de inmediato.
  - Al abrir el modal, la consulta a `ICatalogoRepository` obtiene el valor cacheado si es fresco o consulta la base si fue invalidado.
  - **El callback `alRevalidar` queda obsoleto y se elimina.**
  - **Riesgo observable para el usuario:** Desaparece el repintado en caliente en pantalla (`Dg.ItemsSource`). Una vez que el modal abre con los datos en memoria, la lista no mutará de forma espontánea mientras el modal permanezca abierto en esa misma apertura. Este es el único cambio de comportamiento perceptible en UI y representa una mejora en estabilidad visual.

---

### 2.4. `RolPermisoRepository.cs` (`CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs`)

Repositorio encargado del resumen de roles y la estructura RBAC de módulos y acciones.

#### Caché Estática Oculta y Bloqueo de Acceso
- **Líneas 23-24:**
  ```csharp
  private static IReadOnlyList<ModuloAccionesDto>? _catalogoCache;
  private static readonly SemaphoreSlim CatalogoLock = new(1, 1);
  ```
  - `_catalogoCache` es un campo `private static`.
  - `CatalogoLock` es un `SemaphoreSlim(1, 1)` estático para serializar la carga inicial.
- **Método `CargarCatalogoAsync` (líneas 84-130):**
  - Implementa el patrón *double-checked locking* asíncrono:
    ```csharp
    if (_catalogoCache is not null) return _catalogoCache;
    await CatalogoLock.WaitAsync(ct);
    try {
        if (_catalogoCache is not null) return _catalogoCache;
        ...
        _catalogoCache = modulos...;
        return _catalogoCache;
    }
    finally { CatalogoLock.Release(); }
    ```
- **Falta Absoluta de Mecanismo de Invalidación:**
  - La clase **no contiene ningún método para invalidar, limpiar o forzar la recarga** de `_catalogoCache`.
  - ADR-014 (`contexto/45 - Decisiones/ADR-014 - Precarga unica y cache del catalogo RBAC.md`) documentó que esto era aceptable porque `modulos` y `acciones` son estáticos y solo cambian con migraciones SQL.
  - **Desalineación con Realtime:** Las tablas `modulos`, `acciones` y `roles` **no forman parte de `supabase_realtime`**. No reciben eventos reactivos.

---

### 2.5. `EmpresaRepository.cs` y `LogoEmpresaCache.cs`

- **`EmpresaRepository.cs` (`CapaDatos/Repositories/Empresa/EmpresaRepository.cs`):**
  - Administra datos de configuración institucional (`empresa`) y almacenamiento de archivos en Supabase Storage (bucket `empresa-logos`).
  - La tabla `empresa` **no está publicada en `supabase_realtime`**.
- **`LogoEmpresaCache.cs` (`CapaUI/Core/Empresa/LogoEmpresaCache.cs`):**
  - Implementa una caché de archivos locales en `%AppData%\BimboPesaje\LogoEmpresa` (líneas 24-27).
  - Utiliza el nombre de archivo en storage como clave de versión inmutable (ej. `logo_empresa_1_20260902_xxx.png`).
  - `ObtenerRutaCacheadaSinRed()` (líneas 35-48) permite al login renderizar el logo corporativo de inmediato sin tocar la red.

---

## 3. Concurrencia y Orden de Ejecución

### 3.1. Flujo de Hilos en `RealtimeService.cs` (`CapaDatos/Realtime/RealtimeService.cs`)

```
 [Supabase WebSocket]
          │ (Socket Thread)
          ▼
   AbrirCanalAsync -> channel.AddPostgresChangeHandler
          │
          ▼
   OnCambioRecibido(clave, tabla, change)
          │
          ├─► ExtraerCambio(tabla, change)  [Hilo de Socket / ThreadPool]
          │
          └─► DespacharEnUIThread(...)
                   │
                   ▼
         _syncContext.Post(...)             [Encola en Dispatcher de WPF]
                   │
═══════════════════╪═════════════════════════════════════════════════════════
                   │
                   ▼ (Hilo Principal de UI - Dispatcher Loop)
           h(cambio)  --> InvalidadorCacheRealtime.OnCambio(cambio)
```

#### Evidencia en Código
1. **Captura del contexto:** Constructor de `RealtimeService` (línea 60):
   ```csharp
   _syncContext = SynchronizationContext.Current;
   ```
   En una aplicación WPF, `SynchronizationContext.Current` es una instancia de `DispatcherSynchronizationContext`.
2. **Recepción del paquete:** En `AbrirCanalAsync` (líneas 170-172), el handler se suscribe a Supabase:
   ```csharp
   channel.AddPostgresChangeHandler(ListenType.All, (_, change) => OnCambioRecibido(clave, tabla, change));
   ```
   Este callback es disparado por la librería cliente de Supabase en un hilo de socket o ThreadPool.
3. **Extracción y Despacho:** `OnCambioRecibido` (líneas 181-208):
   ```csharp
   var cambio = ExtraerCambio(tabla, change);
   ...
   DespacharEnUIThread(() =>
   {
       foreach (var h in snapshot)
       {
           try   { h(cambio); }
           catch (Exception ex) { Serilog.Log.Warning(...); }
       }
   });
   ```
   `DespacharEnUIThread` (líneas 262-269) ejecuta `_syncContext.Post(_ => accion(), null)`.
   `SynchronizationContext.Post` realiza un despacho **asíncrono y no bloqueante** a la cola de mensajes del `Dispatcher` de WPF.

### 3.2. Interacción de `InvalidadorCacheRealtime` con `RemoveByTag` y `Channel<T>`

#### Ejecución en Hilo de UI
Dado que `IRealtimeService` impone por contrato y diseño que todos sus handlers se ejecutan en el UI thread (`IRealtimeService.cs`, línea 12: *"El handler se invoca siempre en el UI thread"*), `InvalidadorCacheRealtime.OnCambio(CambioRealtime cambio)` será invocado directamente por el `Dispatcher` de WPF.

#### Requerimientos de Desempeño
1. **Invalidación de Memoria Inmediata (`RemoveByTag`):**
   - La llamada a `FusionCache.RemoveByTag(tag)` opera sobre el índice de tags en RAM.
   - Su tiempo de ejecución es del orden de microsegundos (< 10 µs).
   - Ejecutar `RemoveByTag` sincrónicamente dentro de `OnCambio` en el hilo de UI es completamente seguro y asegura que cualquier interacción subsiguiente del usuario encuentre la caché invalidada.
2. **Desacoplamiento de Logs y Diagnóstico mediante `Channel<T>`:**
   - ⚠️ **Riesgo:** Si `InvalidadorCacheRealtime` realiza operaciones pesadas en el hilo de UI —tales como serialización de payloads JSON (`cambio.Valores`), formateo complejo de cadenas, escritura síncrona en archivos de log vía Serilog, o emisión de telemetría—, generará retrasos en la cola del Dispatcher (*frame drops* o congelamientos de animaciones).
   - ✅ **Mitigación requerida:** Se debe incorporar una cola desacoplada basada en `System.Threading.Channels.Channel<CambioRealtime>` o `Channel<LogEventoCache>`:
     ```csharp
     // En el hilo de UI (OnCambio):
     _cache.RemoveByTag(etiqueta); // Inmediato
     _logChannel.Writer.TryWrite(new InvalidationLogEntry(tabla, tag, DateTime.UtcNow)); // O(1) sin contención

     // En tarea en segundo plano (Background Worker):
     await foreach (var log in _logChannel.Reader.ReadAllAsync(ct))
     {
         Serilog.Log.Information("Caché invalidada para tabla {Tabla} con tag {Tag}", log.Tabla, log.Tag);
     }
     ```

#### Concurrencia y Orden de Eventos
- **Eventos de UI vs Eventos de Realtime:** Debido a que tanto los eventos de entrada de usuario (clics en botones, aperturas de modales) como los callbacks de `_syncContext.Post` transitan por la cola FIFO del `Dispatcher` de WPF:
  - Si un evento Realtime llega al socket antes de que el usuario haga clic en una lupa, el callback de invalidación se encola antes del mensaje de clic. Al procesarse el clic, la caché ya está invalidada.
  - Si una operación de fondo (ej. lectura desde un hilo secundario) consulta FusionCache, FusionCache es totalmente *thread-safe* y responderá con el estado actual de la memoria.

---

## 4. Ciclo de Vida de la Aplicación y Mecanismo Concreto de Fuga entre Usuarios (P-048)

### 4.1. Escenario Operativo Real en Planta
En las plantas de producción de Bimbo Honduras, una estación de pesaje o terminal de supervisión opera de forma continua en una misma computadora física.
- Múltiples operadores o supervisores inician y cierran sesión a lo largo de los turnos de trabajo sin reiniciar el sistema operativo ni cerrar el proceso ejecutable de la aplicación.

### 4.2. La Arquitectura Estática de Inyección de Dependencias (`App.xaml.cs`)
En `CapaUI/App.xaml.cs`:
```csharp
// Líneas 38-39:
private static IServiceProvider? _services;
public static IServiceProvider Services => _services ??= ConfigureServices();
```
- `_services` se inicializa una sola vez y permanece vivo durante toda la ejecución del proceso Windows.
- Cuando un usuario cierra sesión (`MainWindow.HandleCerrarSesionAsync`):
  ```csharp
  // Líneas 144-150:
  private static void OnSesionCerrada(object? s, EventArgs e)
  {
      _mainActual!.SesionCerrada -= OnSesionCerrada;
      _mainActual = null;
      MostrarLogin();
  }
  ```
  - `_mainActual` se destruye y se vuelve a mostrar `LoginWindow`.
  - **El contenedor `App.Services` NO se recrea ni se limpia.**
  - Todos los servicios registrados como `Singleton` en `DependencyInjection.cs` y `App.xaml.cs` (`RealtimeService`, `ConexionMonitor`, `PerfilUsuarioService`, `UsuarioSesionService`, `EmpresaThemeService`, etc.) continúan residiendo en memoria con sus instancias originales.

### 4.3. Omisión en `LimpiarRecursosAsync()` (`MainWindow.xaml.cs`)
En `CapaUI/Formularios/Principal/MainWindow.xaml.cs` (líneas 660-691):
```csharp
private async Task LimpiarRecursosAsync()
{
    try
    {
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
        await client.Auth.SignOut();
    }
    catch (OperationCanceledException) { ... }
    catch (Exception ex) { ... }

    CapaUI.Core.Permisos.SesionPermisos.Limpiar();
    _sesionService.CerrarSesion();

    _hwndSource?.RemoveHook(WndProc);
    _hwndSource = null;
    Vm.CierreRequerido -= OnCierreRequerido;
    Vm.Dispose();

    _conexionMonitor.Detener();
    await _realtimeService.DesconectarAsync();
}
```

#### Análisis Forense de lo que se Ejecuta vs lo que se Omite
| Componente | Acción en `LimpiarRecursosAsync()` | ¿Queda Limpio? |
|---|---|---|
| Supabase Auth | `client.Auth.SignOut()` | ✅ Sí (cierra sesión en backend) |
| Permisos Estáticos | `SesionPermisos.Limpiar()` | ✅ Sí (limpia caché estática de permisos UI) |
| Sesión de Usuario | `_sesionService.CerrarSesion()` | ✅ Sí (anula `_sesion` y limpia `_perfilService`) |
| ViewModel Principal | `Vm.Dispose()` | ✅ Sí (cancela timers y suscripciones de VM) |
| Monitor de Red | `_conexionMonitor.Detener()` | ✅ Sí (apaga temporizador de sondeo) |
| WebSocket Realtime | `_realtimeService.DesconectarAsync()` | ✅ Sí (cierra canales y resetea socket) |
| **`CatalogoCache`** | **NINGUNA** | 🔴 **NO. Persiste intacto en memoria.** |
| **`RolPermisoRepository`** | **NINGUNA** | 🔴 **NO. `_catalogoCache` estático persiste intacto.** |
| **`App.Services` (Root DI)** | **NINGUNA** | 🔴 **NO. Contenedor inmutable a nivel de proceso.** |

### 4.4. La Mecánica Exacta de la Fuga de Datos (P-048)
1. **Paso 1:** El Operador A (ej. Administrador o Supervisor de Calidad) inicia sesión. Navega por Productos, Pesaje y Roles.
   - `CatalogoCache._cache` se puebla con los catálogos completos de `"proveedores"`, `"fabricantes"`, `"categorias"`, `"productos"`, etc.
   - `RolPermisoRepository._catalogoCache` se puebla con la lista de módulos y acciones del sistema.
2. **Paso 2:** El Operador A cierra sesión.
   - `LimpiarRecursosAsync()` ejecuta `_realtimeService.DesconectarAsync()`. Esto **destruye el WebSocket de Realtime**.
   - `CatalogoCache.InvalidarTodo()` **NO es invocado**.
   - `RolPermisoRepository._catalogoCache` **NO es reseteado**.
3. **Paso 3:** Mientras la aplicación está en la pantalla de Login (`LoginWindow`), se realizan cambios en la base de datos (por ejemplo, otro puesto de trabajo modifica un proveedor o desactiva una categoría).
   - Como el WebSocket fue cerrado en `DesconectarAsync()`, **ningún evento Realtime es recibido ni procesado**.
4. **Paso 4:** El Operador B (ej. Operador de Pesaje con permisos restringidos) inicia sesión en la misma terminal.
   - El Operador B abre un selector (`SelectorCatalogoModal`).
   - `SelectorCatalogoModal` invoca `CatalogoCache.ObtenerCompletoAsync`.
   - `CatalogoCache` localiza la clave en `_cache.TryGetValue` y **retorna de inmediato en 0 ms los datos cargados por el Operador A**.
   - Si existieran políticas de seguridad RLS o particionamiento de catálogos por rol/área, el Operador B visualiza directamente los datos del Operador A.
   - Si la revalidación de fondo falla o se cancela, la vista nunca se corrige.
5. **Paso 5:** En el caso de `RolPermisoRepository._catalogoCache`, al ser un campo estático sin expiración ni método de invalidación, el Operador B recibe exactamente la misma referencia en memoria del catálogo RBAC cargada por el Operador A.

---

## 5. Validación Adversarial de Trampas Técnicas en el Código Vivo

| Trampa / Regla | Estado en Código Vivo | Riesgo Detectado | Mitigación Requerida en ADR-026 |
|---|---|---|---|
| **1. `ICacheService` Singleton** | Inexistente hoy (`CatalogoCache` es clase estática; `AddDataLayer` no registra caché) | Si el implementador registra `services.AddTransient<ICacheService>()`, cada ViewModel recibe su propia instancia vacía y la caché queda inoperante en silencio. | Registrar obligatoriamente `services.AddSingleton<ICacheService, FusionCacheService>()`. |
| **2. Registro de Repositorio Concreto por Tipo** | `CatalogoRepository` se registra únicamente como `ICatalogoRepository` (`DependencyInjection.cs:69-70`). | Si el decorador de caché intenta resolver `sp.GetRequiredService<ICatalogoRepository>()`, se produce recursión infinita (`StackOverflowException`). | Registrar `services.AddTransient<CatalogoRepository>()` y registrar la interfaz mediante fábrica delegada. |
| **3. `CancellationToken.None` en Fábrica** | `SelectorCatalogoModal` usa `_ctsVida.Token` (`SelectorCatalogoModal.xaml.cs:159`). | Si el decorador pasa el `ct` del llamador al método `GetOrSetAsync` de FusionCache, cerrar el modal cancela la tarea compartida de single-flight para todos los demás consumidores. | La llamada interna a `_innerRepo.Get*Async` dentro de la fábrica de FusionCache debe usar `CancellationToken.None` o token desacoplado. |
| **4. Retiro de `alRevalidar`** | `CatalogoCache` expone `Action<IReadOnlyList<FiltroItem>>? alRevalidar` en sus dos métodos públicos. | Desaparición del repintado en caliente en UI. | Asumir el cambio como observable y beneficioso; la reactividad de Realtime sobre las 8 tablas publicadas garantiza la frescura en el momento de la consulta. |
| **5. Tablas No Publicadas en Realtime** | `contactos_fabricante` y `contactos_proveedor` son escuchadas con `Observar()` en `ContactosFabricantesViewModel` y `ContactosProveedoresViewModel` (líneas 127). | Ambas tablas **no están en `pg_publication_tables`**. Las suscripciones son no-ops silenciosos (deuda `P-049`). | No depender de Realtime para tablas fuera de las 8 publicadas. Limitar invalidación reactiva exclusivamente a las 8 tablas publicadas confirmadas. |
| **6. No-serialización de `Result<T>`** | `Result<T>` (`CapaAplicacion4/Common/Result.cs`) tiene constructores privados y no soporta serialización limpia JSON. | Si se usara L2 distribuido o serialización a disco, fallaría al deserializar `Result<T>`. | Blindar L1 exclusivamente en memoria de proceso (objetos nativos .NET sin serialización). Descarte definitivo de L2. |
| **7. Aislamiento de Permisos** | `UsuarioSesionService` almacena permisos en memoria de sesión (`_sesion`). | Si se cachean resultados dependientes de permisos en una clave global sin IdUsuario, ocurre fuga horizontal de privilegios. | Prohibición estricta de almacenar objetos derivados de `IUsuarioSesionService.SesionActual` en la caché de catálogos. |
| **8. Resincronización tras Reconexión** | `RealtimeService` detecta reconexión en `AddStateChangedHandler` (línea 157). | Durante una caída del WebSocket pueden haberse perdido eventos de mutación en la base de datos. | En el evento de reconexión (`SocketState.Reconnect` o `OnReconectado`), `InvalidadorCacheRealtime` debe purgar todas las etiquetas de catálogos (`RemoveByTag`). |

---

## 6. Conclusiones y Especificaciones para ADR-026

1. **Reemplazo total de `CatalogoCache`:**
   `CatalogoCache.cs` debe ser retirado o refactorizado hacia un facade que delegue en `ICacheService` (FusionCache). La mecánica *stale-while-revalidate* con `alRevalidar` debe suprimirse en favor de la invalidación reactiva por eventos de Realtime.
2. **Resolución de Deuda Técnica P-048:**
   Es mandatorio incorporar en `MainWindow.LimpiarRecursosAsync()` la llamada explícita a invalidar toda la caché (`cache.ClearAsync()` o `InvalidarTodo()`).
   Asimismo, el campo estático `RolPermisoRepository._catalogoCache` debe ser refactorizado para residir en FusionCache bajo una etiqueta `roles` / `permisos`, permitiendo su invalidación controlada al cerrar sesión.
3. **Manejo Seguro de Hilos en `InvalidadorCacheRealtime`:**
   Dado que `RealtimeService` despacha en el hilo de UI, `InvalidadorCacheRealtime` ejecutará la invalidación L1 en dicho hilo de forma síncrona y ultrarrápida, pero canalizará todo registro de bitácora mediante `System.Threading.Channels.Channel<T>` hacia un trabajador en segundo plano.
