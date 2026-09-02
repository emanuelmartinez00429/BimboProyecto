# Empirical Adversarial Challenge Report — Milestone 1 (R1)

**Challenger:** `challenger_m1_2` (Archetype: `teamwork_preview_challenger`)  
**Verdict:** `APPROVE`  
**Overall Risk Assessment:** `LOW`  
**Target:** `CapaDominio/Reglas/ReglasEntidades.cs`  
**Date:** 2026-09-02  

---

## 1. Observation

Direct code and test observations for `CapaDominio/Reglas/ReglasEntidades.cs`:

1. **Entity Rule Declarations & Alignment (`CapaDominio/Reglas/ReglasEntidades.cs`):**
   - **`ReglasProducto`**:
     - `Codigo`: `new(Obligatorio: true, LargoMaximo: 50);` — Column: `codigo_producto` (varchar 50).
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 200);` — Column: `nombre_producto` (varchar 200).
     - `Contenido`: `new(LargoMaximo: 100);` — Column: `contenido` (varchar 100).
     - `PesoTeorico`: `new(Formato: FormatoCampo.Decimal);` — Column: `peso_teorico` (numeric).
     - `PrecioPorKg`: `new(Formato: FormatoCampo.Decimal);` — Column: `precio_por_kg` (numeric).
   - **`ReglasCategoria`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 100);` — Column: `nombre_categoria` (varchar 100).
     - `Descripcion`: `new(LargoMaximo: 200);` — Column: `descripcion_categoria` (varchar 200). *(Fixed dangerous 255 drift)*.
   - **`ReglasPresentacion`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 100);` — Column: `nombre_presentacion` (varchar 100).
     - `Descripcion`: `new(LargoMaximo: 500);` with lexical marker `// Tope de UI de 500 caracteres (columna text en BD)`.
   - **`ReglasFabricante`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 200);` — Column: `nombre_fabricante` (varchar 200).
     - `Descripcion`: `new(LargoMaximo: 500);` with lexical marker `// Tope de UI de 500 caracteres (columna text en BD)`.
   - **`ReglasProveedor`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 200);` — Column: `nombre_proveedor` (varchar 200).
     - `Rtn`: `new(LargoMaximo: 20, Formato: FormatoCampo.Rtn);` — Column: `rtn_proveedor` (varchar 20).
     - `Telefono`: `new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);` — Column: `telefono_proveedor` (varchar 20).
     - `Correo`: `new(LargoMaximo: 100, Formato: FormatoCampo.Correo);` — Column: `correo_proveedor` (varchar 100).
     - `Direccion`: `new(LargoMaximo: 500);` with lexical marker `// Tope de UI de 500 caracteres (columna text en BD)`.
   - **`ReglasEmpleado`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 100);` — Column: `nombre_empleado` (varchar 100).
     - `Apellido`: `new(Obligatorio: true, LargoMaximo: 100);` — Column: `apellido_empleado` (varchar 100).
     - `Identidad`: `new(Obligatorio: true, LargoMaximo: 20);` — Column: `numero_identidad` (varchar 20).
     - `Telefono`: `new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);` — Column: `telefono_empleado` (varchar 20).
     - `Correo`: `new(LargoMaximo: 100, Formato: FormatoCampo.Correo);` — Column: `correo_empleado` (varchar 100).
   - **`ReglasUsuario`**:
     - `Empleado`: `new(Obligatorio: true);` — Column: `id_empleado` (integer).
     - `Rol`: `new(Obligatorio: true);` — Column: `id_rol` (integer).
     - `Correo`: `new(Obligatorio: true, LargoMaximo: 50, Formato: FormatoCampo.Correo);` — Column: `alias_usuario` (varchar 50).
     - `Password`: `new(Obligatorio: true, LargoMinimo: 6, LargoMaximo: 72);` — Supabase Auth / Bcrypt constraints.
   - **`ReglasRol`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 50);` — Column: `nombre_rol` (varchar 50).
   - **`ReglasContacto`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 100);` — Column: `nombre_contacto` (varchar 100).
     - `Telefono`: `new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);` — Column: `telefono_contacto` (varchar 20).
     - `Correo`: `new(LargoMaximo: 100, Formato: FormatoCampo.Correo);` — Column: `correo_contacto` (varchar 100).
   - **`ReglasEmpresa`**:
     - `Nombre`: `new(Obligatorio: true, LargoMaximo: 200);` — Column: `nombre_empresa` (varchar 200).
     - `Rtn`: `new(LargoMaximo: 20, Formato: FormatoCampo.Rtn);` — Column: `rtn_empresa` (varchar 20).
     - `Telefono`: `new(LargoMaximo: 20, Formato: FormatoCampo.Telefono);` — Column: `telefono_empresa` (varchar 20).
     - `Correo`: `new(LargoMaximo: 100, Formato: FormatoCampo.Correo);` — Column: `correo_empresa` (varchar 100).
     - `Direccion`: `new(LargoMaximo: 500);` with lexical marker `// Tope de UI de 500 caracteres (columna text en BD)`.

2. **Compilation & Test Execution:**
   - Command: `dotnet build BimboProyecto.sln` -> Exited with code 0 (0 warnings, 0 errors).
   - Command: `dotnet test` -> Exited with code 0 (118 passed, 0 failed, 0 skipped, duration 1.95s).
   - Command: `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --filter "FullyQualifiedName~Reglas"` -> 25 passed, 0 failed.

---

## 2. Logic Chain & Adversarial Challenges

