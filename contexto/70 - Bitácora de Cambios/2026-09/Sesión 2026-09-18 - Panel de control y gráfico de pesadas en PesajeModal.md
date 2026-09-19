---
title: "Sesión 2026-09-18 — Panel de control y gráfico de pesadas en PesajeModal"
tags:
  - sesion
  - pesaje
  - wpf
  - configuracion
date: 2026-09-18
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente)
revisor: Fernando
---

# Sesión 2026-09-18 — Panel de control y gráfico de pesadas en PesajeModal

> [!success] Resultado
> `PesajeModal` tiene ahora un panel lateral con la diferencia, 4 tarjetas de acumulados, la barra de avance, el gráfico "Pesadas (kg neto)" con el punto "Ahora" en vivo y avisos en cascada. Las cuentas y la cascada viven en `CapaDominio` con 14 tests. Además se corrigieron dos detalles de `ConfiguracionEmpresaView`: los títulos logo/ícono estaban cruzados y el aviso del dominio pasó a mostrarse solo con foco. Fernando lo revisó visualmente y lo aprobó.

---

## Problema / motivo

Existía un plan externo (`D:\Proyectos\investigaciones\Plan_Panel_Control_y_Grafico_Pesadas_Modal.md`) y una imagen de referencia del modal. Al revisar el plan contra el código aparecieron errores que se corrigieron en la implementación en vez de copiarse:

| Error del plan | Corrección aplicada |
|---|---|
| Tara acumulada = `Bultos × Tara unitaria` | La tara individual es **plana** por pesada, igual que el trigger de BD → `Σ TaraInd` |
| La alerta "bruto ≤ tara" iba antes de "sin bruto" → el modal abría en rojo | "Sin bruto" va primero (Info); neto ≤ 0 es crítico solo con bruto cargado |
| "Excede el faltante" y "Excedente" como dos alertas | Son la misma condición (`neto > manif − previo ⇔ previo + neto > manif`); queda una sola |
| Desvío ±35 % del promedio sin caso base | Exige ≥ 2 pesadas confirmadas (`MinimoParaPromedio`) |
| Modo edición ignorado | La entrada editada se excluye de las previas y "Ahora" toma su lugar en el eje X |
| Fase 2 con ViewModel + `ObservableCollection` | El modal es code-behind; `Recalcular()` le pasa un array nuevo al gráfico (`AffectsRender` no redibuja ante `Add`/`Remove`) |
| Tests de escala en `BimboProyecto.Tests` sobre un control de `CapaUI` | El proyecto de tests no referencia `CapaUI` → las cuentas van a `CapaDominio.Reglas` |
| Escala Y desde `max(pico, manifestado × 0.35)` redondeada a 100/250 | Con pesadas chicas (10 kg) dejaba la línea pegada al piso → ver "Escala del eje Y" |
| `GetDpi(Application.Current.MainWindow)`, pens sin congelar por nodo, cifras de rendimiento sin medir | `GetDpi(this)`, brushes/pens estáticos congelados; sin promesas de "0 GC / 60 FPS" |

## Cambios aplicados

### `CapaDominio/Reglas/ReglasPanelPesaje.cs` (nuevo)
- `EscalaMaxima(netos)`: tope del eje Y = `max(2 × promedio, pico × 1.1)`, redondeado hacia arriba a un paso par `2·10ⁿ⁻¹`. El promedio queda **a media altura** con cualquier magnitud (10 kg → 0–20; ~1466 kg → 0–3000). Una pesada fuera de rango entra con un 10 % de margen. El manifestado ya **no** influye. Sin datos → 100.
- `MostrarEtiquetaEjeX(i, total)`: hasta 10 nodos se muestran todas las etiquetas; con más se ralean de 2 en 2 (de 5 en 5 pasados 25), conservando siempre la primera y la última.
- `EvaluarAlertas(...)` → `AlertaPesaje(Nivel, Titulo, Mensaje)`, en cascada: sin bruto (Info) → neto ≤ 0 (Crítica, excluye el resto) → Excedente (Crítica) → Sin tara extra (Advertencia) → Pesada atípica ±35 % (Advertencia) → "Pesada dentro de lo esperado" (Ok).

