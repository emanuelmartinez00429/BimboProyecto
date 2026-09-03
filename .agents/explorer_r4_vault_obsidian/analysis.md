# Análisis y Diseño de Arquitectura: ADR-026, Deuda Técnica (P-048, P-049) y Bóveda Obsidian

**Fecha:** 2026-09-02  
**Autor:** Explorer 3 (`teamwork_preview_explorer`)  
**Misión:** R4 — Estructura Obsidian, Bóveda de Conocimiento y Preparación del Diseño para ADR-026, Deuda Técnica y Arquitectura Actual  
**Estado:** Completo — Listo para Ejecución Editorial  

---

## 1. Resumen Ejecutivo

El presente informe establece la formulación técnica integral y las directrices de edición documental para la incorporación de **ADR-026** ("Caché en memoria con FusionCache e invalidación por Realtime"), la catalogación quirúrgica de las deudas técnicas **P-048** y **P-049**, y la actualización del mapa de arquitectura viva en **[[Arquitectura Actual]]**.

Se realizó una investigación profunda sobre las reglas de la bóveda Obsidian (`contexto/AGENTS.md`), las decisiones arquitectónicas previas ([[ADR-015 - Cache de catalogos mostrar y revalidar]] y [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]), el registro de deuda técnica ([[Deuda Técnica - Pendientes]]) y el código fuente en C#/WPF de `BimboProyecto`.

