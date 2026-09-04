---
title: "Validación de formularios"
tags:
  - patron
  - validacion
  - wpf
  - modales
date: 2026-08-15
lifecycle: verified
---

# Validación de formularios

> [!abstract]
> Las reglas de negocio viven en **`CapaDominio/Reglas/`**; la presentación del error, en **`CapaUI/Core/Validacion/`**. **Un modal no escribe sus propios `if` de validación, ni sus propios `MessageBox`, ni decide qué campo es obligatorio.** El porqué del diseño está en [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]; acá va cómo se usa.

---

## Las piezas

| Archivo | Qué hace |
|---|---|
| `ReglasFormato` *(Dominio)* | Predicados: `EsCorreo`, `EsRtn`, `EsTelefono`, largos. **Sin nada de WPF.** |
| `ReglaCampo`, `FormatoCampo` *(Dominio)* | Descriptor: qué exige el negocio de un campo, como dato. |
| `ReglasProducto`, `ReglasProveedor`… *(Dominio)* | **Qué campos son obligatorios y con qué largo, por entidad.** |
| `ParseoNumerico` *(UI)* | Convierte el texto a número según el `CultureInfo` del usuario. No es regla de negocio. |
| `ValidadorFormulario` *(UI)* | API fluida: aplica las reglas, deriva topes preventivos (`MaxLength`) a los controles y se ocupa del borde rojo, el foco y el mensaje. |
| `Validacion` | Propiedad adjunta `Error` / `TieneError` que disparan los estilos. |
| `ConfirmacionEstado` | El aviso al pasar un registro de activo a inactivo o viceversa. |
| `ErroresRepositorio` | Traduce el `23505` de Postgres y unifica el mensaje de excepción inesperada. |

---

## Cómo se usa en un modal

Las reglas se declaran **una vez**, en `OnLoaded`:

```csharp
private ValidadorFormulario _validador = null!;

private void OnLoaded(object sender, RoutedEventArgs e)
{
    _validador = ValidadorFormulario.Nuevo()
        .Campo(TxtNombre, "El nombre").Segun(ReglasProveedor.Nombre)
        .Campo(TxtCorreo, "El correo").Segun(ReglasProveedor.Correo)
        .Campo(TxtTelefono, "El teléfono").Segun(ReglasProveedor.Telefono)
        .Combo(CmbRol, "El rol").Segun(ReglasUsuario.Rol)
        .ValidarAlSalirDelCampo();
    ...
}
```

Y se evalúan al guardar:

```csharp
private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
{
    if (!_validador.Validar()) return;

    if (!ConfirmacionEstado.Confirmar(
            esNuevo:        _esNuevo,
            estabaActivo:   !_esNuevo && _entidad!.IdEstado == EstadoRegistro.Activo,
            quedaActivo:    RbActivo.IsChecked == true,
            entidad:        "categoría",
            nombreRegistro: TxtNombre.Text.Trim()))
        return;

    // recién acá: BtnGuardar.IsEnabled = false; ...
}
```

> [!important] El orden importa
> Validar y confirmar van **antes** de `BtnGuardar.IsEnabled = false`. Si se meten dentro del `try`, el usuario ve "Guardando…" y recién después le rechazan el dato — que es exactamente el bug que tenía `ProductoModal`.

---

## Reglas disponibles

**Lo normal es `Segun(ReglasXxx.Campo)`** — el qué sale del dominio. Los métodos sueltos siguen ahí para lo que no tiene una regla de negocio detrás:

`Obligatorio()` · `Correo()` · `Rtn()` · `Telefono()` · `LargoMaximo(n)` · `LargoMinimo(n)` · `Decimal()` · `Entero()`

### Validación de Campos de Catálogo (Lupas)

Para campos de solo lectura que se completan a través de un selector de catálogo (`SelectorCatalogoModal`), se utiliza `.Catalogo()`:

```csharp
_validador = ValidadorFormulario.Nuevo()
    .Campo(TxtNombre, "El nombre").Segun(ReglasProducto.Nombre)
    .Catalogo(TxtPresentacion, "La presentación", () => _idPresentacion).Obligatorio()
    .Catalogo(TxtProveedor, "El proveedor", () => _idProveedor).Obligatorio()
    ...
```

1. **Doble verificación:** Evalúa tanto que el `TextBox` contenga texto visible como que la clave foránea (`_idXxx`) tenga un ID válido asignado.
2. **Concordancia de género automática:** Si la etiqueta comienza con *"La "* genera *"La [etiqueta] es obligatoria."*; si comienza con *"El "* genera *"El [etiqueta] es obligatorio."*.
3. **Soporte para Grid/Lupa en el árbol visual:** `BuscarPanelDelCampo` recorre hasta 4 niveles hacia arriba en la jerarquía visual para localizar el `StackPanel` contenedor (`CampoModal`), permitiendo insertar el renglón de error debajo del campo aun cuando el `TextBox` y el `Button` de la lupa estén dentro de un `Grid` interno.
4. **Limpieza inmediata:** En los callbacks de selección de catálogo se asigna primero el ID y luego el texto (`_idXxx = item.Id; TxtXxx.Text = item.Nombre;`). Al dispararse `TextChanged`, `ValidadorFormulario` comprueba que el ID ya está presente y remueve el borde rojo y el mensaje de error al instante.

