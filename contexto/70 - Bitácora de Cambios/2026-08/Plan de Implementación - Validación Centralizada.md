---
title: Plan de Implementación — Validación Centralizada
type: plan
status: vigente
tags:
  - plan
  - validacion
  - wpf
  - modales
  - arquitectura
date: 2026-08-15
updated: 2026-08-15
summary: "El plan ubicaba las reglas en CapaUI/Core/Validacion/ReglasCampo. Al revisarlo, Fernando señaló que las validaciones son reglas de negocio y les corresponde el…"
scope:
  - CapaDominio/Reglas
  - CapaUI/Core/Validacion/ReglasCampo
symbols:
  - CampoModal
  - ConfiguracionEmpresaViewModel
  - Control
  - DialogoConfirmacion
  - EsDecimalValido
  - EsEmail
  - EsRtn
  - EsTelefono
  - EstadoCategoria
  - EstadoRegistro
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando
estado: ejecutado-con-desvios
---

# Plan de Implementación — Validación Centralizada

> [!info] Ejecutado, con un desvío
> El plan ubicaba las reglas en `CapaUI/Core/Validacion/ReglasCampo`. Al revisarlo, Fernando señaló que **las validaciones son reglas de negocio y les corresponde el dominio** — y tenía razón. Terminaron en `CapaDominio/Reglas/`, con las especificaciones por entidad incluidas. Lo que efectivamente se hizo está en [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]] y [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]; este documento queda como registro de lo que se planeó.

> [!abstract]
> Plan de la tanda que sigue a [[Sesión 2026-08-15 - Normalizacion de modales y advertencia al inactivar]]. Tres frentes: reemplazar el `DialogoConfirmacion` por `MessageBox`, centralizar TODA la validación de formularios, y cambiar los campos de catálogo a doble clic.

---

## Contexto

1. **El `DialogoConfirmacion` de la tanda anterior no funciona visualmente.** Se dibuja como ventana propia encima del modal y tapa el formulario a medias: no se entiende qué se está confirmando. Se reemplaza por `MessageBox` nativo.
2. **Las validaciones están duplicadas y desparejas** en cada `BtnGuardar_Click`. No existe ninguna clase central en toda la solución.
3. Los campos de catálogo abren el selector con **clic simple**, lo que impide poner el cursor en el campo sin dispararlo.

---

## Diagnóstico previo (verificado, no asumido)

Lo que encontró la exploración del código y que justifica el alcance:

- **No existe ninguna clase de validación** en `CapaUI`, `CapaAplicacion4` ni `CapaDominio`.
- **No hay validación de email, RTN ni teléfono en NINGÚN modal**, aunque los DTOs los persistan. Se toman con `.Trim()` y se mandan crudos al repositorio.
- La **única regex de email del proyecto** está privada y encerrada en `Formularios/InicioSesion/ForgotEmailPanel.xaml.cs:14-15`.
- `"El nombre es obligatorio."` es **idéntico en 6 modales**, pero solo `PresentacionModal` hace `Focus()` después.
- `EmpleadoModal:66` usa el título **`"Validacion"` sin tilde** — único caso del proyecto.
- **Solo `PresentacionModal` traduce el error `23505`** de constraint única (`:117-140`). Los otros ocho vuelcan el texto crudo de PostgREST en un `MessageBox`.
- Conviven **cinco mecanismos distintos** de mostrar errores: `MessageBox`, `TxtError`, `TxtAvisoError`, propiedad de VM, y botón deshabilitado sin mensaje.
- `UsuarioModal.Normalizar` (`:174-184`) es un **duplicado privado** de `TextoBusqueda.Normalizar`, que además está espejado a la función `public.sin_tildes()` de Postgres.
- Tres registros lingüísticos conviven en los mensajes: impersonal ("El nombre es obligatorio"), imperativo formal ("Seleccione un rol") e imperativo voseo ("Ingresá la placa") — este último exclusivo de Pesaje.

---

## Decisiones tomadas

| Punto | Decisión |
|---|---|
| Aviso de estado | `MessageBox` Sí/No **en ambos caminos** (activar e inactivar), cancelable |
| Cuándo valida | Al guardar **y** al salir del campo (`LostFocus`) |
| Error en línea | Borde rojo + renglón de error debajo del campo |
| Al guardar con errores | Marca todos, enfoca el primero, **y** `MessageBox` con el primer error |
| Alcance | Los 7 modales con toggle + los 2 de Contactos + Configuración de Empresa |

