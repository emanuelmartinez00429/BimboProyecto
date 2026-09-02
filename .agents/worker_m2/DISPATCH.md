## 2026-09-02T18:06:27Z
You are worker_m2 (Archetype: teamwork_preview_worker).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m2
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
The UI survey report is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_2\survey_report.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

EXCLUSIVE WRITE OWNERSHIP:
You own:
- `CapaUI/Core/Validacion/ValidadorFormulario.cs`
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedorModal.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricanteModal.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriaModal.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionModal.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml` and `EmpleadoModal.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs`
Do NOT modify files outside your ownership.

TASK (Milestone 2 - R2: Derivación Automática de MaxLength y Limpieza de XAML):
1. In `CapaUI/Core/Validacion/ValidadorFormulario.cs`:
   - In `ConstructorCampo`: Implement `TopePreventivo(int max)`:
     If `_campo.Control` is `TextBox tb` and `tb.MaxLength == 0`, set `tb.MaxLength = max`.
     Else if `_campo.Control` is `PasswordBox pb` and `pb.MaxLength == 0`, set `pb.MaxLength = max`.
   - In `Segun(ReglaCampo regla)`: When `regla.LargoMaximo is int m`, call `TopePreventivo(m)` and `LargoMaximo(m)`.
   - In `LargoMaximo(int largo, string? mensaje = null)`: Also call `TopePreventivo(largo)`.
2. Remove manual `MaxLength="100"` from XAML:
   - `ProveedorModal.xaml`: `TxtNombre`
   - `FabricanteModal.xaml`: `TxtNombre`
   - `CategoriaModal.xaml`: `TxtNombre`
   - `PresentacionModal.xaml`: `TxtNombre`
   - `EmpleadoModal.xaml`: `TxtNombre` and `TxtApellido`
3. Add missing field registrations in modal code-behinds:
   - `EmpleadoModal.xaml.cs`: Add `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)` to `_validador`.
   - `ProductoModal.xaml.cs`: Add `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)` to `_validador`.
   - `UsuarioModal.xaml.cs`: Add `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)` to `_validador`.
4. In `UsuarioModal.xaml.cs`:
   - Update `GenerarEmail(string nombreCompleto)` to truncate the local part before `@empresa.com` (max local part = 38 chars) so the generated email never exceeds the 50-character limit of `alias_usuario` in PostgreSQL while preserving the full `@empresa.com` domain.
5. Verify build and tests:
   Run `dotnet build BimboProyecto.sln` (0 errors) and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` (all passing).
6. Write your handoff report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m2\handoff.md` and message the parent orchestrator when complete.
