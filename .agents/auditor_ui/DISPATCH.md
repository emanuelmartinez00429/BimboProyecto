## 2026-09-02T18:10:27Z

<USER_REQUEST>
You are auditor_ui (Archetype: teamwork_preview_auditor).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\auditor_ui
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md
The project scope is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\PROJECT.md
Worker handoffs:
- `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m2\handoff.md`
- `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_m3\handoff.md`

TASK (Forensic Integrity Audit for Milestones 2 & 3):
1. Read ORIGINAL_REQUEST.md, PROJECT.md, and worker handoff reports.
2. Inspect all modified files across M2 and M3:
   - `CapaUI/Core/Validacion/ValidadorFormulario.cs`
   - `CapaUI/Core/Controls/GhostTextBox.xaml(.cs)`
   - `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs`
   - `CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml(.cs)`
   - `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml(.cs)`
   - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaViewModel.cs`
   - `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml`
   - Modal XAML and Code-Behind files for Proveedor, Fabricante, Categoria, Presentacion, Empleado, Producto, Usuario.
3. Perform forensic integrity checks:
   - Verify genuine implementation of `TopePreventivo`, `MaxLengthProperty`, scroll synchronization, foreign domain checks, memory cleanup (`.Dispose()`), and length validation.
   - Verify no dummy/facade implementations, no hardcoded cheating, no fake mocks.
   - Verify build and tests pass authentically.
4. Write your forensic audit report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\auditor_ui\handoff.md` with an explicit verdict: `CLEAN` or `INTEGRITY VIOLATION`.
5. Send a message to parent with your verdict and report path.
</USER_REQUEST>