**Fuera de alcance:** la familia **Pesaje** (valida por pasos y deshabilitando botones — es otro modelo) y **Reportería** (sus campos con lupa no tienen ningún handler hoy, así que no hay "clic simple" que cambiar).

---

## El patrón elegido

El razonamiento completo con alternativas descartadas va en un ADR aparte. Resumen:

**Es un builder que produce un validador retenido, no una cadena de un solo uso.** La razón es la validación en dos momentos: si corre en `LostFocus` **y** en Guardar, las reglas no pueden declararse dentro de `BtnGuardar_Click` — hay que construir una vez un objeto que las retiene y evaluarlo cuando haga falta.

### Capa 1 — Reglas puras (`ReglasCampo`)

Predicados sin UI: `EsEmail`, `EsRtn`, `EsTelefono`, `LargoMaximo`, `EsDecimalValido`. Sin `MessageBox`, sin `Focus()`, sin `TextBox`.

Esta separación es lo que permite que **`ConfiguracionEmpresaViewModel` las use**: es MVVM puro y no tiene TextBoxes que pasarle a un validador de UI. Si la regla trajera el `MessageBox` adentro, la capa quedaría inservible para el ViewModel, para un test, y para validación futura del lado servidor.

### Capa 2 — Validador fluido (`ValidadorFormulario`)

```csharp
// Se declara UNA vez, en OnLoaded del modal
_validador = ValidadorFormulario.Nuevo()
    .Campo(TxtNombre, "Nombre").Obligatorio().LargoMaximo(100)
    .Campo(TxtCorreo, "Correo").Email()          // opcional: solo valida si hay contenido
    .Campo(TxtRtn,    "RTN").Rtn()
    .Combo(CmbRol,    "Rol").Obligatorio()
    .ValidarAlSalirDelCampo();                    // engancha LostFocus a cada campo

// En BtnGuardar_Click
if (!_validador.Validar()) return;                // marca todos, enfoca el primero, MessageBox
```

### Capa 3 — Presentación del error

Propiedad adjunta `Validacion.Error` sobre el campo:
- Dispara el borde rojo vía trigger en el `ModalInput` centralizado (ver [[Anatomia compartida de los modales]]).
- El renglón de error lo **inserta el validador en el `StackPanel` `CampoModal`** que ya envuelve cada etiqueta+input. Así **no hay que tocar el XAML de ~50 campos** — la estructura ya es uniforme desde la tanda anterior.
- Si un campo no está dentro de un `CampoModal` (Configuración usa su propio layout), cae a mostrar el error por `ToolTip` en vez de romper.

---

## Entregables

### V1 — Reglas puras
Crear `ReglasCampo`. Reusar en vez de reescribir: mover la regex de email de `ForgotEmailPanel` (y que el panel la consuma), mover `ProductoModal.TryParseDecimal` (`:503-517`, ya es `static`), y borrar `UsuarioModal.Normalizar` en favor de `TextoBusqueda.Normalizar` cuidando el `.Trim()` extra.

> [!warning] Divergencia de cultura
> Los modales CRUD parsean con `CurrentCulture` y los de Pesaje con `InvariantCulture`. Para números tipeados por el usuario corresponde `CurrentCulture`. Como Pesaje queda fuera de alcance, **no se toca** — queda anotado como deuda para no cambiar su comportamiento de refilón.

### V2 — Validador fluido y marcado visual
`ValidadorFormulario` + `Validacion.Error` + triggers de borde rojo en `ModalInput`/`ModalCombo`/`ModalPassword`. Al limpiarse el error, quitar el renglón para que el modal recupere su alto.

### V3 — MessageBox de estado en ambos caminos
Borrar `DialogoConfirmacion.xaml`/`.cs` y su nota de bóveda. Los 7 call sites tienen forma idéntica y viven en `BtnGuardar_Click`, después de validar y antes de `BtnGuardar.IsEnabled = false`.

> [!important] El tipo del estado original varía — el helper recibe el `bool` ya calculado, no el DTO
> | Tipo | Modales |
> |---|---|
> | `bool` | Categoría (`EstadoCategoria`) |
> | `int == 1` | Empleado, Producto, Usuario |
> | enum `EstadoRegistro` | Fabricante, Presentación, Proveedor |

