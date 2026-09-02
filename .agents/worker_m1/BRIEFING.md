# BRIEFING — 2026-09-02T12:03:30-06:00

## Mission
Milestone 1 - R1: Align domain rules (`CapaDominio/Reglas/ReglasEntidades.cs`) with Supabase / PostgreSQL schema and UI bounds.

## 🔒 My Identity
- Archetype: teamwork_preview_worker
- Roles: implementer, qa, specialist
- Working directory: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m1
- Original parent: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Milestone: Milestone 1 - R1

## 🔒 Key Constraints
- Exclusive write ownership: `CapaDominio/Reglas/ReglasEntidades.cs`
- Integrity mandate: No cheating, no hardcoding test results, genuine domain rules matching schema constraints
- Full documentation with DB column names and UI limit markers (`// Tope de UI de 500 caracteres (columna text en BD)`)

## Current Parent
- Conversation ID: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Updated: 2026-09-02T12:03:30-06:00

## Task Summary
- **What to build**: Update `ReglasEntidades.cs` according to the Supabase column definitions from survey miner and user request specifications.
- **Success criteria**: All rules for Producto, Categoria, Presentacion, Fabricante, Proveedor, Empleado, Usuario, Rol, Contacto, Empresa configured with exact constraints, `dotnet build` passes with 0 warnings in CapaDominio / 0 errors in solution, all existing 118 tests pass.
- **Interface contracts**: `CapaDominio/Reglas/ReglasEntidades.cs`, `CapaDominio/Reglas/ReglaCampo.cs`, `CapaDominio/Reglas/FormatoCampo.cs`
- **Code layout**: `CapaDominio/Reglas/ReglasEntidades.cs`

## Key Decisions Made
- Updated lengths, required flags, formats, and comments for all 10 domain entities (34 rules).
- Documented physical database columns and UI limit markers (500 chars) for `text` columns.

## Change Tracker
- **Files modified**: `CapaDominio/Reglas/ReglasEntidades.cs` — Updated all entity rules to match Postgres schema constraints.
- **Build status**: Pass (0 errors).
- **Pending issues**: None.

## Quality Status
- **Build/test result**: Pass (118/118 tests passing, 0 errors).
- **Lint status**: Clean in CapaDominio.
- **Tests added/modified**: Existing tests run cleanly; worker_m5 will add drift test suite.

## Loaded Skills
- None required

## Artifact Index
- `DISPATCH.md` — Assignment instructions
- `BRIEFING.md` — Persistent memory
- `progress.md` — Liveness and task tracking
- `handoff.md` — Final handoff report
