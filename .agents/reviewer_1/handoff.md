# Informe de Revisión y Validación Adversarial — ADR-026

**Revisor**: Reviewer 1 (`teamwork_preview_reviewer`)  
**Roles**: reviewer, critic  
**Fecha**: 2026-09-03  
**Directorio de trabajo**: `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\reviewer_1\`  
**Veredicto**: **APPROVE**  

---

## 1. Observation (Observaciones Directas y Evidencia Empírica)

### 1.1. Integridad de Código Fuente y Árbol Git
- **Comandos ejecutados**: `git status` y `git diff --stat`.
- **Archivos modificados**:
  - `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` (modificado — callout y relaciones)
  - `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (modificado — P-048 y P-049)
  - `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md` (creado)
  - `.agents/*` (metadatos exclusivos de agentes)
- **Código fuente alterado**: **0 líneas**. Ningún archivo `.cs`, `.xaml`, `.csproj`, `.sln` ni migración SQL fue modificado en el working tree.

### 1.2. Preservación Estricta de ADR-015
- **Archivo**: `contexto/45 - Decisiones/ADR-015 - Cache de catalogos mostrar y revalidar.md`
- **Frontmatter observado (Líneas 1-10)**:
  ```yaml
  ---
  title: "ADR-015 — Caché de catálogos: mostrar y revalidar"
  tags:
    - adr
    - decision
    - cache
    - realtime
  date: 2026-08-13
  estado: aceptado
  ---
  ```
- **Verificación**: El frontmatter permanece 100% inalterado, conservando `estado: aceptado` sin marcarlo como reemplazado prematuramente, en total apego a la directriz arquitectónica.

### 1.3. Conformidad del Frontmatter y Estilo de ADR-026
- **Archivo**: `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`
- **Frontmatter observado (Líneas 1-11)**:
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
- **Verificación contra `contexto/AGENTS.md §2`**:
  - `estado: propuesto` (correcto para ADRs en evaluación).
  - Tags: `adr`, `decision`, `cache`, `realtime`, `rendimiento` (alineados con la taxonomía del vault).
  - Fecha absoluta: `2026-09-02`.
  - **No contiene** los campos `autor:` ni `autor_cambios:` (reservados exclusivamente para notas de sesión).
  - Concluye con la sección `## Relaciones` con wikilinks bidireccionales hacia `[[Arquitectura Actual]]`, `[[ADR-015 - Cache de catalogos mostrar y revalidar]]`, `[[Deuda Técnica - Pendientes]]`, `[[Conocimiento Principal]]`, `[[Módulo Productos]]` y `[[Gestor Realtime - Diseño Arquitectónico]]`.

### 1.4. Cobertura Técnica en ADR-026
1. **5 Problemas Actuales (§1)**:
   - Sobrecarga de consultas de fondo redundantes por apertura (ineficiencia de *stale-while-revalidate*).
   - Fuga de datos y permisos entre sesiones en terminal compartida (P-048).
   - Suscripciones zombies a tablas no publicadas en Realtime (P-049).
   - Dispersión de cachés sin gobernanza arquitectónica.
   - Inexistencia de resincronización tras corte y reconexión de red.
2. **8 Tablas Publicadas en `supabase_realtime` (§2)**:
   - Identificadas con certeza empírica: `categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`. Delimita explícitamente que tablas como `contactos_fabricante`, `contactos_proveedor`, `bitacora`, `roles`, etc., no están en la publicación.
3. **Descarte Categórico de Niveles L2 (§4)**:
   - Rechazo formal de Redis/Garnet: ausencia de backend intermedio en app de escritorio, credenciales maestras desprotegidas en PCs de planta sin RLS, costo innecesario para <500 KB de datos.
   - Rechazo formal de SQLite local en disco: persistencia y agravamiento de fuga de datos entre usuarios (P-048), incompatibilidad de serialización JSON con constructores privados de `Result<T>` (`CapaAplicacion4/Common/Result.cs:13,28`), latencia y contención de I/O en disco.
4. **Matriz de TTL, Jitter, Fail-Safe y Zonas Zero-Cache (§5)**:
   - Matriz por familias: Ultra-estables (TTL 24h, Jitter 30m, Fail-Safe 7d), Negocio (TTL 2h, Jitter 15m, Fail-Safe 24h), Productos (TTL 30m, Jitter 5m, Fail-Safe 2h), RBAC (TTL 1h, Jitter 10m, Fail-Safe 4h).
   - Zonas Zero-Cache delimitadas taxativamente: Pesajes, Bitácora, Notificaciones, Reportes, Sesión/Permisos de usuario autenticado, Tablas no publicadas.
