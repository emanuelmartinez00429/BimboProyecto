## 2026-09-02T18:14:55Z
You are worker_m4 (Archetype: teamwork_preview_test_writer).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m4
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
The test infra spec is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\TEST_INFRA.md
The domain survey report is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\survey_report.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

EXCLUSIVE WRITE OWNERSHIP:
You own:
- `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`
- `BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs`
Do NOT modify files outside your ownership.

TASK (Milestone 4 - R5: Suite de Tests Automatizados de Deriva y Frontera):
1. Create `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` implementing three test suites:
   - **Test A (Deriva de Esquema contra PostgreSQL)**:
     - Reads `BIMBO_POSTGRES_CONNECTION_STRING` via `Environment.GetEnvironmentVariable`.
     - If connection string is missing or empty, gracefully returns without failing (for offline CI).
     - If present, opens `NpgsqlConnection` and queries `information_schema.columns` for `table_schema = 'public'`.
     - Validates for all 34 rules that mapped columns exist in database, no `LargoMaximo` exceeds physical column length, and all `text` columns are marked as UI ceilings (500).
   - **Test B (Valores Fijados para Ejecución Offline / CI)**:
     - `[Theory]` tests covering all 34 domain rules across all 10 domain classes (`ReglasProducto`, `ReglasCategoria`, `ReglasPresentacion`, `ReglasFabricante`, `ReglasProveedor`, `ReglasEmpleado`, `ReglasUsuario`, `ReglasRol`, `ReglasContacto`, `ReglasEmpresa`).
     - Asserts `Obligatorio`, `LargoMaximo`, `LargoMinimo`, `Formato`.
   - **Test C (Auditoría Exhaustiva por Reflexión)**:
     - Uses Reflection (`typeof(ReglasProducto).Assembly`) to scan `CapaDominio.Reglas.*` for all `public static readonly ReglaCampo` fields.
     - Asserts that 100% of declared `ReglaCampo` fields exist in the audit map.
2. Extend `BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs` with boundary test cases:
   - `NoExcedeLargo`: null, empty, whitespace trim, exact length N, N + 1, whitespace-padded N, whitespace-padded N + 1.
   - `TieneLargoMinimo`: null (min 6 -> false, min 0 -> true), empty (min 6 -> false), exact min, min - 1.
3. Verify compilation and test execution:
   Run `dotnet build BimboProyecto.sln` (0 errors) and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` (100% pass rate).
4. Write your handoff report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m4\handoff.md` and send a completion message to the parent orchestrator.
