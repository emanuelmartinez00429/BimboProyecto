## 2026-09-02T18:03:38Z

You are reviewer_m1_1 (Archetype: teamwork_preview_reviewer).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\reviewer_m1_1
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
The worker handoff is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m1\handoff.md

TASK:
1. Read ORIGINAL_REQUEST.md, PROJECT.md, and `worker_m1/handoff.md`.
2. Inspect `CapaDominio/Reglas/ReglasEntidades.cs`.
3. Verify that all 10 domain entities and their 34 rules strictly match R1 specifications and PostgreSQL column sizes:
   - Producto: Codigo (50), Nombre (200), Contenido (100).
   - Categoria: Nombre (100), Descripcion (200).
   - Presentacion: Nombre (100), Descripcion (500 - UI limit).
   - Fabricante: Nombre (200), Descripcion (500 - UI limit).
   - Proveedor: Nombre (200), Rtn (20), Telefono (20), Correo (100), Direccion (500 - UI limit).
   - Empleado: Nombre (100), Apellido (100), Identidad (20, obligatorio), Telefono (20), Correo (100).
   - Usuario: Empleado, Rol, Correo (50, FormatoCampo.Correo), Password (min 6, max 72).
   - Rol: Nombre (50).
   - Contacto: Nombre (100), Telefono (20), Correo (100).
   - Empresa: Nombre (200), Rtn (20), Telefono (20), Correo (100), Direccion (500 - UI limit).
   - All `text` columns have lexical marker `// Tope de UI de 500 caracteres (columna text en BD)`.
4. Run `dotnet build BimboProyecto.sln` and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` and verify 0 errors and all tests pass.
5. Write your report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\reviewer_m1_1\handoff.md` with a clear verdict of `APPROVE` or `REQUEST_CHANGES`.
6. Send a message to parent with your verdict and report path.
