# Handoff Report: survey_explorer_3

**Agent:** survey_explorer_3 (teamwork_preview_explorer)  
**Parent Agent:** 9e4750ff-0c86-44d7-aa45-ab43466b3848 (parent)  
**Date:** 2026-09-02  
**Handoff Type:** Hard (Task complete)  
**Artifact:** `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3\survey_report.md`

---

## 1. Observation

1. **`GhostTextBox` (`CapaUI/Core/Controls/GhostTextBox.xaml` & `GhostTextBox.xaml.cs`):**
   - Lines 13-44: Only `TextProperty`, `GhostSuffixProperty`, and `PlaceholderProperty` are registered as `DependencyProperty`. No `MaxLengthProperty` exists.
   - Lines 234-254: `GetRemainingSuffix(string input)` matches the tail of `input` with the head of `GhostSuffix`. When no overlap is found (e.g. typing `test@yahoo.com`), it returns the full `suffix` (`@gmail.com`), causing `GetFullText()` to produce `test@yahoo.com@gmail.com`.
   - Lines 142, 164, 190: `_ghostDebounce?.Cancel()` is called without `_ghostDebounce?.Dispose()`, leaking `CancellationTokenSource` instances.
   - `InnerBox` and `GhostDisplay` are separate `TextBox` controls inside a `Grid`. No `ScrollViewer.ScrollChangedEvent` listener exists to replicate `HorizontalOffset` from `InnerBox` to `GhostDisplay`.
   - `GetFullText()` at lines 264-272 does not clamp the output string if its length exceeds `MaxLength`.

2. **Login & Recovery Panels (`LoginWindow`, `ForgotEmailPanel`, `ForgotNewPanel`):**
   - `LoginWindow.xaml:136`: `TxtEmail` (`GhostTextBox`) has no `MaxLength`.
   - `LoginWindow.xaml:159, 164`: `TxtPassword` (`PasswordBox`) and `TxtPasswordVisible` (`TextBox`) have no `MaxLength`.
   - `ForgotEmailPanel.xaml:60`: `TxtEmail` (`TextBox`) has no `MaxLength`.
   - `ForgotNewPanel.xaml:46, 50, 96, 100`: `TxtNew`, `TxtNewVisible`, `TxtConfirm`, `TxtConfirmVisible` have no `MaxLength`.

3. **`ConfiguracionEmpresaViewModel.cs` & `ConfiguracionEmpresaModal.xaml`:**
   - `ConfiguracionEmpresaViewModel.cs:197-224`: `DatosValidos()` only validates content presence, RTN format, telephone format, and email format. It does not invoke `ReglasFormato.NoExcedeLargo` on any property (`NombreEmpresa`, `RtnEmpresa`, `DireccionEmpresa`, `TelefonoEmpresa`, `CorreoEmpresa`, `DominioCorreo`).
   - `ConfiguracionEmpresaModal.xaml:74, 77, 80, 87, 91, 96`: None of the input `TextBox` controls have `MaxLength` attributes.

4. **Knowledge Vault (`contexto/`):**
   - `ADR-021` (`contexto/45 - Decisiones/ADR-021 - Validacion en tres capas reglas de negocio en Dominio.md`): Documents 3-tier validation (Domain rules, Parsing, UI ValidadorFormulario). Needs addendum for `TopePreventivo(m)` and database column alignment.
   - `ADR-004` (`contexto/45 - Decisiones/ADR-004 - GhostTextBox Autocompletado de Dominio en Login.md`): Documents `GhostTextBox` design and earlier fixes. Needs addendum for `MaxLength` DP, scroll offset synchronization, alien `@` domain detection, CTS disposal, and clamp.
   - `Deuda Técnica - Pendientes.md` (`contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`): Contains `P-042` and `P-045` requiring status/plan updates, plus registering new debt for `ModalInput` / `InputBox`.
   - Session notes format confirmed in `contexto/70 - Bitácora de Cambios/2026-08/` and `2026-09/` with strict YAML frontmatter, `[[wikilinks]]`, and `## Relaciones`.

