# Regla: Dirty Tracking Tipado en Formularios y Modales (ChangeTracker<T>)

## Propósito
Prevenir el antipatrón de comparaciones manuales campo por campo (cadenas de `OR` con `!string.Equals` o `!=`) en los modales de edición, evitando fallos silenciosos (*silent failures*) por campos omitidos y llamadas innecesarias a la base de datos cuando no hay cambios.

## Directiva Obligatoria
En cualquier modal o formulario de edición en WPF (`*Modal.xaml.cs`):

1. **Prohibido el Dirty Tracking Manual**:
   - ❌ **NO** encadenar comparaciones manuales de propiedades (`!string.Equals(dto.X, _orig.X) || dto.Y != _orig.Y`).
   - ❌ **NO** ejecutar actualizaciones de red si ni los datos ni el estado cambiaron.

2. **Uso Obligatorio de `ChangeTracker<T>` con `record` Privado**:
   - ✅ Definir un `record` privado y posicional en el code-behind del modal que represente exclusivamente los campos editables por el usuario (ej. `ProveedorSnapshot`).
   - ✅ Instanciar `ChangeTracker<TSnapshot>` en `OnLoaded` capturando el snapshot inicial del DTO existente.
   - ✅ En el guardado (`BtnGuardar_Click`), instanciar el snapshot actual con los valores procesados y evaluar `bool datosCambiaron = _tracker.IsDirty(snapshotActual);`.
   - ✅ Si `!datosCambiaron && !estadoCambio`, cerrar el modal limpiamente sin roundtrips de red.

3. **Garantía en Tiempo de Compilación**:
   - Al agregar o modificar campos del formulario, la firma del constructor posicional del `record` obliga al desarrollador a actualizar tanto la carga inicial como la recolección del snapshot actual, haciendo imposible olvidar campos.
