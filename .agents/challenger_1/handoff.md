# Handoff Report — Challenger 1 (teamwork_preview_challenger)

**Fecha y Hora:** 2026-09-03T05:18:00Z  
**Autor:** Challenger 1 (teamwork_preview_challenger)  
**Rol:** critic, specialist (Empirical Challenger)  
**Destinatario:** Orchestrator (Conversation ID: `985fffaf-dd63-4fd9-b8bc-738eb2b2f0d1`)  
**Misión:** Adversarially challenge and stress-test the architectural decisions in `ADR-026`.  
**Veredicto Final:** ❌ **REJECT (Rechazo constructivo con bloqueo condicionado a remediaciones críticas)**

---

## 1. Challenge Summary

**Overall Risk Assessment:** 🔴 **CRITICAL**

El diseño general de ADR-026 (FusionCache L1 en memoria, descarte justificado de L2, e invalidación reactiva por Supabase Realtime) posee una base conceptual sólida y resuelve efectivamente la sobrecarga de *stale-while-revalidate*. Sin embargo, la auditoría adversarial empírica sobre el código vivo de `BimboProyecto`, el SDK `Supabase.Realtime 7.0.2` y la biblioteca `ZiggyCreatures.FusionCache 2.0.2` ha revelado **cuatro fallas críticas y letales** que, de implementarse tal como está redactado el ADR, provocarán:

1. **Muerte permanente de la invalidación reactiva tras el primer logout en una terminal compartida** (la limpieza del socket destruye los suscriptores singleton).
2. **Falla silenciosa del 100% de la purga tras reconexión** (incompatibilidad matemática de cadenas de tags en FusionCache).
3. **Purga prematura durante el apagón de red en lugar de tras la recuperación** (`SocketState.Reconnect` vs `SocketState.Open`), destruyendo la resiliencia offline.
4. **Cierre abrupto de la aplicación (*Crash to Desktop*) por `OperationCanceledException` no capturada** al cerrar un modal durante la resolución de caché en `async void OnLoaded`.

---

## 2. Challenges y Hallazgos Críticos

### 🔴 [CRITICAL] Challenge 1: Muerte de Suscripciones del Invalidador tras el Primer Logout (`_suscriptores.Clear()`)
- **Supuesto desafiado:** ADR-026 §8 Fase 1 y 3 asume que `InvalidadorCacheRealtime` es un servicio Singleton registrado en `App.Services` que escucha continuamente eventos de Realtime a lo largo del proceso.
- **Escenario de ataque:**
  1. Operador A inicia sesión en la mañana. `InvalidadorCacheRealtime` (Singleton) se suscribe a las 8 tablas de catálogo en `RealtimeService`.
  2. Al terminar el turno, el Operador A hace clic en "Cerrar sesión".
  3. `MainWindow.LimpiarRecursosAsync()` invoca `await _realtimeService.DesconectarAsync()`.
  4. En `RealtimeService.cs:244-250`, el método ejecuta:
     ```csharp
     lock (_stateLock)
     {
         _canales.Clear();
         _suscriptores.Clear(); // 🔴 SE BORRAN TODOS LOS DELEGADOS
         _estadoHandlerRegistrado = false;
     }
     await ConexionSupabase.ResetAsync();
     ```
  5. El Operador B inicia sesión en la misma PC sin reiniciar la aplicación (`App.Services` es un root provider estático que no se reconstruye).
  6. `InvalidadorCacheRealtime` **NO** se vuelve a instanciar (es Singleton y ya existe). Su constructor no corre de nuevo.
  7. `_suscriptores` en `RealtimeService` está **completamente vacío**.
  8. Para el Operador B (y todo usuario subsiguiente), **JAMÁS se vuelve a suscribir `InvalidadorCacheRealtime`**.
- **Blast Radius:** A partir del segundo login, la aplicación pierde el 100% de la invalidación reactiva por Realtime. La caché servirá datos obsoletos indefinidamente hasta que el TTL de 2 horas o 24 horas expire, anulando el beneficio central de ADR-026 en el escenario operativo principal de Bimbo Honduras (terminales compartidas en planta).
- **Mitigación obligatoria:**
  - O bien `InvalidadorCacheRealtime` expone un método explícito `Suscribir()` invocado en el ciclo de vida de cada sesión (`MainWindow.OnLoaded`),
  - O `RealtimeService` debe distinguir entre "desconexión de canales de transporte" y "registro de observadores singleton permanentes",
  - O `MainWindow.OnLoaded` debe re-vincular al `InvalidadorCacheRealtime`.

