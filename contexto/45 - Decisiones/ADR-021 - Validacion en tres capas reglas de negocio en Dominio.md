---
title: "ADR-021 — Validación en tres capas: reglas de negocio en Dominio, validador en UI"
tags:
  - adr
  - validacion
  - wpf
  - arquitectura
date: 2026-08-15
estado: aceptado
---

# ADR-021 — Validación en tres capas: reglas de negocio en Dominio, validador en UI

## Contexto

No existía ninguna clase de validación en toda la solución. Cada `BtnGuardar_Click` traía su propio bloque inline, y el resultado era el esperable:

- `"El nombre es obligatorio."` aparecía **idéntico en 6 modales**, pero solo `PresentacionModal` devolvía el foco al campo.
- `EmpleadoModal` titulaba el aviso `"Validacion"` **sin tilde** — único caso del proyecto.
- **Correo, RTN y teléfono no se validaban en ningún lado**, aunque los DTOs los persistieran. La única regex de correo del proyecto estaba privada y encerrada en el panel de recuperación de contraseña.
- Convivían **cinco mecanismos** distintos de mostrar el error: `MessageBox`, `TxtError`, `TxtAvisoError`, una propiedad del ViewModel, y "deshabilitar el botón sin decir nada".
- Solo `PresentacionModal` traducía el `23505` de constraint única; los otros ocho volcaban el texto crudo de PostgREST.

Existía además un pendiente registrado el 2026-06-10 ([[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]]) que proponía un diseño concreto y pedía explícitamente plantearlo antes de implementar.

## Decisión

Validación en **tres capas**, cada una donde le corresponde:

**Capa 1 — `CapaDominio/Reglas/`**, las reglas de negocio:
- `ReglasFormato` — predicados (`EsCorreo`, `EsRtn`, `EsTelefono`, largos). Que un RTN hondureño tenga 14 dígitos es ley tributaria, no una decisión de pantalla. Mismo criterio que `PesoCalculator`, que ya estaba ahí.
- `ReglaCampo` + `FormatoCampo` — un **dato** que describe la exigencia (obligatorio, largo, formato) sin ejecutarla.
- `ReglasProducto`, `ReglasProveedor`, `ReglasEmpleado`… — **qué campos son obligatorios y con qué largo, por entidad**. Eso también es conocimiento de negocio y del esquema, y antes estaba escrito dentro de cada modal.

**Capa 2 — `CapaUI/Core/Validacion/ParseoNumerico`**: convierte a número lo que tipeó una persona. **No es regla de negocio**: depende del `CultureInfo` del usuario (si escribe "1,5" o "1.5"), y el dominio no tiene por qué saber eso. El dominio declara que un campo es `FormatoCampo.Decimal`; traducir el texto es de esta capa.

**Capa 3 — `CapaUI/Core/Validacion/ValidadorFormulario`**: API fluida que declara las reglas de un formulario **una sola vez** y las evalúa en dos momentos —al salir de cada campo y al guardar—, encargándose del borde rojo, el renglón de error, el foco y el mensaje.

```csharp
_validador = ValidadorFormulario.Nuevo()
    .Campo(TxtNombre, "El nombre").Segun(ReglasProveedor.Nombre)
    .Campo(TxtCorreo, "El correo").Segun(ReglasProveedor.Correo)
    .Combo(CmbRol,    "El rol").Segun(ReglasUsuario.Rol)
    .ValidarAlSalirDelCampo();

if (!_validador.Validar()) return;   // marca todo, enfoca el primero, avisa
```

`Segun(...)` es la forma preferida: el **qué** sale del dominio y la UI solo lo aplica. Los métodos sueltos (`Obligatorio()`, `Correo()`…) siguen existiendo para lo que no tiene una regla de negocio detrás.

La separación no es ceremonia: `ConfiguracionEmpresaViewModel` es MVVM puro y **no tiene controles** que pasarle al validador, así que consume `ReglasFormato` del dominio directamente. Si la regla trajera el `MessageBox` adentro, o viviera en la capa de UI, esa capa serviría en un solo lugar.

## Por qué el objeto se retiene y no se arma dentro de `BtnGuardar_Click`

Porque la validación corre en **dos momentos**. Si las reglas se declararan dentro del handler de guardado, habría que repetirlas para el `LostFocus`. Declararlas una vez en `OnLoaded` y guardarlas en un campo es lo que hace que el "builder" tenga sentido acá — construye un validador reutilizable, no una cadena de un solo uso.

## Alternativas descartadas

**`INotifyDataErrorInfo` / validación por binding de WPF.** Es lo idiomático en MVVM y da adornos de error gratis. Pero estos modales **no bindean**: leen `TxtNombre.Text` directo del control en el code-behind. Adoptarlo obligaba a reescribir el flujo de datos completo de los 10 modales. Es la respuesta correcta para un proyecto que ya bindea; no para este.