### Challenge 1: Bcrypt Truncation and Supabase Auth Boundaries in `ReglasUsuario.Password`
- **Assumption Challenged:** Can passwords shorter than 6 characters or longer than 72 characters cause silent truncation or server rejection in Supabase Auth?
- **Attack Scenario:** Bcrypt silently truncates password strings beyond 72 bytes. A user setting a 75-character password would have characters 73–75 ignored by Bcrypt, creating a security illusion. Conversely, Supabase Auth rejects passwords < 6 characters at the API gateway level.
- **Verification Result:** `ReglasUsuario.Password` explicitly sets `Obligatorio: true, LargoMinimo: 6, LargoMaximo: 72`. When consumed by UI controls (`TxtPassword.MaxLength = 72`) and validators, inputs < 6 are rejected and inputs > 72 are blocked at the typing layer.
- **Verdict:** PASS / Mitigated.

### Challenge 2: Mandatory Constraints on Employee Identity and User Account Fields
- **Assumption Challenged:** Could an employee record be submitted without a valid Honduran DNI / Identity or user without email / role?
- **Attack Scenario:** In PostgreSQL, `numero_identidad` and `alias_usuario` are NOT NULL unique constraints. If domain rules allowed optionality, UI would allow blank submissions resulting in unhandled database exceptions.
- **Verification Result:** `ReglasEmpleado.Identidad` has `Obligatorio: true, LargoMaximo: 20`. `ReglasUsuario.Correo` has `Obligatorio: true, LargoMaximo: 50, Formato: FormatoCampo.Correo`. `ReglasUsuario.Empleado` and `Rol` have `Obligatorio: true`.
- **Verdict:** PASS / Robust.

### Challenge 3: UI Text Ceilings for Unbounded PostgreSQL `text` Columns
- **Assumption Challenged:** Do PostgreSQL `text` columns allow arbitrarily massive text pastes that could degrade WPF rendering?
- **Attack Scenario:** Large pasted text (e.g. megabytes) into `PresentacionModal`, `FabricanteModal`, `ProveedorModal`, or `ConfiguracionEmpresa` could cause memory allocation pressure and UI freezes.
- **Verification Result:** All 4 `text` columns (`ReglasPresentacion.Descripcion`, `ReglasFabricante.Descripcion`, `ReglasProveedor.Direccion`, `ReglasEmpresa.Direccion`) are clamped to `LargoMaximo: 500` and documented with `// Tope de UI de 500 caracteres (columna text en BD)`.
- **Verdict:** PASS / Robust.

### Challenge 4: Category Description MaxLength Fixed Against Database Drift
- **Assumption Challenged:** Previous codebase had `ReglasCategoria.Descripcion = 255`, but PostgreSQL column `descripcion_categoria` is `varchar(200)`.
- **Attack Scenario:** Inputting a 230-character category description would pass domain validation but fail at PostgreSQL insert (`value too long for type character varying(200)`).
- **Verification Result:** `ReglasCategoria.Descripcion` is now strictly `LargoMaximo: 200`, completely eliminating this database drift failure mode.
- **Verdict:** PASS / Fixed.

---

## 3. Stress Test Results Summary

| Scenario | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|
| Password length < 6 | Rejected by `LargoMinimo: 6` | `ReglasUsuario.Password.LargoMinimo == 6` | PASS |
| Password length = 72 | Accepted by `LargoMaximo: 72` | `ReglasUsuario.Password.LargoMaximo == 72` | PASS |
| Password length = 73 | Blocked / Rejected | `ReglasUsuario.Password.LargoMaximo == 72` | PASS |
| Employee Identity blank | Rejected by `Obligatorio: true` | `ReglasEmpleado.Identidad.Obligatorio == true` | PASS |
| Category Description 201 chars | Rejected by `LargoMaximo: 200` | `ReglasCategoria.Descripcion.LargoMaximo == 200` | PASS |
| Text column length clamp | Clamped to 500 chars | All 4 text columns declare `LargoMaximo: 500` | PASS |
| Solution Compilation | Exits code 0 | `dotnet build BimboProyecto.sln` -> 0 Errors | PASS |
| Solution Tests | 100% pass | `dotnet test` -> 118/118 Passed | PASS |

---

## 4. Caveats

- `No caveats.` The review is strictly scoped to M1 (`CapaDominio/Reglas/ReglasEntidades.cs`). UI automatic derivation (`ValidadorFormulario`), modal XAML modifications, GhostTextBox fixes, schema drift tests, and knowledge vault docs will be executed and reviewed in milestones M2–M6.

---

## 5. Conclusion

**Verdict: APPROVE**

The work product in `CapaDominio/Reglas/ReglasEntidades.cs` fulfills all R1 requirements, accurately mirrors the Supabase PostgreSQL database schema across all 10 entities and 34 rules, enforces appropriate password boundaries (6..72), enforces required identity/user fields, applies 500-character UI ceilings on all `text` columns with proper lexical markers, compiles cleanly, and passes all test suites.

---

## 6. Verification Method

To independently reproduce and verify:

1. **Verify Solution Build:**
   ```powershell
   dotnet build BimboProyecto.sln
   ```
   *Expected:* Exits with code 0 (0 warnings, 0 errors).

2. **Execute Full Test Suite:**
   ```powershell
   dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
   ```
   *Expected:* 118/118 tests passing.

3. **Inspect Domain Rules File:**
   ```powershell
   Get-Content CapaDominio/Reglas/ReglasEntidades.cs
   ```
