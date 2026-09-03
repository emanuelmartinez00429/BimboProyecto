# Handoff Report — Explorer 1 (R1: Auditoría Adversarial de Código Vivo)

**Fecha:** 2026-09-03  
**Agente:** Explorer 1 (`teamwork_preview_explorer`)  
**Tipo de Handoff:** Hard (Tarea de auditoría de código completa)  
**Destinatario:** Orquestador (`parent`, `985fffaf-dd63-4fd9-b8bc-738eb2b2f0d1`)  
**Documento de Análisis Principal:** `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r1_code\analysis.md`  

---

## 1. Observation (Observaciones Directas)

1. **Implementación actual de `CatalogoCache.cs` (`CapaUI/Core/Catalogos/CatalogoCache.cs`):**
   - Línea 28: Clase estática: `public static class CatalogoCache`.
   - Línea 30: Almacén estático: `private static readonly ConcurrentDictionary<string, IReadOnlyList<FiltroItem>> _cache = new();`.
   - Líneas 51-57: Devuelve cacheado de inmediato y revalida en segundo plano si `alRevalidar != null`:
     `if (_cache.TryGetValue(cfg.Clave, out var cacheado)) { if (alRevalidar is not null) _ = RevalidarAsync(cfg, umbral, cacheado, alRevalidar, ct); return Result<IReadOnlyList<FiltroItem>?>.Ok(cacheado); }`.
   - Línea 119: Pasa el `CancellationToken ct` del llamador a la consulta: `var r = await cfg.Cargar(string.Empty, 1, umbral, ct); if (!r.Success || ct.IsCancellationRequested) return;`.
   - Líneas 170-178: Métodos de invalidación: `Invalidar(string clave)` y `InvalidarTodo() => _cache.Clear();`.

2. **Uso en `SelectorCatalogoModal.xaml.cs` (`CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`):**
   - Línea 74: Token de ciclo de vida del modal: `private readonly CancellationTokenSource _ctsVida = new();`.
   - Líneas 156-160: Invocación a la caché pasando `_ctsVida.Token`:
     ```csharp
     var r = await CatalogoCache.ObtenerCompletoAsync(
         _cfg, UmbralMemoria,
         alRevalidar: lista => { if (!_dispuesto) PintarEnMemoria(lista, preservarSeleccion: true); },
         _ctsVida.Token);
     ```
   - Líneas 588-589: En `Dispose()` (cierre del modal):
     `_ctsVida.Cancel(); _ctsVida.Dispose();`.

3. **Caché estática en `RolPermisoRepository.cs` (`CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs`):**
   - Línea 23: `private static IReadOnlyList<ModuloAccionesDto>? _catalogoCache;`.
   - Línea 24: `private static readonly SemaphoreSlim CatalogoLock = new(1, 1);`.
   - Líneas 84-130: `CargarCatalogoAsync(ct)` puebla `_catalogoCache` bajo doble chequeo. **No existe ningún método público ni privado en toda la clase para invalidar o limpiar este campo.**

4. **Despacho de eventos en `RealtimeService.cs` (`CapaDatos/Realtime/RealtimeService.cs`):**
   - Línea 60: Constructor captura el contexto del hilo UI: `_syncContext = SynchronizationContext.Current;`.
   - Línea 171: Suscripción a Supabase: `channel.AddPostgresChangeHandler(ListenType.All, (_, change) => OnCambioRecibido(clave, tabla, change));`.
   - Líneas 194-202 y 262-269: El método `DespacharEnUIThread` traslada la ejecución de **todos los suscriptores** al hilo de UI mediante `_syncContext.Post(_ => accion(), null)`.

5. **Tablas no publicadas con `Observar()` activo:**
   - `ContactosFabricantesViewModel.cs:127`: `Observar("contactos_fabricante", OnCambioContacto);`.
   - `ContactosProveedoresViewModel.cs:127`: `Observar("contactos_proveedor", OnCambioContacto);`.
   - Ambas tablas **no están presentes en `pg_publication_tables` de `supabase_realtime`** (confirmado empíricamente por la consulta SQL de auditoría).

