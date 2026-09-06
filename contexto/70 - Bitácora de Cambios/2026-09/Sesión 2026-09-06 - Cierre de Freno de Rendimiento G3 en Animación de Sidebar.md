# Sesión 2026-09-06 — Cierre de Freno de Rendimiento G3 en Animación de Sidebar (P-031)

## Contexto y Causa Raíz
El hallazgo G3 de **P-031** señalaba el uso de `CacheMode="BitmapCache"` sobre `Sidebar` y `BrandBlock` en `MainWindow.xaml` (líneas 127 y 528).

Ambos contenedores sufren una animación continua de `Width` al colapsar y expandir el menú lateral (160 ms con curva `QuarticEase.EaseOut`). 

### Por qué BitmapCache degradaba el rendimiento
`BitmapCache` indica al subsistema de renderizado de WPF (Direct3D) que rasterice el sub-árbol visual en una textura bitmap en memoria gráfica. Sin embargo:
- Si las dimensiones del elemento cambian (`Width` cambia en cada frame de la animación), la textura cacheada se vuelve inválida inmediatamente.
- Direct3D se ve forzado a re-rasterizar el bitmap completo frame a frame, generando sobrecarga de CPU/GPU y consumo constante de asignación de texturas de video.
- Al retirar `BitmapCache`, WPF utiliza su canal de renderizado vectorial nativo directo, el cual maneja el cambio de `Width` de un `Border` con fondo uniforme o degradado simple de forma significativamente más ligera y sin re-allocación de superficies bitmap.

---

## Modificaciones Realizadas
1. **`CapaUI/Formularios/Principal/MainWindow.xaml`**:
   - Se removió el atributo `CacheMode="BitmapCache"` de `BrandBlock`.
   - Se removió el atributo `CacheMode="BitmapCache"` de `Sidebar`.
   - Se preservó íntegramente la lógica y tiempos de animación en `MainWindow.xaml.cs` (`AnimateSidebarWidth`, duraciones, retardos de fade de etiquetas).

---

## Verificación y Validación en Runtime
1. **Compilación estricta**:
   - `dotnet build BimboProyecto.sln --no-incremental` → 0 errores, 0 advertencias.
2. **Suite de pruebas unitarias y de integración**:
   - `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` → 286/286 pruebas aprobadas (100%).
3. **Validación interactiva con el usuario**:
   - El usuario ejecutó la aplicación y probó el colapso y expansión repetida del sidebar en su entorno físico de prueba, confirmando fluidez absoluta e idéntica o superior a la versión previa, sin tirones ni regresiones visuales.

---

## Archivos Involucrados
- `CapaUI/Formularios/Principal/MainWindow.xaml`
- `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (P-031 actualizado: 10/11 resueltos, solo G11 pendiente)

---

## Relaciones
- [[Deuda Técnica - Pendientes]] — P-031 (Frenos de rendimiento de toda la aplicación)
- [[Sesión 2026-09-06 - Auditoría de commits a05f006 y 404796c]]
- [[MainWindow y Navegación Principal]]
