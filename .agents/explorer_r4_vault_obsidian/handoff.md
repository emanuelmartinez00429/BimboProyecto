# Handoff Report — Explorer 3 (Vault Obsidian, ADR-026, Deuda Técnica)

**Fecha:** 2026-09-02  
**Autor:** Explorer 3 (`teamwork_preview_explorer`)  
**Misión:** R4 — Estructura Obsidian, Bóveda de Conocimiento y Preparación del Diseño para ADR-026, Deuda Técnica y Arquitectura Actual  
**Tipo de Handoff:** Hard (Tarea de exploración completada al 100%)  
**Archivo de Análisis Asociado:** `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r4_vault_obsidian\analysis.md`  

---

## 1. Observation (Observaciones Verificadas)

Se realizaron observaciones directas y empíricas sobre el código fuente, la bóveda de conocimiento y la base de datos de producción:

1. **Protocolo de la Bóveda Obsidian (`contexto/AGENTS.md`):**
   - Líneas 58-76 (`§2. Frontmatter obligatorio`): El frontmatter debe contener `title`, `tags`, `date` y `estado` (`propuesto`, `aceptado`, `reemplazado`). Los campos `autor:` y `autor_cambios:` aplican exclusivamente a notas de sesión (`70 - Bitácora de Cambios/`).
   - Líneas 79-88 (`§3. Convención de nombres`): Formato `ADR-NNN - Título.md`. Deuda técnica exclusivamente como ítems `P-NNN` dentro de `Deuda Técnica - Pendientes.md`.
   - Líneas 93-97 (`§4. Enlazado y relaciones`): Uso estricto de `[[wikilink]]` sin `.md` y cierre obligatorio con `## Relaciones`.
   - Líneas 100-108 (`§5. Regla anti-duplicados`): "si dudás entre dos nombres para el mismo concepto → usá el que ya exista".
2. **Estado y Frontmatter de ADR-015 (`contexto/45 - Decisiones/ADR-015 - Cache de catalogos mostrar y revalidar.md`):**
   - Líneas 1-10:
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
   - ADR-015 describe el comportamiento vigente en producción. Está terminantemente prohibido alterar su frontmatter (`estado: aceptado`) o marcarlo como reemplazado mientras ADR-026 permanezca en `estado: propuesto`.
