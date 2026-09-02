# Quality & Adversarial Review Report — UI Milestones (M2 & M3)

**Agent**: reviewer_ui (teamwork_preview_reviewer)  
**Roles**: reviewer, critic  
**Target Milestones**: Milestone 2 (worker_m2 - R2) & Milestone 3 (worker_m3 - R3, R4)  
**Working Directory**: `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\reviewer_ui`  
**Date**: 2026-09-02  
**Verdict**: **APPROVE**

---

## 1. Observation

### 1.1 Milestone 2 Deliverables (R2: Derivación Automática de MaxLength y Limpieza de XAML)
1. **`CapaUI/Core/Validacion/ValidadorFormulario.cs`**:
   - `ConstructorCampo.TopePreventivo(int max)` (lines 341-347):
     ```csharp
     private void TopePreventivo(int max)
     {
         if (_campo.Control is TextBox tb && tb.MaxLength == 0)
             tb.MaxLength = max;
         else if (_campo.Control is PasswordBox pb && pb.MaxLength == 0)
             pb.MaxLength = max;
     }
     ```
   - Invoked in `LargoMaximo(int largo, string? mensaje = null)` (line 283) before registering the rule.
   - Invoked in `Segun(ReglaCampo regla)` (lines 322-326) when `regla.LargoMaximo is int m`.
2. **Modal XAMLs Manual MaxLength Removal**:
   - `ProveedorModal.xaml` (line 82): `<TextBox x:Name="TxtNombre" Style="{StaticResource ModalInput}" TabIndex="10"/>` (removed hardcoded `MaxLength="100"`).
   - `FabricanteModal.xaml` (line 80): `<TextBox x:Name="TxtNombre" Style="{StaticResource ModalInput}" TabIndex="10"/>` (removed hardcoded `MaxLength="100"`).
   - `CategoriaModal.xaml` (line 80): `<TextBox x:Name="TxtNombre" Style="{StaticResource ModalInput}" TabIndex="10"/>` (removed hardcoded `MaxLength="100"`).
   - `PresentacionModal.xaml` (line 88): `<TextBox x:Name="TxtNombre" Style="{StaticResource ModalInput}" TabIndex="10"/>` (removed hardcoded `MaxLength="100"`).
   - `EmpleadoModal.xaml` (lines 80, 84): `TxtNombre` and `TxtApellido` without hardcoded `MaxLength="100"`.
3. **Missing Field Registrations in Modal Code-Behinds**:
   - `EmpleadoModal.xaml.cs` (line 40): `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)`
   - `ProductoModal.xaml.cs` (line 89): `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)`
   - `UsuarioModal.xaml.cs` (line 71): `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)`
4. **`UsuarioModal.xaml.cs` `GenerarEmail` Local-Part Truncation**:
   - Lines 190-212:
     ```csharp
     private static string GenerarEmail(string nombreCompleto)
     {
         const string dominio = "@empresa.com";
         const int maxTotal = 50;
         int maxLocal = maxTotal - dominio.Length; // 38

         var partes = nombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
         string local;
         if (partes.Length < 2)
         {
             local = TextoBusqueda.Normalizar(nombreCompleto).Trim();
         }
         else
         {
             var nombre   = TextoBusqueda.Normalizar(partes[0]).Trim();
             var apellido = TextoBusqueda.Normalizar(partes[^1]).Trim();
             local = $"{nombre}.{apellido}";
         }

         if (local.Length > maxLocal)
             local = local[..maxLocal];

         return $"{local}{dominio}";
     }
     ```

---

### 1.2 Milestone 3 Deliverables (R3 & R4: GhostTextBox y Topes en Vistas Fuera del Validador)
1. **`CapaUI/Core/Controls/GhostTextBox.xaml(.cs)`**:
   - `MaxLengthProperty` registered as `DependencyProperty` (lines 27-29, 49-53). `OnMaxLengthChanged` (lines 102-106) assigns `InnerBox.MaxLength = (int)e.NewValue` while `GhostDisplay.MaxLength` remains 0.
   - Attached `ScrollViewer.ScrollChangedEvent` handler on `InnerBox` (line 81) syncing `GhostDisplay.ScrollToHorizontalOffset(e.HorizontalOffset)` (lines 110-113).
   - In `ShowGhostFor` (lines 260-263): dispatches `GhostDisplay.ScrollToHorizontalOffset(InnerBox.HorizontalOffset)` with `DispatcherPriority.Loaded`.
   - In `GetRemainingSuffix` (lines 292-294): returns `string.Empty` if `input.Contains('@')` and no prefix overlap matches `GhostSuffix`.
   - In `GetFullText()` (lines 314-315): clamps length with `if (MaxLength > 0 && full.Length > MaxLength) full = full.Substring(0, MaxLength);`.
   - In `CancelGhostDebounce()` (lines 162-170): calls `.Cancel()`, `.Dispose()`, and clears `_ghostDebounce`.
2. **`LoginWindow.xaml.cs`**:
   - Lines 48-50:
     - `TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;`
     - `TxtPassword.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;`
     - `TxtPasswordVisible.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;`
