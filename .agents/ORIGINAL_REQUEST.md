# Original User Request

## 2026-09-02T17:49:04Z

Use a full team of agents to implement this project across multiple specialized areas.

Implementación integral de la validación de longitud máxima de campos de texto en la aplicación de escritorio .NET 8 (WPF) de Bimbo Honduras, alineando las reglas de dominio con el esquema real de Supabase (Postgres), propagando topes automáticamente desde `ValidadorFormulario.Segun()`, solucionando los bugs de desfase y concatenación de `GhostTextBox` en el login, y creando tests de deriva de esquema y documentación en la bóveda `contexto/`.

Working directory: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
Integrity mode: development

## Requirements

### R1. Alineación de Reglas de Dominio con el Esquema de Supabase
- Actualizar `CapaDominio/Reglas/ReglasEntidades.cs` para reflejar con precisión los tamaños reales de columna de PostgreSQL (`information_schema.columns`):
  - `ReglasProducto`: `Codigo` (50), `Nombre` (200), `Contenido` (100).
  - `ReglasCategoria`: `Nombre` (100), `Descripcion` (200).
  - `ReglasPresentacion`: `Nombre` (100), `Descripcion` (500 - tope UI).
  - `ReglasFabricante`: `Nombre` (200), `Descripcion` (500 - tope UI).
  - `ReglasProveedor`: `Nombre` (200), `Rtn` (20), `Telefono` (20), `Correo` (100), `Direccion` (500 - tope UI).
  - `ReglasEmpleado`: `Nombre` (100), `Apellido` (100), `Identidad` (20, obligatorio), `Telefono` (20), `Correo` (100).
  - `ReglasUsuario`: `Correo` (50, `FormatoCampo.Correo`), `Password` (min 6, max 72).
  - `ReglasRol`: `Nombre` (50).
  - `ReglasContacto`: `Nombre` (100), `Telefono` (20), `Correo` (100).
  - `ReglasEmpresa`: `Nombre` (200), `Rtn` (20), `Telefono` (20), `Correo` (100), `Direccion` (500 - tope UI).
- Documentar en cada campo la columna de BD correspondiente e incluir el marcador léxico para columnas `text` (topes de UI de 500 caracteres).

### R2. Derivación Automática de MaxLength y Limpieza de XAML
- Modificar `ValidadorFormulario.Segun()` en `CapaUI/Core/Validacion/ValidadorFormulario.cs` para invocar un método `TopePreventivo(m)` que asigne `MaxLength` a controles `TextBox` y `PasswordBox` únicamente si `MaxLength == 0`.
- Eliminar los atributos `MaxLength="100"` manuales en los XAML de modales (`ProveedorModal.xaml`, `FabricanteModal.xaml`, `CategoriaModal.xaml`, `PresentacionModal.xaml`, `EmpleadoModal.xaml`) para evitar solapamientos con las reglas del dominio.
- Incorporar los campos pendientes al validador en sus modales respectivos:
  - `EmpleadoModal.xaml.cs`: `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)`
  - `ProductoModal.xaml.cs`: `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)`
  - `UsuarioModal.xaml.cs`: `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)`
- Modificar `GenerarEmail` en `UsuarioModal.xaml.cs` para recortar la parte local preservando el dominio, de modo que la dirección final nunca exceda los 50 caracteres permitidos por `alias_usuario`.

### R3. Corrección Integral de `GhostTextBox` (Login)
- Implementar la DependencyProperty `MaxLength` en `GhostTextBox` propagándola a `InnerBox`, manteniendo `GhostDisplay.MaxLength = 0`.
- Resolver el desfase visual de scroll suscribiendo `InnerBox` al evento `ScrollViewer.ScrollChangedEvent` y replicando `HorizontalOffset` hacia `GhostDisplay`. Asegurar sincronización en `ShowGhostFor` mediante `Dispatcher.BeginInvoke(SincronizarScroll, DispatcherPriority.Loaded)`.
- Corregir `GetRemainingSuffix` para que devuelva `string.Empty` si el texto ingresado ya contiene una arroba (`@`), evitando concatenaciones de dominios ajenos hacia Supabase Auth.
- Aplicar clamp preventivo en `GetFullText()` y limpieza con `.Dispose()` en los CTS debouncers.

