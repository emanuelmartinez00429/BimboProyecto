# Reglas del Proyecto Bimbo Honduras (Memoria Persistente)

Este archivo define las reglas de oro y directrices de desarrollo, ejecución y documentación para el asistente de IA en este repositorio.

---

## 1. Reglas de Código y Arquitectura (No Negociables)

1. **Todo el trabajo nuevo va en `CapaUI`:**
   - Nunca tocar `BimboPesaje` (proyecto histórico de referencia WinForms, fuera de la solución).
2. **Dirección estricta de dependencias:**
   - `CapaAplicacion` **nunca** referencia `CapaDatos`. `CapaDatos` implementa los contratos de `CapaAplicacion`.
3. **Mapeo con Supabase / Postgres:**
   - Columnas en BD en `snake_case` ↔ C# en `camelCase` con atributo `[Column("...")]`.
   - Claves primarias con `[PrimaryKey("id_...")]`.
   - Convención de estados: `id_estado = 1` (activo) / `id_estado = 2` (inactivo).
   - `Fabricante` **no** hereda de `BaseModel` (nunca invocar `client.From<Fabricante>()`, se carga vía join).
4. **Patrón MVVM:**
   - Uso mandatorio de **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`) en clases `partial`.
   - Prohibido implementar `INotifyPropertyChanged` a mano o usar `RelayCommand` locales.
5. **Repositorios y Manejo de Errores:**
   - Repositorios nuevos deben heredar de `RepositorioBase`, usar `TryAsync` y retornar `Result` o `Result<T>`.
6. **Sesión y Permisos:**
   - Se leen exclusivamente vía `IUsuarioSesionService` (no existe estado global ni `SesionActual` estático).
7. **Fuente de verdad para validaciones (ADR-021):**
   - El negocio y los límites físicos viven en `CapaDominio/Reglas/ReglasEntidades.cs`.
   - Ante cualquier discrepancia entre dominio y esquema, **el esquema de la BD manda** (cero migraciones innecesarias).

---

## 2. Reglas de Ejecución y Verificación

1. **Build:**
   - `dotnet build BimboProyecto.sln` debe compilar siempre con **0 errores** (los warnings de nullable en `CapaDatos` son preexistentes).
2. **Tests:**
   - `dotnet test` debe mantenerse en verde.
3. **Verificación funcional:**
   - Compilación limpia + prueba manual del flujo/pantalla modificada.

---

## 3. Reglas de Documentación en la Bóveda (`contexto/`)

1. **Documentar siempre:**
   - Trabajo de la sesión → `contexto/70 - Bitácora de Cambios/AAAA-MM/Sesión AAAA-MM-DD - Título.md`.
   - Decisiones arquitecturales → `contexto/45 - Decisiones/ADR-NNN - Título.md`.
   - Patrones recurrentes → `contexto/20 - Patrones/`.
   - Deuda técnica descubierta → ítem `P-NNN` en `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`.
2. **Regla anti-duplicados:**
   - Buscar primero en `contexto/00 - MOC/Conocimiento Principal.md` antes de crear notas nuevas.
3. **Formato:**
   - Frontmatter YAML obligatorio (`title`, `tags`, `date` con fecha absoluta).
   - Enlaces con `[[wikilink]]` y sección final `## Relaciones`.