3. **Publicación empírica en Supabase Realtime (Producción `bzmmrifjgzlvsphctais`):**
   - Consulta SQL viva sobre `pg_publication_tables`:
     ```sql
     select tablename from pg_publication_tables where pubname = 'supabase_realtime';
     ```
     Resultado confirmado: Las **8 tablas de catálogo** (`categoria`, `fabricante`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida`) **están efectivamente publicadas**.
     Tablas no publicadas: `contactos_fabricante`, `contactos_proveedor`, `bitacora`, `roles`, `acciones`, `modulos`, `empresa`.
4. **Fuga de datos entre sesiones en terminal compartida (P-048):**
   - `CapaUI/Core/Catalogos/CatalogoCache.cs:177`: `public static void InvalidarTodo() => _cache.Clear();` existe pero **nunca es invocado** durante el logout.
   - `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs:23`: `private static IReadOnlyList<ModuloAccionesDto>? _catalogoCache;` es un campo estático privado que carece de cualquier método de invalidación o ciclo de vida.
   - `CapaUI/Formularios/Principal/MainWindow.xaml.cs:660-692` (`LimpiarRecursosAsync`): Realiza `SignOut`, limpia `SesionPermisos` y cierra el WebSocket, pero **no purga `CatalogoCache` ni `RolPermisoRepository._catalogoCache`**. Como `App.Services` persiste en el proceso WPF tras cerrar `MainWindow`, un nuevo usuario hereda los datos y permisos cacheados.
5. **Suscripciones zombies a tablas no publicadas (P-049):**
   - `ContactosFabricantesViewModel.cs:127`: `Observar("contactos_fabricante", OnCambioContacto);`
   - `ContactosProveedoresViewModel.cs:127`: `Observar("contactos_proveedor", OnCambioContacto);`
   - Dado que `contactos_fabricante` y `contactos_proveedor` no están en `supabase_realtime`, PostgreSQL jamás emite eventos WAL hacia el websocket para estas tablas. El modo de falla es completamente silencioso.
6. **Dependencias del paquete `ZiggyCreatures.FusionCache` (NuGet API):**
   - Consulta a la API de registro de NuGet: Para el target `net8.0`, `ZiggyCreatures.FusionCache` versión `2.0.2` depende únicamente de `Microsoft.Extensions.Caching.Memory [8.0.1, )`.
   - `CapaUI.csproj` y `CapaDatos.csproj` ya referencian `Microsoft.Extensions.DependencyInjection` en `8.0.1`. No se introducen paquetes de .NET 9 ni dependencias en preview.

---

## 2. Logic Chain (Cadena Lógica de Razonamiento)

1. **Premisa 1 (Frescura y Costo de Red):** En ADR-015, *stale-while-revalidate* obligaba a realizar 1 consulta HTTP por apertura de modal para evitar que la UI mostrara datos obsoletos.
2. **Premisa 2 (Viabilidad de Eventos Push):** La confirmación empírica de que las 8 tablas de catálogos están 100% publicadas en `supabase_realtime` (Observación 3) demuestra que cualquier mutación efectuada en la base de datos se notifica de forma instantánea a todos los clientes conectados.
3. **Inferencia 1 (Eliminación de Redundancia):** Al contar con invalidación reactiva garantizada por Realtime, el sondeo HTTP en cada apertura de modal (`alRevalidar`) se vuelve redundante y puede eliminarse sin riesgo de inconsistencia, logrando aperturas puras en 0 ms.
4. **Premisa 3 (Seguridad de Planta y Simplicidad de Memoria):** Las terminales de planta son compartidas entre turnos y carecen de un backend intermediario (arquitectura BaaS). Emplear Redis expondría secretos maestros sin RLS, mientras que SQLite persistiría datos en disco entre usuarios y fallaría al deserializar `Result<T>` con constructores privados.
5. **Inferencia 2 (Confinamiento L1):** La memoria RAM (L1) gestionada por FusionCache con tagging (`RemoveByTag`) es el único nivel que garantiza latencia sub-milisegundo, costo cero de infraestructura, inmunidad ante deserialización de `Result<T>` y purga total en cierre de sesión.
6. **Inferencia 3 (Saneamiento P-048 y P-049):** La integración de `ICacheService.ClearAsync()` en `MainWindow.LimpiarRecursosAsync()` erradica la fuga P-048. La identificación de `contactos_fabricante` y `contactos_proveedor` fuera de la publicación permite documentar formalmente P-049 y prevenir suscripciones ilusorias.

---

## 3. Caveats (Límites de la Investigación y Supuestos)

- **Supuesto de conectividad WebSocket:** Se asume que el canal WebSocket de Supabase permanece conectado en condiciones normales de red de planta. Ante desconexión prolongada, la frescura depende del TTL de seguridad (2 h) o de la reconexión que fuerza la purga selectiva en `OnReconexionAsync`.
- **Áreas no investigadas:** No se alteraron archivos de código fuente (`.cs`, `.xaml`, `.csproj`) ni de la bóveda (`contexto/`), cumpliendo estrictamente la restricción de agente de sólo lectura (Read-Only). Toda la documentación generada reside en `.agents/explorer_r4_vault_obsidian/`.
- **Riesgo visual aceptado:** Al retirar `alRevalidar`, desaparece el parpadeo de revalidación. Se asume como un beneficio de UX y no como una degradación.

---

## 4. Conclusion (Conclusiones y Decisiones)

1. **ADR-026 redactado y listo:** Se formuló el contenido completo de `ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md` con frontmatter estricto (`estado: propuesto`), contexto de los 5 problemas, descarte fundamentado de L2, matriz de TTL/Fail-Safe/Zero-Cache, mitigación de 12 trampas y roadmap de 5 fases.
2. **ADR-015 preservado:** Su frontmatter se mantiene inalterado (`estado: aceptado`).
3. **P-048 y P-049 formulados quirúrgicamente:** Listos para ser insertados en `Deuda Técnica - Pendientes.md` tras el ítem P-047 y en su tabla de historial.
4. **Callout para Arquitectura Actual definido:** Ubicación e inserción exacta identificada en las líneas 12-15 de `Arquitectura Actual.md`.
5. **Dependencia fijada:** `ZiggyCreatures.FusionCache` versión `2.0.2` sobre `net8.0` es la versión recomendada y verificada.

---

## 5. Verification Method (Método de Verificación Independiente)

Para verificar independientemente los hallazgos y artefactos generados:

1. **Inspección de artefactos generados:**
   - Visualizar `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r4_vault_obsidian\analysis.md`
   - Visualizar `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r4_vault_obsidian\handoff.md`
2. **Verificación de dependencias de FusionCache en NuGet:**
   ```powershell
   powershell -Command "(Invoke-RestMethod 'https://api.nuget.org/v3/registration5-semver1/ziggycreatures.fusioncache/index.json').items[0].items | Where-Object { $_.catalogEntry.version -eq '2.0.2' } | Select-Object -ExpandProperty catalogEntry | Select-Object -ExpandProperty dependencyGroups"
   ```
3. **Verificación de archivos y líneas críticas:**
   - Inspeccionar `CapaUI/Core/Catalogos/CatalogoCache.cs:177` (presencia de `InvalidarTodo()`).
   - Inspeccionar `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs:23` (campo `_catalogoCache`).
   - Inspeccionar `CapaUI/Formularios/Principal/MainWindow.xaml.cs:660-692` (ausencia de purga en `LimpiarRecursosAsync()`).
   - Inspeccionar `ContactosFabricantesViewModel.cs:127` y `ContactosProveedoresViewModel.cs:127` (llamadas `Observar()` a tablas no publicadas).