### R4. Topes en Login y Vistas Fuera del Validador
- Asignar en `LoginWindow.xaml.cs` los topes leyendo directamente las reglas del dominio (`TxtEmail.MaxLength = 50`, contraseñas en 72), cubriendo también los paneles de recuperación (`ForgotEmailPanel`, `ForgotNewPanel`).
- Aplicar validación de longitud defensiva en `ConfiguracionEmpresaViewModel` mediante `ReglasFormato.NoExcedeLargo` y `MaxLength` en su XAML.

### R5. Suite de Tests Automatizados de Deriva y Frontera
- Crear `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`:
  - **Test A (Deriva):** Conexión vía `Npgsql` leyendo `BIMBO_POSTGRES_CONNECTION_STRING` (omitiendo si no está presente) contra `information_schema.columns`. Detecta columnas inexistentes, reglas más permisivas que la BD, o columnas `text` no marcadas como tope de UI.
  - **Test B (Valores fijados):** Tests unitarios con `[Theory]` para ejecuciones offline / CI.
  - **Test C (Auditoría por reflexión):** Asegura que toda `ReglaCampo` declarada en `CapaDominio` con `LargoMaximo` esté cubierta en el mapa de auditoría.
- Agregar en `ReglasFormatoTests` pruebas de frontera (`null`, string vacío y longitud exacta).

### R6. Documentación en la Bóveda de Conocimiento
- Registrar addendums en `contexto/45 - Decisiones/ADR-021 - Validacion en tres capas...md` y `ADR-004 - GhostTextBox...md`.
- Actualizar `contexto/20 - Patrones/Validacion de formularios.md` y `Anatomia compartida de los modales.md`.
- Actualizar fichas `P-045` y `P-042` y registrar la nueva ficha de deuda para `ModalInput` en `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`.
- Generar la nota de sesión en `contexto/70 - Bitácora de Cambios/2026-09/Sesión 2026-09-02 - Validación de longitud máxima en campos de texto.md`.

## Acceptance Criteria

### Compilación y Ejecución
- [ ] `dotnet build BimboProyecto.sln` finaliza con 0 errores.
- [ ] `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` finaliza con 100% de pruebas superadas (incluyendo la nueva suite).

### Validación de Datos y Dominio
- [ ] Ninguna regla en `ReglasEntidades.cs` permite una longitud mayor a la columna física en PostgreSQL.
- [ ] Los campos de texto en los modales no permiten tipear más allá del largo especificado en su regla.
- [ ] `GenerarEmail` genera correos de máximo 50 caracteres sin truncar el dominio `@empresa.com`.

### Login y UX
- [ ] En `GhostTextBox`, textos largos (>60 caracteres) mantienen el texto de entrada y el ghost perfectamente alineados en ambos sentidos de scroll.
- [ ] Al ingresar un correo con dominio ajeno (ej. `test@yahoo.com`), no se concatena el sufijo `@gmail.com`.
- [ ] Los campos de contraseña en login y recuperación tienen tope en 72 caracteres.

### Documentación
- [ ] Todos los archivos modificados o creados en `contexto/` cumplen el protocolo de la bóveda (frontmatter YAML estricto, enlaces `[[wikilink]]` y sección `## Relaciones`).


## 2026-09-03T04:53:04Z

Revisión crítica, validación adversarial y formalización del Architectural Decision Record (ADR-026) para el Plan Técnico de Caching en memoria (FusionCache L1) con invalidación reactiva por Supabase Realtime en Bimbo Honduras (.NET 8 · WPF · Supabase).

Working directory: d:/Proyectos/Proyecto de BIMBO/BimboProyecto
Integrity mode: development

---

## Contexto y Restricciones Principales

