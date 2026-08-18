---
title: "Diálogo de confirmación reusable"
tags:
  - patron
  - wpf
  - ux
  - modales
date: 2026-08-15
lifecycle: verified
---

# Diálogo de confirmación reusable

> [!abstract]
> `CapaUI/Core/Controls/DialogoConfirmacion.xaml` — diálogo modal con el lenguaje visual del proyecto para acciones que el usuario no puede deshacer solo. Reemplaza al `MessageBox.Show(..., YesNo)` nativo.

---

## Por qué no `MessageBox`

Antes de esto, las únicas cuatro confirmaciones del proyecto usaban `MessageBox.Show` con `YesNo`, y dos de ellas eran **copias literales** entre sí. Tres problemas:

1. El diálogo gris de Windows encima de un modal azul se lee como **un error del sistema**, no como una pregunta de la aplicación.
2. **"Sí/No" no dice qué se va a hacer.** El botón que confirma tiene que llevar el verbo de la acción ("Inactivar"), así se entiende sin releer el mensaje.
3. Repetir el bloque en cada sitio garantiza que los textos se desincronicen.

---

## Uso

Para el caso más común hay un atajo que centraliza el texto:

```csharp
if (!DialogoConfirmacion.ConfirmarInactivacion("categoría", TxtNombre.Text.Trim()))
    return;
```

Para cualquier otra confirmación:

```csharp
bool sigue = DialogoConfirmacion.Confirmar(
    titulo: "Eliminar contacto",
    mensaje: "Esta acción no se puede deshacer.",
    textoConfirmar: "Eliminar");
```

---

## Decisiones de diseño

**Es una `Window`, no un overlay dentro del modal.** Los modales del proyecto son `UserControl` hospedados por la vista padre; un overlay propio obligaría a que **cada host** lo soporte. Como `Window` modal (`ShowDialog`) funciona igual desde cualquiera, sin que el host se entere.

**El foco arranca en "Volver" y Escape cancela.** Ante la duda, ni Enter ni Escape deben terminar ejecutando lo destructivo.

**El botón de confirmar va en ámbar, no en verde.** El verde de "Guardar" significa "seguí adelante"; acá el botón que confirma es justamente el que hace lo irreversible. La cinta superior también es ámbar en vez del verde/azul de los modales normales, para que se lea como advertencia desde el primer vistazo.

---

## Cuándo dispararlo — la condición importa

En los siete modales con toggle Activo/Inactivo, la advertencia salta **solo al pasar de Activo → Inactivo en un registro que ya existía**:

```csharp
bool estabaActivo = !_esNuevo && _entidad!.IdEstado == 1;   // o == EstadoRegistro.Activo
if (estabaActivo && RbInactivo.IsChecked == true && !DialogoConfirmacion.ConfirmarInactivacion(...))
    return;
```

Crear un registro nuevo directamente inactivo **no** avisa: es una decisión explícita del usuario, no una sorpresa. Advertir ahí sería ruido que enseña a ignorar el diálogo.

⚠️ `CategoriaModal` es la excepción de forma: usa `bool EstadoCategoria` en vez de `IdEstado` int/enum.

**Dónde va la llamada:** dentro de `BtnGuardar_Click`, **después** del bloque de validación y **antes** de `BtnGuardar.IsEnabled = false`. Así el usuario no ve "Guardando…" si cancela, y el `finally` que restaura el botón no queda involucrado.

---

## Dónde está conectado

Producto · Presentación · Categoría · Proveedor · Fabricante · Empleado · Usuario.

**No aplica** a los modales de Contactos (Proveedores/Fabricantes): fijan `IdEstado = Activo` hardcodeado, no tienen toggle.

---

## Relaciones

- [[Anatomia compartida de los modales]] — los estilos que este diálogo reusa
- [[Módulo Usuarios]]
- [[Módulo Productos]]