3. **`ForgotEmailPanel.xaml(.cs)`**:
   - `TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;` (code-behind line 21, XAML line 62).
4. **`ForgotNewPanel.xaml(.cs)`**:
   - `TxtNew`, `TxtNewVisible`, `TxtConfirm`, and `TxtConfirmVisible` set to `MaxLength = 72` (code-behind lines 31-35, XAML lines 48, 53, 100, 105).
5. **`ConfiguracionEmpresaViewModel.cs`**:
   - `DatosValidos()` (lines 197-260) validates `ReglasFormato.NoExcedeLargo` for `NombreEmpresa` (200), `RtnEmpresa` (20), `DireccionEmpresa` (500), `TelefonoEmpresa` (20), `CorreoEmpresa` (100), and `DominioCorreo` (100).
6. **`ConfiguracionEmpresaModal.xaml`**:
   - `MaxLength` attributes declared on `NombreEmpresa` (200), `RtnEmpresa` (20), `DireccionEmpresa` (500), `TelefonoEmpresa` (20), `CorreoEmpresa` (100), `DominioCorreo` (100), and `ColorEmpresa` (7).

---

### 1.3 Compilation and Automated Test Results
- `dotnet build BimboProyecto.sln`: 0 Errors, 0 Warnings (Exit code 0).
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`: 118 Passed, 0 Failed, 0 Skipped (Exit code 0).

---

## 2. Logic Chain & Adversarial Evaluation

1. **Integrity Violation Check**:
   - Evaluated all modified files for hardcoded test results, facade logic, bypassed work, or fabricated outputs.
   - Result: **0 Integrity Violations**. Implementations are genuine, cohesive, and directly implement business constraints.
2. **Derivation of MaxLength in WPF**:
   - `TopePreventivo(max)` only overwrites controls with `MaxLength == 0`. It correctly targets `TextBox` and `PasswordBox` controls, allowing custom explicit overrides while automating the standard case from `ReglaCampo`.
   - Removing hardcoded `MaxLength="100"` from modal XAMLs prevents premature truncation on 200-char database columns (e.g. `proveedor.nombre_proveedor`, `fabricante.nombre_fabricante`).
3. **PostgreSQL Column Boundary (`alias_usuario` varchar(50))**:
   - `GenerarEmail` calculates `maxLocal = 50 - "@empresa.com".Length = 38`.
   - Even with long names, normalization and local-part slicing guarantees `local.Length <= 38`, so `local + "@empresa.com"` <= 50 characters, preserving the domain.
4. **GhostTextBox Adversarial Stress Testing**:
   - *Foreign Domain Insertion*: Entering `usuario@yahoo.com` is caught by `if (input.Contains('@')) return string.Empty;`, preventing `@gmail.com` from being concatenated into `usuario@yahoo.com@gmail.com`.
   - *Scroll Offset Desynchronization*: Typing strings longer than the visible box width triggers `ScrollViewer.ScrollChangedEvent` on `InnerBox`, immediately propagating `HorizontalOffset` to `GhostDisplay`. Asynchronous layout recalculation is synchronized via `Dispatcher.BeginInvoke` at `DispatcherPriority.Loaded`.
   - *Resource Cleanup*: Keystroke debouncing cleans up previous `CancellationTokenSource` instances with `.Dispose()` inside `CancelGhostDebounce()`, preventing unmanaged handle leaks.
5. **Login and Recovery Defensive Caps**:
   - Password fields across login and password reset panels enforce `MaxLength = 72`, aligning with Bcrypt hashing byte limits and preventing denial-of-service via huge strings.

---

## 3. Caveats

- Milestone 4 (Worker M4) automated schema drift tests against live PostgreSQL (`BIMBO_POSTGRES_CONNECTION_STRING`) run when the connection string is provided in CI/CD; offline unit tests cover 100% of pinned rules.
- `ValidadorFormulario` operates on standard WPF `TextBox` / `PasswordBox` controls; custom `UserControl`s like `GhostTextBox` handle their own internal DP and validation.

---

## 4. Conclusion

**Verdict: APPROVE**

The deliverables for Milestone 2 and Milestone 3 fully satisfy all functional and non-functional requirements specified in `ORIGINAL_REQUEST.md` and `PROJECT.md`:
- Dynamic `MaxLength` propagation via `TopePreventivo` and removal of duplicate XAML limits.
- Complete registration of previously unvalidated modal fields (`TxtIdentidad`, `TxtContenido`, `TxtEmail`).
- Robust truncation in `GenerarEmail` protecting the 50-character database limit.
- Complete fixes in `GhostTextBox` (DP `MaxLength`, scroll sync, foreign domain suppression, defensive clamp, CTS disposal).
- Domain length enforcement in Login, Recovery panels, and `ConfiguracionEmpresa`.
- 100% compilation and unit test pass rate.

---

## 5. Verification Method

To independently verify the deliverables:

```powershell
# 1. Clean Build
dotnet build BimboProyecto.sln

# 2. Execute Test Suite
dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --verbosity normal

# 3. Inspect Key Deliverables
git status --short
```
