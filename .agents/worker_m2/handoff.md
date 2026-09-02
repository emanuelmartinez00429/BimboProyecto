# Handoff Report - Milestone 2 (R2: Derivación Automática de MaxLength y Limpieza de XAML)

**Agent**: worker_m2 (teamwork_preview_worker)  
**Milestone**: Milestone 2 - R2  
**Date**: 2026-09-02  
**Status**: Completed  

---

## 1. Observation

### 1.1 `CapaUI/Core/Validacion/ValidadorFormulario.cs`
- Prior state: `Segun(ReglaCampo regla)` and `LargoMaximo(int largo, string? mensaje = null)` added reactive validation rules without preventing keyboard input at the control level when `MaxLength == 0`.
- Modified state:
  - Added `TopePreventivo(int max)` to `ConstructorCampo`:
    ```csharp
    private void TopePreventivo(int max)
    {
        if (_campo.Control is TextBox tb && tb.MaxLength == 0)
            tb.MaxLength = max;
        else if (_campo.Control is PasswordBox pb && pb.MaxLength == 0)
            pb.MaxLength = max;
    }
    ```
  - In `LargoMaximo(int largo, string? mensaje = null)`: calls `TopePreventivo(largo)` before adding the rule.
  - In `Segun(ReglaCampo regla)`: when `regla.LargoMaximo is int m`, calls `TopePreventivo(m)` and `LargoMaximo(m)`.

### 1.2 XAML Modal Files Manual `MaxLength` Removal
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedorModal.xaml` (line 82): removed redundant `MaxLength="100"` from `TxtNombre`.
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricanteModal.xaml` (line 80): removed redundant `MaxLength="100"` from `TxtNombre`.
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriaModal.xaml` (line 80): removed redundant `MaxLength="100"` from `TxtNombre`.
- `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionModal.xaml` (line 88): removed redundant `MaxLength="100"` from `TxtNombre`.
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml` (lines 81, 85): removed redundant `MaxLength="100"` from `TxtNombre` and `TxtApellido`.

### 1.3 Missing Field Registrations in Modal Code-Behinds
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml.cs` (lines 37-43): Added `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)`.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs` (lines 86-93): Added `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)`.
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs` (lines 68-75): Added `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)`.

### 1.4 Truncation in `UsuarioModal.xaml.cs` (`GenerarEmail`)
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs` (lines 188-212):
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

### 1.5 Verification Commands and Results
- `dotnet build BimboProyecto.sln`: Exited with code 0 (0 warnings, 0 errors).
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`: Exited with code 0 (118 passed, 0 failed, 0 skipped).

---

## 2. Logic Chain

1. **Automatic MaxLength Derivation**: By attaching `TopePreventivo(max)` in `ValidadorFormulario.ConstructorCampo`, any control registered through `.Segun(regla)` or `.LargoMaximo(largo)` inherits the maximum character length restriction directly on the WPF `TextBox` or `PasswordBox` element as long as `MaxLength == 0`.
2. **Preventing Conflict and Redundancy in XAML**: Hardcoded `MaxLength="100"` in modal XAML controls for entities with 200-character database columns (like Proveedor and Fabricante) artificially truncated valid input. Removing them allows `ValidadorFormulario` to dynamically configure the exact `MaxLength` from domain rules (`ReglasProveedor.Nombre` = 200, `ReglasFabricante.Nombre` = 200).
3. **Closing Validation Gaps**: Unregistered fields (`TxtIdentidad`, `TxtContenido`, `TxtEmail`) allowed invalid/empty data to reach database queries or be submitted unvalidated. Registering them in `_validador` ensures proper validation highlighting and error tooltip display.
4. **PostgreSQL Column Boundary Protection**: `usuarios.alias_usuario` in PostgreSQL is `varchar(50)`. Truncating the local part of auto-generated emails to 38 chars (`50 - "@empresa.com".Length`) guarantees the total email string never exceeds 50 characters while preserving the full domain name.

---

## 3. Caveats

- Controls that do not use `ValidadorFormulario` (e.g., `ConfiguracionEmpresaModal`, `LoginWindow`, `Forgot*.xaml`) are outside `worker_m2` ownership and are handled by `worker_m3`.
- Unit tests for PostgreSQL schema drift and domain rules are owned by `worker_m4`.

---

## 4. Conclusion

All Milestone 2 tasks assigned to `worker_m2` have been fully and genuinely implemented. The solution builds with 0 errors and all 118 existing unit tests pass.

---

## 5. Verification Method

1. **Solution Build**:
   ```powershell
   dotnet build BimboProyecto.sln
   ```
   *Expected result*: 0 Errors, 0 Warnings, exit code 0.

2. **Automated Tests**:
   ```powershell
   dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
   ```
   *Expected result*: 118 tests passed, 0 failed, exit code 0.

3. **File Inspection**:
   - `CapaUI/Core/Validacion/ValidadorFormulario.cs`
   - `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedorModal.xaml`
   - `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricanteModal.xaml`
   - `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriaModal.xaml`
   - `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionModal.xaml`
   - `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml`
   - `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml.cs`
   - `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs`
   - `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs`
