# Original User Request

## 2026-09-02T17:49:04Z

Use a full team of agents to implement this project across multiple specialized areas.

Implementación integral de la validación de longitud máxima de campos de texto en la aplicación de escritorio .NET 8 (WPF) de Bimbo Honduras, alineando las reglas de dominio con el esquema real de Supabase (Postgres), propagando topes automáticamente desde `ValidadorFormulario.Segun()`, solucionando los bugs de desfase y concatenación de `GhostTextBox` en el login, y creando tests de deriva de esquema y documentación en la bóveda `contexto/`.

Working directory: D:\Proyectos\Proyecto de BIMBO\BimboProyecto
Integrity mode: development

## Requirements

### R1. Alineación de Reglas de Dominio con el Esquema de Supabase
- Actualizar `CapaDominio/Reglas/ReglasEntidades.cs` para reflejar con precisión los tamaños reales de columna de PostgreSQL (`information_schema.columns`):
  - `ReglasProducto`: `Codigo` (50), `Nombre` (200), `Contenido` (100).
  - `ReglasCategoria`: `Nombre` (100), `Descripcion` (200).
  - `ReglasPresentacion`: `Nombre` (100), `Descripcion` (500 - tope UI).
  - `ReglasFabricante`: `Nombre` (200), `Descripcion` (500 - tope UI).
  - `ReglasProveedor`: `Nombre` (200), `Rtn` (20), `Telefono` (20), `Correo` (100), `Direccion` (500 - tope UI).
  - `ReglasEmpleado`: `Nombre` (100), `Apellido` (100), `Identidad` (20, obligatorio), `Telefono` (20), `Correo` (100).
  - `ReglasUsuario`: `Correo` (50, `FormatoCampo.Correo`), `Password` (min 6, max 72).
  - `ReglasRol`: `Nombre` (50).
  - `ReglasContacto`: `Nombre` (100), `Telefono` (20), `Correo` (100).
  - `ReglasEmpresa`: `Nombre` (200), `Rtn` (20), `Telefono` (20), `Correo` (100), `Direccion` (500 - tope UI).
- Documentar en cada campo la columna de BD correspondiente e incluir el marcador léxico para columnas `text` (topes de UI de 500 caracteres).

### R2. Derivación Automática de MaxLength y Limpieza de XAML
- Modificar `ValidadorFormulario.Segun()` en `CapaUI/Core/Validacion/ValidadorFormulario.cs` para invocar un método `TopePreventivo(m)` que asigne `MaxLength` a controles `TextBox` y `PasswordBox` únicamente si `MaxLength == 0`.
- Eliminar los atributos `MaxLength="100"` manuales en los XAML de modales (`ProveedorModal.xaml`, `FabricanteModal.xaml`, `CategoriaModal.xaml`, `PresentacionModal.xaml`, `EmpleadoModal.xaml`) para evitar solapamientos con las reglas del dominio.
- Incorporar los campos pendientes al validador en sus modales respectivos:
  - `EmpleadoModal.xaml.cs`: `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)`
  - `ProductoModal.xaml.cs`: `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)`
  - `UsuarioModal.xaml.cs`: `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)`
- Modificar `GenerarEmail` en `UsuarioModal.xaml.cs` para recortar la parte local preservando el dominio, de modo que la dirección final nunca exceda los 50 caracteres permitidos por `alias_usuario`.

### R3. Corrección Integral de `GhostTextBox` (Login)
- Implementar la DependencyProperty `MaxLength` en `GhostTextBox` propagándola a `InnerBox`, manteniendo `GhostDisplay.MaxLength = 0`.
- Resolver el desfase visual de scroll suscribiendo `InnerBox` al evento `ScrollViewer.ScrollChangedEvent` y replicando `HorizontalOffset` hacia `GhostDisplay`. Asegurar sincronización en `ShowGhostFor` mediante `Dispatcher.BeginInvoke(SincronizarScroll, DispatcherPriority.Loaded)`.
- Corregir `GetRemainingSuffix` para que devuelva `string.Empty` si el texto ingresado ya contiene una arroba (`@`), evitando concatenaciones de dominios ajenos hacia Supabase Auth.
- Aplicar clamp preventivo en `GetFullText()` y limpieza con `.Dispose()` en los CTS debouncers.

### R4. Topes en Login y Vistas Fuera del Validador
- Asignar en `LoginWindow.xaml.cs` los topes leyendo directamente las reglas del dominio (`TxtEmail.MaxLength = 50`, contraseñas en 72), cubriendo también los paneles de recuperación (`ForgotEmailPanel`, `ForgotNewPanel`).
- Aplicar validación de longitud defensiva en `ConfiguracionEmpresaViewModel` mediante `ReglasFormato.NoExcedeLargo` y `MaxLength` en su XAML.

### R5. Suite de Tests Automatizados de Deriva y Frontera
- Crear `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`:
  - **Test A (Deriva):** Conexión vía `Npgsql` leyendo `BIMBO_POSTGRES_CONNECTION_STRING` (omitiendo si no está presente) contra `information_schema.columns`. Detecta columnas inexistentes, reglas más permisivas que la BD, o columnas `text` no marcadas como tope de UI.
  - **Test B (Valores fijados):** Tests unitarios con `[Theory]` para ejecuciones offline / CI.
  - **Test C (Auditoría por reflexión):** Asegura que toda `ReglaCampo` declarada en `CapaDominio` con `LargoMaximo` esté cubierta en el mapa de auditoría.
- Agregar en `ReglasFormatoTests` pruebas de frontera (`null`, string vacío y longitud exacta).

### R6. Documentación en la Bóveda de Conocimiento
- Registrar addendums en `contexto/45 - Decisiones/ADR-021 - Validacion en tres capas...md` y `ADR-004 - GhostTextBox...md`.
- Actualizar `contexto/20 - Patrones/Validacion de formularios.md` y `Anatomia compartida de los modales.md`.
- Actualizar fichas `P-045` y `P-042` y registrar la nueva ficha de deuda para `ModalInput` en `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`.
- Generar la nota de sesión en `contexto/70 - Bitácora de Cambios/2026-09/Sesión 2026-09-02 - Validación de longitud máxima en campos de texto.md`.

## Acceptance Criteria

### Compilación y Ejecución
- [ ] `dotnet build BimboProyecto.sln` finaliza con 0 errores.
- [ ] `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` finaliza con 100% de pruebas superadas (incluyendo la nueva suite).

### Validación de Datos y Dominio
- [ ] Ninguna regla en `ReglasEntidades.cs` permite una longitud mayor a la columna física en PostgreSQL.
- [ ] Los campos de texto en los modales no permiten tipear más allá del largo especificado en su regla.
- [ ] `GenerarEmail` genera correos de máximo 50 caracteres sin truncar el dominio `@empresa.com`.

### Login y UX
- [ ] En `GhostTextBox`, textos largos (>60 caracteres) mantienen el texto de entrada y el ghost perfectamente alineados en ambos sentidos de scroll.
- [ ] Al ingresar un correo con dominio ajeno (ej. `test@yahoo.com`), no se concatena el sufijo `@gmail.com`.
- [ ] Los campos de contraseña en login y recuperación tienen tope en 72 caracteres.

### Documentación
- [ ] Todos los archivos modificados o creados en `contexto/` cumplen el protocolo de la bóveda (frontmatter YAML estricto, enlaces `[[wikilink]]` y sección `## Relaciones`).
