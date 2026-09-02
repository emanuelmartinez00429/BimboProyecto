# Reporte de Investigación: Capa de Validación de UI y Modales (survey_explorer_2)

**Fecha**: 2026-09-02  
**Explorador**: `survey_explorer_2` (teamwork_preview_explorer)  
**Alcance**: Capa de Validación de UI (`ValidadorFormulario.cs`), Modales XAML y Code-Behind, eliminación de atributos manuales `MaxLength`, registros faltantes en `ValidadorFormulario`, y truncamiento inteligente en `GenerarEmail`.

---

## 1. Resumen Ejecutivo

Esta investigación analiza exhaustivamente el estado actual de la validación en la capa de interfaz de usuario (.NET 8 WPF) de Bimbo Honduras. Se identificaron:
1. **Mecanismo de derivación de topes preventivos (`TopePreventivo`)** en `ValidadorFormulario.cs`: cómo asociar dinámicamente `MaxLength` a controles `TextBox` y `PasswordBox` a partir de `ReglaCampo.LargoMaximo` respetando `MaxLength == 0`.
2. **Atributos manuales `MaxLength="100"` en XAML** que entran en conflicto con los tamaños de columnas reales de PostgreSQL (como `varchar(200)` en Proveedor y Fabricante).
3. **Omisiones de validación en modales clave**: campos críticos (`TxtIdentidad` en `EmpleadoModal`, `TxtContenido` en `ProductoModal`, `TxtEmail` en `UsuarioModal`) que no estaban registrados en `ValidadorFormulario`.
4. **Desbordamiento potencial en `GenerarEmail`**: la generación automática de correo institucional no acotaba la parte local, arriesgando exceder el límite de 50 caracteres de `alias_usuario` en Postgres.
5. **Brechas defensivas en vistas fuera del validador**: `ConfiguracionEmpresaViewModel`, `LoginWindow`, `ForgotEmailPanel` y `ForgotNewPanel`.

---

## 2. Análisis Detallado de `ValidadorFormulario.cs`

### 2.1 Ubicación y Estado Actual
- **Archivo**: `CapaUI/Core/Validacion/ValidadorFormulario.cs`
- **Líneas 318-334**:
```csharp
public ConstructorCampo Segun(ReglaCampo regla)
{
    if (regla.Obligatorio)          Obligatorio();
    if (regla.LargoMaximo is int m) LargoMaximo(m);
    if (regla.LargoMinimo is int n) LargoMinimo(n);

    switch (regla.Formato)
    {
        case FormatoCampo.Correo:   Correo();   break;
        case FormatoCampo.Rtn:      Rtn();      break;
        case FormatoCampo.Telefono: Telefono(); break;
        case FormatoCampo.Decimal:  Decimal();  break;
        case FormatoCampo.Entero:   Entero();   break;
    }

    return this;
}
```
- **Líneas 282-286**:
```csharp
public ConstructorCampo LargoMaximo(int largo, string? mensaje = null)
{
    _campo.Reglas.Add((c => ReglasFormato.NoExcedeLargo(c.LeerTexto(), largo),
        mensaje ?? $"{_campo.Etiqueta} no puede superar los {largo} caracteres."));
    return this;
}
```

### 2.2 Diagnóstico y Diseño de `TopePreventivo`
- Actualmente, `ValidadorFormulario` evalúa la longitud únicamente de forma reactiva (en `LostFocus` o al invocar `Validar()`), sin impedir que el usuario tipee caracteres excedentes en el control WPF nativo.
- En WPF, la propiedad `MaxLength` por defecto en `TextBox` y `PasswordBox` es `0` (indicando sin límite).
- **Implementación recomendada**:
  Agregar en `ValidadorFormulario.ConstructorCampo`:
  ```csharp
  private void TopePreventivo(int max)
  {
      if (_campo.Control is TextBox tb && tb.MaxLength == 0)
          tb.MaxLength = max;
      else if (_campo.Control is PasswordBox pb && pb.MaxLength == 0)
          pb.MaxLength = max;
  }
  ```
  E invocar `TopePreventivo(m)` en `Segun(ReglaCampo regla)` cuando `regla.LargoMaximo is int m` (así como en el método directo `LargoMaximo(int largo, ...)`).
- **Comportamiento ante controles personalizados**: Para `GhostTextBox` (que es un `UserControl`), la propiedad `MaxLength` debe definirse como `DependencyProperty` en el control y propagarse a `InnerBox.MaxLength` (manteniendo `GhostDisplay.MaxLength = 0`).

---

## 3. Auditoría de Modales XAML y Remoción de `MaxLength` Manual