5. **Test Project (`BimboProyecto.Tests/`):**
   - `BimboProyecto.Tests.csproj` references .NET 8, `xunit` (2.9.3), `Npgsql` (8.0.3), `Microsoft.NET.Test.Sdk` (17.14.1), and projects `CapaDominio`, `CapaDatos`, `CapaAplicacion`.
   - Running `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` executed 118 tests with 100% pass rate in 1s.
   - `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` does not exist yet and is ready to be implemented with Test A (Npgsql drift against Postgres), Test B (pinned values), and Test C (reflection audit).
   - `Dominio/ReglasFormatoTests.cs` is ready for expanded boundary tests (`null`, empty, whitespace, exact length, length + 1, padded whitespace).

---

## 2. Logic Chain

1. **GhostTextBox Scroll & Suffix Logic:**
   - *Observation 1* shows that `GhostDisplay` is a detached `TextBox` that does not track `InnerBox`'s horizontal scroll offset, and `GetRemainingSuffix` returns `@gmail.com` when typing a different domain containing `@`.
   - *Inference:* Implementing `MaxLengthProperty` forwarding to `InnerBox`, adding `ScrollViewer.ScrollChangedEvent` handler to set `GhostDisplay.ScrollToHorizontalOffset(e.HorizontalOffset)`, scheduling post-render sync via `Dispatcher.BeginInvoke`, returning `string.Empty` in `GetRemainingSuffix` when `input.Contains('@')`, clamping in `GetFullText()`, and calling `.Dispose()` on `_ghostDebounce` will resolve all known UI and authentication issues.

2. **Login & Configuration Forms:**
   - *Observations 2 and 3* show that neither `LoginWindow` / recovery panels nor `ConfiguracionEmpresa` enforce length limits in their views and viewmodels.
   - *Inference:* Setting `TxtEmail.MaxLength = 50` and password `MaxLength = 72` in `LoginWindow`, `ForgotEmailPanel`, and `ForgotNewPanel`, combined with defensive `ReglasFormato.NoExcedeLargo` in `ConfiguracionEmpresaViewModel.DatosValidos()` and `MaxLength` attributes in `ConfiguracionEmpresaModal.xaml`, will guarantee that input never exceeds Postgres database column boundaries across non-modal and MVVM views.

3. **Domain, Validation & Test Architecture:**
   - *Observations 4 and 5* show that `CapaDominio` rules can be updated to exact database sizes, `ValidadorFormulario.Segun()` can set `TopePreventivo(m)` when `MaxLength == 0`, and `BimboProyecto.Tests` with `Npgsql` can verify schema drift against live Supabase / Postgres and offline unit test cases.

---

## 3. Caveats

- **Live Database Connection:** Running Test A in `ReglasEntidadesTests.cs` against a live Supabase instance requires the environment variable `BIMBO_POSTGRES_CONNECTION_STRING` to be populated; when not set, Test A skips gracefully, while Test B (offline unit tests) and Test C (reflection audit) always execute.
- **Pesaje Module:** As documented in `P-045`, the `Pesaje` module has its own validation flow and is tracked as technical debt for future consolidation; changes in this scope focus on CRUD modales, Login, Configuration, Domain, and Tests as specified in `ORIGINAL_REQUEST.md`.

---

## 4. Conclusion

All components requested for investigation (`GhostTextBox`, `LoginWindow`, `ForgotEmailPanel`, `ForgotNewPanel`, `ConfiguracionEmpresaViewModel`, `ConfiguracionEmpresaModal`, `contexto/` vault, and `BimboProyecto.Tests/`) have been thoroughly inspected, traced to line-level code, and synthesized into the comprehensive report `survey_report.md`. The implementation plan is concrete, actionable, and ready for execution.

---

## 5. Verification Method

1. **Inspect Report:** Read `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3\survey_report.md`.
2. **Build and Test Verification:**
   ```powershell
   dotnet build BimboProyecto.sln
   dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
   ```
3. **Invalidation Conditions:** Any discrepancy between the line numbers/code snippets identified and the repository files invalidates the specific section, which can be verified by running `view_file` on the target paths.
