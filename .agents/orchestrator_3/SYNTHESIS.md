# Síntesis de Exploración Técnica R1-R4 (ADR-026)

## 1. R1: Auditoría Adversarial de Código Vivo
- `CatalogoCache.cs` (`CapaUI/Core/Catalogos/CatalogoCache.cs`): Clase estática con `ConcurrentDictionary<string, IReadOnlyList<FiltroItem>>` global de proceso. En cada apertura ejecuta `RevalidarAsync` consultando a Supabase. Cada apertura de lupa consume 1 round-trip innecesario.
- `SelectorCatalogoModal.xaml.cs`: `_ctsVida` privado gobierna la vida del modal. Al cerrar el modal se llama `_ctsVida.Cancel()`. Si la fábrica del decorador usa este token, aborta la carga compartida en single-flight para otros llamadores.
- `RolPermisoRepository.cs`: `_catalogoCache` es un campo estático privado con `CatalogoLock` (SemaphoreSlim). No tiene método de invalidación ni limpieza.
- `RealtimeService.cs`: Despacha todos los callbacks a través de `_syncContext.Post`, es decir, en el hilo de UI de WPF.
- `InvalidadorCacheRealtime`: La invalidación en memoria con `FusionCache.RemoveByTag` es inmediata (<10 µs) y segura en el hilo de UI. El registro de bitácora y telemetría debe desacoplarse mediante un `System.Threading.Channels.Channel<T>` consumido en segundo plano.
- **P-048 (Fuga entre usuarios en terminal compartida):** `MainWindow.LimpiarRecursosAsync()` ejecuta `SignOut` y limpia `SesionPermisos`, pero **nunca invoca `CatalogoCache.InvalidarTodo()`** ni resetea `RolPermisoRepository._catalogoCache`. `App.Services` es un root provider estático que persiste en el proceso. Un nuevo usuario en la misma PC hereda catálogos y permisos del usuario previo en 0 ms.

## 2. R2: Las 12 Trampas Neutralizadas
1. `ICacheService` Singleton: Si fuera Transient, cada ViewModel recibe una caché vacía (0% hit rate silencioso).
2. Registro concreto por tipo: `services.AddTransient<CatalogoRepository>()`, y la interfaz `ICatalogoRepository` se registra resolviendo el decorador `CachedCatalogoRepository`. Si se registra por interfaz, `sp.GetRequiredService<ICatalogoRepository>()` dentro de la factoría causa recursión infinita (`StackOverflowException`).
3. `CancellationToken.None` en fábrica: La consulta subyacente a la base de datos dentro de la lambda de FusionCache debe usar `CancellationToken.None` para que cerrar un modal no cancele la petición compartida para otras pantallas en single-flight.
4. Retiro de `alRevalidar`: Desaparición del repintado en caliente en UI (`Dg.ItemsSource`). Es el único cambio observable para el usuario, justificado por la publicación al 100% de las 8 tablas en Realtime.
5. Tablas no publicadas: `contactos_fabricante` y `contactos_proveedor` no están publicadas en `supabase_realtime` en Postgres. Suscripciones con `Observar()` son no-ops silenciosos (deuda `P-049`).
6. No-serialización de `Result<T>`: Constructores privados impiden serialización JSON con `System.Text.Json`. L1 opera en RAM con referencias tipadas sin serializar.
7. Aislamiento de permisos: Prohibición estricta de almacenar objetos derivados de `IUsuarioSesionService.SesionActual`.
8. Resincronización tras reconexión: En `OnReconectado` / `OnReconexionAsync`, purgar etiquetas de catálogos (`RemoveByTagAsync("catalogos")`).
9. Límites de memoria L1: Sin `SizeLimit` en MemoryCache para evitar excepciones por omisión de tamaño; control mediante umbral de elementos (`UmbralMemoria = 200`).
10. Anti-stampede con Jitter: Jitter de 10-15 min sobre TTL base de 2 h.
11. Duración de Fail-Safe: 24 h a 7 días con throttle de 30 s ante errores de red.
12. Zonas Zero-Cache: Prohibición estricta en Pesaje (`movimientos`, `movimiento_productos`, `entradas_producto`), Bitácora (`bitacora`, `bitacora_pesaje`), Notificaciones (`notificaciones_usuario`), Reportes operativos y Sesión de usuario activo.

## 3. R3: Dependencias NuGet Fijadas
- `ZiggyCreatures.FusionCache [2.0.2]` es la versión óptima y canónica para `.NET 8 LTS` (`net8.0` / `net8.0-windows`).
- Soporta nativamente `RemoveByTag`, `RemoveByTagAsync`, `ClearAsync(allowFailSafe: false)`.
- Dependencia transitiva estricta en `Microsoft.Extensions.Caching.Memory [8.0.1, )`, compatible con `Microsoft.Extensions.DependencyInjection 8.0.1` ya presente en `CapaUI` y `CapaDatos`.
- NO arrastra dependencias de la línea 9.x ni paquetes en preview.

## 4. R4: Estructura Obsidian y Reglas de la Bóveda
- Frontmatter estricto de ADR-026:
  ```yaml
  ---
  title: "ADR-026 — Caché en memoria con FusionCache e invalidación por Realtime"
  tags:
    - adr
    - decision
    - cache
    - realtime
    - rendimiento
  date: 2026-09-02
  estado: propuesto
  ---
  ```
  (Sin campos `autor:` ni `autor_cambios:`).
- `ADR-015`: Permanece estrictamente intacto con `estado: aceptado`.
- Alcance de escritura permitido (3 archivos exactos):
  1. `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`
  2. `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (agregar P-048 y P-049 en el cuerpo y tabla).
  3. `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` (callout descriptivo y enlace en relaciones).
- Cero código alterado (.cs, .xaml, .csproj, .sln).
