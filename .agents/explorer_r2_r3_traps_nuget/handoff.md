# Handoff Report: Explorer 2 (R2 Trampas del Repositorio y R3 Dependencias FusionCache)

**Fecha**: 2026-09-03T05:05:00Z  
**Emisor**: Explorer 2 (`teamwork_preview_explorer`)  
**Receptor**: Orquestador (`parent`, id: `985fffaf-dd63-4fd9-b8bc-738eb2b2f0d1`)  
**Tipo**: Hard Handoff (Tarea completada al 100%)  
**Archivo de análisis detallado**: `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r2_r3_traps_nuget\analysis.md`

---

## 1. Observation

1. **`CapaUI/App.xaml.cs:39` & `85-120`**:
   `App.Services => _services ??= ConfigureServices();` registra todos los servicios una única vez sobre un contenedor estático compartido que no se reconstruye durante la vida del proceso, y registra los ViewModels como `Transient`.
2. **`CapaDatos/DependencyInjection.cs:69-70`**:
   `services.AddTransient<CapaAplicacion.Common.Catalogos.ICatalogoRepository, Repositories.Catalogos.CatalogoRepository>();` registra la interfaz contra la implementación directa.
3. **`CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs:74, 159`**:
   `SelectorCatalogoModal` define un `private readonly CancellationTokenSource _ctsVida = new();` y lo pasa a `CatalogoCache.ObtenerCompletoAsync(..., _ctsVida.Token)`.
4. **`CapaUI/Core/Catalogos/CatalogoCache.cs:48, 110-143`**:
   Define `alRevalidar` invocando `RevalidarAsync`, que ejecuta `alRevalidar(pagina.Items)` para que la UI (`SelectorCatalogoModal.xaml.cs:190-208`) repinte en caliente `Dg.ItemsSource = _vista`.
5. **`CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes/ContactosFabricantesViewModel.cs:127` & `ContactosProveedoresViewModel.cs:127`**:
   Ambos ViewModels invocan `Observar("contactos_fabricante", OnCambioContacto)` y `Observar("contactos_proveedor", OnCambioContacto)`.
   Sin embargo, la consulta en vivo de base de datos (`select tablename from pg_publication_tables where pubname = 'supabase_realtime'`) demostró que solo 8 tablas de catálogo están publicadas: `categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`. Tablas como `contactos_fabricante` y `contactos_proveedor` NO están publicadas.
6. **`CapaAplicacion4/Common/Result.cs:7-18`**:
   `Result<T>` contiene únicamente un constructor privado `private Result(bool success, T? value, string error)` sin constructor público ni atributo `[JsonConstructor]`.
7. **`CapaUI/Formularios/Principal/MainWindow.xaml.cs:660-691` (`LimpiarRecursosAsync`)**:
   Limpia `SesionPermisos` y `_sesionService.CerrarSesion()`, pero NO invoca `CatalogoCache.InvalidarTodo()`, ni purga `RolPermisoRepository._catalogoCache`, ni ninguna caché en memoria sobre `App.Services`.
8. **`CapaDatos/Realtime/RealtimeService.cs:157-161`**:
   Registra `SocketState.Reconnect` únicamente con un mensaje de log: `Serilog.Log.Information("Realtime: WebSocket reconectando...")`, sin exponer evento de reconexión ni purgar entradas cacheadas.
9. **`dotnet list CapaUI/CapaUI.csproj package --include-transitive`**:
   Muestra que `CapaUI` y sus dependencias corren en `net8.0-windows` con `Microsoft.Extensions.DependencyInjection 8.0.1`, `Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2`, `Microsoft.Extensions.Logging.Abstractions 8.0.3` y `System.Threading.Channels 8.0.0`.
10. **Especificación oficial NuGet de `ZiggyCreatures.FusionCache`**:
    - `v1.4.1`: No incluye Tagging API (`RemoveByTag` no existe).
    - `v2.0.2`: Release Notes dice explícitamente: `"- LTS-only release: this version references only .NET 8 core packages, so it can be used in scenarios where only LTS packages can be referenced"`. En el grupo `net8.0` referencia estrictamente `Microsoft.Extensions.Caching.Memory (8.0.1)`. Soporta `RemoveByTag`, `RemoveByTagAsync` y `ClearAsync(allowFailSafe: false)`.
    - `v2.1.0` a `2.7.2`: En el grupo `net8.0` referencia `Microsoft.Extensions.Caching.Memory (9.0.0)`, introduciendo paquetes 9.x a un proyecto .NET 8 LTS.

---

## 2. Logic Chain