6. **Ciclo de vida en `MainWindow.xaml.cs` y `App.xaml.cs`:**
   - `App.xaml.cs:39`: Contenedor Singleton estático: `public static IServiceProvider Services => _services ??= ConfigureServices();`.
   - `MainWindow.xaml.cs:660-691`: `LimpiarRecursosAsync()` ejecuta `client.Auth.SignOut()`, `SesionPermisos.Limpiar()`, `_sesionService.CerrarSesion()`, `Vm.Dispose()`, `_conexionMonitor.Detener()`, y `_realtimeService.DesconectarAsync()`.
   - **En ningún punto de `LimpiarRecursosAsync()` ni de `MainWindow` se invoca `CatalogoCache.InvalidarTodo()`, ni se limpia `RolPermisoRepository._catalogoCache`, ni se reconstruye `App.Services`.**

---

## 2. Logic Chain (Cadena Lógica de Razonamiento)

1. **Fuga de datos entre usuarios (P-048):**
   - De (1), `CatalogoCache` almacena datos en un `ConcurrentDictionary` estático a nivel de proceso.
   - De (3), `RolPermisoRepository` retiene módulos y acciones en un campo `private static` inalterable.
   - De (6), `App.Services` es un contenedor estático a nivel de proceso que nunca se destruye ni reconstruye al cerrar sesión, y `LimpiarRecursosAsync()` omite explícitamente llamar a `CatalogoCache.InvalidarTodo()` o resetear cachés.
   - *Inferencia:* Cuando el Operador A cierra sesión y el Operador B inicia sesión en la misma computadora física de planta sin reiniciar el ejecutable, las consultas de catálogos y permisos del Operador B golpean la memoria estática y leen de inmediato (0 ms) los datos cargados por el Operador A. Si además hubo modificaciones en la BD mientras la app mostraba la pantalla de Login, el WebSocket estaba desconectado (por `DesconectarAsync()`), por lo que no se recibió ningún evento de invalidación.

2. **Riesgo de micro-stutters en el UI Thread durante invalidación Realtime:**
   - De (4), `RealtimeService` enruta cada evento recibido en el socket WebSocket hacia el hilo de UI mediante `_syncContext.Post`.
   - De la arquitectura propuesta en ADR-026, `InvalidadorCacheRealtime` se suscribe a `IRealtimeService` y recibe los eventos en el hilo de UI.
   - *Inferencia:* Invocar `_cache.RemoveByTag(tag)` en el hilo de UI es seguro por durar microsegundos. Sin embargo, si `InvalidadorCacheRealtime` realiza serializaciones JSON, formateo de logs o I/O síncrono a disco con Serilog en ese mismo callback, causará micro-bloqueos en la interfaz gráfica. Se requiere desacoplar el procesamiento de bitácora mediante `System.Threading.Channels.Channel<T>` consumido por una tarea en segundo plano.

3. **Riesgo de cancelación prematura en single-flight (Trampa 3):**
   - De (2), `SelectorCatalogoModal` crea un token de vida `_ctsVida` que se cancela tan pronto como el modal se cierra en `Dispose()`.
   - Si el decorador de FusionCache utiliza el `ct` del llamador dentro de la lambda de fábrica (`factory(ct)` de `GetOrSetAsync`), y dos componentes en paralelo solicitan el mismo catálogo:
   - *Inferencia:* Si el usuario cierra el modal velozmente, la cancelación de `_ctsVida` abortará la tarea compartida de la fábrica, provocando `OperationCanceledException` en el otro llamador no cancelado. La fábrica debe pasar `CancellationToken.None` a la consulta de red.

4. **Riesgo de recursión infinita en DI (Trampa 2):**
   - En `CapaDatos/DependencyInjection.cs`, `CatalogoRepository` se registra como `services.AddTransient<ICatalogoRepository, CatalogoRepository>()`.
   - Si se añade un decorador `CachedCatalogoRepository(ICatalogoRepository inner, ...)` registrado mediante `services.AddTransient<ICatalogoRepository>(sp => new CachedCatalogoRepository(sp.GetRequiredService<ICatalogoRepository>()))`:
   - *Inferencia:* `sp.GetRequiredService<ICatalogoRepository>()` se llamará recursivamente a sí mismo hasta agotar la pila de llamadas (`StackOverflowException`). Se debe registrar `CatalogoRepository` por su tipo de clase concreto.

