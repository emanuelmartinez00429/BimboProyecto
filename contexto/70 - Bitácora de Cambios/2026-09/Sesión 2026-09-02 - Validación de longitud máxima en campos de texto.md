---
title: "Sesión 2026-09-02 - Validación de longitud máxima en campos de texto"
tags:
  - sesion
  - validacion
  - dominio
  - wpf
  - supabase
  - ghosttextbox
  - tests
date: 2026-09-02
estado: implementado
---

# Sesión 2026-09-02 - Validación de longitud máxima en campos de texto

> [!success] Resultado
> Se completó la implementación integral y sincronización de topes de longitud máxima en toda la aplicación: 10 entidades de dominio (34 reglas) alineadas con el esquema PostgreSQL de Supabase, derivación automática de `MaxLength` en UI mediante `ValidadorFormulario.Segun()`, limpieza de `MaxLength` redundante en XAML, estabilización completa de `GhostTextBox` (scroll horizontal sincronizado, detección de dominios externos y liberación de CTS), suite de pruebas de deriva y frontera, y documentación exhaustiva en la bóveda de conocimiento.

---

## 1. Contexto y Diagnóstico

Antes de esta intervención, la validación de longitud máxima en la solución presentaba diversas inconsistencias y deficiencias arquitectónicas:

1. **Discrepancia entre Dominio y Esquema Físico (PostgreSQL):** Varias reglas en `CapaDominio/Reglas/ReglasEntidades.cs` estaban desfasadas respecto a `information_schema.columns`. Por ejemplo, `ReglasCategoria.Descripcion` permitía hasta 255 caracteres cuando la columna en Postgres `descripcion_categoria` es `varchar(200)` (riesgo de rechazo por BD), mientras que `ReglasProveedor.Nombre` y `ReglasFabricante.Nombre` tenían topes de 100 en vez de 200.
2. **Truncamientos Artificiales en XAML:** Múltiples modales CRUD declaraban manualmente `MaxLength="100"` en sus archivos `.xaml`, restringiendo innecesariamente entradas de usuario válidas de hasta 200 caracteres permitidas por la base de datos.
3. **Validación Reactiva sin Prevención en Teclado:** `ValidadorFormulario` evaluaba las longitudes en `LostFocus` o al hacer clic en Guardar, pero no fijaba la propiedad `MaxLength` en los controles WPF, permitiendo al usuario ingresar cadenas largas que luego eran rechazadas.
4. **Defectos en `GhostTextBox` (Login):**
   - No exponía una `DependencyProperty` para `MaxLength`, impidiendo fijar límites a la caja de texto interna (`InnerBox`).
   - Al escribir más de 60 caracteres, el `ScrollViewer` interno de `InnerBox` desplazaba el texto pero `GhostDisplay` permanecía en offset 0, causando un desfase visual evidente.
   - Si el usuario escribía un correo de un dominio ajeno (ej. `test@yahoo.com`), `GetRemainingSuffix` concatenaba indebidamente el sufijo de empresa, produciendo `test@yahoo.com@gmail.com`.
   - Los debouncers de autocompletado acumulaban instancias de `CancellationTokenSource` sin llamar a `.Dispose()`.
   - `GetFullText()` no aplicaba recorte defensivo con base en `MaxLength`.
5. **Carencia de Pruebas Automatizadas de Deriva de Esquema:** No existían pruebas que auditaran que las constantes de C# se mantuvieran sincronizadas con los tipos y longitudes de PostgreSQL.

---

## 2. Cambios Implementados

### 2.1 CapaDominio: Alineación de Reglas con Supabase
En `CapaDominio/Reglas/ReglasEntidades.cs` se sincronizaron las 34 reglas de campo para las 10 entidades del sistema:
- **`ReglasProducto`**: `Codigo` (50, `codigo_producto`), `Nombre` (200, `nombre_producto`), `Contenido` (100, `contenido`).
- **`ReglasCategoria`**: `Nombre` (100, `nombre_categoria`), `Descripcion` (200, `descripcion_categoria`).
- **`ReglasPresentacion`**: `Nombre` (100, `nombre_presentacion`), `Descripcion` (500 - tope UI, `descripcion_presentacion` text).
- **`ReglasFabricante`**: `Nombre` (200, `nombre_fabricante`), `Descripcion` (500 - tope UI, `descripcion_fabricante` text).
- **`ReglasProveedor`**: `Nombre` (200, `nombre_proveedor`), `Rtn` (20, `rtn_proveedor`), `Telefono` (20, `telefono_proveedor`), `Correo` (100, `correo_proveedor`), `Direccion` (500 - tope UI, `direccion_proveedor` text).
- **`ReglasEmpleado`**: `Nombre` (100, `nombre_empleado`), `Apellido` (100, `apellido_empleado`), `Identidad` (20, obligatorio, `numero_identidad`), `Telefono` (20, `telefono_empleado`), `Correo` (100, `correo_empleado`).
- **`ReglasUsuario`**: `Correo` (50, `alias_usuario`, `FormatoCampo.Correo`), `Password` (min 6, max 72, restricción de Bcrypt).
- **`ReglasRol`**: `Nombre` (50, `nombre_rol`).
- **`ReglasContacto`**: `Nombre` (100, `nombre_contacto`), `Telefono` (20, `telefono_contacto`), `Correo` (100, `correo_contacto`).
- **`ReglasEmpresa`**: `Nombre` (200, `nombre_empresa`), `Rtn` (20, `rtn_empresa`), `Telefono` (20, `telefono_empresa`), `Correo` (100, `correo_empresa`), `Direccion` (500 - tope UI, `direccion_empresa` text).
- Se documentó cada columna física de PostgreSQL y se incluyó el marcador léxico `// Tope de UI de 500 caracteres (columna text en BD)` en todas las columnas `text`.