Al centralizar el tope en `Segun(ReglasXxx.Campo)` a través de `TopePreventivo`, los atributos `MaxLength` manuales en XAML no solo son redundantes, sino que provocan errores de negocio al restringir artificialmente a 100 caracteres columnas que en PostgreSQL soportan 200 o más.

### 3.1 Hallazgos Espécíficos por Archivo

| Archivo Modal | Control XAML | Línea | Atributo Actual | Regla de Dominio | Largo BD Postgres | Acción Requerida |
|---|---|---|---|---|---|---|
| `ProveedorModal.xaml` | `TxtNombre` | 82 | `MaxLength="100"` | `ReglasProveedor.Nombre` | `varchar(200)` | **Eliminar `MaxLength="100"`** |
| `FabricanteModal.xaml` | `TxtNombre` | 80 | `MaxLength="100"` | `ReglasFabricante.Nombre` | `varchar(200)` | **Eliminar `MaxLength="100"`** |
| `CategoriaModal.xaml` | `TxtNombre` | 80 | `MaxLength="100"` | `ReglasCategoria.Nombre` | `varchar(100)` | **Eliminar `MaxLength="100"`** |
| `PresentacionModal.xaml` | `TxtNombre` | 88 | `MaxLength="100"` | `ReglasPresentacion.Nombre` | `varchar(100)` | **Eliminar `MaxLength="100"`** |
| `EmpleadoModal.xaml` | `TxtNombre` | 81 | `MaxLength="100"` | `ReglasEmpleado.Nombre` | `varchar(100)` | **Eliminar `MaxLength="100"`** |
| `EmpleadoModal.xaml` | `TxtApellido` | 85 | `MaxLength="100"` | `ReglasEmpleado.Apellido` | `varchar(100)` | **Eliminar `MaxLength="100"`** |

### 3.2 Casos Especiales Analizados
- `RolModal.xaml` (línea 35): `TxtNombre` tiene `MaxLength="50"`. `RolModal` es una `Window` liviana que no utiliza `ValidadorFormulario` sino `RolesViewModel`; su `MaxLength="50"` coincide exactamente con `ReglasRol.Nombre` (50) y `roles.nombre_rol` (`varchar(50)`).
- `LoginResources.xaml` (línea 232): `PinDigitBox` tiene `MaxLength="1"`. Corresponde a los casilleros individuales de dígitos PIN, por lo que debe mantenerse intacto.
- `ContactoFabricanteModal.xaml` y `ContactoProveedorModal.xaml`: No contienen `MaxLength` en XAML; usan `ValidadorFormulario` en su code-behind con `ReglasContacto`.

---

## 4. Auditoría de Registros Faltantes en `ValidadorFormulario`

Se detectaron tres omisiones críticas en el code-behind de los modales donde los campos no contaban con validación en `ValidadorFormulario`:

### 4.1 `EmpleadoModal.xaml.cs` — `TxtIdentidad`
- **Ubicación**: `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadoModal.xaml.cs`, líneas 37-42.
- **Código Actual**:
  ```csharp
  _validador = ValidadorFormulario.Nuevo()
      .Campo(TxtNombre, "El nombre").Segun(ReglasEmpleado.Nombre)
      .Campo(TxtApellido, "El apellido").Segun(ReglasEmpleado.Apellido)
      .Campo(TxtTelefono, "El teléfono").Segun(ReglasEmpleado.Telefono)
      .Campo(TxtCorreo, "El correo").Segun(ReglasEmpleado.Correo)
      .ValidarAlSalirDelCampo();
  ```
- **Problema**: `TxtIdentidad` no está en el validador. El número de identidad es obligatorio en la base de datos (`empleados.numero_identidad` `varchar(20) NOT NULL`), pero si el usuario lo dejaba vacío o excedía el tamaño, el error se disparaba recién en la llamada a Postgres.
- **Corrección Requerida**:
  ```csharp
  _validador = ValidadorFormulario.Nuevo()
      .Campo(TxtNombre, "El nombre").Segun(ReglasEmpleado.Nombre)
      .Campo(TxtApellido, "El apellido").Segun(ReglasEmpleado.Apellido)
      .Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)
      .Campo(TxtTelefono, "El teléfono").Segun(ReglasEmpleado.Telefono)
      .Campo(TxtCorreo, "El correo").Segun(ReglasEmpleado.Correo)
      .ValidarAlSalirDelCampo();
  ```

