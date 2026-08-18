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
> Toda la validación de modales vive en `CapaUI/Core/Validacion/`. **Un modal no escribe sus propios `if` de validación ni sus propios `MessageBox`.** El porqué del diseño está en [[ADR-021 - Validacion en dos capas reglas puras y validador fluido]]; acá va cómo se usa.

---

## Las piezas

| Archivo | Qué hace |
|---|---|
| `ReglasCampo` | Predicados puros: `EsCorreo`, `EsRtn`, `EsTelefono`, `NoExcedeLargo`, `EsDecimalOpcional`. **Sin nada de WPF.** |
| `ValidadorFormulario` | API fluida: declara las reglas del formulario y las evalúa. Se ocupa del borde rojo, el foco y el mensaje. |
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
        .Campo(TxtNombre, "El nombre").Obligatorio().LargoMaximo(100)
        .Campo(TxtCorreo, "El correo").Correo()
        .Campo(TxtTelefono, "El teléfono").Telefono()
        .Combo(CmbRol, "El rol").Obligatorio()
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

1. El predicado puro va en `ReglasCampo` — sin WPF, para que lo pueda usar también un ViewModel.
2. El método fluido va en `ValidadorFormulario.ConstructorCampo`, con el mensaje por defecto parametrizado con la etiqueta del campo.

Si la regla es de un solo modal, no hace falta nada de esto: `Regla(...)` alcanza.

---

## Desde un ViewModel (sin controles)

`ConfiguracionEmpresaViewModel` es MVVM y no tiene TextBoxes que pasarle al validador, así que usa la capa pura y reporta por su propia propiedad `Error`:

```csharp
if (!ReglasCampo.EsRtn(RtnEmpresa))
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

- [[ADR-021 - Validacion en dos capas reglas puras y validador fluido]] — el porqué, con las alternativas descartadas
- [[Anatomia compartida de los modales]] — la estructura `CampoModal` de la que depende el renglón de error
- [[ADR-001 - Result Pattern en Repositorios]] — el `Result` que traduce `ErroresRepositorio`
