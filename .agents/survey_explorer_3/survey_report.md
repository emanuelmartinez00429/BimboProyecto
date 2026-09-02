# Survey Report: GhostTextBox, Login/Views, Tests, and Knowledge Vault Investigation

**Investigator:** survey_explorer_3 (Archetype: teamwork_preview_explorer)  
**Date:** 2026-09-02  
**Target Solution:** `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\BimboProyecto.sln`  
**Authoritative Request:** `D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md`

---

## 1. Executive Summary

This investigation covers five interconnected subsystems of the Bimbo Honduras WPF desktop application (.NET 8):
1. **`GhostTextBox` control (`CapaUI/Core/Controls/GhostTextBox.xaml(.cs)`)**: Analyzing missing `MaxLength` DependencyProperty, visual horizontal scroll desynchronization between `InnerBox` and `GhostDisplay`, foreign domain concatenation bugs in `GetRemainingSuffix`, memory leaks in `CancellationTokenSource` debouncing, and defensive clamping in `GetFullText()`.
2. **Login and Recovery Panels (`LoginWindow`, `ForgotEmailPanel`, `ForgotNewPanel`)**: Auditing email (max 50) and password (min 6, max 72) field constraints in XAML and code-behind.
3. **`ConfiguracionEmpresaViewModel` & `ConfiguracionEmpresaModal.xaml`**: Assessing missing defensive length validation (`ReglasFormato.NoExcedeLargo`) in MVVM and missing `MaxLength` attributes in XAML.
4. **Knowledge Vault (`contexto/`)**: Reviewing ADRs (`ADR-004`, `ADR-021`), pattern notes (`Validacion de formularios`, `Anatomia compartida de los modales`), technical debt tracking (`Deuda Técnica - Pendientes.md` P-042, P-045, and new entries), and session notes in `contexto/70 - Bitácora de Cambios/2026-09/`.
5. **Test Project (`BimboProyecto.Tests/`)**: Examining project setup (.NET 8, xUnit 2.9.3, Npgsql 8.0.3, project references), existing test suites (118 passing tests), and structure for the new schema drift and boundary test suite (`Dominio/ReglasEntidadesTests.cs` and `Dominio/ReglasFormatoTests.cs`).

---

## 2. GhostTextBox Investigation (`CapaUI/Core/Controls/GhostTextBox`)

### 2.1 File Locations and Current Structure
- **XAML:** `CapaUI/Core/Controls/GhostTextBox.xaml` (36 lines)
- **Code-behind:** `CapaUI/Core/Controls/GhostTextBox.xaml.cs` (277 lines)

```
GhostTextBox (UserControl)
 └── Grid
      ├── GhostDisplay (TextBox, read-only, non-interactive, foreground #B0B8C4)
      └── InnerBox     (TextBox, interactive input, foreground #1A1F2E)
```

### 2.2 Detailed Defect Analysis & Remediation Blueprint

#### Defect A: Missing `MaxLength` DependencyProperty
- **Current State (`GhostTextBox.xaml.cs:13-44`):** Only `TextProperty`, `GhostSuffixProperty`, and `PlaceholderProperty` are registered.
- **Problem:** When `GhostTextBox` is used in forms or Login, setting `MaxLength` on the `UserControl` does nothing unless a DP exists that forwards the value to `InnerBox.MaxLength`.
- **Solution:**
  1. Register `MaxLengthProperty`:
     ```csharp
     public static readonly DependencyProperty MaxLengthProperty =
         DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(GhostTextBox),
             new PropertyMetadata(0, OnMaxLengthChanged));

     public int MaxLength
     {
         get => (int)GetValue(MaxLengthProperty);
         set => SetValue(MaxLengthProperty, value);
     }

     private static void OnMaxLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
     {
         var ctrl = (GhostTextBox)d;
         ctrl.InnerBox.MaxLength = (int)e.NewValue;
     }
     ```
  2. Maintain `GhostDisplay.MaxLength = 0` (unconstrained) so that the ghost overlay (which renders `input + remainingSuffix`) is never artificially truncated by the user input limit.