### 4.2 `ProductoModal.xaml.cs` — `TxtContenido`
- **Ubicación**: `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs`, líneas 86-92.
- **Código Actual**:
  ```csharp
  _validador = ValidadorFormulario.Nuevo()
      .Campo(TxtCodigo, "El código").Segun(ReglasProducto.Codigo)
      .Campo(TxtNombre, "El nombre").Segun(ReglasProducto.Nombre)
      .Campo(TxtPesoTeorico, "El peso teórico").Segun(ReglasProducto.PesoTeorico)
      .Campo(TxtPrecioPorKg, "El precio por kg").Segun(ReglasProducto.PrecioPorKg)
      .ValidarAlSalirDelCampo();
  ```
- **Problema**: `TxtContenido` no está registrado en `_validador`. Aunque `contenido` en Postgres es `varchar(100)`, no se validaba su longitud máxima.
- **Corrección Requerida**:
  ```csharp
  _validador = ValidadorFormulario.Nuevo()
      .Campo(TxtCodigo, "El código").Segun(ReglasProducto.Codigo)
      .Campo(TxtNombre, "El nombre").Segun(ReglasProducto.Nombre)
      .Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)
      .Campo(TxtPesoTeorico, "El peso teórico").Segun(ReglasProducto.PesoTeorico)
      .Campo(TxtPrecioPorKg, "El precio por kg").Segun(ReglasProducto.PrecioPorKg)
      .ValidarAlSalirDelCampo();
  ```

### 4.3 `UsuarioModal.xaml.cs` — `TxtEmail`
- **Ubicación**: `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs`, líneas 68-74.
- **Código Actual**:
  ```csharp
  _validador = ValidadorFormulario.Nuevo()
      .Combo(CmbEmpleado, "El empleado").Segun(ReglasUsuario.Empleado)
          .SoloSi(() => _esNuevo && !_esCreacionConEmpleado)
      .Clave(TxtPassword, "La contraseña").Segun(ReglasUsuario.Password)
          .SoloSi(() => _esNuevo)
      .Combo(CmbRolModal, "El rol").Segun(ReglasUsuario.Rol)
      .ValidarAlSalirDelCampo();
  ```
- **Problema**: `TxtEmail` no está registrado en el validador. En modo edición o creación, el correo debe cumplir formato de correo y tope de 50 caracteres (`ReglasUsuario.Correo`).
- **Corrección Requerida**:
  ```csharp
  _validador = ValidadorFormulario.Nuevo()
      .Combo(CmbEmpleado, "El empleado").Segun(ReglasUsuario.Empleado)
          .SoloSi(() => _esNuevo && !_esCreacionConEmpleado)
      .Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)
      .Clave(TxtPassword, "La contraseña").Segun(ReglasUsuario.Password)
          .SoloSi(() => _esNuevo)
      .Combo(CmbRolModal, "El rol").Segun(ReglasUsuario.Rol)
      .ValidarAlSalirDelCampo();
  ```

---

## 5. Análisis de `GenerarEmail` en `UsuarioModal.xaml.cs`

### 5.1 Implementación Actual
- **Ubicación**: `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs`, líneas 188-197:
```csharp
private static string GenerarEmail(string nombreCompleto)
{
    var partes = nombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (partes.Length < 2)
        return TextoBusqueda.Normalizar(nombreCompleto).Trim() + "@empresa.com";

    var nombre   = TextoBusqueda.Normalizar(partes[0]).Trim();
    var apellido = TextoBusqueda.Normalizar(partes[^1]).Trim();
    return $"{nombre}.{apellido}@empresa.com";
}
```

### 5.2 Análisis de Riesgo
- La columna `usuarios.alias_usuario` en Postgres tiene tipo `character varying(50)` (y `ReglasUsuario.Correo` fija `LargoMaximo: 50`).
- El sufijo de dominio institucional `@empresa.com` tiene **12 caracteres**.
- Por lo tanto, la parte local (`nombre.apellido` o `nombre`) puede tener a lo sumo `50 - 12 = 38` caracteres.
- Si un empleado tiene nombres compuestos o apellidos largos, la concatenación podría exceder los 38 caracteres. Si se exceden los 50 caracteres en total, la inserción en Supabase/Postgres falla con error de truncamiento de cadena (`22001`).

### 5.3 Algoritmo de Truncamiento Propuesto
El truncamiento debe recortar **únicamente la parte local**, garantizando que el dominio `@empresa.com` quede intacto y que la longitud total nunca sobrepase 50 caracteres:

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

## 6. Revisión de Vistas Fuera del Validador

