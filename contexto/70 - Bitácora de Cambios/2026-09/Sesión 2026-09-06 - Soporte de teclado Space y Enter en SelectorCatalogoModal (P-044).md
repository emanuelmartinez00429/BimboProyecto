# Sesión 2026-09-06 — Soporte de teclado (Space y Enter) en SelectorCatalogoModal (P-044)

## Contexto y Motivación
El selector genérico `SelectorCatalogoModal` permite a los operadores elegir elementos de catálogos (productos, proveedores, etc.). En modo múltiple (`CatalogoConfig.PermiteMultiple`), cada fila dispone de un `CheckBox`. Sin embargo, la interacción estaba limitada al uso del ratón: al navegar por teclado (`↓`/`↑`), la barra espaciadora (`Space`) no conmutaba la casilla de verificación. Asimismo, la tecla `Enter` requería que ya existieran marcas activas.

Esto obligaba al operador de báscula a alternar constantemente entre teclado y ratón durante el pesaje y recepción de materias primas.

---

## Decisiones y Solución Implementada

1. **Atajo de Barra Espaciadora (`Space`):**
   - Interceptado en el túnel `OnPreviewKeyDown` del `UserControl`.
   - **Guards de Foco:**
     - Si el foco está dentro de `SearchBox`, no se intercepta (se escribe el espacio normalmente en el cuadro de búsqueda).
     - Si el foco está sobre cualquier botón (`EsBoton(Keyboard.FocusedElement)`), se deja pasar para que el botón lo procese como clic nativo de WPF.
   - **Acción en la Tabla (`Dg.IsKeyboardFocusWithin`):**
     - **Modo múltiple (`_cfg.PermiteMultiple == true`):** Invoca `ToggleMarcado(fila)` sobre la fila actual (si no está ya elegida/bloqueada), conmutando su checkbox y manteniendo el cursor en dicha fila.
     - **Modo simple (`_cfg.PermiteMultiple == false`):** Invoca `Confirmar()`, emitiendo el ítem y cerrando el modal de inmediato.

2. **Atajo Rápido con `Enter`:**
   - Si existen marcas en `_marcados`, `Enter` confirma y emite el lote acumulado.
   - Si no hay ninguna casilla marcada (`_marcados.Count == 0`), `Enter` sobre una fila no bloqueada la emite directamente, proporcionando un atajo de 1 solo toque para agregar un producto individual.

3. **Centralización en `ToggleMarcado`:**
   - Se unificó la lógica de conmutación de `Marcado`, actualización atómica del diccionario `_marcados` y refresco visual (`RefrescarEstadoMarcas()`), siendo compartida por `Dg_DoubleClick` y por la tecla `Space`.

4. **Inspección de Árbol Defensiva (`EsBoton`):**
   - Valida si el control enfocado o alguno de sus ancestros es un `Button`, protegiendo botones de paginación y pie sin riesgo de excepciones de tipo `Visual`.

---

## Archivos Modificados
- `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`
- `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (P-044 resuelto)

---

## Verificación
- `dotnet build BimboProyecto.sln --no-incremental` -> 0 advertencias, 0 errores.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` -> 270/270 pruebas aprobadas (100%).