#### Defect B: Scroll Desynchronization (`ScrollViewer.ScrollChangedEvent` & `HorizontalOffset`)
- **Current State:** `InnerBox` and `GhostDisplay` are stacked inside a `Grid`. When the user types long text (>60 characters) that causes `InnerBox`'s internal `ScrollViewer` to scroll horizontally, `GhostDisplay` stays at horizontal offset 0. The ghost text drifts horizontally and becomes visibly detached from the typed text.
- **Root Cause:** `GhostDisplay` is a separate non-interactive `TextBox` and does not automatically listen to `InnerBox`'s scroll offset changes.
- **Solution:**
  1. In `GhostTextBox` constructor / `Loaded`:
     ```csharp
     InnerBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(InnerBox_ScrollChanged));
     ```
  2. Implement `InnerBox_ScrollChanged`:
     ```csharp
     private void InnerBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
     {
         GhostDisplay.ScrollToHorizontalOffset(e.HorizontalOffset);
     }
     ```
  3. In `ShowGhostFor`: When ghost text is assigned, WPF layout updates may defer scroll positioning. Synchronize via `Dispatcher.BeginInvoke`:
     ```csharp
     Dispatcher.BeginInvoke(new Action(() =>
     {
         GhostDisplay.ScrollToHorizontalOffset(InnerBox.HorizontalOffset);
     }), System.Windows.Threading.DispatcherPriority.Loaded);
     ```

#### Defect C: Foreign Domain Concatenation in `GetRemainingSuffix`
- **Current State (`GhostTextBox.xaml.cs:234-254`):**
  ```csharp
  private string GetRemainingSuffix(string input)
  {
      string suffix = GhostSuffix;
      int maxOverlap = Math.Min(input.Length, suffix.Length);
      for (int len = maxOverlap; len >= 1; len--)
      {
          string inputTail = input.Substring(input.Length - len);
          string suffixHead = suffix.Substring(0, len);
          if (string.Equals(inputTail, suffixHead, StringComparison.OrdinalIgnoreCase))
              return suffix.Substring(len);
      }
      return suffix; // BUG: Returns full suffix even if input contains an alien email domain!
  }
  ```
- **Problem:** If a user types `test@yahoo.com`, `input` contains `@`. Because `yahoo.com` does not match `@gmail.com`, the loop finds no overlap and returns `@gmail.com`. Consequently:
  - `ShowGhostFor` displays `test@yahoo.com@gmail.com`.
  - `GetFullText()` returns `test@yahoo.com@gmail.com`.
  - Authentication against Supabase Auth receives a corrupted email string.
- **Solution:** If `input` already contains `@` and there is no overlap with `GhostSuffix` (e.g. typing a third-party domain like `yahoo.com`, `hotmail.com`), return `string.Empty`:
  ```csharp
  private string GetRemainingSuffix(string input)
  {
      if (string.IsNullOrEmpty(GhostSuffix) || string.IsNullOrEmpty(input))
          return GhostSuffix ?? string.Empty;

      string suffix = GhostSuffix;
      int maxOverlap = Math.Min(input.Length, suffix.Length);

      for (int len = maxOverlap; len >= 1; len--)
      {
          string inputTail = input.Substring(input.Length - len);
          string suffixHead = suffix.Substring(0, len);

          if (string.Equals(inputTail, suffixHead, StringComparison.OrdinalIgnoreCase))
              return suffix.Substring(len);
      }

      // If user typed an '@' and it didn't match the suffix prefix, do NOT append suffix
      if (input.Contains('@'))
          return string.Empty;

      return suffix;
  }
  ```

#### Defect D: Clamp in `GetFullText()`
- **Current State (`GhostTextBox.xaml.cs:264-272`):**
  ```csharp
  public string GetFullText()
  {
      string input = InnerBox.Text?.Trim() ?? "";
      if (string.IsNullOrEmpty(input)) return "";
      if (IsFullyCompleted(input)) return input;
      return input + GetRemainingSuffix(input);
  }
  ```
