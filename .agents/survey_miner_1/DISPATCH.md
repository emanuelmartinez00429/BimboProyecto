## 2026-09-02T17:50:22Z
TASK:
1. Read ORIGINAL_REQUEST.md completely.
2. Investigate the Domain Layer and Database Schema definitions in the codebase:
   - Check `CapaDominio/Reglas/ReglasEntidades.cs`, `ReglasFormato.cs`, `ReglaCampo.cs`, and all related domain rules.
   - Check all entity models (Producto, Categoria, Presentacion, Fabricante, Proveedor, Empleado, Usuario, Rol, Contacto, Empresa).
   - Check any database schema files, migrations, SQL scripts, or Supabase connection definitions in the repository.
   - Analyze R1 requirements in detail: column lengths, data types, `text` columns requiring UI limits of 500 chars, RTN, telefono, correo, identity rules.
   - Analyze R5 requirements: structure of existing tests in `BimboProyecto.Tests/`, how `ReglasEntidadesTests.cs` and `ReglasFormatoTests.cs` should be designed (Deriva/Postgres information_schema vs offline fixed values vs reflection audit).
3. Write your comprehensive findings to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\survey_report.md`.
4. Send a completion message to the parent orchestrator with the path to your report.