5. **Mitigación Exhaustiva de las 12 Trampas (§6)**:
   - **Trampa 1 (Error silencioso)**: `ICacheService` Singleton vs Transient (hit-rate 0% silencioso).
   - **Trampa 2 (Error silencioso / Crash)**: Registro concreto `CatalogoRepository` por su tipo de clase vs interfaz (evita recursión infinita y `StackOverflowException` en DI).
   - **Trampa 3 (Error silencioso)**: `CancellationToken.None` en fábrica de FusionCache vs `_ctsVida.Token` de `SelectorCatalogoModal.xaml.cs:74` (evita aborto en cascada de solicitudes single-flight coalescidas).
   - **Trampa 4 (Riesgo observable UX)**: Retiro de `alRevalidar` y cese del repintado en caliente en el `DataGrid`, suprimiendo parpadeo y pérdida de foco.
   - **Trampas 5 a 12**: Detección de tablas no publicadas, no-serialización de `Result<T>`, purga determinística de permisos con `ClearAsync(allowFailSafe: false)`, purga en `OnReconexionAsync`, supresión de `SizeLimit`, anti-stampede con jitter, armonización de fail-safe, y desacoplamiento de logs por `Channel<T>` fuera del hilo de UI.
6. **Selección y Pinning de Dependencias (§7)**:
   - Fijación estricta de **`ZiggyCreatures.FusionCache [2.0.2]`**.
   - Verificado: única versión que soporta `RemoveByTag`, `ClearAsync(allowFailSafe: false)` y confina sus dependencias transitivas estrictamente a `Microsoft.Extensions.Caching.Memory 8.0.1` (net8.0 LTS), sin arrastrar paquetes de .NET 9.
7. **Roadmap de 5 Fases (§8)**:
   - Fase 0 a Fase 4 estructuradas con alcance, entregables y criterios de término medibles.

### 1.5. Fichas de Deuda Técnica P-048 y P-049
- **Archivo**: `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`
- **P-048 (Líneas 917-941)**:
  - Archivos referenciados: `CatalogoCache.cs:177` (`InvalidarTodo`), `RolPermisoRepository.cs:23-24` (`_catalogoCache` estático privado sin invalidación), `MainWindow.xaml.cs:660-692` (`LimpiarRecursosAsync` no limpia catálogos), `App.xaml.cs` (root provider singleton).
  - Riesgo: Alto en terminales compartidas de planta.
  - Solución y estado: `[ ] Pendiente 🔴`.
- **P-049 (Líneas 943-970)**:
  - Archivos referenciados: `ContactosFabricantesViewModel.cs:127` y `ContactosProveedoresViewModel.cs:127` invocan `Observar()` sobre tablas fuera de `supabase_realtime`.
  - Modo de falla: Suscripciones zombies silenciosas.
  - Solución y estado: `[ ] Pendiente`.
- **Tabla `## Historial de resolución`**: Ambas fichas incorporadas al final de la tabla (líneas 1022-1023).
- **Sección `## Relaciones`**: Enlace bidireccional incorporado hacia ADR-026.

### 1.6. Callout y Relaciones en Arquitectura Actual
- **Archivo**: `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`
- **Callout (Líneas 16-18)**:
  ```markdown
  > [!info] Propuesta arquitectónica — Caché L1 en memoria e invalidación reactiva por Realtime
  > Se encuentra en evaluación la transición del modelo de catálogos (*stale-while-revalidate* de [[ADR-015 - Cache de catalogos mostrar y revalidar]]) hacia una caché unificada L1 en memoria administrada mediante `ZiggyCreatures.FusionCache` e invalidación reactiva basada en eventos de `supabase_realtime`. Esta propuesta elimina las consultas de fondo redundantes por apertura de selector modal, erradica el riesgo de fuga de datos entre sesiones en una misma terminal ([[Deuda Técnica - Pendientes#P-048]]), y descarta formalmente el uso de almacenamiento L2 en clientes de planta. Ver [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]].
  ```
- **Próximos pasos (Línea 214)**: Actualizado el ítem 2 con wikilink a ADR-026.
- **Sección `## Relaciones` (Líneas 218-226)**: Incorporada al final de la nota enlazando a ADR-026, ADR-015, Deuda Técnica, Conocimiento Principal, Módulo Productos y Gestor Realtime.

### 1.7. Compilación y Suite de Tests
- **`dotnet build BimboProyecto.sln`**: 0 Advertencias, 0 Errores.
- **`dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`**: 223 pruebas ejecutadas, 223 superadas (100%), 0 omitidas, 0 fallos.

---

## 2. Logic Chain (Cadena de Razonamiento Lógico)

1. **Premisa 1 (Alcance Estricto y Cero Código)**: El requerimiento de misión prohíbe alterar código fuente (.cs, .xaml, .csproj, .sln). Los comandos de inspección git (`git status`, `git diff --stat`) demuestran que la superficie de modificación se restringió exclusivamente a la documentación Obsidian (`ADR-026`, `Deuda Técnica - Pendientes.md`, `Arquitectura Actual.md`), preservando la integridad del código existente.
2. **Premisa 2 (No Regresión de ADR-015)**: ADR-015 describe el comportamiento operativo actual en producción. Su frontmatter no fue alterado (`estado: aceptado`). La relación de sucesión futura queda claramente delimitada en ADR-026 (`estado: propuesto`).
3. **Premisa 3 (Garantía Factual de P-048 y P-049)**: La inspección en frío del código fuente confirmó que:
   - `MainWindow.LimpiarRecursosAsync()` carece de llamadas a `CatalogoCache.InvalidarTodo()`.
   - `RolPermisoRepository._catalogoCache` es un campo estático sin mecanismo de purga.
   - `ContactosFabricantesViewModel` y `ContactosProveedoresViewModel` observan tablas ausentes de `supabase_realtime`.
   Por lo tanto, la documentación de P-048 y P-049 en Deuda Técnica no es una elucubración teórica, sino un hallazgo verificado en el código del repositorio.