### 6.1 `ConfiguracionEmpresaViewModel.cs` y `ConfiguracionEmpresaModal.xaml`
- `ConfiguracionEmpresaModal` es un formulario MVVM donde no se usa `ValidadorFormulario` directamente sobre controles.
- **En `ConfiguracionEmpresaViewModel.cs` (líneas 198-224)**:
  `DatosValidos()` valida `TieneContenido`, `EsRtn`, `EsTelefono` y `EsCorreo`, pero omite validar `ReglasFormato.NoExcedeLargo`.
  - Debe incorporar defensivamente:
    - `NombreEmpresa`: `ReglasFormato.NoExcedeLargo(NombreEmpresa, 200)`
    - `RtnEmpresa`: `ReglasFormato.NoExcedeLargo(RtnEmpresa, 20)`
    - `DireccionEmpresa`: `ReglasFormato.NoExcedeLargo(DireccionEmpresa, 500)`
    - `TelefonoEmpresa`: `ReglasFormato.NoExcedeLargo(TelefonoEmpresa, 20)`
    - `CorreoEmpresa`: `ReglasFormato.NoExcedeLargo(CorreoEmpresa, 100)`
    - `DominioCorreo`: `ReglasFormato.NoExcedeLargo(DominioCorreo, 100)`
- **En `ConfiguracionEmpresaModal.xaml`**:
  Agregar atributos `MaxLength` preventivos en los `TextBox`:
  - `NombreEmpresa`: `MaxLength="200"`
  - `RtnEmpresa`: `MaxLength="20"`
  - `DireccionEmpresa`: `MaxLength="500"`
  - `TelefonoEmpresa`: `MaxLength="20"`
  - `CorreoEmpresa`: `MaxLength="100"`
  - `DominioCorreo`: `MaxLength="100"`
  - `ColorEmpresa`: `MaxLength="7"`

### 6.2 `LoginWindow.xaml.cs` y Paneles de Recuperación
- En `LoginWindow.xaml.cs`:
  - Asignar `TxtEmail.MaxLength = 50` (o `ReglasUsuario.Correo.LargoMaximo!.Value`).
  - Asignar `TxtPassword.MaxLength = 72` y `TxtPasswordVisible.MaxLength = 72` (límite bcrypt / Supabase Auth).
- En `ForgotEmailPanel.xaml.cs`: `TxtEmail.MaxLength = 50`.
- En `ForgotNewPanel.xaml.cs`: `TxtNew.MaxLength = 72`, `TxtNewVisible.MaxLength = 72`, `TxtConfirm.MaxLength = 72`, `TxtConfirmVisible.MaxLength = 72`.

---

## 7. Matriz Resumen de Archivos a Modificar en la Capa UI

| Componente | Archivo | Líneas Relevantes | Modificación Requerida |
|---|---|---|---|
| Validador Core | `CapaUI/Core/Validacion/ValidadorFormulario.cs` | 282-286, 318-334 | Implementar `TopePreventivo(m)` en `ConstructorCampo` para `TextBox` y `PasswordBox` si `MaxLength == 0` |
| Proveedores | `CapaUI/.../Proveedores/ProveedorModal.xaml` | 82 | Quitar `MaxLength="100"` de `TxtNombre` |
| Fabricantes | `CapaUI/.../Fabricantes/FabricanteModal.xaml` | 80 | Quitar `MaxLength="100"` de `TxtNombre` |
| Categorías | `CapaUI/.../Categorias/CategoriaModal.xaml` | 80 | Quitar `MaxLength="100"` de `TxtNombre` |
| Presentaciones | `CapaUI/.../Presentaciones/PresentacionModal.xaml` | 88 | Quitar `MaxLength="100"` de `TxtNombre` |
| Empleados | `CapaUI/.../Empleados/EmpleadoModal.xaml` | 81, 85 | Quitar `MaxLength="100"` de `TxtNombre` y `TxtApellido` |
| Empleados Code-Behind | `CapaUI/.../Empleados/EmpleadoModal.xaml.cs` | 37-42 | Agregar `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)` |
| Productos Code-Behind | `CapaUI/.../Productos/ProductoModal.xaml.cs` | 86-92 | Agregar `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)` |
| Usuarios Code-Behind | `CapaUI/.../Usuarios/UsuarioModal.xaml.cs` | 68-74 | Agregar `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)` |
| Usuarios Truncamiento | `CapaUI/.../Usuarios/UsuarioModal.xaml.cs` | 188-197 | Truncar local part en `GenerarEmail` a 38 chars (`50 - dominio.Length`) |
| Configuración Empresa | `CapaUI/.../Configuracion/ConfiguracionEmpresaModal.xaml` | 74-97 | Agregar `MaxLength` defensivos en XAML |
| Configuración ViewModel | `CapaUI/.../Configuracion/ConfiguracionEmpresaViewModel.cs` | 198-224 | Agregar validaciones `NoExcedeLargo` en `DatosValidos()` |
| Login & Recuperación | `CapaUI/Formularios/InicioSesion/*.xaml.cs` | Varios | Asignar topes en 50 para email y 72 para passwords |