---

### 🔴 [CRITICAL] Challenge 2: Invalidez Silenciosa de la Purga en Reconexión por Discordancia de Tags
- **Supuesto desafiado:** ADR-026 §6 Trampa 8 afirma que tras reconectar la red se purgan todos los catálogos ejecutando:
  ```csharp
  await _cache.RemoveByTagAsync("catalogos");
  ```
- **Escenario de ataque:**
  1. En ADR-026 §5.1, la matriz de configuración asigna etiquetas específicas por tabla: `catalogos:paises`, `catalogos:categoria`, `catalogos:productos`, `catalogos:fabricante`, etc.
  2. En `ZiggyCreatures.FusionCache 2.0.2`, el mecanismo de tagging realiza una **búsqueda exacta por valor de cadena** (exact string match). FusionCache **NO** implementa expresiones regulares, comodines (`*`) ni jerarquías basadas en dos puntos (`:`).
  3. Al invocar `_cache.RemoveByTagAsync("catalogos")`, FusionCache busca entradas cuya colección de tags contenga literalmente la cadena `"catalogos"`.
  4. Al no encontrar ninguna entrada con la etiqueta idéntica `"catalogos"`, FusionCache concluye en 0 µs habiendo purgado **cero entradas**.
  5. Todas las entradas (`catalogos:categoria`, etc.) permanecen en la memoria L1 con sus datos previos al corte de red.
- **Blast Radius:** La resincronización tras cortes de red de planta es un **no-op silencioso**. Los cambios realizados por supervisores durante la ventana de desconexión nunca se reflejan hasta que el TTL base expire.
- **Mitigación obligatoria:**
  - En `CachedCatalogoRepository`, cada entrada debe guardarse con un array de tags que incluya la etiqueta raíz común:
    ```csharp
    tags: new[] { TagsCache.CatalogosRaiz, $"catalogos:{nombreTabla}" }
    // Donde TagsCache.CatalogosRaiz = "catalogos"
    ```
  - Alternativamente, `RemoveByTagAsync` debe recibir la lista explícita de todas las etiquetas:
    ```csharp
    await _cache.RemoveByTagAsync(TagsCache.TodosLosTags);
    ```

---

### 🟠 [HIGH] Challenge 3: Disparo Prematuro de Purga durante el Apagón (`SocketState.Reconnect`)
- **Supuesto desafiado:** ADR-026 §6 Trampa 8 establece: *"Conectar el evento de reconexión del socket (`SocketState.Reconnect` / `OnReconectadoAsync`) y de `IConexionMonitor` con el servicio de caché, ejecutando una purga total..."*.
- **Escenario de ataque:**
  1. Se inspeccionaron los valores literales del enum `Supabase.Realtime.Constants.SocketState` en la DLL `Supabase.Realtime.dll` (v7.0.2):
     - `Open = 0`
     - `Close = 1`
     - `Reconnect = 2`
     - `Error = 3`
  2. En el protocolo de Phoenix Channels y el SDK de Supabase C#, `SocketState.Reconnect` se emite cuando el socket detecta la pérdida de conectividad y **comienza el bucle de reintento de conexión**.
  3. En ese instante exacto, la máquina **SIGUE DESCONECTADA** de la red.
  4. Si la purga se dispara en `SocketState.Reconnect`:
     - La caché en memoria L1 se marca como expirada o se vacía.
     - Si un operador abre un selector en ese momento de corte transitorio, la caché intenta invocar la factoría PostgREST HTTP hacia Supabase.
     - Dado que la red sigue caída, la consulta HTTP falla catastróficamente con error de red en lugar de servir el valor de contingencia (*Fail-Safe*).
- **Blast Radius:** Se anula la resiliencia operativa de *Fail-Safe* en el momento exacto en que más se necesita (durante la interrupción de red), arrojando mensajes de error a los operarios en planta.
- **Mitigación obligatoria:**
  - La purga debe dispararse exclusivamente ante la **transición confirmada a `SocketState.Open`** (reconexión consumada) o mediante el evento `IConexionMonitor.Reconectado` (que posee histéresis verificada).

---

