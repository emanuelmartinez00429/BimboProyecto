---
title: "Sesión 2026-09-06 — Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput"
tags:
  - sesion
  - rendimiento
  - estilos
  - wpf
date: 2026-09-06
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente), con hallazgos aplicados por otra sesión (Gemini) revisados y completados en esta
revisor: Claude Fernando (agente)
---

# Sesión 2026-09-06 — Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput

> [!success] Resultado
> Cierra 3 de los 11 hallazgos de [[Deuda Técnica - Pendientes|P-031]] (G7, G8, G9) y completa [[Deuda Técnica - Pendientes|P-043]], que había quedado a medias — arreglado un solo estilo (`InputBox`) de los dos que pedía el ítem (`ModalInput` seguía con el bug). De paso, se deja documentado el arreglo del campo Descripción de `RegistroCamionesModal` que se había hecho en la sesión anterior y no se había commiteado.

---

## Problema / motivo

Otra sesión (Gemini) atacó una parte de un plan de cierre de deuda técnica de 20 ítems, pero se quedó sin cuota a mitad de camino. Al revisar qué había tocado realmente contra el plan completo, aparecieron 3 arreglos correctos y uno a medias — y ninguno de los cuatro estaba documentado ni commiteado.

---

## Cambios aplicados

### G7 — `DropShadowEffect Opacity="0"` no apaga el shader

**Archivos:** `ContactoFabricanteModal.xaml`, `ContactoProveedorModal.xaml`

**Qué estaba mal:** para "apagar" la sombra de un campo deshabilitado se usaba `<DropShadowEffect Opacity="0"/>`. WPF sigue rasterizando el efecto en cada frame aunque su opacidad sea 0 — el shader de sombra queda activo, solo invisible.

**Afecta a:** el rendimiento de composición de esos dos modales cuando un campo pasa a `IsEnabled="False"` (por ejemplo, al deshabilitar el campo de teléfono si el contacto no tiene uno). Cada frame de esa animación de estado paga el costo de un `DropShadowEffect` real, por nada.

**Arreglo:** `Effect="{x:Null}"` — apaga el shader de verdad, sin cambiar el resultado visual (opacidad 0 y sin efecto se ven idénticos).

**Ejemplo concreto de la mejora:** abrir el modal de un Contacto de Fabricante y tocar el checkbox que habilita/deshabilita un campo. Antes, WPF recalculaba una sombra invisible en cada repintado del campo mientras estuviera deshabilitado — trabajo de GPU/composición desperdiciado todo el tiempo que el modal queda abierto con ese campo apagado. Ahora ese costo es cero. En un modal con 2-3 campos condicionales, es la diferencia entre 0 y varios shaders fantasma corriendo en simultáneo mientras el usuario completa el formulario.

---

### G8 — `AddScoped` en una app WPF sin scopes

**Archivo:** `CapaAplicacion4/DependencyInjection.cs`

**Qué estaba mal:** `ISearchStrategy` (×3: Producto, Empleado, Cliente) y `SearchStrategyRegistry` se registraban con `AddScoped`. Esta aplicación **no crea scopes de DI en ningún lado** (confirmado: cero `CreateScope()` en todo el repo) — todo se resuelve contra el root provider. Un `AddScoped` sin scope es, en la práctica, un singleton — pero **sin decirlo**, lo que hace pensar erróneamente que hay una instancia nueva por "unidad de trabajo" cuando en realidad es una sola para toda la sesión.

**Verificado antes de aplicar:** las tres estrategias inyectan `IRepository<T>` (Transient, sin estado — no guardan conexión ni resultado entre llamadas, cada `SearchAsync` pide el cliente Supabase de nuevo). Convertir el registro a `AddSingleton` explícito no genera una "dependencia cautiva" peligrosa, porque el repo Transient capturado no tiene nada que perdure entre búsquedas.

**Arreglo:** `AddScoped` → `AddSingleton`, alineado con el mismo criterio que ya usa `CapaDatos/DependencyInjection.cs` para sus propios servicios (Singleton explícito, no `Scoped` engañoso).

**Afecta a:** nada del comportamiento observable — el afecta es de intención del código. Antes, alguien que viera `AddScoped` podía asumir (equivocadamente) que cada búsqueda del buscador universal usa una instancia fresca de la estrategia; ahora el registro dice la verdad: es una sola instancia para toda la sesión, como ya era en los hechos.

---

### G9 — `MainViewModel` disponía `_searchVm`, una instancia compartida

**Archivo:** `CapaUI/Formularios/Principal/MainViewModel.cs`

**Qué estaba mal:** `_searchVm` (`UniversalSearchViewModel`) es una instancia **única para toda la sesión** — la mantiene `MainViewModel` desde que arranca y la reutiliza cada vez que el usuario abre el buscador universal. Pero `MainViewModel.Dispose()` hacía `(_searchVm as IDisposable)?.Dispose()`.

**Riesgo real, no teórico:** `UniversalSearchViewModel.Dispose()` cancela y dispone su `CancellationTokenSource` interno. Una vez disponido un `CancellationTokenSource`, **cualquier intento posterior de usarlo lanza `ObjectDisposedException`**. Si algo en el ciclo de vida de la app llegaba a invocar `MainViewModel.Dispose()` sin destruir la ventana entera (o si `_searchVm` se reutilizaba después), la siguiente búsqueda del usuario explotaba con una excepción no controlada.

**Arreglo:** se quitó la línea que disponía `_searchVm`. Sigue desuscribiéndose del evento `ResultSelected` (correcto, eso sí hay que hacerlo), pero el objeto en sí queda vivo — es responsabilidad de todo el proceso, no de un `Dispose()` puntual.