- **Solution:** Add defensive clamping when `MaxLength > 0`:
  ```csharp
  public string GetFullText()
  {
      string input = InnerBox.Text?.Trim() ?? "";
      if (string.IsNullOrEmpty(input)) return "";

      string full = IsFullyCompleted(input) ? input : input + GetRemainingSuffix(input);
      if (MaxLength > 0 && full.Length > MaxLength)
          full = full[..MaxLength];

      return full;
  }
  ```

#### Defect E: CTS Debouncer `.Dispose()`
- **Current State (`GhostTextBox.xaml.cs:142, 164, 190`):** `_ghostDebounce?.Cancel();` is called without `.Dispose()`.
- **Solution:** Safely dispose prior debouncer instances:
  ```csharp
  if (_ghostDebounce is not null)
  {
      _ghostDebounce.Cancel();
      _ghostDebounce.Dispose();
      _ghostDebounce = null;
  }
  ```

---

## 3. Login and Recovery Panels Investigation

### 3.1 `LoginWindow` (`CapaUI/Formularios/InicioSesion/LoginWindow.xaml(.cs)`)
- **`TxtEmail` (`GhostTextBox`):** Currently has no `MaxLength` set. Must be set to `50` (`ReglasUsuario.Correo.LargoMaximo`).
- **`TxtPassword` (`PasswordBox`):** Currently has no `MaxLength` set (`0` / unlimited). Must be set to `72` (`ReglasUsuario.Password.LargoMaximo`).
- **`TxtPasswordVisible` (`TextBox`):** Currently has no `MaxLength` set. Must be set to `72`.
- **Implementation in `LoginWindow.xaml.cs`:**
  In constructor or `LoginWindow_Loaded`:
  ```csharp
  TxtEmail.MaxLength = ReglasUsuario.Correo.LargoMaximo ?? 50;
  TxtPassword.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;
  TxtPasswordVisible.MaxLength = ReglasUsuario.Password.LargoMaximo ?? 72;
  ```

### 3.2 `ForgotEmailPanel` (`CapaUI/Formularios/InicioSesion/ForgotEmailPanel.xaml(.cs)`)
- **`TxtEmail` (`TextBox`):** Currently has no `MaxLength`.
- **Implementation:**
  - In `ForgotEmailPanel.xaml`: Set `MaxLength="50"` on `TxtEmail` (or in code-behind via `ReglasUsuario.Correo.LargoMaximo`).

### 3.3 `ForgotNewPanel` (`CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml(.cs)`)
- **Controls involved:**
  - `TxtNew` (`PasswordBox`)
  - `TxtNewVisible` (`TextBox`)
  - `TxtConfirm` (`PasswordBox`)
  - `TxtConfirmVisible` (`TextBox`)
- **Current State:** None of the 4 controls specify `MaxLength`.
- **Implementation:**
  - Set `MaxLength="72"` in XAML for all 4 controls (or in constructor via `ReglasUsuario.Password.LargoMaximo`).

---

## 4. `ConfiguracionEmpresaViewModel` & `ConfiguracionEmpresaModal.xaml`

### 4.1 `ConfiguracionEmpresaViewModel.cs` (`CapaUI/Formularios/Principal/Pantallas/Configuracion/`)
- **Current Validation (`ConfiguracionEmpresaViewModel.cs:197-224`):**
  ```csharp
  private bool DatosValidos()
  {
      if (!ReglasFormato.TieneContenido(NombreEmpresa))
      {
          Error = "El nombre de la empresa es obligatorio.";
          return false;
      }
      if (!ReglasFormato.EsRtn(RtnEmpresa))
      {
          Error = "El RTN debe tener 14 dígitos.";
          return false;
      }
      if (!ReglasFormato.EsTelefono(TelefonoEmpresa))
      {
          Error = "El teléfono debe tener entre 8 y 15 dígitos.";
          return false;
      }
      if (!ReglasFormato.EsCorreo(CorreoEmpresa))
      {
          Error = "El correo no tiene un formato válido.";
          return false;
      }
      return true;
  }
  ```
