# Regla: Hábitos de Proceso, Calidad y Cierre de Sesión

## Propósito
Garantizar que toda entrega técnica sea sólida, autocontenida y visualmente íntegra desde la primera iteración, eliminando la necesidad de auditorías externas para detectar errores de proceso, regresiones en componentes compartidos o desactualización documental.

---

## Directiva 1: Validación Sintáctica de Notas de Bitácora (`contexto/70`)

Antes de dar por concluida cualquier nota de sesión en `contexto/70 - Bitácora de Cambios/`:
1. **Delimitadores de Código Triples**: Todo bloque de código multilínea DEBE abrir y cerrar obligatoriamente con tres comillas invertidas (```` ``` ````). Queda terminantemente prohibido usar comillas simples o dobles que rompen el resaltado sintáctico de Obsidian.
2. **Identificador de Lenguaje**: Indicar explícitamente el lenguaje del bloque (```` ```csharp ````, ```` ```xml ````, ```` ```powershell ````, ```` ```sql ````, ```` ```markdown ````, ```` ```json ````).
3. **Revisión de Integridad Textual**: Releer el diff o contenido final confirmando que ninguna edición o reemplazo haya dejado caracteres o palabras amputadas (ej. `et10.0` por `net10.0`, `alse` por `false`).

---

## Directiva 2: Verificación Exhaustiva de Usos en Controles Compartidos

Cualquier archivo ubicado en `CapaUI/Core/Controls/` (o estilos globales en `CapaUI/Resources/Styles.xaml`) es compartido por definición:
1. **Grep de Usos Obligatorio**: Antes de modificar la lógica, callbacks de dependencias (`DependencyProperty`), estilos o comportamiento visual de un control compartido, es obligatorio buscar y listar todas sus referencias en el proyecto:
   ```powershell
   Select-String -Path "CapaUI\*.xaml" -Pattern "NombreDelControl"
   ```
2. **Prueba Multi-Consumo (Evitar el sesgo del caso motivador)**: Queda prohibido validar el cambio únicamente contra la pantalla que motivó la intervención (como ocurrió en P-062 con `PesajeView` vs `ProductosView`). Se debe verificar que cada una de las vistas consumidoras existentes mantenga su comportamiento y estética previas intactas.
3. **Opt-in para Comportamientos Especiales**: Si un caso de uso requiere una mutación sobre las propiedades base (ej. pasar de renderizado por contorno `Stroke` a relleno sólido `Fill`), debe implementarse como una propiedad booleana de habilitación explícita (ej. `IconoEsRelleno`, default `false`), de modo que ningún consumidor preexistente o futuro sufra regresiones silenciosas.

---

## Directiva 3: Actualización Mandatoria de `Arquitectura Actual.md`

`contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` es la fuente viva de verdad arquitectónica del proyecto:
1. **Al Cierre de Toda Sesión con Cambios Reales**: Si la sesión introdujo nuevas funcionalidades, refactors arquitectónicos, solución de deuda técnica estructural, o nuevos patrones de UI/Backend, es obligatorio actualizar este archivo antes de despedirse.
2. **Formato y Orden Cronológico Inverso**: Insertar el callout inmediatamente arriba del todo (lo más reciente primero, justo debajo del encabezado de primer nivel `# Arquitectura Actual — Bimbo`):
   ```markdown
   > [!success] Actualizado AAAA-MM-DD — [Título conciso del cambio]
   > [Párrafo explicativo claro: qué cambió a nivel de arquitectura o componentes, qué contratos o archivos clave se introdujeron, y enlace a la nota de sesión ([[Sesión AAAA-MM-DD - Título]])].
   ```
