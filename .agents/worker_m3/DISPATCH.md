## 2026-09-02T18:06:28Z
You are worker_m3 (Archetype: teamwork_preview_worker).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m3
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
The survey report is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3\survey_report.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

EXCLUSIVE WRITE OWNERSHIP:
You own:
- `CapaUI/Core/Controls/GhostTextBox.xaml` and `GhostTextBox.xaml.cs`
- `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs`
- `CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml` and `ForgotEmailPanel.xaml.cs`
- `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml` and `ForgotNewPanel.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaViewModel.cs`
- `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml`
Do NOT modify files outside your ownership.

TASK (Milestone 3 - R3 & R4: Corrección Integral de GhostTextBox y Topes en Login / Vistas Fuera del Validador):
1. In `GhostTextBox.xaml.cs`:
   - Register `MaxLengthProperty` DependencyProperty on `GhostTextBox`, forwarding changes to `InnerBox.MaxLength`, while keeping `GhostDisplay.MaxLength = 0`.
   - Add handler for `ScrollViewer.ScrollChangedEvent` on `InnerBox` replicating `HorizontalOffset` to `GhostDisplay`. In `ShowGhostFor`, ensure synchronization via `Dispatcher.BeginInvoke(..., DispatcherPriority.Loaded)`.
   - In `GetRemainingSuffix(string input)`: If `input.Contains('@')` and no prefix overlap matches `GhostSuffix`, return `string.Empty` so foreign email domains (e.g. `test@yahoo.com`) are not appended with `@gmail.com`.
   - In `GetFullText()`: Clamp result to `MaxLength` if `MaxLength > 0` and string length exceeds `MaxLength`.
   - In debouncers: Cancel and `.Dispose()` existing `_ghostDebounce` `CancellationTokenSource` instances before allocating new ones.
2. In `LoginWindow.xaml.cs`:
   - Set `TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;`
   - Set `TxtPassword.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;`
   - Set `TxtPasswordVisible.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;`
3. In `ForgotEmailPanel.xaml(.cs)`: Set `TxtEmail.MaxLength = 50`.
4. In `ForgotNewPanel.xaml(.cs)`: Set `MaxLength="72"` for `TxtNew`, `TxtNewVisible`, `TxtConfirm`, `TxtConfirmVisible`.
5. In `ConfiguracionEmpresaViewModel.cs`:
   - In `DatosValidos()`: Add defensive `ReglasFormato.NoExcedeLargo` checks for `NombreEmpresa` (200), `RtnEmpresa` (20), `DireccionEmpresa` (500), `TelefonoEmpresa` (20), `CorreoEmpresa` (100), and `DominioCorreo` (100).
6. In `ConfiguracionEmpresaModal.xaml`:
   - Add `MaxLength` attributes to the corresponding `TextBox` controls.
7. Verify build and tests:
   Run `dotnet build BimboProyecto.sln` (0 errors) and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` (all passing).
8. Write your handoff report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m3\handoff.md` and message the parent orchestrator when complete.
