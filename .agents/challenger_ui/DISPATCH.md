## 2026-09-02T18:10:27Z
TASK:
1. Read ORIGINAL_REQUEST.md, PROJECT.md, and worker handoffs.
2. Adversarially challenge the implementations of M2 and M3:
   - Challenge `GenerarEmail`: Test behavior when given very long employee names (>50 chars), single-word names, multiple last names, and verify the resulting email is <= 50 chars and ends with `@empresa.com`.
   - Challenge `GhostTextBox`: Test `GetRemainingSuffix` when typing foreign domain emails like `user@yahoo.com` (must return `string.Empty` and NOT append `@gmail.com`). Verify `GetFullText()` clamp and DP `MaxLength` propagation.
   - Challenge `ValidadorFormulario`: Verify `TopePreventivo` correctly assigns `MaxLength` to `TextBox` and `PasswordBox` when initially 0.
   - Challenge `ConfiguracionEmpresaViewModel`: Verify `DatosValidos()` rejects oversized fields using `ReglasFormato.NoExcedeLargo`.
3. Run `dotnet build BimboProyecto.sln` and `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`.
4. Write your challenge report to `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\challenger_ui\handoff.md` with an explicit verdict: `APPROVE` or `REQUEST_CHANGES`.
5. Send a message to parent with your verdict and report path.
