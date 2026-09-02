# BRIEFING — 2026-09-02T17:53:00Z

## Mission
Investigate GhostTextBox, Login/Views, Tests, and Knowledge Vault for MaxLength & validation alignment project.

## 🔒 My Identity
- Archetype: teamwork_preview_explorer
- Roles: explorer, investigator, synthesizer
- Working directory: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3
- Original parent: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Milestone: survey_and_investigation

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Produce structured survey report in survey_report.md
- Verify all findings directly against codebase and files

## Current Parent
- Conversation ID: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Updated: not yet

## Investigation State
- **Explored paths**:
  - `CapaUI/Core/Controls/GhostTextBox.xaml` & `GhostTextBox.xaml.cs`
  - `CapaUI/Formularios/InicioSesion/LoginWindow.xaml` & `LoginWindow.xaml.cs`
  - `CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml(.cs)`, `ForgotNewPanel.xaml(.cs)`, `ForgotCodePanel.xaml(.cs)`
  - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaViewModel.cs` & `ConfiguracionEmpresaModal.xaml(.cs)`
  - `CapaDominio/Reglas/ReglasEntidades.cs` & `ReglasFormato.cs`
  - `CapaUI/Core/Validacion/ValidadorFormulario.cs`
  - `contexto/` vault notes (`ADR-004`, `ADR-021`, `Validacion de formularios.md`, `Anatomia compartida de los modales.md`, `Deuda Técnica - Pendientes.md`, `contexto/70 - Bitácora de Cambios/`)
  - `BimboProyecto.Tests/` (`BimboProyecto.Tests.csproj`, `Dominio/ReglasFormatoTests.cs`, `Auth/LoginQATests.cs`, `Rbac/ContratoRbacTests.cs`)
- **Key findings**:
  - GhostTextBox: missing `MaxLength` DP; scroll offset desync between `InnerBox` and `GhostDisplay`; `GetRemainingSuffix` concats full `@gmail.com` on foreign domain; un-disposed CTS debouncer; unclamped `GetFullText()`.
  - Login & Recovery: missing email max 50 and password max 72 across `LoginWindow`, `ForgotEmailPanel`, and `ForgotNewPanel`.
  - ConfiguracionEmpresa: missing `ReglasFormato.NoExcedeLargo` defensive validation in `DatosValidos()` and missing `MaxLength` in `ConfiguracionEmpresaModal.xaml`.
  - Vault: ADR addendums (`ADR-004`, `ADR-021`), pattern notes updates, debt updates (`P-042`, `P-045`), and session note creation.
  - Tests: `BimboProyecto.Tests.csproj` has Npgsql 8.0.3 and xUnit; existing 118 tests pass; ready for `ReglasEntidadesTests.cs` (Tests A, B, C) and `ReglasFormatoTests.cs` boundary tests.
- **Unexplored areas**: None. Complete investigation finished.

## Key Decisions Made
- Structured findings into `survey_report.md` with complete code snippets, exact paths, line numbers, analysis, and implementation roadmap.

## Artifact Index
- `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3\survey_report.md` — Comprehensive survey report
- `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3\handoff.md` — 5-component handoff report