`UsuarioModal` es el único que pasa `TxtEmail` en vez de `TxtNombre` como nombre del registro.

### V4 — Doble clic en campos de catálogo
Los 6 campos de `ProductoModal.xaml`: `PreviewMouseLeftButtonUp` → **`PreviewMouseDoubleClick`**.

> [!warning] Tiene que ser `PreviewMouseDoubleClick`, no `MouseDoubleClick`
> Un `TextBox` dispara los dos (deriva de `Control`), pero la selección de palabra la hace el `TextEditor` en el manejo **bubbling** de `MouseLeftButtonDown`: el tunneling corre **antes** y alcanza a cancelarla con `e.Handled = true`; el bubbling corre **después** y deja el flash de palabra resaltada. `IsReadOnly=True` no cambia nada de esto.

El teclado (Enter/Espacio) queda igual. Ojo con el `<see cref="TxtCatalogo_PreviewMouseLeftButtonUp"/>` del XML-doc: si se renombra el handler sin actualizarlo, sale warning **CS1574**.

### V5 — Aplicar a los 7 modales con toggle
Producto, Presentación, Categoría, Proveedor, Fabricante, Empleado, Usuario. Corrige de paso un **bug real**: en `ProductoModal` el parseo decimal ocurre **dentro del `try`, después** de deshabilitar el botón y poner "Guardando…" (`:344-346`) — el usuario ve el spinner y recién ahí le rechazan el número.

### V6 — Contactos y Configuración
Los dos modales de Contactos son **gemelos byte a byte** en lo validatorio. `ConfiguracionEmpresaViewModel` **no valida nada** en `GuardarAsync` (`:184-245`) y usa solo la Capa 1. Unificar de paso los mensajes divergentes de `SeleccionarLogo` vs `ValidarImagenSeleccionada` (`"El logo debe pesar…"` vs `"La imagen debe pesar…"`).

### V7 — Traducción centralizada de duplicados
Generalizar la lógica de `PresentacionModal:117-140` a un mapa `constraint → mensaje`. El reconocimiento (`23505` / `duplicate key`) es genérico; lo por-entidad es el nombre de la constraint y el texto traducido. Unificar también `"Error inesperado: " + ex.Message`, idéntico en 9 modales.

### V8 — Build, documentación y commit
`dotnet build BimboProyecto.sln` en 0 errores. ADR con las alternativas descartadas, nota de patrón, borrar la nota del `DialogoConfirmacion`, nota de sesión.

---

## Verificación manual

- [ ] Campo obligatorio vacío: salir con Tab → borde rojo + texto debajo; al corregir desaparece y el modal recupera su alto
- [ ] Guardar con varios campos malos → todos en rojo, foco en el primero, MessageBox con ese mismo error
- [ ] Correo inválido en Proveedor/Empleado/Contactos → lo rechaza (antes no validaba nada)
- [ ] Producto: número inválido → lo rechaza **antes** de que el botón diga "Guardando…"
- [ ] Registro Activo → Inactivo → MessageBox Sí/No; "No" cancela sin guardar
- [ ] Registro Inactivo → Activo → **también** pregunta
- [ ] Crear nuevo directamente Inactivo → no pregunta
- [ ] Producto: clic simple en campo de catálogo → solo pone el cursor; **doble** clic → abre el selector sin flash de palabra seleccionada
- [ ] Producto: Tab + Enter → sigue abriendo el selector
- [ ] Nombre repetido al guardar → mensaje legible, no el texto crudo de PostgREST

---

## Deuda que este plan NO resuelve

- **Pesaje** mantiene su propio modelo de validación (por pasos, botón deshabilitado) y su `InvariantCulture`.
- **Reportería** tiene campos con lupa sin ningún handler de mouse ni teclado — darles paridad sería agregar interacción nueva, no cambiar la existente.
- `ProductoModal` mantiene su `LupaBtn` local en vez de usar el `LupaBtnCompartido` de `Styles.xaml`, que hoy solo consume Reportería.

---

## Relaciones

- [[Sesión 2026-08-15 - Normalizacion de modales y advertencia al inactivar]] — la tanda anterior, que dejó la estructura `CampoModal` uniforme de la que depende este plan
- [[Anatomia compartida de los modales]] — los estilos centralizados que este plan extiende con el estado de error
- [[Dialogo de confirmacion reusable]] — el componente que este plan **elimina**
- [[Convenciones C#]]