4. **Premisa 4 (Solidez Adversarial de las 12 Trampas)**:
   - La trampa de `StackOverflowException` al decorar `ICatalogoRepository` se verificó directamente en `CapaDatos/DependencyInjection.cs:69-70`.
   - La trampa de cancelación en cascada de single-flight por `_ctsVida.Token` se verificó en `SelectorCatalogoModal.xaml.cs:74, 588`.
   - La imposibilidad de serializar `Result<T>` en L2 se verificó en los constructores privados de `Result.cs:13, 28`.
   - Por lo tanto, las mitigaciones formuladas en ADR-026 neutralizan fallos silenciosos críticos antes de escribir una sola línea de código en Fase 1.
5. **Premisa 5 (Aislamiento de Dependencias de Plataforma)**: La auditoría del ecosistema NuGet confirma que `ZiggyCreatures.FusionCache 2.0.2` es la versión dorada que preserva la compatibilidad estricta con `net8.0` y `Microsoft.Extensions 8.x`, evitando contaminaciones de paquetes `9.x`.
6. **Conclusión Lógica**: El entregable satisface al 100% todos los criterios de aceptación, no introduce deuda técnica, respeta el protocolo de la bóveda Obsidian (`contexto/AGENTS.md`) y proporciona un plano arquitectónico de máxima rigurosidad técnica.

---

## 3. Caveats (Advertencias y Supuestos Considerados)

- **Supuesto de Publicación**: Se asume que las 8 tablas de catálogos (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`) permanecerán en `supabase_realtime` en la base de datos de producción. Si en el futuro un DBA ejecutara `ALTER PUBLICATION supabase_realtime DROP TABLE`, el sistema degradaría a su red de seguridad (TTL base de 2 horas) hasta que se detecte la desconexión.
- **Comportamiento Offline Prolongado**: Si una terminal pierde la conexión de red por más de 7 días (límite máximo de fail-safe para catálogos ultra-estables), la caché expirará totalmente y requerirá reconexión a Supabase para reabrir selectores. Esto se considera un comportamiento aceptable y deseable para evitar obsolescencia desmedida.
- **Implementación Futura**: La aprobación de este ADR no altera el binario de la aplicación en producción. La ejecución de las Fases 0 a 4 requerirá una sesión posterior de desarrollo con su respectivo plan de pruebas e integración continua.

---

## 4. Conclusion (Veredicto y Dictamen)

**Veredicto Oficial: APPROVE**

El trabajo entregado por el equipo cumple con los más altos estándares de rigor técnico, diseño arquitectónico y defensa adversarial:
- Cero líneas de código alteradas en esta fase de diseño.
- Frontmatter de ADR-015 inalterado y en `estado: aceptado`.
- ADR-026 exhaustivo, formal, con frontmatter limpio sin autor, con las 12 trampas neutralizadas y pinning exacto en `[2.0.2]`.
- Fichas P-048 y P-049 rigurosamente documentadas en Deuda Técnica con anclas exactas al código fuente.
- Callout y enlaces bidireccionales perfectamente integrados en `Arquitectura Actual.md`.
- Build (0 errores) y test suite (223/223 pasando) verificados.

---

## 5. Verification Method (Comandos de Verificación Independiente)

Cualquier auditor o agente puede verificar independientemente este dictamen ejecutando los siguientes comandos en la raíz del proyecto:

1. **Verificar que no hay cambios en código fuente**:
   ```powershell
   git status --porcelain | Select-String -Pattern '\.(cs|xaml|csproj|sln)$'
   # Resultado esperado: Salida vacía (0 coincidencias)
   ```

2. **Verificar compilación limpia de la solución**:
   ```powershell
   dotnet build BimboProyecto.sln
   # Resultado esperado: 0 Advertencia(s), 0 Errores
   ```

3. **Verificar ejecución de la suite de pruebas**:
   ```powershell
   dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
   # Resultado esperado: 223 pruebas superadas (100% de éxito)
   ```

4. **Verificar frontmatter de ADR-015**:
   ```powershell
   Get-Content 'contexto/45 - Decisiones/ADR-015 - Cache de catalogos mostrar y revalidar.md' -Head 10
   # Resultado esperado: estado: aceptado
   ```

5. **Verificar frontmatter de ADR-026**:
   ```powershell
   Get-Content 'contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md' -Head 12
   # Resultado esperado: estado: propuesto, sin campos autor / autor_cambios
   ```