- **Missing Defensive Validations (`ReglasFormato.NoExcedeLargo`):**
  1. `NombreEmpresa`: `ReglasEmpresa.Nombre` (max 200)
  2. `RtnEmpresa`: `ReglasEmpresa.Rtn` (max 20)
  3. `DireccionEmpresa`: `ReglasEmpresa.Direccion` (max 500 - UI limit)
  4. `TelefonoEmpresa`: `ReglasEmpresa.Telefono` (max 20)
  5. `CorreoEmpresa`: `ReglasEmpresa.Correo` (max 100)
  6. `DominioCorreo`: max 50 / 100
- **Remediated `DatosValidos()`:**
  ```csharp
  private bool DatosValidos()
  {
      if (!ReglasFormato.TieneContenido(NombreEmpresa))
      {
          Error = "El nombre de la empresa es obligatorio.";
          return false;
      }
      if (!ReglasFormato.NoExcedeLargo(NombreEmpresa, ReglasEmpresa.Nombre.LargoMaximo ?? 200))
      {
          Error = $"El nombre de la empresa no puede superar los {ReglasEmpresa.Nombre.LargoMaximo ?? 200} caracteres.";
          return false;
      }
      if (!ReglasFormato.EsRtn(RtnEmpresa))
      {
          Error = "El RTN debe tener 14 dígitos.";
          return false;
      }
      if (!ReglasFormato.NoExcedeLargo(RtnEmpresa, ReglasEmpresa.Rtn.LargoMaximo ?? 20))
      {
          Error = "El RTN no puede superar los 20 caracteres.";
          return false;
      }
      if (!ReglasFormato.NoExcedeLargo(DireccionEmpresa, ReglasEmpresa.Direccion.LargoMaximo ?? 500))
      {
          Error = $"La dirección no puede superar los {ReglasEmpresa.Direccion.LargoMaximo ?? 500} caracteres.";
          return false;
      }
      if (!ReglasFormato.EsTelefono(TelefonoEmpresa))
      {
          Error = "El teléfono debe tener entre 8 y 15 dígitos.";
          return false;
      }
      if (!ReglasFormato.NoExcedeLargo(TelefonoEmpresa, ReglasEmpresa.Telefono.LargoMaximo ?? 20))
      {
          Error = "El teléfono no puede superar los 20 caracteres.";
          return false;
      }
      if (!ReglasFormato.EsCorreo(CorreoEmpresa))
      {
          Error = "El correo no tiene un formato válido.";
          return false;
      }
      if (!ReglasFormato.NoExcedeLargo(CorreoEmpresa, ReglasEmpresa.Correo.LargoMaximo ?? 100))
      {
          Error = "El correo no puede superar los 100 caracteres.";
          return false;
      }
      return true;
  }
  ```

### 4.2 `ConfiguracionEmpresaModal.xaml`
- **Current State:** None of the `TextBox` controls have `MaxLength`.
- **Remediation:**
  - `NombreEmpresa`: `MaxLength="200"`
  - `RtnEmpresa`: `MaxLength="20"`
  - `DireccionEmpresa`: `MaxLength="500"`
  - `TelefonoEmpresa`: `MaxLength="20"`
  - `CorreoEmpresa`: `MaxLength="100"`
  - `DominioCorreo`: `MaxLength="50"` (or `100`)

---

## 5. Knowledge Vault (`contexto/`)

### 5.1 Vault Architecture & Protocol
All notes in `contexto/` strictly adhere to the following conventions:
1. **YAML frontmatter:**
   ```yaml
   ---
   title: "..."
   tags: [tag1, tag2]
   date: YYYY-MM-DD
   estado: aceptado / implementado / verificado
   ---
   ```
