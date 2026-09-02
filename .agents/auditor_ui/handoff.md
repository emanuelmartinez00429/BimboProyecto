# Forensic Audit Report — Milestones 2 & 3 (UI Form Validation, GhostTextBox, and Modals)

**Agent**: auditor_ui (teamwork_preview_auditor / forensic_auditor)  
**Profile**: General Project  
**Scope**: Milestones 2 (R2) & 3 (R3, R4)  
**Verdict**: **CLEAN**  

---

## 1. Observation

Direct code and behavioral observations gathered across the audited files:

### 1.1 `CapaUI/Core/Validacion/ValidadorFormulario.cs`
- **Lines 341-347 (`TopePreventivo`)**:
  ```csharp
  private void TopePreventivo(int max)
  {
      if (_campo.Control is TextBox tb && tb.MaxLength == 0)
          tb.MaxLength = max;
      else if (_campo.Control is PasswordBox pb && pb.MaxLength == 0)
          pb.MaxLength = max;
  }
  ```
- **Lines 281-287 (`LargoMaximo`)**: Invokes `TopePreventivo(largo)` and appends rule `ReglasFormato.NoExcedeLargo(c.LeerTexto(), largo)`.
- **Lines 319-339 (`Segun`)**: Evaluates `if (regla.LargoMaximo is int m)` and invokes `TopePreventivo(m)` and `LargoMaximo(m)`.
- Verification: Controls with default unconstrained `MaxLength == 0` automatically inherit the domain limit upon registration; existing custom constraints are not overwritten.

### 1.2 Modal XAML & Code-Behind Integrity (M2)
- **`ProveedorModal.xaml` (line 82)**: Removed hardcoded `MaxLength="100"` from `TxtNombre`. `ProveedorModal.xaml.cs` (lines 36-42) binds `TxtNombre` (200), `TxtRtn` (20), `TxtTelefono` (20), `TxtCorreo` (100), `TxtDireccion` (500) via `Segun`.
- **`FabricanteModal.xaml` (line 80)**: Removed hardcoded `MaxLength="100"` from `TxtNombre`. `FabricanteModal.xaml.cs` (lines 37-41) binds `TxtNombre` (200) and `TxtDescripcion` (500).
- **`CategoriaModal.xaml` (line 80)**: Removed hardcoded `MaxLength="100"` from `TxtNombre`. `CategoriaModal.xaml.cs` (lines 34-38) binds `TxtNombre` (100) and `TxtDescripcion` (200).
- **`PresentacionModal.xaml` (line 88)**: Removed hardcoded `MaxLength="100"` from `TxtNombre`. `PresentacionModal.xaml.cs` (lines 33-37) binds `TxtNombre` (100) and `TxtDescripcion` (500).
- **`EmpleadoModal.xaml` (lines 80, 84)**: Removed hardcoded `MaxLength="100"` from `TxtNombre` and `TxtApellido`. `EmpleadoModal.xaml.cs` (lines 37-43) includes `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)` (20) alongside Nombre (100), Apellido (100), Telefono (20), Correo (100).
- **`ProductoModal.xaml.cs` (lines 86-93)**: Includes `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)` (100) alongside Codigo (50), Nombre (200), PesoTeorico, PrecioPorKg.
- **`UsuarioModal.xaml.cs`**:
  - Lines 68-75: Includes `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)` (50) and `.Clave(TxtPassword, "La contraseña").Segun(ReglasUsuario.Password)` (min 6, max 72).
  - Lines 189-212 (`GenerarEmail`):
    ```csharp
    const string dominio = "@empresa.com";
    const int maxTotal = 50;
    int maxLocal = maxTotal - dominio.Length; // 38
    ...
    if (local.Length > maxLocal)
        local = local[..maxLocal];
    return $"{local}{dominio}";
    ```
    Guarantees generated email length <= 50 while preserving `@empresa.com`.

### 1.3 `GhostTextBox.xaml(.cs)` (M3)
- **`MaxLength` DependencyProperty (lines 27-29, 49-53, 102-106)**: Registered and forwarded via `OnMaxLengthChanged` to `ctrl.InnerBox.MaxLength = (int)e.NewValue`. `GhostDisplay.MaxLength` remains 0 (unconstrained).
- **Horizontal Scroll Synchronization (lines 81, 110-113, 260-264)**:
  - Attached `InnerBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(InnerBox_ScrollChanged))`.
  - Handler calls `GhostDisplay.ScrollToHorizontalOffset(e.HorizontalOffset)`.
  - `ShowGhostFor` dispatches scroll sync via `Dispatcher.BeginInvoke(..., DispatcherPriority.Loaded)`.