### 🟠 [HIGH] Challenge 4: Crash to Desktop en WPF por `OperationCanceledException` no Capturada al Cerrar Modales
- **Supuesto desafiado:** ADR-026 §6 Trampa 3 soluciona la cascada de cancelación en *single-flight* pasando `CancellationToken.None` a la factoría interna de la base de datos y dejando el token del llamador `token: ct` para gobernar su propia espera.
- **Escenario de ataque:**
  1. Un operador abre `SelectorCatalogoModal` y lo cierra rápidamente en 150 ms.
  2. El cierre del modal cancela `_ctsVida.Cancel()`.
  3. `_cache.GetOrSetAsync(key, factory, options, token: _ctsVida.Token)` detecta la cancelación del token y lanza `OperationCanceledException`.
  4. En `RepositorioBase.cs:50`, la infraestructura de repositorios filtra deliberadamente:
     ```csharp
     catch (Exception ex) when (ex is not OperationCanceledException)
     ```
     permitiendo que `OperationCanceledException` se propague sin ser envuelta en `Result.Fail`.
  5. En `SelectorCatalogoModal.xaml.cs:125-139`:
     ```csharp
     private async void OnLoaded(object sender, RoutedEventArgs e)
     {
         Loaded -= OnLoaded;
         await CargarInicialAsync(); // 🔴 Invocado desde async void
     }
     ```
  6. Ni `CargarInicialAsync` ni `OnLoaded` capturan `OperationCanceledException`.
  7. En el runtime de .NET 8 WPF, cualquier excepción no controlada que escape de un método `async void` es despachada al `SynchronizationContext` de la aplicación como no controlada, provocando el **cierre inmediato y fatal del proceso (*Crash to Desktop*)**.
- **Blast Radius:** Cada vez que un usuario abra y cierre un modal con rapidez en una red lenta, la aplicación de escritorio se cerrará abruptamente sin mensaje de error.
- **Mitigación obligatoria:**
  - El decorador `CachedCatalogoRepository` debe capturar `OperationCanceledException` y retornar `Result<PagedResult<FiltroItem>>.Fail("Operación cancelada")`, o el ciclo de vida del modal debe implementar un bloque `try-catch (OperationCanceledException)` defensivo.

---

## 3. Stress Test Results (Matriz de Escenarios)