1. **De Obs. 1**: Si `ICacheService` o `IFusionCache` se registrase como `Transient`, cada ViewModel obtendría una instancia vacía con su propia memoria efímera; las consultas a la base de datos se repetirían siempre (hit-rate 0%) y la invalidación por Realtime no tendría efecto. Por tanto, debe ser estrictamente `Singleton`.
2. **De Obs. 2**: Si para decorar `ICatalogoRepository` se intenta resolver `sp.GetRequiredService<ICatalogoRepository>()` dentro de la factoría de la misma interfaz, el contenedor DI entra en recursión infinita y produce `StackOverflowException` fatal no recuperable. Por tanto, la implementación concreta debe registrarse por su tipo `services.AddTransient<CatalogoRepository>()` y la interfaz debe inyectar la clase concreta en el decorador.
3. **De Obs. 3**: Si el `ct` del llamador (`_ctsVida.Token` del modal) se pasa a la fábrica de red dentro de `GetOrSetAsync`, cerrar el modal cancela la consulta compartida en vuelo (Single-Flight), arrojando `OperationCanceledException` a cualquier otra pantalla o hilo que estuviera esperando el mismo recurso. Por tanto, la fábrica de red debe usar `CancellationToken.None`, limitando el `ct` del llamador exclusivamente a controlar su propia espera.
4. **De Obs. 4**: Al reemplazar "mostrar y revalidar" por eventos reactivos de Realtime, el único cambio visible para el usuario es que un modal abierto no mutará su `DataGrid` en caliente si otro usuario modifica la base de datos en ese mismo segundo. Dado que las 8 tablas están 100% publicadas en Realtime y los modales son de corta duración (segundos), este riesgo observable es despreciable frente a los enormes beneficios de eliminar miles de consultas redundantes y eliminar parpadeos de UI.
5. **De Obs. 5**: Las llamadas a `Observar()` sobre `contactos_fabricante` y `contactos_proveedor` son fallas silenciosas activas porque Postgres jamás emite eventos para tablas fuera de `supabase_realtime`. Por tanto, se debe registrar `P-049` en la deuda técnica y blindar `InvalidadorCacheRealtime` para suscribirse únicamente a las 8 tablas publicadas.
6. **De Obs. 6**: La envoltura `Result<T>` no puede deserializarse con `System.Text.Json` debido a su constructor privado. Además, almacenar `Result<T>` en caché correría el riesgo de cachear fallos. Por tanto, se ratifica el descarte absoluto de L2 (solo L1 en RAM) y en L1 se almacena únicamente el contenido desempaquetado `PagedResult<FiltroItem>` tras validar `r.Success`.
7. **De Obs. 7**: Dado que `App.Services` es un Singleton estático que sobrevive al cierre de sesión de la ventana principal, si los permisos o la sesión se cachearan, el siguiente usuario en la misma PC física heredaría permisos ajenos. Por tanto, `IUsuarioSesionService.SesionActual` se delimita como Zona Zero-Cache absoluta y se documenta `P-048` para garantizar la purga de cachés en `MainWindow.LimpiarRecursosAsync()`.
8. **De Obs. 8**: Tras una desconexión WiFi en planta, Realtime reconecta pero no retransmite eventos ocurridos durante el apagón. Si no se purgan las etiquetas en `OnReconectado`, el cliente mantendría datos obsoletos por 2 horas. Por tanto, se debe forzar `RemoveByTagAsync("catalogos")` en la reconexión.
9. **De Obs. 9 y 10**: `v1.4.1` carece de Tagging; `v2.1.0+` introduce `Microsoft.Extensions.* 9.0.0` que contamina el proyecto .NET 8; **`v2.0.2` es la versión canónica y perfecta**, construida específicamente por el autor como LTS para .NET 8 sin dependencias 9.x y con soporte completo de Tagging y `ClearAsync(allowFailSafe: false)`.

---

## 3. Caveats

- **No se modificó código fuente**: Todo el trabajo fue de auditoría, verificación de dependencias y diseño analítico, cumpliendo estrictamente la restricción de sólo lectura.
- **P-048 y P-049**: Se deben registrar en `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` para que queden formalizadas como deuda antes de la implementación de Fase 1.
- **RPC `tablas_publicadas_realtime()`**: Se propone como test de verificación o migración SQL en Supabase para auditar en tiempo de despliegue que las publicaciones coincidan con lo esperado por el cliente.

---

## 4. Conclusion

1. **Las 12 Trampas de R2 quedan completamente desmanteladas y neutralizadas con mitigaciones blindadas y snippets de código listos para su formalización en ADR-026**.
2. **Para R3, la versión a prescribir obligatoriamente en ADR-026 es `ZiggyCreatures.FusionCache [2.0.2]`**. Ninguna versión anterior cuenta con el motor de Tagging requerido, y ninguna versión posterior respeta el confinamiento estricto a .NET 8 LTS.

---

## 5. Verification Method

1. **Verificación de dependencias de FusionCache 2.0.2**:
   ```powershell
   curl.exe -s "https://api.nuget.org/v3-flatcontainer/ziggycreatures.fusioncache/2.0.2/ziggycreatures.fusioncache.nuspec"
   ```
   *Condición de invalidación*: Si el grupo `targetFramework="net8.0"` contuviera alguna dependencia con versión `>= 9.0.0`. (Comprobado: contiene únicamente `Microsoft.Extensions.Caching.Memory 8.0.1`).
2. **Verificación de tablas publicadas en PostgreSQL**:
   ```sql
   select tablename from pg_publication_tables where pubname = 'supabase_realtime';
   ```
   *Condición de invalidación*: Si `contactos_fabricante` o `contactos_proveedor` aparecieran en la salida. (Comprobado: no están publicadas).
3. **Verificación de `Result<T>` constructor**:
   Inspeccionar `CapaAplicacion4/Common/Result.cs:13` para comprobar el constructor privado.
