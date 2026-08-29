---
title: Validación de formularios
type: patron
status: vigente
tags:
  - patron
  - validacion
  - wpf
  - modales
date: 2026-08-15
updated: 2026-08-15
summary: "Las reglas de negocio viven en CapaDominio/Reglas/; la presentación del error, en CapaUI/Core/Validacion/. Un modal no escribe sus propios if de validación, ni…"
scope:
  - CapaDominio/Reglas
  - CapaDominio/Reglas/ReglasFormato
  - CapaUI/Core/Validacion
symbols:
  - CampoModal
  - ConfiguracionEmpresaViewModel
  - ConfirmacionEstado
  - Error
  - ErroresRepositorio
  - EsCorreo
  - EsRtn
  - EsTelefono
  - FormatoCampo
  - MessageBox
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
| `ValidadorFormulario` *(UI)* | API fluida: aplica las reglas y se ocupa del borde rojo, el foco y el mensaje. |
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

Dos escapes para lo que no entra:

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

## Cómo agregar una regla nueva

1. El predicado va en `CapaDominio/Reglas/ReglasFormato` — sin WPF, para que lo pueda usar también un ViewModel o un repositorio.
2. Si es un formato nuevo, sumalo a `FormatoCampo` y al `switch` de `Segun(...)`.
3. El método fluido va en `ValidadorFormulario.ConstructorCampo`, con el mensaje por defecto parametrizado con la etiqueta del campo.
4. **Si cambia una exigencia de una entidad** (un largo máximo, un campo que pasa a obligatorio) se toca solo `ReglasEntidades.cs`, no los modales.

Si la regla es de un solo modal, no hace falta nada de esto: `Regla(...)` alcanza.

---

## Desde un ViewModel (sin controles)

`ConfiguracionEmpresaViewModel` es MVVM y no tiene TextBoxes que pasarle al validador, así que usa las reglas del dominio directamente y reporta por su propia propiedad `Error`:

```csharp
if (!ReglasFormato.EsRtn(RtnEmpresa))
{
    Error = "El RTN debe tener 14 dígitos.";
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

- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — el porqué, con las alternativas descartadas
- [[Anatomia compartida de los modales]] — la estructura `CampoModal` de la que depende el renglón de error
- [[ADR-001 - Result Pattern en Repositorios]] — el `Result` que traduce `ErroresRepositorio`