### Hallazgos Principales:
1. **Regla de oro de ADR-015:** `[[ADR-015 - Cache de catalogos mostrar y revalidar]]` representa la arquitectura vigente en producción. Su frontmatter debe mantenerse estrictamente intacto con `estado: aceptado`. El nuevo `ADR-026` debe nacer con `estado: propuesto`, explicitando que reemplazará formalmente a ADR-015 únicamente una vez que el código sea implementado y desplegado.
2. **Confirmación de Dependencias de FusionCache:** Se verificó empíricamente contra NuGet que `ZiggyCreatures.FusionCache` versión **`2.0.2`** sobre el target `net8.0` depende únicamente de `Microsoft.Extensions.Caching.Memory [8.0.1, )`. Dado que la solución ya utiliza `Microsoft.Extensions.DependencyInjection` versión `8.0.1`, no se introduce ninguna contaminación de paquetes fuera del árbol .NET 8.x.
3. **Confirmación empírica de 8 tablas en Realtime:** Las 8 tablas de catálogos (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`) están publicadas en `supabase_realtime` en la base de datos de producción (`bzmmrifjgzlvsphctais`), lo que garantiza la viabilidad del modelo reactivo.
4. **Deudas Técnicas Críticas P-048 y P-049:**
   - **P-048:** Fuga de catálogos y permisos entre sesiones en terminales compartidas debido a que `MainWindow.LimpiarRecursosAsync()` nunca invoca `CatalogoCache.InvalidarTodo()`, y `RolPermisoRepository._catalogoCache` es un campo estático sin mecanismo de purga en el root provider singleton `App.Services`.
   - **P-049:** `ContactosFabricantesViewModel` y `ContactosProveedoresViewModel` mantienen suscripciones activas vía `Observar()` sobre `contactos_fabricante` y `contactos_proveedor`, las cuales no están publicadas en `supabase_realtime`, provocando un fallo silencioso de reactividad.

---

## 2. Cumplimiento de Reglas de la Bóveda Obsidian (`contexto/AGENTS.md`)

Para garantizar la integridad y navegabilidad del grafo de conocimiento en Obsidian, el diseño respeta rigurosamente las normas de `contexto/AGENTS.md`:

### 2.1 Frontmatter YAML Estricto
- Se deben incluir únicamente los campos admitidos para ADRs:
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
- **Prohibición expresa:** NO incluir campos `autor:` ni `autor_cambios:` en el frontmatter del ADR (estos campos están reservados exclusivamente para las notas de sesión en `70 - Bitácora de Cambios/`).

### 2.2 Convenciones de Nombres y Rutas
- Ruta exacta del archivo de decisión:
  `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`
- Cumple la convención `ADR-NNN - Título descriptivo.md` con numeración correlativa inmediata tras ADR-025.

### 2.3 Enlaces Bidireccionales y Sección Relaciones
- Uso exclusivo de sintaxis `[[wikilink]]` sin extensiones `.md`.
- Toda nota debe culminar con la sección canónica `## Relaciones`.

---

## 3. Especificación Quirúrgica para `Deuda Técnica - Pendientes.md`

`Deuda Técnica - Pendientes.md` es clasificado como un punto caliente en `contexto/AGENTS.md §7`. Las adiciones deben ser quirúrgicas y no alterar los ítems precedentes (P-001 a P-047).

### 3.1 Redacción Exacta de Ficha P-048

**Ubicación de inserción en el cuerpo:** Directamente tras el separador final de `P-047` (línea 914), antes de `---` y la sección `## Historial de resolución`.

```markdown
---

### P-048 · Fuga de datos y permisos entre sesiones en terminal compartida (CatalogoCache y RolPermisoRepository)

**Archivos:** `CapaUI/Core/Catalogos/CatalogoCache.cs:177`, `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs:23-24`, `CapaUI/Formularios/Principal/MainWindow.xaml.cs:660-692` (`LimpiarRecursosAsync`), `CapaUI/App.xaml.cs`
**Detectado en:** Auditoría adversarial y diseño de [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] (2026-09-02)

En la aplicación de escritorio WPF de Bimbo Honduras, el contenedor de inyección de dependencias `App.Services` es un root provider estático instanciado una sola vez durante el inicio del proceso (`App.xaml.cs`). Cuando un usuario cierra su sesión en la ventana principal (`MainWindow`), la rutina `HandleCerrarSesionAsync()` ejecuta el método privado `LimpiarRecursosAsync()`, dispara el evento `SesionCerrada` y cierra `MainWindow`, retornando a la ventana de login (`LoginWindow`) **sin reiniciar el proceso del sistema operativo**.

Se constató una fuga de estado bidireccional entre usuarios sucesivos en la misma máquina física:

1. **`CatalogoCache` estático sin purga:** `CatalogoCache._cache` es un `ConcurrentDictionary<string, IReadOnlyList<FiltroItem>>` en memoria. A pesar de que expone el método `public static void InvalidarTodo() => _cache.Clear();` (línea 177), `MainWindow.LimpiarRecursosAsync()` **nunca lo invoca**. Los catálogos consultados por el Usuario A permanecen en RAM para el Usuario B.
2. **`RolPermisoRepository._catalogoCache` privado sin ciclo de vida:** Contiene el campo `private static IReadOnlyList<ModuloAccionesDto>? _catalogoCache` con un `SemaphoreSlim` asociado. No posee ningún método público ni interno para invalidar o limpiar la lista. La estructura de módulos y acciones precargada por el primer usuario persiste inmutable para sesiones posteriores.
3. **Contenedor Singleton no reiniciado:** Los servicios y repositorios registrados como Singleton en `App.Services` conservan instancias vivas entre sesiones.

**Riesgo:** Alto en terminales de planta y despachos con rotación de turnos entre múltiples operarios y supervisores. Si bien `SesionPermisos.Limpiar()` restablece los permisos en memoria del usuario autenticado, los datos de catálogos y la estructura base de permisos no se resetean, pudiendo exponer datos cacheados o provocar inconsistencias de visualización.

**Solución diseñada:**
1. Invocar explícitamente `CatalogoCache.InvalidarTodo()` en `MainWindow.LimpiarRecursosAsync()`.
2. Exponer un método de invalidación en `IRolPermisoRepository` y ejecutarlo durante el cierre de sesión.
3. Como solución arquitectónica definitiva: Migrar todas las cachés estáticas hacia la abstracción centralizada `ICacheService` gobernada por `FusionCache`, ejecutando `ClearAsync(allowFailSafe: false)` de forma determinística en `LimpiarRecursosAsync()`, tal como se establece en [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]].

**Estado:** `[ ] Pendiente 🔴`
```

### 3.2 Redacción Exacta de Ficha P-049

**Ubicación de inserción en el cuerpo:** Directamente tras la ficha `P-048`.

```markdown
---

### P-049 · Suscripciones inactivas a Realtime en Contactos (tablas no publicadas en supabase_realtime)

**Archivos:** `CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes/ContactosFabricantesViewModel.cs:127`, `CapaUI/Formularios/Principal/Pantallas/ContactosProveedores/ContactosProveedoresViewModel.cs:127`, `CapaDatos/Repositories/Realtime/RealtimeService.cs`
**Detectado en:** Auditoría adversarial y diseño de [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] (2026-09-02)

Tanto `ContactosFabricantesViewModel` como `ContactosProveedoresViewModel` heredan de `RealtimeAwareViewModel` y ejecutan llamadas de suscripción reactiva en su método `CargarDatosAsync()`:
- `Observar("contactos_fabricante", OnCambioContacto);` (línea 127)
- `Observar("contactos_proveedor", OnCambioContacto);` (línea 127)

Sin embargo, la auditoría empírica ejecutada directamente sobre el catálogo del sistema PostgreSQL de producción (`bzmmrifjgzlvsphctais`) mediante:
```sql
select tablename from pg_publication_tables where pubname = 'supabase_realtime';
```
revela que las únicas tablas publicadas en el canal de replicación lógica de Supabase son: `categoria`, `empleados`, `entradas_producto`, `fabricante`, `movimiento_productos`, `movimientos`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida` y `usuarios`. 

Las tablas secundarias `contactos_fabricante` y `contactos_proveedor` **no forman parte de `supabase_realtime`**.

**Modo de falla silencioso:**
El cliente `Supabase.Realtime` negocia y abre el canal websocket para la tabla solicitada sin arrojar excepciones. No obstante, PostgreSQL nunca emite eventos WAL hacia el slot de replicación para tablas que no pertenezcan a la publicación. Como consecuencia, las pantallas de contactos jamás reciben eventos de inserción, edición o borrado ejecutados desde otros clientes, generando un comportamiento ilusorio de reactividad y manteniendo canales websocket abiertos en vano.

**Riesgo:** Medio en consistencia de visualización multiusuario; degradación de arquitectura por código que presupone reactividad inexistente.

**Solución diseñada:**
1. Definir si el tráfico y volumen de `contactos_fabricante` y `contactos_proveedor` ameritan su incorporación a la publicación mediante una migración SQL en Supabase (`ALTER PUBLICATION supabase_realtime ADD TABLE contactos_fabricante, contactos_proveedor;`).
2. En caso de no incorporarlas, remover las llamadas `Observar()` en ambos ViewModels para liberar recursos del websocket y documentar que la actualización de contactos depende de recarga explícita o navegación drill-down.
3. Incorporar en `RealtimeService` una verificación defensiva o advertencia en log al intentar suscribirse a tablas fuera del catálogo de `supabase_realtime` (ver trampa 5 en [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]]).

