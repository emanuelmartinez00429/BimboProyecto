---
title: "Sesión 2026-08-15 — Validación centralizada y doble clic en catálogos"
tags:
  - sesion
  - validacion
  - wpf
  - modales
  - refactor
date: 2026-08-15
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando
---

# Sesión 2026-08-15 — Validación centralizada y doble clic en catálogos

Ejecución de [[Plan de Implementación - Validación Centralizada]]. Ocho entregables.

## Qué motivó la tanda

El `DialogoConfirmacion` de la sesión anterior **no funcionó visualmente**: al ser una ventana propia se dibujaba encima del modal tapándolo a medias, y no se entendía sobre qué registro preguntaba. Se eliminó por completo y volvió el `MessageBox` nativo.

Aprovechando eso, se atacó lo que había debajo: **no existía ninguna clase de validación en toda la solución** y cada `BtnGuardar_Click` traía su propio bloque inline.

## Qué se hizo

1. **`ReglasCampo`** — predicados puros sin WPF. La única regex de correo del proyecto estaba privada en `ForgotEmailPanel`; se movió acá y el panel ahora la consume. `UsuarioModal.Normalizar` era un duplicado de `TextoBusqueda.Normalizar` (que además está espejada a `public.sin_tildes()` de Postgres) y se borró.

2. **`ValidadorFormulario`** — API fluida declarada una vez en `OnLoaded` y evaluada en dos momentos: `LostFocus` y Guardar. Detalle de uso en [[Validacion de formularios]]; el porqué del diseño en [[ADR-021 - Validacion en dos capas reglas puras y validador fluido]].

3. **Error en línea** — borde rojo (trigger `Validacion.TieneError`) + renglón debajo del campo, insertado por código en el `StackPanel` `CampoModal`. No hubo que tocar el XAML de ~50 campos porque esa estructura quedó uniforme en la sesión anterior.

4. **`ConfirmacionEstado`** — `MessageBox` Sí/No que ahora avisa en **los dos sentidos**: al inactivar y al activar. Sigue sin preguntar en altas.

5. **`ErroresRepositorio`** — la traducción del `23505` que solo tenía `PresentacionModal` ahora la usan los nueve.

6. **Doble clic en los campos de catálogo** de `ProductoModal`. Con clic simple no se podía ni poner el cursor sin que se abriera el selector.

7. **Alcance**: los 7 modales con toggle + los 2 de Contactos + `ConfiguracionEmpresaViewModel`.

## Decisiones que vale la pena recordar

**`PreviewMouseDoubleClick`, no `MouseDoubleClick`.** Un `TextBox` dispara los dos, pero la selección de palabra la hace su editor interno durante el burbujeo de `MouseLeftButtonDown`: el túnel corre antes y alcanza a cancelarla con `Handled`; el burbujeo corre después y dejaría la palabra resaltada un instante antes de abrir el selector.

**El trigger de error va después del de foco.** Cuando dos triggers pisan la misma propiedad gana el último. Si ganara el verde de foco, taparía el error justo mientras el usuario intenta corregirlo.

**El aviso de estado recibe un `bool` ya calculado, no el DTO.** Cada módulo guarda el estado distinto: `bool` en Categorías, `int == 1` en Empleados/Productos/Usuarios, enum `EstadoRegistro` en Fabricantes/Presentaciones/Proveedores.

**Un campo nunca visitado no se marca en rojo.** Si no, abrir un formulario nuevo y tabular lo pintaría todo antes de que el usuario escriba nada.

## Bugs y deuda corregidos de paso

- **`ProductoModal` parseaba los decimales dentro del `try`**, después de deshabilitar el botón y poner "Guardando…": el usuario veía el spinner y recién entonces le rechazaban el número. Al mover la validación antes, se corrigió solo.
- **Correo, RTN y teléfono no se validaban en ningún lado**, aunque los DTOs los persistieran.
- `EmpleadoModal` titulaba el aviso `"Validacion"` **sin tilde** — único caso del proyecto.
- `"El nombre es obligatorio."` estaba idéntico en 6 modales pero solo uno devolvía el foco al campo.
- `ConfiguracionEmpresaViewModel` tenía `SeleccionarLogo` y `ValidarImagenSeleccionada` **duplicados entre sí** con mensajes divergentes ("El logo debe pesar…" vs "La imagen debe pesar…"). Quedó uno solo.
- `ConfiguracionEmpresaViewModel.GuardarAsync` **no validaba nada** — ni nombre, ni RTN, ni correo.
- `"Error inesperado: " + ex.Message` estaba repetido palabra por palabra en 9 modales.

