# Project: Bimbo Honduras - MaxLength Validation & Domain Alignment

## Architecture
- **CapaDominio**: Core business rules and format validators (`ReglasEntidades.cs`, `ReglasFormato.cs`, `ReglaCampo.cs`, `ReglasLogin.cs`, `ReglasContrasena.cs`). Single source of truth for string lengths, obligatory constraints, and data formats.
- **CapaUI**: WPF Presentation layer.
  - Core Validation (`ValidadorFormulario.cs`): Automatically derives UI `MaxLength` via `TopePreventivo(m)` when `MaxLength == 0`.
  - Custom Controls (`GhostTextBox.xaml(.cs)`): Domain auto-completion input control with DP `MaxLength`, horizontal scroll sync, foreign domain detection, and cancellation token cleanup.
  - Modals & Views (`ProveedorModal`, `FabricanteModal`, `CategoriaModal`, `PresentacionModal`, `EmpleadoModal`, `ProductoModal`, `UsuarioModal`, `LoginWindow`, `Forgot*.xaml`, `ConfiguracionEmpresa*`).
- **BimboProyecto.Tests**: Automated testing suite (.NET 8, xUnit, Npgsql). Contains schema drift verification against PostgreSQL `information_schema.columns`, offline pinned value tests, reflection audit, and boundary tests.
- **contexto/**: Obsidian Knowledge Vault adhering to strict YAML frontmatter, `[[wikilinks]]`, and `## Relaciones` standards.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---|---|---|---|
| 1 | `ReglasProducto` MaxLength alignment | Codigo (50), Nombre (200), Contenido (100) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 2 | `ReglasCategoria` MaxLength alignment | Nombre (100), Descripcion (200) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 3 | `ReglasPresentacion` MaxLength alignment | Nombre (100), Descripcion (500 - UI limit) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 4 | `ReglasFabricante` MaxLength alignment | Nombre (200), Descripcion (500 - UI limit) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 5 | `ReglasProveedor` MaxLength alignment | Nombre (200), Rtn (20), Telefono (20), Correo (100), Direccion (500 - UI limit) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 6 | `ReglasEmpleado` MaxLength alignment | Nombre (100), Apellido (100), Identidad (20, obligatorio), Telefono (20), Correo (100) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 7 | `ReglasUsuario` MaxLength alignment | Correo (50, `FormatoCampo.Correo`), Password (min 6, max 72) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 8 | `ReglasRol` MaxLength alignment | Nombre (50) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 9 | `ReglasContacto` MaxLength alignment | Nombre (100), Telefono (20), Correo (100) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 10 | `ReglasEmpresa` MaxLength alignment | Nombre (200), Rtn (20), Telefono (20), Correo (100), Direccion (500 - UI limit) in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 11 | Lexical documentation for `text` columns | Document PostgreSQL column name and UI limit marker (500 chars) for each `text` column in `ReglasEntidades.cs` | M1 | survey_miner_1 |
| 12 | `ValidadorFormulario.Segun()` preventive limit | Implement `TopePreventivo(m)` assigning `MaxLength` to `TextBox` and `PasswordBox` if `MaxLength == 0` | M2 | survey_explorer_2 |
| 13 | Manual XAML `MaxLength` removal | Remove hardcoded `MaxLength="100"` from `ProveedorModal`, `FabricanteModal`, `CategoriaModal`, `PresentacionModal`, `EmpleadoModal` XAML | M2 | survey_explorer_2 |
| 14 | Missing validator field registrations | Register `TxtIdentidad` in `EmpleadoModal`, `TxtContenido` in `ProductoModal`, `TxtEmail` in `UsuarioModal` | M2 | survey_explorer_2 |
| 15 | `GenerarEmail` local-part truncation | Truncate local part preserving `@empresa.com` so total length never exceeds 50 chars in `UsuarioModal.xaml.cs` | M2 | survey_explorer_2 |
| 16 | `GhostTextBox` MaxLength DependencyProperty | Register `MaxLengthProperty` and forward to `InnerBox.MaxLength` while keeping `GhostDisplay.MaxLength = 0` | M3 | survey_explorer_3 |
| 17 | `GhostTextBox` ScrollViewer synchronization | Subscribe `InnerBox` to `ScrollViewer.ScrollChangedEvent` and replicate `HorizontalOffset` to `GhostDisplay`, plus `Dispatcher.BeginInvoke` in `ShowGhostFor` | M3 | survey_explorer_3 |
| 18 | `GhostTextBox` foreign domain detection | Fix `GetRemainingSuffix` to return `string.Empty` if input contains `@` and no prefix overlap matches | M3 | survey_explorer_3 |
| 19 | `GhostTextBox` clamp & CTS dispose | Defensive clamp in `GetFullText()` and `.Dispose()` on debouncer CTS instances | M3 | survey_explorer_3 |
| 20 | `LoginWindow` & Recovery Panels limits | Set `TxtEmail.MaxLength = 50`, `TxtPassword.MaxLength = 72` in `LoginWindow`, `ForgotEmailPanel`, `ForgotNewPanel` | M3 | survey_explorer_3 |
| 21 | `ConfiguracionEmpresa` defensive limits | Add `ReglasFormato.NoExcedeLargo` checks in `ConfiguracionEmpresaViewModel.cs` and `MaxLength` in `ConfiguracionEmpresaModal.xaml` | M3 | survey_explorer_3 |
| 22 | Schema Drift Test (Test A) | Create `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` querying `information_schema.columns` via `Npgsql` (reads `BIMBO_POSTGRES_CONNECTION_STRING`) | M4 | survey_miner_1 |
| 23 | Pinned Values Unit Tests (Test B) | Create `[Theory]` tests for offline / CI verification in `ReglasEntidadesTests.cs` | M4 | survey_miner_1 |
| 24 | Reflection Audit Test (Test C) | Create test ensuring all `ReglaCampo` fields in `CapaDominio.Reglas` are in the audit map | M4 | survey_miner_1 |
| 25 | Boundary Tests Expansion | Add null, empty, trimmed, exact boundary, and overflow test cases in `ReglasFormatoTests.cs` | M4 | survey_miner_1 |
| 26 | Knowledge Vault ADR Addendums | Add addendums to `ADR-021` (3-tier validation) and `ADR-004` (GhostTextBox) | M5 | survey_explorer_3 |
| 27 | Knowledge Vault Pattern Updates | Update `Validacion de formularios.md` and `Anatomia compartida de los modales.md` | M5 | survey_explorer_3 |
| 28 | Technical Debt Updates | Update `P-042`, `P-045` and add new `ModalInput` debt note in `Deuda Técnica - Pendientes.md` | M5 | survey_explorer_3 |
| 29 | Session Note Creation | Create `contexto/70 - Bitácora de Cambios/2026-09/Sesión 2026-09-02 - Validación de longitud máxima en campos de texto.md` | M5 | survey_explorer_3 |
| 30 | Full Solution Build & Test Pass | Verify `dotnet build BimboProyecto.sln` has 0 errors and `dotnet test` achieves 100% pass | M6 | orchestrator |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|---|---|---|---|
| M1 | Domain Rules & Database Alignment (R1) | `CapaDominio/Reglas/ReglasEntidades.cs` | none | DONE |
| M2 | Automatic MaxLength Derivation & Modals Cleanup (R2) | `ValidadorFormulario.cs`, Modal XAMLs, Modal code-behinds, `GenerarEmail` | M1 | DONE |
| M3 | GhostTextBox & Login/View Limits (R3, R4) | `GhostTextBox.xaml(.cs)`, `LoginWindow.xaml.cs`, `Forgot*.xaml`, `ConfiguracionEmpresa*` | M1 | DONE |
| M4 | Automated Test Suite (Drift & Boundary) (R5) | `ReglasEntidadesTests.cs`, `ReglasFormatoTests.cs` in `BimboProyecto.Tests` | M1 | IN_PROGRESS |
| M5 | Knowledge Vault Documentation in `contexto/` (R6) | `ADR-021`, `ADR-004`, Pattern notes, Technical Debt, Session Note | M1, M2, M3, M4 | IN_PROGRESS |
| M6 | Final Verification & Dual Track Acceptance | Solution compilation, test suite run, adversarial validation, and integrity audit | M1, M2, M3, M4, M5 | PLANNED |

## Interface Contracts
### `CapaDominio.Reglas.ReglasEntidades` ↔ `CapaUI.Core.Validacion.ValidadorFormulario`
- `ReglaCampo.LargoMaximo` (int?) is read by `ValidadorFormulario.Segun()` to execute `TopePreventivo(m)`.
- If `tb.MaxLength == 0`, `tb.MaxLength` is set to `m`.
- If `pb.MaxLength == 0`, `pb.MaxLength` is set to `m`.

### `CapaDominio.Reglas.ReglasUsuario` ↔ `CapaUI.Formularios.InicioSesion.LoginWindow`
- `ReglasUsuario.Correo.LargoMaximo` (50) is assigned to `TxtEmail.MaxLength`.
- `ReglasUsuario.Password.LargoMaximo` (72) is assigned to `TxtPassword.MaxLength` and `TxtPasswordVisible.MaxLength`.

### `CapaDominio.Reglas.ReglasEmpresa` ↔ `CapaUI.Formularios.Principal.Pantallas.Configuracion`
- `ReglasEmpresa.*.LargoMaximo` is checked via `ReglasFormato.NoExcedeLargo` inside `ConfiguracionEmpresaViewModel.DatosValidos()`.

## Code Layout (Write Ownership)
- `worker_m1`: `CapaDominio/Reglas/ReglasEntidades.cs`
- `worker_m2`: `CapaUI/Core/Validacion/ValidadorFormulario.cs`, `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedorModal.xaml`, `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricanteModal.xaml`, `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriaModal.xaml`, `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionModal.xaml`, `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml(.cs)`, `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs`, `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs`
- `worker_m3`: `CapaUI/Core/Controls/GhostTextBox.xaml(.cs)`, `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs`, `CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml(.cs)`, `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml(.cs)`, `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaViewModel.cs`, `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml`
- `worker_m4`: `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`, `BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs`
- `worker_m5`: `contexto/45 - Decisiones/ADR-021 - Validacion en tres capas reglas de negocio en Dominio.md`, `contexto/45 - Decisiones/ADR-004 - GhostTextBox Autocompletado de Dominio en Login.md`, `contexto/20 - Patrones/Validacion de formularios.md`, `contexto/20 - Patrones/Anatomia compartida de los modales.md`, `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`, `contexto/70 - Bitácora de Cambios/2026-09/Sesión 2026-09-02 - Validación de longitud máxima en campos de texto.md`
