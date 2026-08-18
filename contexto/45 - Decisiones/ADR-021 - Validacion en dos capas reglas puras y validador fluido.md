---
title: "ADR-021 — Validación en dos capas: reglas puras y validador fluido"
tags:
  - adr
  - validacion
  - wpf
  - arquitectura
date: 2026-08-15
estado: aceptado
---

# ADR-021 — Validación en dos capas: reglas puras y validador fluido

## Contexto

No existía ninguna clase de validación en toda la solución. Cada `BtnGuardar_Click` traía su propio bloque inline, y el resultado era el esperable:

- `"El nombre es obligatorio."` aparecía **idéntico en 6 modales**, pero solo `PresentacionModal` devolvía el foco al campo.
- `EmpleadoModal` titulaba el aviso `"Validacion"` **sin tilde** — único caso del proyecto.
- **Correo, RTN y teléfono no se validaban en ningún lado**, aunque los DTOs los persistieran. La única regex de correo del proyecto estaba privada y encerrada en el panel de recuperación de contraseña.
- Convivían **cinco mecanismos** distintos de mostrar el error: `MessageBox`, `TxtError`, `TxtAvisoError`, una propiedad del ViewModel, y "deshabilitar el botón sin decir nada".
- Solo `PresentacionModal` traducía el `23505` de constraint única; los otros ocho volcaban el texto crudo de PostgREST.

Existía además un pendiente registrado el 2026-06-10 ([[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]]) que proponía un diseño concreto y pedía explícitamente plantearlo antes de implementar.

## Decisión

Validación en **dos capas separadas**, en `CapaUI/Core/Validacion/`:

**Capa 1 — `ReglasCampo`**: predicados puros (`EsCorreo`, `EsRtn`, `EsTelefono`, `NoExcedeLargo`, `EsDecimalOpcional`). Sin `MessageBox`, sin `Focus()`, sin `TextBox`.

**Capa 2 — `ValidadorFormulario`**: API fluida que declara las reglas de un formulario **una sola vez** y las evalúa en dos momentos —al salir de cada campo y al guardar—, encargándose del borde rojo, el renglón de error, el foco y el mensaje.

```csharp
_validador = ValidadorFormulario.Nuevo()
    .Campo(TxtNombre, "El nombre").Obligatorio().LargoMaximo(100)
    .Campo(TxtCorreo, "El correo").Correo()
    .Combo(CmbRol,    "El rol").Obligatorio()
    .ValidarAlSalirDelCampo();

if (!_validador.Validar()) return;   // marca todo, enfoca el primero, avisa
```

La separación no es ceremonia: `ConfiguracionEmpresaViewModel` es MVVM puro y **no tiene controles** que pasarle al validador, así que consume `ReglasCampo` directamente. Si la regla trajera el `MessageBox` adentro, esa capa serviría en un solo lugar.

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
- `ReglasCampo` es testeable sin WPF, que es la precondición de las pruebas de caja negra que pedía el pendiente de 2026-06.

**En contra / a vigilar**
- El renglón de error se inserta **por código** en el `StackPanel` `CampoModal`, apoyándose en que esa estructura es uniforme (ver [[Anatomia compartida de los modales]]). Un modal que no la respete pierde el renglón y se queda con el borde rojo y el ToolTip. Es degradación, no rotura, pero es un acoplamiento a tener presente.
- `ReglasCampo` vive en `CapaUI` aunque no dependa de WPF. Si en algún momento se quiere validar del lado servidor, **el paso natural es moverla a `CapaAplicacion`** — está escrita para que ese movimiento no requiera tocar nada más.
- Pesaje queda afuera: valida por pasos y deshabilitando botones, y parsea con `InvariantCulture` mientras los modales CRUD usan `CurrentCulture`. Esa divergencia es preexistente y no se tocó para no alterar su cálculo de pesos de refilón.

## Lo que este ADR NO cubre

El pendiente de 2026-06 listaba también reglas de negocio que siguen sin implementar y **no corresponden a esta capa**: "no operar sobre registros inactivos" y "FK válida antes de guardar". Son invariantes de dominio, no de formulario.

Tampoco existe todavía proyecto de tests; las reglas puras habilitan las pruebas de caja negra, pero nadie las escribió aún.

## Relaciones

- [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]] — el pendiente que este ADR resuelve parcialmente
- [[Anatomia compartida de los modales]] — la estructura `CampoModal` de la que depende el renglón de error
- [[ADR-001 - Result Pattern en Repositorios]] — el `Result` que devuelven los repositorios y que `ErroresRepositorio` traduce
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — `TextoBusqueda.Normalizar`, que ahora reusa `UsuarioModal`