## Sobre el pendiente de 2026-06

Existía [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]], que proponía un diseño concreto (`IValidacionService`, una clase por regla, `ValidacionResult` con lista de strings). **No se siguió**, y el ADR explica por qué: una lista de strings pierde el vínculo con el control, y sin saber *qué campo* falló no se puede pintar el borde ni mover el foco.

Ese pendiente quedó marcado como **parcialmente resuelto**: la validación de formulario está, pero siguen abiertas las reglas de negocio que listaba ("no operar sobre registros inactivos", "FK válida antes de guardar") y las pruebas de caja negra en sí — `ReglasCampo` quedó testeable sin WPF, que era la precondición, pero no hay proyecto de tests.

## Huecos encontrados al reverificar

Después del primer commit se barrió el árbol por grep en vez de confiar en lo hecho. Aparecieron tres cosas:

**`UsuarioModal` solo había migrado la validación, no los errores del repositorio.** Seguía con `MostrarError(r.Error)` crudo, así que un correo repetido mostraba el texto de PostgREST sin traducir. Se agregaron `ErroresRepositorio.Traducir` y `TextoInesperado`, que **devuelven** el texto en vez de mostrarlo: este modal presenta el error en línea (`TxtError`) y no por `MessageBox`, y no tenía por qué elegir entre conservar su presentación o recibir la traducción.

**`ProductoModal.TryParseDecimal` quedó como código muerto** al mover el parseo al validador — nadie lo llamaba y arrastraba su propio `MessageBox` de validación, justo lo que la centralización venía a eliminar.

**`FabricanteModal` tenía una asimetría con pérdida de dato.** Si fallaba la carga de **países** deshabilitaba Guardar, pero si fallaba la de **proveedores** no. No es cosmético: con el combo vacío `SelectedItem` queda `null`, y al guardar un fabricante existente el DTO viaja con `IdProveedor = null`, **borrándole el proveedor sin avisar** — justo el catálogo que se usa para encadenar fabricantes. Ahora las dos ramas bloquean igual.

> [!note] Sobre el método
> Los tres aparecieron por `grep` sobre el árbol, no por releer lo que se había hecho. Vale como recordatorio: "lo implementé" y "está en el código" no son la misma afirmación, y la segunda es la única verificable.

---

## Deuda que NO se tocó

- **Pesaje** mantiene su modelo propio (validación por pasos, botón deshabilitado) y su `InvariantCulture`, contra el `CurrentCulture` de los modales CRUD. Cambiarlo de refilón podría alterar su cálculo de pesos.
- **Reportería** tiene campos con lupa sin ningún handler de mouse ni teclado: darles paridad sería agregar interacción nueva, no cambiar la existente.
- `ReglasCampo` vive en `CapaUI` aunque no dependa de WPF. Si se quiere validar del lado servidor, el paso natural es moverla a `CapaAplicacion` — está escrita para que ese movimiento no requiera tocar nada más.

## Verificación

`dotnet build BimboProyecto.sln` en **0 errores**. Falta la prueba visual manual.

> [!warning] Build con la app corriendo
> Si Visual Studio tiene la app en depuración, el build falla con `MSB3027`/`MSB3021` por bloqueo de archivo. **No** son errores de código: la compilación de C#/XAML ya pasó cuando aparecen.

## Relaciones

- [[Plan de Implementación - Validación Centralizada]] — el plan que ejecuta esta sesión
- [[ADR-021 - Validacion en dos capas reglas puras y validador fluido]]
- [[Validacion de formularios]] — cómo usar el validador
- [[Sesión 2026-08-15 - Normalizacion de modales y advertencia al inactivar]] — la tanda anterior
- [[Anatomia compartida de los modales]]