**Estado:** `[ ] Pendiente`
```

### 3.3 Filas para la Tabla `## Historial de resolución`
Insertar al final de la tabla (después de la fila de `P-047`):
```markdown
| P-048 | Fuga de datos y permisos entre sesiones en terminal compartida (CatalogoCache / RolPermiso) | `[ ]` Pendiente 🔴 | [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] |
| P-049 | Suscripciones Realtime inactivas en Contactos (tablas no publicadas en publicación) | `[ ]` Pendiente | [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] |
```

### 3.4 Actualización de `## Relaciones` en Deuda Técnica
Añadir al final de la lista de relaciones:
```markdown
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] — diseño integral de caché L1 e invalidación reactiva que mitiga P-048 y P-049
```

---

## 4. Inserción Quirúrgica en `Arquitectura Actual.md`

`Arquitectura Actual.md` es el MOC vivo del sistema. Para incorporar la propuesta de ADR-026 sin perturbar los hitos ya consolidados en producción, se identificó el punto de inserción exacto:

### 4.1 Ubicación e Inserción del Callout
En el bloque de encabezado (líneas 12-15), inmediatamente tras el primer callout de estado del 2026-09-02:

```markdown
> [!info] Propuesta arquitectónica — Caché L1 en memoria e invalidación reactiva por Realtime
> Se encuentra en evaluación la transición del modelo de catálogos (*stale-while-revalidate* de [[ADR-015 - Cache de catalogos mostrar y revalidar]]) hacia una caché unificada L1 en memoria administrada mediante `ZiggyCreatures.FusionCache` e invalidación reactiva basada en eventos de `supabase_realtime`. Esta propuesta elimina las consultas de fondo redundantes por apertura de selector modal, erradica el riesgo de fuga de datos entre sesiones en una misma terminal ([[Deuda Técnica - Pendientes#P-048]]), y descarta formalmente el uso de almacenamiento L2 en clientes de planta. Ver [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]].
```

### 4.2 Actualización de `## Relaciones` en Arquitectura Actual
Añadir en `## Relaciones`:
```markdown
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] — propuesta arquitectónica de caché L1 e invalidación reactiva
```

---

## 5. Diseño Completo y Redacción Formal de ADR-026

A continuación se detalla el documento formal de decisión técnica para ser persistido en `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`:

```markdown
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

# ADR-026 — Caché en memoria con FusionCache e invalidación por Realtime

## Estado
**Propuesto** (2026-09-02).
Este registro formula el diseño técnico integral para superar y suceder a [[ADR-015 - Cache de catalogos mostrar y revalidar]]. Dado que ADR-015 describe el comportamiento que opera actualmente en producción, permanecerá en `estado: aceptado` hasta que el presente diseño sea implementado, probado en frío y desplegado.

---

## 1. Contexto y Diagnóstico del Estado Actual

La aplicación de escritorio de Bimbo Honduras (.NET 8 · WPF) consume una base de datos relacional PostgreSQL alojada en Supabase mediante clientes PostgREST y WebSockets. En agosto de 2026, [[ADR-015 - Cache de catalogos mostrar y revalidar]] introdujo una caché estática en memoria (`CatalogoCache`) sustentada en el patrón *stale-while-revalidate* para acelerar la carga de selectores y modales: la caché devuelve los ítems almacenados inmediatamente (0 ms) y lanza en segundo plano una consulta de revalidación HTTP a la base de datos (`RevalidarAsync`), actualizando la UI mediante el callback `alRevalidar` si la lista varió.

A pesar de haber resuelto la congelación inicial de la interfaz, el diagnóstico actual revela **cinco problemas estructurales** que demandan una evolución arquitectónica:

1. **Sobrecarga de consultas redundantes por apertura (Stale-While-Revalidate):**
   Cada vez que un operario abre una lupa modal (`SelectorCatalogoModal`), un formulario CRUD o la pantalla de Productos, se emite una petición HTTP de revalidación en paralelo. A la escala operativa diaria, **se realiza exactamente la misma cantidad de consultas a Supabase que si no existiera caché**, consumiendo ancho de banda y conexiones de base de datos para validar catálogos que cambian con escasa frecuencia (días o semanas).
2. **Fuga de datos entre sesiones en terminales compartidas ([[Deuda Técnica - Pendientes#P-048]]):**
   `App.Services` es un root `ServiceProvider` estático que vive durante toda la ejecución del proceso Windows. Al cerrar sesión en `MainWindow`, la rutina de limpieza `LimpiarRecursosAsync()` nunca invoca `CatalogoCache.InvalidarTodo()`. Asimismo, `RolPermisoRepository._catalogoCache` mantiene un campo privado estático sin mecanismo de purga. Si el Usuario A (ej. Administrador) cierra sesión y el Usuario B (ej. Operador de Pesaje) ingresa en la misma máquina sin reiniciar el ejecutable, hereda en memoria los catálogos y estructuras de permisos cargados previamente.
3. **Suscripciones zombies a tablas no publicadas en Realtime ([[Deuda Técnica - Pendientes#P-049]]):**
   Pantallas como `ContactosFabricantesViewModel` y `ContactosProveedoresViewModel` invocan `Observar("contactos_fabricante", ...)` y `Observar("contactos_proveedor", ...)`. Sin embargo, ninguna de estas tablas está incluida en la publicación `supabase_realtime` de PostgreSQL. El cliente abre canales que jamás recibirán eventos, generando un fallo silencioso de reactividad y consumo infructuoso de sockets.
4. **Dispersión de cachés sin gobernanza unificada:**
   Conviven múltiples mecanismos en memoria no coordinados: `CatalogoCache` (diccionario estático en UI), `RolPermisoRepository._catalogoCache` (campo estático con semáforo en Datos) y cachés de Storage en disco (`LogoEmpresaCache`, `IconoSidebarCache`). No existen políticas de expiración (TTL), jitter para evitar estampidas (thundering herd), ni fail-safe ante degradación de red.
5. **Inexistencia de resincronización tras corte y reconexión:**
   Cuando el cliente pierde la conexión y se reconecta (`OnReconexionAsync`), los catálogos en memoria no verifican si ocurrieron mutaciones durante la ventana offline, provocando que datos obsoletos persistan hasta la siguiente reapertura manual.

---

## 2. Decisión Arquitectónica

Se adopta una solución unificada de **Caché L1 en memoria** gestionada mediante la biblioteca de alto rendimiento **`ZiggyCreatures.FusionCache` (versión 2.0.2)**, complementada con un mecanismo de **invalidación reactiva basada en eventos de `supabase_realtime`**:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           CapaUI / ViewModels                           │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ Inyección DI (ICatalogoRepository)
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│        Decorador: CachedCatalogoRepository : ICatalogoRepository        │
└───────────────────┬─────────────────────────────────┬───────────────────┘
                    │ GetOrSetAsync (Single-Flight)   │ Cache Miss (Factory)
                    ▼                                 ▼
┌───────────────────────────────────────┐   ┌─────────────────────────────┐
│ ICacheService (FusionCache Singleton) │   │     CatalogoRepository      │
│  - L1 en RAM (MemoryCache)            │   │   (Acceso PostgREST HTTP)   │
│  - Fail-Safe & Jitter                 │   └──────────────┬──────────────┘
│  - Tagging: catalogos:{tabla}         │                  │
└───────────────────▲───────────────────┘                  │ Red
                    │ RemoveByTag                          ▼
┌───────────────────┴───────────────────┐   ┌─────────────────────────────┐
│       InvalidadorCacheRealtime        │   │          PostgreSQL         │
│  - Escucha PostgresChange (INSERT/    │◀──│      (supabase_realtime)    │
│    UPDATE/DELETE en WebSocket)        │   │   [8 tablas publicadas]     │
│  - Desacople de métricas por Channel  │   └─────────────────────────────┘
└───────────────────────────────────────┘
```

