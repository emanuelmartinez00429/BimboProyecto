## 2026-09-02T18:00:30Z
You are worker_m1 (Archetype: teamwork_preview_worker).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m1
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
The technical specification report is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\survey_report.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

EXCLUSIVE WRITE OWNERSHIP:
You own `CapaDominio/Reglas/ReglasEntidades.cs`. Do NOT modify files outside your scope.

TASK (Milestone 1 - R1: Alineación de Reglas de Dominio con el Esquema de Supabase):
1. Read ORIGINAL_REQUEST.md and `survey_miner_1/survey_report.md`.
2. Update `CapaDominio/Reglas/ReglasEntidades.cs` to reflect the exact PostgreSQL / Supabase column constraints and UI limits:
   - `ReglasProducto`:
     - `Codigo`: Obligatorio: true, LargoMaximo: 50 (columna `codigo_producto`)
     - `Nombre`: Obligatorio: true, LargoMaximo: 200 (columna `nombre_producto`)
     - `Contenido`: Obligatorio: false, LargoMaximo: 100 (columna `contenido`)
     - `PesoTeorico`: Formato: FormatoCampo.Decimal (columna `peso_teorico`)
     - `PrecioPorKg`: Formato: FormatoCampo.Decimal (columna `precio_por_kg`)
   - `ReglasCategoria`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 100 (columna `nombre_categoria`)
     - `Descripcion`: Obligatorio: false, LargoMaximo: 200 (columna `descripcion_categoria`)
   - `ReglasPresentacion`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 100 (columna `nombre_presentacion`)
     - `Descripcion`: Obligatorio: false, LargoMaximo: 500 (columna `descripcion_presentacion`, con comentario `// Tope de UI de 500 caracteres (columna text en BD)`)
   - `ReglasFabricante`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 200 (columna `nombre_fabricante`)
     - `Descripcion`: Obligatorio: false, LargoMaximo: 500 (columna `descripcion_fabricante`, con comentario `// Tope de UI de 500 caracteres (columna text en BD)`)
   - `ReglasProveedor`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 200 (columna `nombre_proveedor`)
     - `Rtn`: Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Rtn (columna `rtn_proveedor`)
     - `Telefono`: Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono (columna `telefono_proveedor`)
     - `Correo`: Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo (columna `correo_proveedor`)
     - `Direccion`: Obligatorio: false, LargoMaximo: 500 (columna `direccion_proveedor`, con comentario `// Tope de UI de 500 caracteres (columna text en BD)`)
   - `ReglasEmpleado`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 100 (columna `nombre_empleado`)
     - `Apellido`: Obligatorio: true, LargoMaximo: 100 (columna `apellido_empleado`)
     - `Identidad`: Obligatorio: true, LargoMaximo: 20 (columna `numero_identidad`)
     - `Telefono`: Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono (columna `telefono_empleado`)
     - `Correo`: Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo (columna `correo_empleado`)
   - `ReglasUsuario`:
     - `Empleado`: Obligatorio: true (columna `id_empleado`)
     - `Rol`: Obligatorio: true (columna `id_rol`)
     - `Correo`: Obligatorio: true, LargoMaximo: 50, Formato: FormatoCampo.Correo (columna `alias_usuario`)
     - `Password`: Obligatorio: true, LargoMinimo: 6, LargoMaximo: 72 (Supabase Auth / Bcrypt)
   - `ReglasRol`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 50 (columna `nombre_rol`)
   - `ReglasContacto`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 100 (columna `nombre_contacto`)
     - `Telefono`: Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono (columna `telefono_contacto`)
     - `Correo`: Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo (columna `correo_contacto`)
   - `ReglasEmpresa`:
     - `Nombre`: Obligatorio: true, LargoMaximo: 200 (columna `nombre_empresa`)
     - `Rtn`: Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Rtn (columna `rtn_empresa`)
     - `Telefono`: Obligatorio: false, LargoMaximo: 20, Formato: FormatoCampo.Telefono (columna `telefono_empresa`)
     - `Correo`: Obligatorio: false, LargoMaximo: 100, Formato: FormatoCampo.Correo (columna `correo_empresa`)
     - `Direccion`: Obligatorio: false, LargoMaximo: 500 (columna `direccion_empresa`, con comentario `// Tope de UI de 500 caracteres (columna text en BD)`)
3. Ensure every field has comments documenting the physical database column and the `text` UI limit marker.
4. Run `dotnet build BimboProyecto.sln` and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` to verify clean compilation with 0 errors and all existing tests passing.
5. Write your complete handoff report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m1\handoff.md` and send a completion message to the parent orchestrator.