2. **Title:** Single `# Title` matching frontmatter.
3. **Internal Links:** `[[Note Title]]` wikilinks without file extensions.
4. **Relaciones Section:** `## Relaciones` at the bottom listing related wikilinks with concise descriptions.

### 5.2 Target Vault Files for Update & Creation

| Note Path | Action | Content Scope |
|---|---|---|
| `contexto/45 - Decisiones/ADR-021 - Validacion en tres capas reglas de negocio en Dominio.md` | Addendum | Documenting `TopePreventivo(m)` in `ValidadorFormulario.Segun()`, automatic UI `MaxLength` propagation when `MaxLength == 0`, and database schema alignment for string columns. |
| `contexto/45 - Decisiones/ADR-004 - GhostTextBox Autocompletado de Dominio en Login.md` | Addendum | Documenting `MaxLength` DP propagation, ScrollViewer horizontal offset sync (`ScrollViewer.ScrollChangedEvent` + `Dispatcher.BeginInvoke`), alien `@` domain detection in `GetRemainingSuffix`, CTS debouncer `.Dispose()`, and `GetFullText()` clamping. |
| `contexto/20 - Patrones/Validacion de formularios.md` | Update | Updating how `ValidadorFormulario.Segun()` derives preventive `MaxLength` without needing manual XAML attributes. |
| `contexto/20 - Patrones/Anatomia compartida de los modales.md` | Update | Documenting removal of hardcoded `MaxLength="100"` in CRUD modales in favor of domain-derived `TopePreventivo`. |
| `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` | Update & New Entry | Update `P-045` (Pesaje text limits) & `P-042` (Pesaje styles); register new debt item for `ModalInput` / `InputBox` `VerticalAlignment` and MaxLength behavior. |
| `contexto/70 - Bitácora de Cambios/2026-09/Sesión 2026-09-02 - Validación de longitud máxima en campos de texto.md` | Create | Full session log detailing all changes across Domain, UI, Tests, and Documentation. |

---

## 6. Test Project (`BimboProyecto.Tests/`)

### 6.1 Project Configuration
- **Project File:** `BimboProyecto.Tests/BimboProyecto.Tests.csproj`
- **Framework:** `net8.0`
- **Packages:**
  - `Microsoft.NET.Test.Sdk` (17.14.1)
  - `xunit` (2.9.3)
  - `xunit.runner.visualstudio` (3.1.5)
  - `Npgsql` (8.0.3)
- **References:** `CapaAplicacion`, `CapaDatos`, `CapaDominio`
- **Current Status:** 118 unit tests executing in 1.0s with 100% pass rate.

### 6.2 New Test Architecture: `Dominio/ReglasEntidadesTests.cs`
The new test file will contain three distinct test suites:

```
BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs
 ├── Test A: Schema Drift Test (Live PostgreSQL via Npgsql)
 │    ├── Reads BIMBO_POSTGRES_CONNECTION_STRING (skips gracefully if null/empty)
 │    ├── Queries information_schema.columns for public tables
 │    ├── Validates:
 │    │    1. No ReglasEntidades.LargoMaximo is greater than DB character_maximum_length
 │    │    2. All mapped entity columns exist in DB schema
 │    │    3. Columns with data_type = 'text' are designated as UI topes (500)
 ├── Test B: Pinned Values Test (Offline / CI Unit Tests)
 │    └── [Theory] parametrized tests asserting exact LargoMaximo, Obligatorio, Formato
 └── Test C: Reflection Audit Test
      └── Scans CapaDominio.Reglas.* for all public static ReglaCampo fields and ensures 100% coverage in audit map
```

### 6.3 Boundary Tests Expansion in `Dominio/ReglasFormatoTests.cs`
- `NoExcedeLargo`:
  - `null` -> `true`
  - `""` -> `true`
  - `"   "` -> `true` (trimmed length 0 <= N)
  - `new string('x', N)` -> `true` (exact boundary)
  - `new string('x', N + 1)` -> `false`
  - `"  " + new string('x', N) + "  "` -> `true` (whitespace trimmed)