- **Alcance acordado:** Estrictamente diseño y documentación técnica en Obsidian. **Queda prohibido modificar código fuente o archivos de proyecto** (`.cs`, `.xaml`, `.csproj`, `.sln`). No se toca código en este paso.
- **Principio rector:** La invalidación por evento de Realtime es el mecanismo de frescura; el TTL y fail-safe son la red de seguridad.
- **Premisa de publicación en Realtime (Verificada empíricamente contra la BD viva):**
  La consulta en vivo ejecutada sobre `pg_publication_tables`:
  ```sql
  select tablename from pg_publication_tables where pubname = 'supabase_realtime';
  ```
  confirma que las **8 tablas de catálogo** (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`) **están efectivamente publicadas** en la base de datos de producción (`bzmmrifjgzlvsphctais`). Tablas transaccionales y de soporte como `contactos_fabricante`, `contactos_proveedor`, `bitacora`, `roles`, `acciones`, `modulos` y `empresa` **no** están publicadas.
- **Relación con ADR-015:**
  - El archivo real en la bóveda es `[[ADR-015 - Cache de catalogos mostrar y revalidar]]` (cumpliendo la regla de `contexto/AGENTS.md §5`: *"si dudás entre dos nombres, usá el que ya exista"*).
  - 🔴 **PROHIBIDO tocar el frontmatter de ADR-015:** ADR-015 describe el comportamiento vigente en producción. **No** debe marcarse como `estado: reemplazado`. ADR-026 se creará con `estado: propuesto` y explicitará que superará a ADR-015 únicamente una vez que sea implementado.
- **Nivel L2:** Evaluado y descartado justificadamente (no hay tier servidor compartido, riesgo de seguridad con secretos de conexión sin RLS en PCs de planta, persistencia de fugas entre usuarios en disco y problemas de serialización en `Result<T>`).

---

## Alcance de Escritura Permitido (Estricto)

El equipo tiene terminantemente prohibido tocar cualquier archivo fuera de la siguiente lista explícita:

1. **`contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`** (archivo nuevo).
2. **`contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`** (punto caliente según `contexto/AGENTS.md §7` — edición quirúrgica y localizada):
   - Agregar **`P-048`**: Fuga de datos entre usuarios en la misma máquina por ausencia de invocación de `CatalogoCache.InvalidarTodo()` y falta de invalidación en `RolPermisoRepository._catalogoCache` sobre el root provider singleton `App.Services`.
   - Agregar **`P-049`**: `contactos_fabricante` y `contactos_proveedor` con suscripciones Realtime (`Observar()`) vivas en ViewModels contra tablas no publicadas en `supabase_realtime`.
3. **`contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`** (punto caliente según `contexto/AGENTS.md §7` — edición quirúrgica y localizada):
   - Agregar un callout descriptivo breve referenciando a `[[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]]` como propuesta arquitectónica.

---

## Requirements

### R1. Auditoría Adversarial y Contraste con el Código Existente
Revisar cada sección del documento de diseño propuesto contrastándolo línea por línea contra el código real de `BimboProyecto`:
- Contrastar los puntos de entrada de catálogos actuales (`CatalogoCache.cs`, `CatalogoRepository.cs`, `SelectorCatalogoModal.xaml.cs`, `RolPermisoRepository.cs`, `EmpresaRepository.cs`).
- Validar la interacción de concurrencia y el orden de ejecución entre `RealtimeService.OnCambioRecibido` (despachado en el hilo de UI) y `InvalidadorCacheRealtime.OnCambio` (invalidación síncrona `RemoveByTag` y desacoplamiento de logs vía `Channel`).
- Verificar que el ciclo de vida en `MainWindow.xaml.cs` (`OnLoaded` y `LimpiarRecursosAsync`) garantice la eliminación de la fuga entre usuarios distintos que comparten la misma máquina en `App.Services` (root provider estático que no se reconstruye).

### R2. Validación de las 12 Trampas Específicas del Repositorio (Incluyendo Errores Silenciosos)
Evaluar la suficiencia y solidez de las mitigaciones propuestas en la §7, prestando especial atención a las trampas que compilan limpio pero fallan en silencio:
1. **`ICacheService` registrado como Singleton:** Si se registra `Transient`, cada ViewModel recibe su propia caché vacía y la función entera se convierte en un no-op silencioso que compila sin quejarse.
2. **Registro del repositorio concreto por su tipo:** `services.AddTransient<CatalogoRepository>()` y no por `ICatalogoRepository`. Si se registra por interfaz, `sp.GetRequiredService<ICatalogoRepository>()` dentro de la lambda de fábrica se resuelve recursivamente a sí misma produciendo `StackOverflowException`.
3. **`CancellationToken.None` en la fábrica del decorador:** Si se pasa el `ct` del llamador (como `_ctsVida.Token` de `SelectorCatalogoModal`), abrir y cerrar rápido el modal cancela la carga para cualquier otra pantalla que estuviera esperando el mismo single-flight. El `ct` sólo debe controlar la espera del llamador actual.
4. **Retiro de `alRevalidar`:** Documentar la desaparición del repintado en caliente en UI (`Dg.ItemsSource`) como riesgo observable, justificado por la publicación al 100% de las 8 tablas en Realtime.
5. **Detección de tablas no publicadas:** Validación mediante la función `tablas_publicadas_realtime()` para evitar que suscripciones a tablas no publicadas fallen silenciosamente.
6. **No-serialización de `Result<T>`:** Bloqueo de constructor privado en `Result<T>` frente a `System.Text.Json`.
7. **Aislamiento de permisos:** Prohibición estricta de cachear datos derivados de `IUsuarioSesionService.SesionActual`.
8. **Resincronización tras desconexión/reconexión:** Purgar etiquetas de catálogos en `OnReconectado`.

### R3. Selección y Verificación de Dependencias de FusionCache
- La versión inicial sugerida es `[2.0.2]`, pero la regla de oro real es: *"la versión de `ZiggyCreatures.FusionCache` que soporte tagging (`RemoveByTag`), `ClearAsync(allowFailSafe: false)` y que NO arrastre `Microsoft.Extensions.*` fuera de la línea 8.x en el grupo `net8.0`"*.
- El equipo está plenamente autorizado a verificar contra nuget.org y `dotnet list package --include-transitive` y justificar/ajustar la versión exacta a fijar en el ADR.

### R4. Redacción Formal de ADR-026 en Obsidian
Redactar el documento formal de decisión en `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md` respetando las directrices de `contexto/AGENTS.md §2`:
- **Frontmatter estricto:**
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
  *(Sin campos `autor:` ni `autor_cambios:`, ya que corresponden a notas de sesión).*
- Contexto de los 5 problemas actuales y eliminación de revalidate-on-open.
- Confirmación de las 8 tablas publicadas en `supabase_realtime` como base empírica de frescura.
- Descarte exhaustivo de L2 (Redis/Garnet/SQLite local).
- Matriz completa de TTL, Jitter, Fail-Safe y zonas Zero-Cache (Pesajes, Bitácora, Notificaciones, Reportes).
- Roadmap de 5 fases (Fase 0 a Fase 4) con criterios de término medibles.
- Enlaces bidireccionales con notas existentes (`[[Arquitectura Actual]]`, `[[ADR-015 - Cache de catalogos mostrar y revalidar]]`, `[[Deuda Técnica - Pendientes]]`, `[[Conocimiento Principal]]`).

---

## Acceptance Criteria

### Integridad Arquitectónica y Cero Código
- [ ] No se modifica ningún archivo fuera de los 3 autorizados en el Alcance de Escritura (cero líneas de código alteradas en `CapaUI`, `CapaDatos`, `CapaAplicacion4`, `CapaDominio` ni migraciones SQL).
- [ ] El frontmatter de `[[ADR-015 - Cache de catalogos mostrar y revalidar]]` permanece intacto (`estado: aceptado`), sin marcarlo como reemplazado.
- [ ] El ADR-026 se crea con `estado: propuesto` y tags alineados con la taxonomía del vault (`adr`, `decision`, `cache`, `realtime`, `rendimiento`).
- [ ] Las 3 trampas silenciosas (`ICacheService` Singleton, `CatalogoRepository` registrado por tipo, `CancellationToken.None` en fábrica) quedan explícitamente incorporadas y justificadas.
- [ ] El descarte de L2 queda formalmente blindado ante riesgos de seguridad (secretos sin RLS en PCs de planta), persistencia de fugas entre usuarios en disco y limitaciones de serialización.
- [ ] Se documentan formalmente `P-048` y `P-049` en `Deuda Técnica - Pendientes.md` como puntos calientes respetando la estructura de la tabla y detalle existente.
- [ ] Se documenta el riesgo observable de la desaparición del repintado en caliente de `alRevalidar` en `SelectorCatalogoModal` como el único cambio perceptible por el usuario final.

---

## 2026-09-17T19:11:53Z

This is a single self-contained feature; keep it small and focused. Implement live data integration for the existing Dashboard view in BimboProyecto strictly according to the approved implementation plan.

Working directory: C:\Users\Emanuel Lazo\Source\Repos\BimboProyecto
Integrity mode: development

## Context & Scope Constraints
- **Strict Scope Rule**: Touch ONLY the files planned. Do not modify other forms, views, or business flows.
- Dashboard stays under its existing route in Reportería (do NOT change home/welcome screen).
- Trend badges without comparison history must display "-".
- Merma thresholds remain hardcoded for now (note for future configuration).
- ADR-026 Compliance: Cache ONLY inventory catalog counts (5 min, tagged). Zero-cache for pesajes, merma, and real-time feeds.

## Requirements

### R1. Database Migration (RPC `consultar_kpis_pesajes`)
Create a migration in `supabase/migrations/` defining `consultar_kpis_pesajes` with `SECURITY INVOKER` and `search_path = ''`. It must compute in a single pass using PostgreSQL `FILTER (WHERE ...)`:
- Current and previous period weighing counts (`pesajes_actual`, `pesajes_anterior`)
- Current and previous net weight sums (`neto_actual`, `neto_anterior`)
- Manifested theoretical weight and received weight for the current period (excluding state 9 / cancelled)

### R2. Application Contracts & DTOs (`CapaAplicacion4`)
In `CapaAplicacion4/Dashboard/`:
- `DashboardDtos.cs`: Define `PeriodoDashboard` (Hoy, Semana, Mes), `KpisInventarioDto`, `KpisPesajesDto`, `MermaProductoDto`, and `UltimoPesajeDto`. Nullable change percentages/diffs.
- `Interfaces/IDashboardRepository.cs`: Declare methods for:
  - `ObtenerKpisInventarioAsync`
  - `ObtenerKpisPesajesAsync(PeriodoDashboard)`
  - `ObtenerTopMermaAsync(PeriodoDashboard, int top = 5)`
  - `ObtenerUltimosPesajesAsync(int cantidad = 5)`

### R3. Data Repository (`CapaDatos`)
In `CapaDatos/Repositories/Dashboard/DashboardRepository.cs`:
- Inherit from `RepositorioBase`.
- Query catalog tables (`productos`, `proveedores`, `fabricante`) with `FusionCache` (5-minute TTL, tags: `TagsCache.CatalogosRaiz`).
- Call `consultar_kpis_pesajes` for pesajes KPIs (zero-cache).
- Reuse `consultar_reporte_productos_merma` for top merma (zero-cache).
- Query `entradas_producto` joined with products for the 5 most recent weighings (zero-cache).
- Register `IDashboardRepository` as Singleton/Scoped in `CapaDatos/DependencyInjection.cs`.

### R4. ViewModel Refactoring (`CapaUI`)
In `CapaUI/Formularios/Dashboard/DashboardVM.cs`:
- Inject `IDashboardRepository`, `IRealtimeService`, and `IConexionMonitor`.
- Replace mock strings with observable properties supporting live data loading and loading/error states.
- Format trend indicators with "-" when comparison data is null or unavailable.
- Implement asynchronous data loading (`InicializarAsync`, `RefreshCommand`).
- Handle `SelectPeriodoCommand` by updating `Periodo` and reloading pesajes + merma without reloading catalog inventory.
- Connect `IRealtimeService` to stream new entries into `Ultimos`.

### R5. Dependency Injection & Navigation Route
In `CapaUI/Formularios/Principal/MainViewModel.cs`:
- Update `[Routes.Dashboard]` route from direct instantiation `() => new Dashboard.DashboardVM()` to resolution via DI container: `() => App.CrearVm<Dashboard.DashboardVM>()`.
- Register `DashboardVM` in `CapaUI` DI if not already present.

## Acceptance Criteria

### Execution & Build
- [ ] Solution compiles cleanly without C# compiler errors or broken bindings.
- [ ] No files outside the approved list are touched or modified.
- [ ] ADR-026 zero-cache constraints strictly respected on pesajes, merma, and reports.
- [ ] Trend badges display "-" when prior period has no records.
- [ ] Realtime subscription connects to `entradas_producto` to append new weighings live.