### 2.2 CapaUI: Derivación Automática de `MaxLength` y Limpieza de Modales
- **`ValidadorFormulario.cs`**:
  Se introdujo el método `TopePreventivo(int max)` en `ConstructorCampo`, invocado automáticamente desde `.Segun(regla)` y `.LargoMaximo(largo)`. Si el control asociado es un `TextBox` o `PasswordBox` con `MaxLength == 0`, se le asigna el tope de la regla de dominio.
- **Limpieza de XAML**:
  Se eliminaron los atributos redundantes `MaxLength="100"` en `ProveedorModal.xaml`, `FabricanteModal.xaml`, `CategoriaModal.xaml`, `PresentacionModal.xaml` y `EmpleadoModal.xaml`.
- **Registro de Campos Faltantes**:
  - `EmpleadoModal.xaml.cs`: `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)`
  - `ProductoModal.xaml.cs`: `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)`
  - `UsuarioModal.xaml.cs`: `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)`
- **Ajuste en Generación de Correo (`GenerarEmail`)**:
  En `UsuarioModal.xaml.cs`, se limitó la parte local a 38 caracteres (`50 - "@empresa.com".Length`) mediante slicing `local[..38]`, asegurando que el email resultante nunca sobrepase los 50 caracteres del campo `alias_usuario` en Postgres.

### 2.3 CapaUI: Estabilización de `GhostTextBox` y Pantallas de Login / Configuración
- **`GhostTextBox.xaml(.cs)`**:
  1. Se registró la `DependencyProperty` `MaxLengthProperty` propagándola hacia `InnerBox.MaxLength`, conservando `GhostDisplay.MaxLength = 0` para no recortar la sugerencia visual.
  2. Se enrutó el evento `ScrollViewer.ScrollChangedEvent` en `InnerBox` para replicar el `HorizontalOffset` hacia `GhostDisplay`, y se despachó la sincronización en `ShowGhostFor` con `DispatcherPriority.Loaded`.
  3. Se corrigió `GetRemainingSuffix` para retornar `string.Empty` si el texto ingresado contiene un `@` sin solapamiento con el sufijo configurado.
  4. Se añadió recorte preventivo en `GetFullText()`.
  5. Se implementó `CancelGhostDebounce()` con `.Dispose()` explícito sobre `CancellationTokenSource`.
- **`LoginWindow.xaml.cs` y Paneles de Recuperación**:
  - `TxtEmail.MaxLength = 50` (`ReglasUsuario.Correo`).
  - `TxtPassword.MaxLength = 72` y `TxtPasswordVisible.MaxLength = 72` (`ReglasUsuario.Password`).
  - Se configuraron los mismos topes en `ForgotEmailPanel` y `ForgotNewPanel`.
- **`ConfiguracionEmpresa`**:
  - `ConfiguracionEmpresaViewModel.DatosValidos()`: Incorporación de comprobaciones defensivas `ReglasFormato.NoExcedeLargo` para `NombreEmpresa` (200), `RtnEmpresa` (20), `DireccionEmpresa` (500), `TelefonoEmpresa` (20), `CorreoEmpresa` (100) y `DominioCorreo` (100).
  - `ConfiguracionEmpresaModal.xaml`: Asignación de atributos `MaxLength` en todos los `TextBox`.

### 2.4 Suite de Pruebas Automatizadas
- **`BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`**:
  - **Test A (Deriva de Esquema):** Conexión opcional vía `Npgsql` contra `information_schema.columns` en PostgreSQL cuando `BIMBO_POSTGRES_CONNECTION_STRING` está definida. Valida existencia de columnas, que ninguna regla sea más permisiva que la BD y que las columnas `text` estén marcadas como tope UI (500).
  - **Test B (Valores Fijados):** Pruebas unitarias parametrizadas `[Theory]` offline que verifican los 34 valores de dominio en CI.
  - **Test C (Auditoría por Reflexión):** Verifica por reflexión que el 100% de los campos `ReglaCampo` públicos de `CapaDominio.Reglas` estén cubiertos en el mapa de auditoría.
- **`BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs`**:
  - Pruebas exhaustivas de casos frontera para `NoExcedeLargo` y `TieneLargoMinimo` (nulos, strings vacíos, espacios en blanco, longitudes límite exactas y desbordes).