---

## 7. Complete Actionable Plan for Implementation

| Step | Area | Target Files | Key Actions |
|---|---|---|---|
| **1** | CapaDominio | `CapaDominio/Reglas/ReglasEntidades.cs` | Update entity rules: `ReglasProducto` (Codigo 50, Nombre 200, Contenido 100), `ReglasCategoria` (Nombre 100, Descripcion 200), `ReglasPresentacion` (Nombre 100, Descripcion 500), `ReglasFabricante` (Nombre 200, Descripcion 500), `ReglasProveedor` (Nombre 200, Rtn 20, Telefono 20, Correo 100, Direccion 500), `ReglasEmpleado` (Nombre 100, Apellido 100, Identidad 20 obligatoria, Telefono 20, Correo 100), `ReglasUsuario` (Correo 50, Password min 6 max 72), `ReglasRol` (Nombre 50), `ReglasContacto` (Nombre 100, Telefono 20, Correo 100), `ReglasEmpresa` (Nombre 200, Rtn 20, Telefono 20, Correo 100, Direccion 500). |
| **2** | CapaUI Core | `CapaUI/Core/Validacion/ValidadorFormulario.cs` | Add `TopePreventivo(m)` to `Segun()`: sets `tb.MaxLength = m` or `pb.MaxLength = m` only when `MaxLength == 0`. |
| **3** | CapaUI Modales | `ProveedorModal`, `FabricanteModal`, `CategoriaModal`, `PresentacionModal`, `EmpleadoModal` XAML & CS | Remove manual `MaxLength="100"` in XAML. Add missing fields to validator: `TxtIdentidad` in `EmpleadoModal`, `TxtContenido` in `ProductoModal`, `TxtEmail` in `UsuarioModal`. Update `GenerarEmail` in `UsuarioModal` to truncate local part before `@empresa.com` to stay within 50 chars. |
| **4** | CapaUI GhostTextBox | `GhostTextBox.xaml`, `GhostTextBox.xaml.cs` | Add `MaxLength` DP, `ScrollViewer.ScrollChangedEvent` horizontal offset sync, `ShowGhostFor` dispatcher sync, `@` check in `GetRemainingSuffix`, `GetFullText()` clamping, CTS debouncer `.Dispose()`. |
| **5** | CapaUI Login & Views | `LoginWindow.xaml.cs`, `ForgotEmailPanel`, `ForgotNewPanel`, `ConfiguracionEmpresaViewModel.cs`, `ConfiguracionEmpresaModal.xaml` | Set `TxtEmail.MaxLength = 50`, `TxtPassword.MaxLength = 72`. Add `ReglasFormato.NoExcedeLargo` to `DatosValidos()` in `ConfiguracionEmpresaViewModel` and `MaxLength` in XAML. |
| **6** | Tests | `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`, `ReglasFormatoTests.cs` | Create Test A (DB drift with `Npgsql`), Test B (pinned values), Test C (reflection audit). Add boundary test cases to `ReglasFormatoTests`. |
| **7** | Vault | `contexto/45 - Decisiones/`, `contexto/20 - Patrones/`, `contexto/40 - Proyecto Bimbo/`, `contexto/70 - Bitácora de Cambios/2026-09/` | Write ADR addendums, update pattern notes, update `P-042`/`P-045` and add new debt in `Deuda Técnica`, and write session note `Sesión 2026-09-02 - Validación de longitud máxima en campos de texto.md`. |

---

## 8. Verification Commands & Acceptance Criteria

```powershell
# Build entire solution
dotnet build BimboProyecto.sln

# Run test suite
dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj

# Run test suite with PostgreSQL connection for Schema Drift Test (if connection string available)
$env:BIMBO_POSTGRES_CONNECTION_STRING="<connection-string>"
dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
```
