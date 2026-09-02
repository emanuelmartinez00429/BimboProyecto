# Handoff Report: UI Validation Layer & Modals Survey

## 1. Observation
- `CapaUI/Core/Validacion/ValidadorFormulario.cs` (lines 318-334): `Segun()` evaluates `LargoMaximo(m)` as a reactive validation rule, but does NOT assign `MaxLength` on the underlying `TextBox` or `PasswordBox` controls.
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedorModal.xaml` (line 82): `TxtNombre` has hardcoded `MaxLength="100"`.
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricanteModal.xaml` (line 80): `TxtNombre` has hardcoded `MaxLength="100"`.
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriaModal.xaml` (line 80): `TxtNombre` has hardcoded `MaxLength="100"`.
- `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionModal.xaml` (line 88): `TxtNombre` has hardcoded `MaxLength="100"`.
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml` (lines 81, 85): `TxtNombre` and `TxtApellido` have hardcoded `MaxLength="100"`.
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml.cs` (lines 37-42): `TxtIdentidad` is missing from `_validador`.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs` (lines 86-92): `TxtContenido` is missing from `_validador`.
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs` (lines 68-74): `TxtEmail` is missing from `_validador`.
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs` (lines 188-197): `GenerarEmail` builds ${nombre}.${apellido}@empresa.com without limiting the local part length.

## 2. Logic Chain
1. By introducing `TopePreventivo(int max)` inside `ValidadorFormulario.ConstructorCampo`, whenever `Segun()` or `LargoMaximo()` is invoked with a maximum length `m`, if `_campo.Control` is `TextBox` or `PasswordBox` with `MaxLength == 0`, `MaxLength` is dynamically set to `m`.
2. Hardcoded `MaxLength="100"` in XAML interferes with dynamic assignment (and artificially truncates 200-char fields like Proveedor and Fabricante names). Removing these attributes leaves `MaxLength == 0` by default, allowing `TopePreventivo` to automatically assign the exact domain rule length.
3. Adding missing registrations (`TxtIdentidad`, `TxtContenido`, `TxtEmail`) into their respective `_validador` instances ensures that mandatory checks, format rules, and maximum lengths are evaluated at `LostFocus` and on save, preventing unhandled database constraint exceptions.
4. Truncating the local part of generated emails to at most `38` characters (`50 - "@empresa.com".Length`) guarantees the generated address never exceeds 50 characters while preserving the `@empresa.com` domain intact.

## 3. Caveats
- `RolModal.xaml` (line 35) contains `MaxLength="50"`. It is a standalone Window that does not use `ValidadorFormulario`; its `MaxLength="50"` matches `ReglasRol.Nombre` and should remain as is.
- `PinDigitBox` in `LoginResources.xaml` (line 232) contains `MaxLength="1"` for single digit PIN entry and must remain unchanged.
- `GhostTextBox` in Login is a composite `UserControl` and requires its own `MaxLength` DependencyProperty forwarding to `InnerBox.MaxLength`.

## 4. Conclusion
The survey for UI Validation Layer and Modals is complete. All exact files, line numbers, manual XAML attributes to remove, missing validator registrations, and algorithms for `TopePreventivo` and `GenerarEmail` have been cataloged in `survey_report.md`. The findings are actionable and ready for implementation.

## 5. Verification Method
1. Inspect `survey_report.md` at `D:\\Proyectos\\Proyecto de BIMBO\\BimboProyecto\\.agents\\survey_explorer_2\\survey_report.md`.
2. Verify all line numbers against the codebase using `view_file` or editor.
3. Run `dotnet build BimboProyecto.sln` to confirm baseline solution health.