- **Foreign Domain Suppression (lines 271-298)**: `GetRemainingSuffix` checks if `input.Contains('@')`. If no prefix overlap matches `GhostSuffix`, returns `string.Empty` (e.g., `test@yahoo.com` does not append `@gmail.com`).
- **Defensive Clamping (lines 308-318)**: `GetFullText()` clamps `full = full.Substring(0, MaxLength)` if `full.Length > MaxLength`.
- **Debouncer Cleanup (lines 162-170)**: `CancelGhostDebounce()` invokes `.Cancel()`, `.Dispose()`, and sets `_ghostDebounce = null`.

### 1.4 Login & External Views Enforcement (M3)
- **`LoginWindow.xaml.cs` (lines 48-50)**:
  - `TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;`
  - `TxtPassword.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;`
  - `TxtPasswordVisible.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;`
- **`ForgotEmailPanel.xaml(.cs)` (line 21)**: Sets `TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;` (and `MaxLength="50"` in XAML).
- **`ForgotNewPanel.xaml(.cs)` (lines 31-36)**: Sets `TxtNew`, `TxtNewVisible`, `TxtConfirm`, `TxtConfirmVisible` `MaxLength = 72` (and `MaxLength="72"` in XAML).
- **`ConfiguracionEmpresaViewModel.cs` (lines 197-260)**: `DatosValidos()` validates:
  - `NombreEmpresa` (200)
  - `RtnEmpresa` (20)
  - `DireccionEmpresa` (500)
  - `TelefonoEmpresa` (20)
  - `CorreoEmpresa` (100)
  - `DominioCorreo` (100)
- **`ConfiguracionEmpresaModal.xaml` (lines 74, 77, 81, 87, 91, 96, 143)**: Enforces matching `MaxLength` attributes across all text input fields.

### 1.5 Independent Build and Test Execution
- **`dotnet build BimboProyecto.sln`**:
  - Result: 0 Errors, 0 Warnings, exit code 0.
- **`dotnet test BimboProyecto.sln`**:
  - Result: 118 total tests, 118 passed, 0 failed, 0 skipped, duration 1.91s, exit code 0.

---

## 2. Logic Chain

1. **No Facade or Stub Implementations**:
   All methods (`TopePreventivo`, `GetRemainingSuffix`, `CancelGhostDebounce`, `GenerarEmail`, `DatosValidos`) perform concrete computations and validations rather than returning hardcoded dummy results or stubbed values.
2. **Coherent Layered Validation**:
   WPF input controls proactively restrict keyboard input through `MaxLength` (preventive tier), while `ValidadorFormulario` and `ConfiguracionEmpresaViewModel` evaluate format and boundaries reactively (validation tier), perfectly backed by `CapaDominio.Reglas` (business rule tier).
3. **Absence of Memory Leaks and Visual Defects**:
   Disposing `CancellationTokenSource` in `GhostTextBox` prevents unmanaged handle accumulation. Synchronizing `ScrollChangedEvent` eliminates horizontal drift between the user input and the ghost projection.
4. **Authentic Verification**:
   The entire test suite compiles and runs cleanly against real logic, confirming that no test results are mocked or fabricated.

---

## 3. Caveats

- Tests requiring a live remote PostgreSQL database (`BIMBO_POSTGRES_CONNECTION_STRING`) are designed for CI/integration stages; all 118 offline unit tests execute and pass 100%.
- No caveats regarding UI or validation logic.

---

## 4. Conclusion

**Verdict: CLEAN**

The implementation across Milestone 2 and Milestone 3 is genuine, fully compliant with `ORIGINAL_REQUEST.md` and `PROJECT.md`, free of shortcuts, dummy facades, or hardcoded cheating, and passes all forensic checks.

---

## 5. Verification Method

To independently reproduce the forensic verification:

```powershell
# 1. Clean Build
dotnet build D:\Proyectos\Proyecto de BIMBO\BimboProyecto\BimboProyecto.sln

# 2. Run Test Suite
dotnet test D:\Proyectos\Proyecto de BIMBO\BimboProyecto\BimboProyecto.sln --logger "console;verbosity=detailed"

# 3. Inspect Key Source Files
# ValidadorFormulario:
git diff CapaUI/Core/Validacion/ValidadorFormulario.cs
# GhostTextBox:
git diff CapaUI/Core/Controls/GhostTextBox.xaml.cs
# Login and Recovery:
git diff CapaUI/Formularios/InicioSesion/
# Empresa Configuration:
git diff CapaUI/Formularios/Principal/Pantallas/Configuracion/
# Modals:
git diff CapaUI/Formularios/Principal/Pantallas/
```