**Ejemplo concreto de la mejora:** buscar "harina" en el buscador universal, cerrar sesión y volver a entrar sin cerrar la aplicación (mismo proceso), buscar "azúcar". Con el bug, ese segundo escenario dependía de que nadie hubiera disparado el `Dispose()` de por medio — ahora la búsqueda funciona sin importar cuántas veces se dispare ese `Dispose()` en el medio.

---

### P-043 completado — `ModalInput` seguía con el bug que `InputBox` ya no tenía

**Archivo:** `CapaUI/Resources/Styles.xaml`

**Qué había quedado a medias:** el ítem pedía el mismo fix en **dos** estilos globales, `ModalInput` e `InputBox` — ambos con `VerticalAlignment="Center"` fijo en su `PART_ContentHost`, ignorando la propiedad `VerticalContentAlignment` del `TextBox` sin importar qué se le pusiera local o por Setter. La sesión anterior arregló solo `InputBox` (línea 157). `ModalInput` —el estilo que usan **todos los modales CRUD estándar**: Categorías, Fabricantes, Proveedores, Presentaciones, Productos, Usuarios, Configuración de Empresa— seguía con el bug.

**Arreglo (dos cambios, no uno):**
1. `PART_ContentHost` (el `ScrollViewer` real, con foco): `VerticalAlignment="Center"` → `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"`.
2. `PART_Recorte` (el `TextBlock` con `TextTrimming="CharacterEllipsis"` que `ModalInput` superpone cuando el campo **no** tiene foco — mismo mecanismo que se armó para `RegistroCamionesModal`, resulta que ya existía acá desde antes): tenía el **mismo** `VerticalAlignment="Center"` fijo. Si se corregía solo el primero, un campo multilínea top-alineado habría mostrado el texto arriba mientras se edita y saltando al centro apenas se le saca el foco — un bug visual nuevo, introducido por arreglar solo la mitad.

**Ejemplo concreto de la mejora:** el campo "Dirección" de `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml` (`AcceptsReturn="True"`, `MinHeight="72"`, `VerticalContentAlignment="Top"` puesto local) es el caso real que P-043 señalaba. Antes: el cursor arrancaba centrado verticalmente en la caja de 72px de alto apenas se hacía click, en vez de arriba — con una dirección larga de 2-3 líneas, la primera línea quedaba oculta arriba del área visible hasta que el usuario scrolleaba manualmente dentro del campo. Ahora el cursor arranca arriba, como corresponde a un campo multilínea, y las tres líneas se leen desde el principio sin scrollear.

---

### Documentado, no nuevo: campo Descripción de `RegistroCamionesModal`

De la sesión anterior (misma rama, sin commitear todavía): el `TextBox` de Descripción en `RegistroCamionesModal` mostraba la **cola** del texto en vez del principio al escribir texto largo (el cursor arrastra la vista horizontal), y no había ninguna señal de que el texto seguía. Se agregó un `TextBlock` superpuesto con `TextTrimming="CharacterEllipsis"`, visible solo sin foco (usa `InverseBoolToVisibility`, converter ya existente en el proyecto), y un `LostFocus` que resetea `CaretIndex = 0` + `ScrollToHome()`. Mismo mecanismo que resultó ya existir en `ModalInput` (`PART_Recorte`) — no se sabía al momento de escribirlo.

---

## Verificación

Build a un directorio de salida separado (`-o`) porque `CapaUI.exe` estaba corriendo durante la sesión y bloqueaba la copia normal de binarios:

```bash
dotnet build BimboProyecto.sln --no-incremental -o <dir_temporal>
```
0 errores, 0 advertencias.

```bash
dotnet test BimboProyecto.sln
```
270/270, sin regresión.

**Pendiente de prueba visual/manual** (según el protocolo del proyecto, no se documenta como aprobado hasta que el usuario lo confirme en pantalla):
- El campo Dirección de Configuración de Empresa arranca arriba, no centrado.
- Cualquier campo `ModalInput` con texto que no entra muestra "…" al perder el foco, en cualquiera de los modales CRUD estándar (antes solo lo hacía en los pocos que ya usaban `PART_Recorte` correctamente — este fix no cambia CUÁNDO aparece el recorte, solo la alineación vertical del texto debajo).
- El campo Descripción de `RegistroCamionesModal` arranca desde el principio del texto al perder el foco.

---

## Lo que NO se tocó

- **El resto de P-031** sigue abierto: G1 (delays de login), G2 (imagen de 16.7 MB sin `DecodePixelWidth`), G3 (`BitmapCache` sobre elementos animados), G4 (`Storyboard Forever` sin `Unloaded` en Dashboard), G6 (20 viajes de red en serie en Pesaje), G10 (null-deref en 2 vistas), G11 (recursos duplicados sin `Freeze`). El ítem **no se cierra**, solo se tachan 3 de sus 11 hallazgos.
- **P-047** (divergencia `ModalInput`/`InputBox` en general) sigue abierta — este fix solo cierra la alineación vertical, no el resto de la divergencia (tamaño, `TopePreventivo`, etc.).
- Ninguno de los ítems críticos de Fase 1 del plan de 20 (P-023, P-024, P-029, P-032, P-038) se tocó.

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-031 (3 de 11 hallazgos cerrados), P-043 (resuelto)
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — origen de P-043
- [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] — origen de P-031
- [[Anatomía compartida de los modales]] — matriz de estilos de modal donde vive `ModalInput`
- [[Módulo Pesaje]] — `RegistroCamionesModal`, campo Descripción