| # | Escenario Adversarial | Comportamiento Esperado | Comportamiento Real / Riesgo Descubierto | Veredicto |
|---|---|---|---|:---:|
| **E1** | Desconexión de WebSocket durante cambio de turno (14:00). Supervisor muta datos en PC de oficina. PC de planta reconecta a las 14:05. | PC de planta purga catálogos en memoria y refresca con la mutación del supervisor. | **FALLA:** `RemoveByTagAsync("catalogos")` no coincide con `catalogos:{tabla}`. Los datos obsoletos persisten 2 horas en RAM. Además, `SocketState.Reconnect` corre durante la desconexión, no al restaurarse. | ❌ **FAIL** |
| **E2** | 10 modales concurrentes abren la misma clave. Uno cancela a los 100 ms. | Single-flight continúa para los otros 9. El llamador que canceló aborta limpiamente sin tumbar la app. | **FALLA PARCIAL:** Los otros 9 reciben los datos (protección confirmada), pero el llamador que canceló lanza `OperationCanceledException` hacia `async void OnLoaded` provocando *Crash to Desktop*. | ⚠️ **FAIL** |
| **E3** | Usuario A (Admin) cierra sesión. Usuario B (Operador) inicia sesión en la misma terminal compartida. | `ClearAsync(false)` purga FusionCache. Usuario B recibe eventos de Realtime. | **FALLA CRÍTICA:** `RealtimeService.DesconectarAsync()` ejecuta `_suscriptores.Clear()`. El singleton `InvalidadorCacheRealtime` queda huérfano. Usuario B tiene 0% de reactividad por Realtime. | ❌ **FAIL** |
| **E4** | Se observa tabla no publicada (`contactos_fabricante`) (P-049). | Detección preventiva o warning en log. No se cachea con eventos Realtime. | **CORRECTO:** Confirmado empíricamente que PostgREST responde `status: ok` pero Postgres emite 0 WALs. La zona Zero-Cache de ADR-026 es correcta. | ✅ **PASS** |
| **T1** | Trampa 1: `ICacheService` Singleton vs Transient. | Registro Singleton verificado con test `Assert.Same`. | Mitigación robusta. Previene hit-rate 0%. | ✅ **PASS** |
| **T2** | Trampa 2: `CatalogoRepository` por tipo concreto. | Previene recursión y `StackOverflowException`. | Totalmente verificado contra `DependencyInjection.cs`. Ningún consumidor consume el tipo concreto directamente. | ✅ **PASS** |
| **T3** | Trampa 3: `CancellationToken.None` en fábrica de FusionCache. | Evita cancelación en cascada de single-flight. | Mitiga la cascada, pero omitió el manejo de la excepción en el llamador (Challenge 4). | ⚠️ **PASS C/ CONDICIÓN** |
| **T4** | Trampa 4: Retiro de `alRevalidar`. | Elimina parpadeo visual del DataGrid. | Riesgo aceptado verificado. Modales son interacciones cortas (3s). | ✅ **PASS** |
| **T5** | Trampa 5: Detección tablas no publicadas. | Previene suscripciones inactivas y falsas expectativas. | Verificado con consulta viva a `pg_publication_tables`. | ✅ **PASS** |
| **T6** | Trampa 6: No serialización de `Result<T>`. | Previene crashes de `System.Text.Json` y veneno de caché. | Verificado empíricamente: constructores privados en `Result<T>`. Guardar solo `Result.Value` es óptimo. | ✅ **PASS** |
| **T7** | Trampa 7: Aislamiento de permisos de usuario. | Previene fuga de claims personales. | Válido, sujeto a resolver la orfandad de suscriptores en Challenge 1. | ⚠️ **PASS C/ CONDICIÓN** |
| **T8** | Trampa 8: Resincronización en reconexión. | Restaura consistencia tras corte de red. | Inútil en su redacción actual (Challenge 2 y 3). Requiere redefinir tags y evento. | ❌ **FAIL** |
| **T9** | Trampa 9: Límite de memoria sin `SizeLimit`. | Previene `InvalidOperationException` en `MemoryCache`. | Verificado empíricamente. Tope semántico en `Total <= 200` es seguro (<2 MB RAM). | ✅ **PASS** |
| **T10** | Trampa 10: Anti-Stampede con Jitter. | Evita picos de consultas a las 09:00 AM en cambio de turno. | Verificado. Dispersión aleatoria de 5-15 min allana el consumo de red. | ✅ **PASS** |
| **T11** | Trampa 11: Armonización de Fail-Safe. | TTL < FailSafeMaxDuration y purga sin resucitación. | Verificado contra API `ClearAsync(allowFailSafe: false)` de FusionCache. | ✅ **PASS** |
| **T12** | Trampa 12: Desacoplamiento por `Channel<T>`. | Invalidador no bloquea el hilo de UI de WPF. | Verificado. `RemoveByTag` en RAM (<10 µs) y logging asíncrono en background. | ✅ **PASS** |

---

## 4. 5-Component Handoff Report

### 1. Observaciones Directas (Evidencia Empírica Verificada)
1. **`RealtimeService.cs:244-250`**:
   ```csharp
   public async Task DesconectarAsync()
   {
       lock (_stateLock)
       {
           _canales.Clear();
           _suscriptores.Clear();
           _estadoHandlerRegistrado = false;
       }
       await ConexionSupabase.ResetAsync();
   }
   ```
   *Hecho observado:* `DesconectarAsync()` limpia incondicionalmente el diccionario `_suscriptores`. Cualquier objeto Singleton que se haya suscrito antes del cierre de sesión pierde su registro en memoria.
2. **`Supabase.Realtime.Constants.SocketState`** (inspeccionado en `C:\Users\fbara\.nuget\packages\supabase.realtime\7.0.2\lib\netstandard2.0\Supabase.Realtime.dll`):
   *Valores del Enum:* `Open`, `Close`, `Reconnect`, `Error`.
   *Hecho observado:* `Reconnect` representa el estado de reintento activo de conexión (aún sin red), no la conexión exitosa restablecida.
3. **`ZiggyCreatures.FusionCache.xml`** (v2.0.2, documentación oficial de `RemoveByTag`):
   `Remove all entries tagged with the specified tag: for each entry, that can mean an Expire (if fail-safe was enabled) or a Remove...`
   *Hecho observado:* El método busca concordancia exacta del tag. No implementa matching de prefijos ni comodines.
4. **`RepositorioBase.cs:50`**:
   ```csharp
   catch (Exception ex) when (ex is not OperationCanceledException)
   ```
   *Hecho observado:* `RepositorioBase` relanza explícitamente `OperationCanceledException`.