Tres escapes para lo que no entra:

```csharp
// Regla a medida
.Campo(TxtCodigo, "El código").Regla(t => t?.StartsWith("BIM") == true,
                                     "El código debe empezar con BIM.")

// Regla condicional: solo aplica si se cumple algo en tiempo de validación
.Clave(TxtPassword, "La contraseña").LargoMinimo(6).SoloSi(() => _esNuevo)
```

> [!note] Vacío es válido en las reglas de formato
> `Correo()`, `Rtn()` y `Telefono()` aceptan el campo vacío: significan "es opcional, pero si lo llenás tiene que estar bien". La obligatoriedad es una regla aparte y se combinan sin que un campo opcional quede obligatorio sin querer.

---

## Asignación automática de topes preventivos (`MaxLength`)

A partir de la actualización del 2026-09-02, **no se deben declarar atributos `MaxLength` manuales en el XAML de los modales**:

1. **Derivación nativa:** Al llamar a `.Segun(regla)` (o a `.LargoMaximo(n)`), `ValidadorFormulario` ejecuta internamente `TopePreventivo(m)`. Si el control asociado es un `TextBox` o `PasswordBox` y su propiedad `MaxLength` es `0` (valor por defecto en WPF), le asigna automáticamente la longitud máxima de la regla.
2. **Prevención activa:** El control bloquea la escritura o pegado de caracteres en exceso directamente en el teclado del usuario, eliminando errores por desborde antes de disparar el evento `LostFocus`.
3. **Cero redundancia en XAML:** Los modales (`ProveedorModal.xaml`, `FabricanteModal.xaml`, etc.) no colocan `MaxLength="100"`. Esto evita discrepancias donde el XAML imponía un tope artificial de 100 caracteres en columnas de base de datos que admiten 200 (`nombre_proveedor`, `nombre_fabricante`).

---

## Cómo agregar una regla nueva

1. El predicado va en `CapaDominio/Reglas/ReglasFormato` — sin WPF, para que lo pueda usar también un ViewModel o un repositorio.
2. Si es un formato nuevo, sumalo a `FormatoCampo` y al `switch` de `Segun(...)`.
3. El método fluido va en `ValidadorFormulario.ConstructorCampo`, con el mensaje por defecto parametrizado con la etiqueta del campo.
4. **Si cambia una exigencia de una entidad** (un largo máximo, un campo que pasa a obligatorio) se toca solo `ReglasEntidades.cs`, no los modales.

Si la regla es de un solo modal, no hace falta nada de esto: `Regla(...)` alcanza.

---

## Desde un ViewModel (sin controles)

`ConfiguracionEmpresaViewModel` es MVVM y no tiene TextBoxes que pasarle al validador, así que usa las reglas del dominio directamente (`ReglasFormato.NoExcedeLargo`, `EsRtn`, `EsTelefono`, `EsCorreo`) y reporta por su propia propiedad `Error`:

```csharp
if (!ReglasFormato.NoExcedeLargo(NombreEmpresa, ReglasEmpresa.Nombre.LargoMaximo ?? 200))
{
    Error = "El nombre de la empresa no puede superar los 200 caracteres.";
    return false;
}
```

---

## Cómo se ve el error

- **Borde rojo** en el campo, vía el trigger `Validacion.TieneError` de `ModalInput` / `ModalCombo` / `ModalPassword`. Va **después** del trigger de foco a propósito: si el verde de foco ganara, taparía el error justo mientras el usuario intenta corregirlo.
- **Renglón rojo debajo del campo**, insertado por código en el `StackPanel` `CampoModal` (ver [[Anatomia compartida de los modales]]). Si el campo no está en esa estructura, el validador no rompe: cae a ToolTip.
- **Al guardar**: se marcan todos los campos con problema, el foco va al primero y un `MessageBox` muestra su mensaje.

Un campo vacío que nunca se tocó **no se marca** hasta que se lo visita o se intenta guardar — si no, abrir un formulario nuevo y tabular lo pintaría todo de rojo antes de escribir nada.

---

## Relaciones

- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — el porqué, con las alternativas descartadas y el addendum de topes preventivos
- [[Anatomia compartida de los modales]] — la estructura `CampoModal` de la que depende el renglón de error y la eliminación de `MaxLength` en XAML
- [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]] — autocompletado con sincronización y límites
- [[ADR-001 - Result Pattern en Repositorios]] — el `Result` que traduce `ErroresRepositorio`
- [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]] — sesión de derivación de topes y alineación de dominio