### `CapaUI/.../Pesaje/Controles/PesadasChart.cs` (nuevo)
`FrameworkElement` con `OnRender` y sin librerías externas:
- guías punteadas cada 25 %;
- etiquetas del eje Y a ambos lados (formato `0.##`);
- línea del promedio;
- nodos verdes con la cifra encima;
- nodo "Ahora" amarillo.

Con más de 8 nodos la cifra pasa a tooltip por `MouseMove`. API: `Actualizar(confirmados, indiceAhora, netoAhora)`. Expone `EscalaY` para el encabezado "N pesadas · máx X".

### `CapaUI/.../Pesaje/Modales/PesajeModal.xaml(.cs)`
- El ancho pasa de 760 a 980 y el panel lateral de 200 a 360.
- **Formulario** según la referencia:
  - Se agrega el rótulo "Capturá los pesos aquí".
  - Placeholders en bruto (`0.00`) y tara extra (`0 · opcional`).
  - Se quitaron los campos *Peso tara total* y *Bultos declarados*.
- **Panel lateral**:
  - Diferencia en `N0`, con `+` y en rojo si hay excedente; "Recepción completa" cuando la diferencia es menor a 0.5.
  - Contador de pesajes.
  - Tarjetas Manifestado / Neto acumulado (rojo si excede) / Tara acumulada / Tara extra acumulada. Las taras de la pesada en curso solo suman si su neto es > 0.
  - Barra de avance hecha con columnas en estrella.
  - Gráfico con su pie: "tendencia de todas las pesadas" y "promedio N kg".
  - Avisos: máximo 2 visibles, más "+N avisos más".
- Se retiraron `AvisoBultosAprox` y `AvisoNetoInvalido`: sus textos viven ahora en la cascada de avisos.
- Toda la lógica nueva está en `ActualizarPanel()`, que se llama desde `Recalcular()`.

### `CapaUI/.../Configuracion/ConfiguracionEmpresaView.xaml`
- **Títulos cruzados en "Identidad visual"**: la columna izquierda (`LogoVistaPrevia`, `SeleccionarLogo_Click`) decía "ÍCONO DEL MENÚ LATERAL" y la derecha decía "LOGO". Se intercambiaron solo los títulos; los bindings y los botones ya eran correctos.
- **Aviso del dominio de correo**: antes estaba siempre visible. Ahora solo aparece mientras `TxtDominio` tiene el foco (`DataTrigger` sobre `IsKeyboardFocusWithin`), dentro de un recuadro amarillo pálido (`#FFFBEB`, borde `#FDE68A`, esquinas de 6).

### `BimboProyecto.Tests/Dominio/ReglasPanelPesajeTests.cs` (nuevo)
14 tests: escala (5), raleo del eje X (2) y cascada de avisos (7).

## Verificación

- `dotnet test --filter ReglasPanelPesaje` → **14/14**.
- `dotnet build CapaUI` a una carpeta aparte, porque la app abierta bloqueaba `bin/` → **0 errores, 0 advertencias**.
- Arné de instanciación y render fuera de pantalla (`RenderTargetBitmap`) con 7 casos:
  - constructor de diseño;
  - vacío;
  - pesada normal;
  - excedente;
  - edición;
  - 3 pesadas de 10 kg;
  - 3 de 10 kg con "Ahora" = 12.

  Todos cargan sin excepciones y hacen layout a 980 px.
- Revisión visual y aprobación de Fernando el 2026-09-18.

## Lo que NO cambió

- **El excedente no bloquea el guardado**: se pinta en rojo como antes. Bloquearlo es un cambio de una línea en `PesajeModal.Valido` y queda a decisión del negocio.
- No se tocaron la base de datos, las RPC ni el ViewModel de Pesaje.
- No se hizo commit. La rama `feat/fase8-MaquetadodeRoles` tiene además otros cambios del usuario en `ConfiguracionEmpresaView.xaml` y `MainWindow.xaml.cs`.

---

## Relaciones

- [[Módulo Pesaje]]
- [[Módulo Configuración de Empresa]]
- [[Arquitectura Actual]]
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — por qué las cuentas viven en `CapaDominio.Reglas`
- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] — constructor de diseño del modal
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]]
- [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]]
