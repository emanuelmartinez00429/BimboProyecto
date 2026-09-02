## 2026-09-02T18:10:27Z

You are reviewer_ui (Archetype: teamwork_preview_reviewer).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\reviewer_ui
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
Worker handoffs:
- `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m2\handoff.md`
- `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m3\handoff.md`

TASK:
1. Read ORIGINAL_REQUEST.md, PROJECT.md, and both worker handoff reports.
2. Review Milestone 2 deliverables (R2):
   - `CapaUI/Core/Validacion/ValidadorFormulario.cs`: `TopePreventivo(m)` implementation and integration in `Segun(regla)` and `LargoMaximo()`.
   - Modals XAML: `ProveedorModal.xaml`, `FabricanteModal.xaml`, `CategoriaModal.xaml`, `PresentacionModal.xaml`, `EmpleadoModal.xaml` (confirm removal of hardcoded `MaxLength="100"`).
   - Modals Code-Behind: `EmpleadoModal.xaml.cs` (TxtIdentidad), `ProductoModal.xaml.cs` (TxtContenido), `UsuarioModal.xaml.cs` (TxtEmail) registered in `_validador`.
   - `UsuarioModal.xaml.cs`: `GenerarEmail` local-part truncation to 38 chars preserving `@empresa.com` (max 50 chars total).
3. Review Milestone 3 deliverables (R3, R4):
   - `CapaUI/Core/Controls/GhostTextBox.xaml(.cs)`: `MaxLengthProperty` DP, `ScrollViewer.ScrollChangedEvent` horizontal offset sync, `Dispatcher.BeginInvoke` in `ShowGhostFor`, `@` foreign domain check in `GetRemainingSuffix`, `GetFullText()` clamping, CTS cancellation token `.Dispose()`.
   - `LoginWindow.xaml.cs`: `TxtEmail.MaxLength = 50`, `TxtPassword.MaxLength = 72`.
   - `ForgotEmailPanel.xaml(.cs)`: `TxtEmail.MaxLength = 50`.
   - `ForgotNewPanel.xaml(.cs)`: `MaxLength = 72` on all password inputs.
   - `ConfiguracionEmpresaViewModel.cs`: `ReglasFormato.NoExcedeLargo` checks in `DatosValidos()`.
   - `ConfiguracionEmpresaModal.xaml`: `MaxLength` attributes on inputs.
4. Run `dotnet build BimboProyecto.sln` and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`.
5. Write your report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\reviewer_ui\handoff.md` with an explicit verdict: `APPROVE` or `REQUEST_CHANGES`.
6. Send a message to parent with your verdict and report path.