---

## 3. Caveats (Advertencias y Supuestos)

1. **Estado de publicación de tablas:** Se asume verificada la consulta empírica a `pg_publication_tables` del 2026-09-03 que demostró que las 8 tablas de catálogos están publicadas (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`), mientras que tablas satélite como `contactos_fabricante` y `contactos_proveedor` no lo están.
2. **Alcance de lectura:** No se examinó el código de pruebas de integración que dependa de `CatalogoCache` estático; sin embargo, `Plan de Tests Unitarios.md` ya identifica que las cachés estáticas requieren `[CollectionDefinition]` para evitar carreras en tests paralelos.
3. **Restricción de modificación:** En concordancia con el principio de integridad arquitectónica, este handoff es de solo lectura y no incluye cambios a código fuente (.cs ni .xaml).

---

## 4. Conclusion (Conclusiones Técnicas)

1. **Aceptación de la Propuesta ADR-026:** Reemplazar `CatalogoCache.cs` por `FusionCache L1` con invalidación reactiva por Supabase Realtime elimina el costo de 1 consulta innecesaria por apertura de lupa (heredada de ADR-015) y proporciona frescura verificada por eventos.
2. **Corrección Mandatoria de P-048:** En la Fase de implementación de ADR-026, es mandatorio agregar la invalidación total de caché (`cache.ClearAsync()`) en `MainWindow.LimpiarRecursosAsync()` y encapsular `RolPermisoRepository._catalogoCache` para que no sobreviva entre sesiones en terminales compartidas de planta.
3. **Diseño de Concurrencia Validado:** `InvalidadorCacheRealtime` debe operar como suscriptor de vida larga a nivel de aplicación, invocando `RemoveByTag` en el hilo despachado y escribiendo los eventos de auditoría/diagnóstico en un `Channel<T>` desacoplado.
4. **Blindaje ante Errores Silenciosos:** El ADR-026 debe incorporar formalmente las mitigaciones para las trampas de DI (Singleton para `ICacheService`, registro por tipo concreto de `CatalogoRepository`, y uso de `CancellationToken.None` en la fábrica de red).

---

## 5. Verification Method (Método de Verificación Independiente)

1. **Inspección de archivos y líneas en el repositorio vivo:**
   - `CapaUI/Core/Catalogos/CatalogoCache.cs`: Líneas 28-30 (campo estático), 51-57 (stale-while-revalidate), 177 (método `InvalidarTodo`).
   - `CapaDatos/Repositories/Catalogos/CatalogoRepository.cs`: Líneas 25 (clase concreta), 230-279 (`PagedInternalAsync`).
   - `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`: Líneas 74 (`_ctsVida`), 156-160 (`ObtenerCompletoAsync`), 588 (`Dispose`).
   - `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs`: Líneas 23-24 (`_catalogoCache` y `CatalogoLock`).
   - `CapaDatos/Realtime/RealtimeService.cs`: Líneas 60 (`_syncContext`), 171-172 (`AddPostgresChangeHandler`), 262-269 (`DespacharEnUIThread`).
   - `CapaUI/Formularios/Principal/MainWindow.xaml.cs`: Líneas 660-691 (`LimpiarRecursosAsync`).
   - `CapaUI/App.xaml.cs`: Líneas 38-39 (`Services` estático).
2. **Comando de comprobación de compilación del proyecto:**
   ```powershell
   dotnet build d:\Proyectos\Proyecto de BIMBO\BimboProyecto\BimboProyecto.sln
   ```
   (Debe finalizar con 0 errores y 0 advertencias nuevas, garantizando que el árbol de código permanece intacto).
3. **Condición de Invalidación:**
   Si la base de datos de producción revocara la publicación de cualquiera de las 8 tablas de catálogos de `supabase_realtime`, la premisa de frescura reactiva se invalidaría y requeriría volver a evaluar TTLs conservadores.