### Componentes Clave:
1. **`ICacheService` Singleton:** Abstracción registrada en `App.Services` como Singleton, encapsulando `IFusionCache`. Provee métodos fuertemente tipados (`GetOrSetAsync`, `RemoveByTagAsync`, `ClearAsync`).
2. **Patrón Decorador en Repositorios:** La lógica de caché se extrae de la interfaz de usuario y se encapsula en decoradores transparentes (ej. `CachedCatalogoRepository : ICatalogoRepository`), desacoplando a los ViewModels de los detalles de almacenamiento.
3. **Eliminación definitiva de `alRevalidar`:** Se retira el patrón *stale-while-revalidate*. Al abrir un modal, los datos se sirven exclusivamente desde la memoria L1 (0 ms). La frescura está garantizada por eventos push de Realtime y no por consultas de sondeo en cada apertura.
4. **Base Empírica de Frescura (8 Tablas Publicadas):**
   La consulta viva sobre `pg_publication_tables`:
   ```sql
   select tablename from pg_publication_tables where pubname = 'supabase_realtime';
   ```
   confirma que las **8 tablas de catálogo** (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`) **están 100% publicadas**. Todo cambio realizado por cualquier cliente dispara una invalidación inmediata en las demás terminales conectadas.

---

## 3. Descarte Formal y Blindado de Niveles L2 (Caché Distribuida o en Disco)

Se analizó rigurosamente la incorporación de un nivel L2 (Redis / Garnet centralizado o SQLite / disco local en el cliente), descartándose categóricamente en virtud de los siguientes fundamentos de seguridad y arquitectura:

| Alternativa L2 | Riesgos Críticos y Justificación de Rechazo | Estado |
|---|---|---|
| **Redis / Garnet Centralizado** | 1. **Inexistencia de backend intermediario:** La arquitectura es cliente rico WPF directo a Supabase (BaaS). Bimbo no opera un servidor de aplicaciones compartido.<br>2. **Riesgo crítico de seguridad:** Exponer un clúster de Redis implicaría embeber credenciales de conexión en los ejecutables de las PCs de planta sin Row Level Security (RLS), otorgando un vector de acceso irrestricto a la memoria compartida corporativa.<br>3. **Costo y fragilidad operativa:** Introduce un nuevo punto único de fallo y costos de infraestructura para almacenar un volumen ínfimo de datos (<500 KB). | ❌ Descartado |
| **SQLite Local / Disco en Cliente** | 1. **Persistencia de fugas entre usuarios:** En terminales compartidas de planta, un archivo de base de datos local retiene datos en disco entre sesiones de distintos usuarios, requiriendo mecanismos de encriptación (SQLCipher) y saneamiento complejo que agravan el problema P-048.<br>2. **Impedimento de serialización con `Result<T>`:** Los repositorios devuelven `Result<T>`, cuyo diseño inmutable posee constructores privados y carece de soporte nativo para `System.Text.Json` sin serializadores a medida frágiles.<br>3. **Latencia innecesaria:** El acceso a disco introduce contención de I/O, locks de archivo y latencias de milisegundos, frente a los microsegundos del acceso directo a punteros en RAM (L1). | ❌ Descartado |
| **Caché L1 en Memoria (RAM)** | Los 8 catálogos completos suman menos de 100 filas en total. Todo el conjunto ocupa menos de 500 KB de RAM. L1 es instantáneo (0.01 ms), totalmente volátil (no persiste tras cerrar la aplicación o invocar `ClearAsync`), y elimina toda complejidad de serialización. | ✅ **Elegido** |

---

## 4. Matriz de TTL, Jitter, Fail-Safe y Zonas Zero-Cache

La invalidación reactiva por Realtime es el mecanismo primario de frescura; la expiración por tiempo (TTL) y el fail-safe constituyen la red de seguridad defensiva:

### 4.1 Matriz de Configuración por Familia de Datos

| Familia de Catálogo | Tablas Físicas | Tag de Invalidación | TTL Base | Jitter | Fail-Safe | Modo de Invalidación |
|---|---|---|---|---|---|---|
| **Catálogos Ultra-Estables** | `paises`, `unidad_medida`, `tara` | `catalogos:paises`<br>`catalogos:unidad_medida`<br>`catalogos:tara` | 24 h | 10% (±2.4 h) | 7 días | Evento Realtime + TTL |
| **Catálogos de Negocio** | `categoria`, `fabricante`, `presentacion_producto`, `proveedores` | `catalogos:categoria`<br>`catalogos:fabricante`<br>`catalogos:presentacion`<br>`catalogos:proveedores` | 2 h | 15 min | 24 h | Evento Realtime (`RemoveByTag`) inmediato |
| **Catálogo Dinámico de Productos** | `productos` (solo combos/filtros rápidos; no páginas) | `catalogos:productos` | 30 min | 5 min | 2 h | Evento Realtime (`RemoveByTag`) |
| **Definiciones RBAC y Permisos** | `roles`, `acciones`, `modulos` | `rbac:definiciones` | 1 h | 10 min | 4 h | Evento Realtime (`roles`) o manual en pantalla Roles |

### 4.2 Zonas Zero-Cache (Prohibición Estricta)
Queda terminantemente prohibido cachear datos pertenecientes a los siguientes dominios:
- **Pesajes (`movimientos`, `movimiento_productos`, `entradas_producto`):** Proceso operativo crítico en báscula. Cada pesada, cálculo de tara y cierre de movimiento debe interactuar directamente con la base de datos para garantizar consistencia ACID absoluta.
- **Bitácora de Auditoría (`bitacora`, `bitacora_pesaje`):** Consultas de seguridad y auditoría legal que deben reflejar el estado vivo del servidor en cada consulta.
- **Notificaciones (`notificaciones_usuario`):** Bandeja gestionada mediante RPC transaccionales y suscripción Realtime dedicada por `id_usuario`.
- **Reportería Operativa y Exportación:** Generación de reportes PDF/Excel que demandan agregación analítica en tiempo real.
- **Sesión del Usuario Activo (`UsuarioSesion`):** Los datos y claims del usuario logueado residen exclusivamente en `IUsuarioSesionService`, aislados del subsistema de caché de catálogos compartidos.

---

## 5. Mitigación Exhaustiva de las 12 Trampas del Repositorio

La implementación debe blindarse específicamente contra las siguientes 12 trampas arquitectónicas, con especial énfasis en aquellas que compilan sin errores pero provocan fallas silenciosas en producción:

1. **`ICacheService` registrado como Singleton (Trampa Silenciosa #1):**
   Si `ICacheService` se registra como `Transient` o `Scoped` en DI, cada ViewModel o servicio consumidor recibirá una instancia aislada con su propia memoria interna vacía. El caching se degradará a un no-op absoluto que compilará limpiamente pero consumirá la red en cada llamada.
   *Mitigación:* Validación estricta en `DependencyInjection.cs` mediante `services.AddSingleton<ICacheService, FusionCacheService>()`.
2. **Registro del repositorio concreto por su tipo (Trampa Silenciosa #2):**
   Si el repositorio concreto se registra por su interfaz (`services.AddTransient<ICatalogoRepository, CatalogoRepository>()`), la lambda de fábrica del decorador `sp.GetRequiredService<ICatalogoRepository>()` se resolverá recursivamente a sí misma, disparando un `StackOverflowException` fatal e instantáneo.
   *Mitigación:* Registrar la implementación concreta por su clase: `services.AddTransient<CatalogoRepository>()`, y registrar la interfaz apuntando al decorador:
   `services.AddTransient<ICatalogoRepository>(sp => new CachedCatalogoRepository(sp.GetRequiredService<CatalogoRepository>(), sp.GetRequiredService<ICacheService>()))`.
3. **`CancellationToken.None` en la fábrica de FusionCache (Trampa Silenciosa #3):**
   Si se pasa el `CancellationToken` del llamador (ej. `_ctsVida.Token` de un modal) dentro de la fábrica de `GetOrSetAsync`, cerrar rápidamente el modal cancelará la fábrica en vuelo para **todas** las demás pantallas que estuvieran encoladas en el mismo single-flight.
   *Mitigación:* El token del llamador solo debe gobernar la espera de ese hilo llamador; la fábrica subyacente que consulta a Supabase debe ejecutarse con `CancellationToken.None` o con un timeout interno independiente.
4. **Retiro de `alRevalidar` y riesgo observable de UX:**
   Al eliminar el patrón *stale-while-revalidate*, desaparece la revalidación en segundo plano y el repintado en caliente (`alRevalidar`). Si Realtime estuviera desconectado, una mutación realizada en otra máquina no se reflejará hasta el vencimiento del TTL o una recarga forzada.
   *Mitigación:* Se asume conscientemente el trade-off; la publicación al 100% de las 8 tablas en `supabase_realtime` garantiza que el evento push reemplace con creces la revalidación por apertura, eliminando además el parpadeo en UI.
5. **Detección de tablas no publicadas en Realtime:**
   Evitar que nuevos módulos suscriban eventos sobre tablas no publicadas (como ocurrió en P-049).
   *Mitigación:* Validación defensiva en `RealtimeService` contrastando el nombre de tabla contra una lista blanca o consultando la RPC `tablas_publicadas_realtime()`, emitiendo advertencias en log ante suscripciones inertes.
6. **Incompatibilidad de serialización de `Result<T>`:**
   `Result<T>` en `CapaAplicacion` posee constructores privados y semántica inmutable, resultando incompatible con deserializadores estándar de `System.Text.Json`.
   *Mitigación:* Se descarta L2; FusionCache opera exclusivamente en L1 guardando referencias de memoria de objetos tipados (`IReadOnlyList<FiltroItem>`), sin serialización intermedia.
7. **Aislamiento estricto de permisos y claims:**
   *Mitigación:* Se prohíbe taxativamente cachear datos derivados de `IUsuarioSesionService.SesionActual`. Solo se permite cachear la matriz de módulos y acciones del sistema bajo la clave `rbac:definiciones`.
8. **Resincronización tras desconexión y reconexión:**
   Si la conexión de red se interrumpe, se pierden los eventos WAL emitidos durante el corte.
   *Mitigación:* En `RealtimeAwareViewModel.OnReconexionAsync` y en el handler global de reconexión de `IConexionMonitor`, invocar `_cache.RemoveByTagAsync("catalogos")` para forzar la recarga limpia de todos los catálogos en su próxima lectura.
9. **Saneamiento estricto de sesión en Logout multiusuario (Mitigación P-048):**
   *Mitigación:* En `MainWindow.LimpiarRecursosAsync()`, invocar determinísticamente `await _cacheService.ClearAsync(allowFailSafe: false)` y `CatalogoCache.InvalidarTodo()`, garantizando que la memoria quede libre de datos antes de que otro usuario ingrese en `LoginWindow`.
10. **Desacoplamiento de logging y UI en `InvalidadorCacheRealtime`:**
    El evento `OnCambioRecibido` de Realtime llega en el hilo de red del socket.
    *Mitigación:* La invalidación de memoria `RemoveByTag` se ejecuta de inmediato (síncrona en RAM), mientras que la emisión de logs estructurados y telemetría se despacha mediante un canal en segundo plano (`System.Threading.Channels.Channel<T>`).
11. **Protección contra avalanchas (Cache Stampede / Coalescencia):**
    Aperturas concurrentes de varias ventanas solicitando el mismo catálogo podrían disparar múltiples consultas simultáneas a Supabase.
    *Mitigación:* FusionCache implementa de forma nativa *single-flight request coalescing*, asegurando que ante 10 peticiones simultáneas, solo una viaje a la base de datos mientras las otras 9 esperan el resultado en memoria.
12. **Normalización canónica de nombres de tablas y tags:**
    Históricamente existieron discrepancias de nomenclatura (ej. `"taras"` vs `"tara"`).
    *Mitigación:* Centralizar las etiquetas de invalidación en constantes canónicas (`TagsCache.Catalogo(string nombreTabla)`), utilizando estrictamente el nombre físico de la tabla en Postgres en minúsculas.

---

## 6. Plan de Implementación y Roadmap en 5 Fases

| Fase | Alcance y Entregables | Criterio de Término Medible |
|---|---|---|
| **Fase 0: Preparación y Contratos** | - Incorporar paquete `ZiggyCreatures.FusionCache` versión `2.0.2` en `CapaDatos`.<br>- Definir contratos `ICacheService` y constantes `TagsCache` en `CapaAplicacion`.<br>- Validar compatibilidad en compilación con `net8.0`. | `dotnet build` finaliza con 0 errores y 0 warnings de dependencias. |
| **Fase 1: Infraestructura Central de Caché** | - Implementar `FusionCacheService : ICacheService` registrándolo como Singleton en DI.<br>- Implementar `InvalidadorCacheRealtime` escuchando `RealtimeService`.<br>- Crear suite de pruebas unitarias para verificación de tagging y fail-safe. | Pruebas unitarias de hit, miss, TTL e invalidación por tag superadas al 100%. |
| **Fase 2: Migración de Catálogos CRUD** | - Implementar decorador `CachedCatalogoRepository`.<br>- Eliminar dependencias directas de `CatalogoCache` en selectores y combos.<br>- Retirar callback `alRevalidar` y simplificar `SelectorCatalogoModal`. | Apertura de selector modal en 0 ms con 0 consultas de red generadas en aperturas repetidas. |
| **Fase 3: Saneamiento de Sesión y Deuda Técnica** | - Integrar `_cacheService.ClearAsync()` en `MainWindow.LimpiarRecursosAsync()` (resolución P-048).<br>- Migrar `RolPermisoRepository._catalogoCache` hacia `ICacheService`.<br>- Corregir o desuscribir `Observar()` en `ContactosFabricantesViewModel` y `ContactosProveedoresViewModel` (resolución P-049). | Cero persistencia de catálogos y permisos tras cerrar e iniciar sesión con otro usuario. |
| **Fase 4: Resincronización, Telemetría y Cierre** | - Implementar purga de tags en eventos de reconexión de red (`OnReconexionAsync`).<br>- Incorporar métricas de Hit/Miss ratio en logs de Serilog.<br>- Actualizar documentación del vault y marcar formalmente ADR-015 como superado. | Simulación de desconexión y reconexión de red recupera el 100% de consistencia sin errores. |

---

## 7. Consecuencias

### Positivas
- **Apertura ultra-fluida (0 ms):** Los selectores modales y combos cargan al instante desde memoria RAM sin latencia de red.
- **Reducción masiva de tráfico a Supabase:** Se eliminan cientos de consultas HTTP redundantes generadas por el anterior *stale-while-revalidate* en cada apertura de pantalla.
- **Consistencia reactiva multiusuario:** Cualquier cambio en catálogos realizado desde cualquier cliente se propaga inmediatamente mediante Realtime, invalidando la entrada en memoria en menos de 100 ms.
- **Aislamiento de terminales compartidas:** Erradicación total de la fuga de datos y permisos entre sesiones sucesivas en una misma PC (P-048).
- **Tolerancia a fallos de red:** La funcionalidad Fail-Safe de FusionCache permite seguir operando en modo lectura con datos en caché si Supabase experimenta micro-cortes o degradación temporal.

### Negativas / Riesgos Aceptados
- **Dependencia externa adicional:** Se incorpora `ZiggyCreatures.FusionCache` (v2.0.2), mitigada por su estricto confinamiento en `Microsoft.Extensions.Caching.Memory` 8.0.1.
- **Pérdida de auto-reparación por sondeo:** Si el servicio de Realtime experimenta una caída silenciosa y se realizan cambios en la base de datos, el cliente retendrá datos cacheados hasta que expire el TTL (2 h) o se reconecte la red. Este riesgo se mitiga con la purga obligatoria en `OnReconexionAsync` y los TTLs de seguridad.

---

## Relaciones

- [[Arquitectura Actual]] — estado vivo del sistema y diagrama de dependencias
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — decisión previa vigente en producción que será sucedida por este ADR
- [[Deuda Técnica - Pendientes]] — registro de deudas técnicas asociadas (P-048, P-049 y P-034)
- [[Conocimiento Principal]] — índice maestro de la base de conocimiento
- [[Módulo Productos]] — módulo principal consumidor de selectores y catálogos
- [[Gestor Realtime - Diseño Arquitectónico]] — infraestructura de WebSockets sobre la que se apoya la invalidación reactiva
```

---

## 6. Matriz de Dependencias Técnicas (FusionCache en .NET 8)

Para dar cumplimiento formal al requerimiento R3, se inspeccionaron los metadatos oficiales del paquete `ZiggyCreatures.FusionCache` en NuGet:

- **Versión evaluada:** `2.0.2`
- **Target Framework:** `net8.0`
- **Dependencias transitivas reportadas:**
  - `Microsoft.Extensions.Caching.Memory` (>= 8.0.1)
- **Estado del proyecto `BimboProyecto`:**
  - `CapaUI.csproj`: contiene `Microsoft.Extensions.DependencyInjection` en `8.0.1`.
  - `CapaDatos.csproj`: contiene `Microsoft.Extensions.DependencyInjection` en `8.0.1`.
- **Conclusión de gobernanza:** La versión `2.0.2` se integra de forma limpia y transparente en la solución. Soporta nativamente las APIs requeridas:
  - `RemoveByTag` / `RemoveByTagAsync`
  - `Clear` / `ClearAsync(allowFailSafe: false)`
  - Single-flight coalescing nativo
  - Fail-Safe configurable
  No introduce dependencias en preview ni paquetes de líneas futuras (.NET 9 / .NET 10).
