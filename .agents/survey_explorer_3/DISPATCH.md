## 2026-09-02T17:50:22Z

You are survey_explorer_3 (Archetype: teamwork_preview_explorer).
Your working directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3
The workspace directory is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
The authoritative request is: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md

TASK:
1. Read ORIGINAL_REQUEST.md completely.
2. Investigate GhostTextBox, Login/Views, Tests, and Knowledge Vault:
   - Examine `GhostTextBox` implementation (`CapaUI/Controles/GhostTextBox.xaml(.cs)` or similar): DependencyProperty `MaxLength`, `InnerBox` vs `GhostDisplay`, ScrollViewer synchronization with `ScrollViewer.ScrollChangedEvent` and `HorizontalOffset`, `ShowGhostFor` with `Dispatcher.BeginInvoke`, `GetRemainingSuffix` with `@` handling, clamp in `GetFullText()`, and CTS debouncer `.Dispose()`.
   - Examine `LoginWindow.xaml(.cs)` and recovery panels (`ForgotEmailPanel`, `ForgotNewPanel`) for email max 50 and password max 72.
   - Examine `ConfiguracionEmpresaViewModel.cs` and its corresponding XAML for defensive length validation (`ReglasFormato.NoExcedeLargo`).
   - Examine `contexto/` vault notes: `contexto/45 - Decisiones/ADR-021...`, `ADR-004...`, `contexto/20 - Patrones/...`, `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`, `contexto/70 - Bitácora de Cambios/...`. Check vault conventions (YAML frontmatter, `[[wikilinks]]`, `## Relaciones`).
   - Check test project setup in `BimboProyecto.Tests/`.
3. Write your comprehensive findings to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_explorer_3\survey_report.md`.
4. Send a completion message to the parent orchestrator with the path to your report.