**DataAnnotations sobre los DTOs.** Pone la regla junto al dato y serviría del lado servidor, pero los modales construyen el DTO **después** de validar, y mapear "propiedad que falló" → "control al que darle foco" queda artificial.

**FluentValidation (librería).** Haría lo mismo que la capa 2 sumando una dependencia, y sin resolver el marcado visual, que es la mitad del trabajo.

**El diseño propuesto en 2026-06** (`IValidacionService` + una clase por regla + `ValidacionResult { bool, IReadOnlyList<string> }`). Se descartó por dos motivos concretos:

1. **Una clase por regla** (`ReglaRequerido.cs`, `ReglaLongitudMaxima.cs`…) paga cuando las reglas se componen o se inyectan en runtime. Acá el conjunto es fijo y conocido; métodos estáticos más la cadena fluida dan la misma composición con mucho menos código que mantener.
2. **`ValidacionResult` con una lista de strings pierde el vínculo con el control.** Sin saber *qué campo* falló no se puede pintar el borde rojo ni mover el foco, que es justamente lo que el usuario pidió. El validador guarda la referencia al control, no solo el mensaje.

## Consecuencias

**A favor**
- Un solo lugar para cambiar un mensaje, un formato o el aspecto del error.
- Aparecen validaciones de correo/RTN/teléfono que antes no existían.
- El bug de `ProductoModal` —parsear los decimales *dentro* del `try`, después de mostrar "Guardando…"— desaparece solo: ahora la validación corre antes.
- Las reglas son testeables sin WPF y sin referenciar nada, que es la precondición de las pruebas de caja negra que pedía el pendiente de 2026-06.

**En contra / a vigilar**
- El renglón de error se inserta **por código** en el `StackPanel` `CampoModal`, apoyándose en que esa estructura es uniforme (ver [[Anatomia compartida de los modales]]). Un modal que no la respete pierde el renglón y se queda con el borde rojo y el ToolTip. Es degradación, no rotura, pero es un acoplamiento a tener presente.
- Las reglas están en `CapaDominio`, que no referencia a nadie, así que **`CapaDatos` también puede usarlas** si en algún momento se quiere rechazar antes de viajar a la red. Hoy no lo hace.
- Pesaje queda afuera: valida por pasos y deshabilitando botones, y parsea con `InvariantCulture` mientras los modales CRUD usan `CurrentCulture`. Esa divergencia es preexistente y no se tocó para no alterar su cálculo de pesos de refilón.

## Lo que este ADR NO cubre

El pendiente de 2026-06 listaba también reglas de negocio que siguen sin implementar y **no corresponden a esta capa**: "no operar sobre registros inactivos" y "FK válida antes de guardar". Son invariantes de dominio, no de formulario.

Tampoco existe todavía proyecto de tests; las reglas puras habilitan las pruebas de caja negra, pero nadie las escribió aún.

## Addendum 2026-09-02 — Propagación automática de topes preventivos y alineación con esquema Supabase

### 1. Tope Preventivo Automático en UI (`TopePreventivo(m)`)
Se modificó `ValidadorFormulario.Segun()` y `ValidadorFormulario.LargoMaximo()` en `CapaUI/Core/Validacion/ValidadorFormulario.cs` para invocar el método interno `TopePreventivo(m)` en `ConstructorCampo`:
- Si `_campo.Control is TextBox tb && tb.MaxLength == 0`, se asigna automáticamente `tb.MaxLength = max`.
- Si `_campo.Control is PasswordBox pb && pb.MaxLength == 0`, se asigna automáticamente `pb.MaxLength = max`.
- Si `MaxLength` ya fue establecido manualmente (`!= 0`), se respeta la configuración previa.

Esto convierte la validación de longitud máxima en una **medida preventiva activa**: el usuario no puede tipear ni pegar más caracteres de los permitidos por las reglas del dominio en ningún control registrado en el validador, evitando la necesidad de esperar al evento `LostFocus` o al clic de guardado para notar el error.

### 2. Eliminación de `MaxLength` redundante en XAML de Modales
Se removió el atributo manual `MaxLength="100"` en los XAML de modales CRUD (`ProveedorModal.xaml`, `FabricanteModal.xaml`, `CategoriaModal.xaml`, `PresentacionModal.xaml`, `EmpleadoModal.xaml`).
- **Problema previo:** El valor estático `100` en XAML causaba truncamientos artificiales en entidades cuyas columnas en PostgreSQL soportan hasta 200 caracteres (ej. `nombre_proveedor` y `nombre_fabricante`), impidiendo el ingreso de nombres válidos de proveedores y fabricantes largos.
- **Beneficio:** La longitud máxima ahora se deriva limpiamente en tiempo de ejecución desde `CapaDominio/Reglas/ReglasEntidades.cs` mediante `.Segun(ReglasXxx.Campo)`, manteniendo una única fuente de verdad.