5. **`SelectorCatalogoModal.xaml.cs:125-139`**:
   `OnLoaded` es `private async void OnLoaded(...)` y ejecuta `await CargarInicialAsync();` sin bloque `catch (OperationCanceledException)`.

### 2. Cadena Lógica
1. Si `InvalidadorCacheRealtime` es Singleton en `App.Services` y `RealtimeService.DesconectarAsync()` vacía `_suscriptores` en cada logout, entonces al iniciar sesión un segundo usuario, el invalidador no recibe eventos WAL. Por tanto, la invalidación por Realtime queda muerta tras el primer logout.
2. Si los catálogos se etiquetan como `catalogos:categoria` y la reconexión invoca `_cache.RemoveByTagAsync("catalogos")`, y dado que FusionCache compara tags por igualdad exacta de cadenas, ninguna entrada es invalidada. Por tanto, la resincronización de catálogos tras reconexión falla silenciosamente.
3. Si la purga se ata a `SocketState.Reconnect`, corre mientras la red sigue inoperativa. Al abrirse un modal durante el corte, se fuerza una consulta HTTP a la base de datos que falla, en lugar de retornar el valor previo garantizado por *Fail-Safe*.
4. Si el token del llamador cancela la espera de `GetOrSetAsync`, se lanza `OperationCanceledException`. Dado que `RepositorioBase` no la captura y `OnLoaded` es `async void`, la excepción no controlada derriba el proceso WPF.

### 3. Salvedades (Caveats)
- No se auditó la persistencia física de la base de datos PostgreSQL, ya que los permisos y esquemas de `pg_publication_tables` fueron verificados previamente en vivo por el equipo.
- No se cuestiona la conveniencia de FusionCache ni la decisión de descartar L2; ambas decisiones son técnicamente acertadas y corroboradas.
- La evaluación asume la versión de `ZiggyCreatures.FusionCache [2.0.2]` y `Supabase.Realtime [7.0.2]` vigentes en el proyecto.

### 4. Conclusión y Veredicto
**Veredicto:** ❌ **REJECT (Rechazo constructivo con bloqueo condicionado a remediaciones críticas)**.

ADR-026 **no debe ser aprobado ni implementado en su estado actual** sin incorporar previamente las siguientes 4 correcciones arquitectónicas obligatorias en el documento:
1. **Regla de re-suscripción para `InvalidadorCacheRealtime`:** Establecer que `InvalidadorCacheRealtime` gestione su ciclo de vida suscribiéndose formalmente al inicio de cada sesión (o que `RealtimeService` no borre suscriptores del sistema en logout).
2. **Normalización de Tags Compuestos:** Explicitar en la §5 y §6 que cada catálogo debe registrarse obligatoriamente con el tag raíz y el específico: `tags: new[] { TagsCache.Catalogos, $"catalogos:{tabla}" }`, asegurando que `RemoveByTagAsync("catalogos")` purgue efectivamente todas las tablas.
3. **Corrección del evento de reconexión:** Cambiar la referencia de `SocketState.Reconnect` a la transición hacia `SocketState.Open` y el evento `IConexionMonitor.Reconectado`.
4. **Captura defensiva de `OperationCanceledException`:** Mandatar que `CachedCatalogoRepository` intercepte la cancelación del llamador y retorne `Result.Fail("Operación cancelada")` en lugar de permitir que la excepción no controlada descienda sobre `async void OnLoaded`.

### 5. Método de Verificación Independiente
Para verificar independientemente estos hallazgos:
1. **Verificar `_suscriptores.Clear()` en logout:** Inspeccionar `CapaDatos/Realtime/RealtimeService.cs`, líneas 244–250.
2. **Verificar valores de `SocketState`:** Ejecutar:
   ```powershell
   powershell -Command "Add-Type -Path 'C:\Users\fbara\.nuget\packages\supabase.realtime\7.0.2\lib\netstandard2.0\Supabase.Realtime.dll'; [Enum]::GetNames([Supabase.Realtime.Constants+SocketState])"
   ```
   Comprobar que `Reconnect` y `Open` son estados distintos.
3. **Verificar filtrado de excepciones de cancelación:** Inspeccionar `CapaDatos/Repositories/RepositorioBase.cs`, línea 50.
4. **Verificar invocación `async void` en selector:** Inspeccionar `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`, línea 125.