### 2.5 Actualización de la Bóveda de Conocimiento
- **`ADR-021`**: Addendum 2026-09-02 documentando `TopePreventivo(m)`, derivación automática en UI, alineación de esquema y truncamiento en `GenerarEmail`.
- **`ADR-004`**: Addendum 2026-09-02 documentando `MaxLength` DP, sincronización de `ScrollViewer`, supresión de dominios foráneos y desecho seguro de debouncers.
- **`Validacion de formularios.md`**: Actualización del patrón de validación y documentación de `TopePreventivo`.
- **`Anatomia compartida de los modales.md`**: Eliminación de `MaxLength="100"` manual en XAML y centralización en `ValidadorFormulario`.
- **`Deuda Técnica - Pendientes.md`**:
  - Actualización de notas de estado en `P-042` y `P-045` (aislamiento restante de Pesaje).
  - Registro de nueva ficha `P-047` para documentar la divergencia entre `ModalInput` e `InputBox`.

### 2.5 Migración en Base de Datos Supabase (PostgreSQL)
- **Archivo de migración:** `supabase/migrations/20260902184000_limitar_columnas_texto_a_varchar_500.sql`
- **Operaciones DDL ejecutadas:**
  - `ALTER TABLE public.fabricante ALTER COLUMN descripcion_fabricante TYPE character varying(500);`
  - `ALTER TABLE public.presentacion_producto ALTER COLUMN descripcion_presentacion TYPE character varying(500);`
  - `ALTER TABLE public.proveedores ALTER COLUMN direccion_proveedor TYPE character varying(500);`
  - `ALTER TABLE public.empresa ALTER COLUMN direccion_empresa TYPE character varying(500);`
- **Verificación previa:** Se validó que ninguna fila preexistente en el sistema excediera los 500 caracteres (0 registros violando el límite).
- **Estado:** Migración aplicada y verificada contra `information_schema.columns` en Supabase PostgreSQL. Ahora las columnas son físicamente `character varying(500)` garantizando paridad física estricta entre la base de datos, el dominio (`ReglasEntidades.cs`) y la UI.

### 2.6 Saneamiento integral de advertencias de compilación (CS8618 y CS8603 en CapaDatos)
- **Contexto:** Históricamente, el proyecto arrastraba advertencias de nullabilidad en `CapaDatos` documentadas en `AGENTS.md` ("hay warnings preexistentes de nullable en CapaDatos, no bloquean").
- **Problemas corregidos:**
  1. **CS8618 (Propiedades no nulas sin inicializar en constructor):**
     - Se inicializaron con `= string.Empty;` en `Categoria.cs`, `Fabricante.cs`, `Paises.cs`, `Presentacion.cs`, `ProductosInsertar.cs`, `Empleados.cs`, `Usuarios.cs`.
     - En `Productos.cs` y `usuarioVista.cs` se tiparon las relaciones de navegación opcional como `Presentacion?`, `Fabricante?`, `Categoria?`, `Paises?`, `Roles?` y `Empleados?`, coincidiendo con su consumo seguro en UI mediante `?.`.
  2. **CS8603 (Posible retorno o asignación nula):**
     - En `RepositorioUsuario.ObtenerPorUuidAsync` se actualizó la firma a `Task<Usuarios?>`, reflejando que si el usuario no existe retorna `null` (consumido con `if (usuario == null)` en `AuthService`).
     - En `RepositorioCategoria.InsertarCategoria` y `RepositorioProducto.ingresarProducto` se implementó fallback defensivo (`response.Model ?? categoria`, `response.Model ?? datos`).
     - En llamadas `.Set()` de `PresentacionCrudRepository`, `ContactoProveedorCrudRepository`, `ContactoFabricanteCrudRepository` y `PesajeRepository` se pacificaron las expresiones de propiedades anulables usando el operador de supresión `!`.
- **Actualización de directriz:** Se actualizó `AGENTS.md` para exigir **0 errores y 0 advertencias**.

---

## 3. Verificación

```powershell
# Compilación completa limpia de la solución (sin incremental)
dotnet build BimboProyecto.sln --no-incremental
# Resultado: 0 advertencias, 0 errores (en todos los proyectos)

# Ejecución de la suite completa de pruebas unitarias
dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
# Resultado: 223 superadas, 0 con error (100% pass rate)
```

---

## 4. Endurecimiento de RTN y teléfono

- `ReglasFormato` ahora rechaza letras, puntos, paréntesis, símbolos y dígitos Unicode en RTN y teléfono; conserva únicamente separadores documentados.
- La validación de proveedor también quedó protegida por restricciones `NOT VALID` en Supabase, sin modificar datos históricos.
- Se añadieron pruebas de regresión para valores que antes podían pasar tras eliminar letras antes de contar dígitos.
- Verificación: compilación con 0 errores y 0 advertencias; 223 pruebas superadas; inserciones remotas inválidas rechazadas y revertidas por bloques de excepción.

## 5. Relaciones

- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]
- [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]]
- [[Validacion de formularios]]
- [[Anatomia compartida de los modales]]
- [[Deuda Técnica - Pendientes]]
- [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]]
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]
- [[Sesión 2026-08-21 - QA Login y migración de validaciones al dominio]]