### 3. Alineación de Reglas de Dominio con PostgreSQL (`information_schema.columns`)
Se actualizó `CapaDominio/Reglas/ReglasEntidades.cs` para sincronizar 34 reglas de campo en 10 entidades con el esquema físico real de Supabase:
- **`ReglasProducto`**: `Codigo` (50, `codigo_producto`), `Nombre` (200, `nombre_producto`), `Contenido` (100, `contenido`).
- **`ReglasCategoria`**: `Nombre` (100, `nombre_categoria`), `Descripcion` (200, `descripcion_categoria`).
- **`ReglasPresentacion`**: `Nombre` (100, `nombre_presentacion`), `Descripcion` (500 - tope UI, `descripcion_presentacion` text).
- **`ReglasFabricante`**: `Nombre` (200, `nombre_fabricante`), `Descripcion` (500 - tope UI, `descripcion_fabricante` text).
- **`ReglasProveedor`**: `Nombre` (200, `nombre_proveedor`), `Rtn` (20, `rtn_proveedor`), `Telefono` (20, `telefono_proveedor`), `Correo` (100, `correo_proveedor`), `Direccion` (500 - tope UI, `direccion_proveedor` text).
- **`ReglasEmpleado`**: `Nombre` (100, `nombre_empleado`), `Apellido` (100, `apellido_empleado`), `Identidad` (20, obligatorio, `numero_identidad`), `Telefono` (20, `telefono_empleado`), `Correo` (100, `correo_empleado`).
- **`ReglasUsuario`**: `Correo` (50, `alias_usuario`, `FormatoCampo.Correo`), `Password` (min 6, max 72, restricción Bcrypt).
- **`ReglasRol`**: `Nombre` (50, `nombre_rol`).
- **`ReglasContacto`**: `Nombre` (100, `nombre_contacto`), `Telefono` (20, `telefono_contacto`), `Correo` (100, `correo_contacto`).
- **`ReglasEmpresa`**: `Nombre` (200, `nombre_empresa`), `Rtn` (20, `rtn_empresa`), `Telefono` (20, `telefono_empresa`), `Correo` (100, `correo_empresa`), `Direccion` (500 - tope UI, `direccion_empresa` text).
- **Marcador de columnas `text`**: Todas las columnas tipo `text` sin límite nativo en BD se documentaron con el marcador léxico `// Tope de UI de 500 caracteres (columna text en BD)`.

### 4. Cierre de Brechas de Validación en Modales
Se incorporaron los campos pendientes al validador en sus respectivos code-behinds:
- `EmpleadoModal.xaml.cs`: `.Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)`
- `ProductoModal.xaml.cs`: `.Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)`
- `UsuarioModal.xaml.cs`: `.Campo(TxtEmail, "El correo").Segun(ReglasUsuario.Correo)`

### 5. Truncamiento Seguro en Generación Automática de Correo (`GenerarEmail`)
En `UsuarioModal.xaml.cs`, el método `GenerarEmail` fue ajustado para calcular la longitud disponible de la parte local (`50 - "@empresa.com".Length = 38`). Si la combinación `nombre.apellido` excede 38 caracteres, se recorta mediante slicing (`local[..38]`), garantizando que la dirección final nunca sobrepase los 50 caracteres del campo `alias_usuario` en PostgreSQL mientras preserva el sufijo del dominio completo.

### 6. Suite Automatizada de Deriva de Esquema y Frontera
Se creó el proyecto de pruebas `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`:
- **Test de Deriva de Esquema:** Compara dinámicamente las reglas de `CapaDominio` contra `information_schema.columns` en PostgreSQL cuando `BIMBO_POSTGRES_CONNECTION_STRING` está presente.
- **Tests Offline / CI:** Pruebas unitarias parametrizadas `[Theory]` para ejecución sin base de datos.
- **Auditoría por Reflexión:** Asegura que toda `ReglaCampo` pública declarada en `CapaDominio` esté registrada y verificada en el mapa de auditoría.

---

## Relaciones

- [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]] — el pendiente que este ADR resuelve parcialmente
- [[Anatomia compartida de los modales]] — la estructura `CampoModal` de la que depende el renglón de error y la eliminación de `MaxLength` en XAML
- [[Validacion de formularios]] — guía práctica del patrón de validación en UI
- [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]] — autocompletado y topes de longitud en pantalla de login
- [[ADR-001 - Result Pattern en Repositorios]] — el `Result` que devuelven los repositorios y que `ErroresRepositorio` traduce
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — `TextoBusqueda.Normalizar`, que ahora reusa `UsuarioModal`
- [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]] — sesión de implementación integral de topes de longitud, pruebas y sincronización de esquema

