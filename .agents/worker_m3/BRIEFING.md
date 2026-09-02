# BRIEFING — 2026-09-02T18:10:00Z

## Mission
Complete Milestone 3 (R3 & R4): Comprehensive correction of GhostTextBox and MaxLength constraints in Login, Forgot Password, and ConfiguracionEmpresaModal.

## 🔒 My Identity
- Archetype: teamwork_preview_worker
- Roles: implementer, qa, specialist
- Working directory: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m3
- Original parent: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Milestone: Milestone 3 - R3 & R4: Corrección Integral de GhostTextBox y Topes en Login / Vistas Fuera del Validador

## 🔒 Key Constraints
- EXCLUSIVE WRITE OWNERSHIP:
  - `CapaUI/Core/Controls/GhostTextBox.xaml` and `GhostTextBox.xaml.cs`
  - `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs`
  - `CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml` and `ForgotEmailPanel.xaml.cs`
  - `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml` and `ForgotNewPanel.xaml.cs`
  - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaViewModel.cs`
  - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml`
  - `.agents/worker_m3/*`
- Do NOT modify files outside ownership.
- Integrity mandate: No dummy implementations, no hardcoding test results, real behavior.

## Current Parent
- Conversation ID: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Updated: 2026-09-02T18:10:00Z

## Task Summary
- **What to build**:
  1. GhostTextBox: MaxLength DP forwarding to InnerBox while keeping GhostDisplay unconstrained, ScrollViewer horizontal offset synchronization with Dispatcher.BeginInvoke, alien '@' email domain suppression in GetRemainingSuffix, GetFullText length clamping, CancellationTokenSource safe disposal.
  2. LoginWindow: MaxLength for TxtEmail (50), TxtPassword (72), TxtPasswordVisible (72) via ReglasUsuario.
  3. ForgotEmailPanel: MaxLength 50 for TxtEmail in XAML and code-behind.
  4. ForgotNewPanel: MaxLength 72 for TxtNew, TxtNewVisible, TxtConfirm, TxtConfirmVisible in XAML and code-behind.
  5. ConfiguracionEmpresaViewModel: Added ReglasFormato.NoExcedeLargo defensive checks in DatosValidos for NombreEmpresa (200), RtnEmpresa (20), DireccionEmpresa (500), TelefonoEmpresa (20), CorreoEmpresa (100), DominioCorreo (100).
  6. ConfiguracionEmpresaModal.xaml: MaxLength attributes on TextBoxes (Nombre: 200, RTN: 20, Dirección: 500, Teléfono: 20, Correo: 100, Dominio: 100, Color: 7).
  7. Verified: Clean compilation (0 errors) and all 118 unit tests passed.

## Key Decisions Made
- Implemented `CancelGhostDebounce()` method in `GhostTextBox.xaml.cs` to cleanly cancel and dispose `CancellationTokenSource` instances before allocating new ones.
- Added ScrollViewer horizontal offset handler and `Dispatcher.BeginInvoke(..., DispatcherPriority.Loaded)` sync to keep ghost text perfectly aligned during horizontal scrolling.
- Added `@` check in `GetRemainingSuffix` so foreign domain emails (e.g. `user@yahoo.com`) are not corrupted with `@gmail.com`.
- Added defensive clamping in `GetFullText()`.
- Added `MaxLength` configuration across Login, Forgot Password panels, and Company Settings.

## Artifact Index
- `.agents/worker_m3/DISPATCH.md` — Assignment instructions
- `.agents/worker_m3/progress.md` — Liveness and progress tracker
- `.agents/worker_m3/BRIEFING.md` — Situational awareness
- `.agents/worker_m3/handoff.md` — Final handoff report

## Change Tracker
- **Files modified**:
  - `CapaUI/Core/Controls/GhostTextBox.xaml.cs`: MaxLength DP, ScrollViewer sync, CTS disposal, foreign domain check, clamping.
  - `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs`: MaxLength for email and password fields.
  - `CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml` & `.cs`: MaxLength="50" on TxtEmail.
  - `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml` & `.cs`: MaxLength="72" on password boxes and textboxes.
  - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaViewModel.cs`: Defensive `ReglasFormato.NoExcedeLargo` checks in `DatosValidos`.
  - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml`: MaxLength on all TextBoxes.
- **Build status**: Pass (0 errors).
- **Pending issues**: None.

## Quality Status
- **Build/test result**: Pass (118 tests passed, 0 failed).
- **Lint status**: Clean.
- **Tests added/modified**: All existing tests pass.

## Loaded Skills
- None.
