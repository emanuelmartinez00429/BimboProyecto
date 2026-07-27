---
title: "Sesión 2026-07-26 — Rediseño del flujo de Pesajes (wizard + megamodal)"
tags: [sesion, pesaje, ux, refactor, fase8]
date: 2026-07-26
branch: feat/fase7-GestióndeUsuarios
autor_cambios: Claude (Opus 5), dirigido por Fernando
---

# Sesión 2026-07-26 — Rediseño del flujo de Pesajes

## Motivo

Fernando: *"el proceso se ve muy disperso y confuso"*. Al entrar a Recepción de Materia Prima el usuario se encontraba tres paneles vacíos sin saber por dónde empezar, y para registrar una descarga tenía que abrir 4 modales distintos desde 9 botones repartidos en tres toolbars, sin orden impuesto ni instrucciones. El picker de productos era una lista de 10 resultados sin tabla, paginación ni código visible.

## Decisiones tomadas antes de implementar

| Tema | Decisión |
|---|---|
| Nivel de la tara extra | **Por camión** (nueva columna en `movimientos`) |
| Numerador de bultos teóricos | **Peso bruto pesado** |
| Tara de empaque | Los datos actuales son de prueba; la fórmula asume tara **por bulto** |
| Estado vacío | Se muestra si **no hay camiones ABIERTOS** |
| Captura del bruto | Sigue en el **modal de pesaje** |

### Prorrateo de la tara extra (interpretación confirmada)

La tara extra se pesa una sola vez para toda la carga, así que se reparte entre **el total de bultos declarados del camión** — no de un solo producto:

```
tara_extra_por_bulto = movimientos.peso_tara_extra / Σ(bultos declarados del camión)
tara_extra_de_esta_pesada = tara_extra_por_bulto × bultos de esta pesada
```

Así, al terminar de pesar toda la carga, la suma de taras atribuidas equivale al total real. Se calcula en el **ViewModel** (fuente única de verdad); el trigger de BD no se toca.

## Qué se construyó

7 pasos, un commit por paso, build verde en cada uno.

**1 · Base de datos y cálculos** (`7d76c6d`)
Migración `agregar_peso_tara_extra_a_movimientos`. `ProductoTaraConsulta` ahora también lee `peso_teorico`. `MovProductoDto.BultosTeoricos` → `BultosDeclarados` (la columna BD conserva el nombre viejo). `PesajeCalc.TaraExtraPorBulto()` y `PesajeCalc.BultosTeoricos()` con guardas contra división por cero y peso teórico faltante.

**2 · Repositorio paginado** (`a8407ad`)
`IPickerProductoRepository.GetPagedAsync` con paginación server-side, reutilizando el puente producto→fabricante→proveedor. `AplicarFiltros()` centraliza la traducción filtro→columna. OR de texto con `.Or()` + `QueryFilter` (nunca `Filter("or", Op.Equals)`).

**3 · Selector de productos** (`58efc3d`)
`SelectorProductosModal` (900px) + `SelectorProductosViewModel`: tabla con código, paginación, buscador con debounce, toggle proveedor/catálogo, filas ya agregadas atenuadas.

**4 · Proceso de descarga** (`ba9d58d`)
`ProcesoDescargaModal`: **un componente con dos modos**. Wizard (una sección a la vez, Atrás/Siguiente, instrucciones escritas) y Edición/megamodal (todas visibles). Tres secciones: camión, productos, tara extra.

**5 · Vista** (`bf1117f`)
Estado vacío con el botón de iniciar proceso. Un solo botón de edición. Eliminados `CamionModal`, `ProductoCamionModal` y `SeleccionarProductoModal`. `GuardarProcesoAsync()` persiste todo en una operación.

**6 · Modal de pesaje** (`5dab3b3`)
Tara extra pasa a solo lectura (viene prorrateada). Nuevo indicador de bultos teóricos en vivo. Aviso ámbar si falta registrar la tara extra.

**7 · Documentación**
[[Módulo Pesaje]] (nota de módulo nueva) + P-023/P-024/P-025 en deuda técnica.

## Hallazgos de datos (verificados contra la BD real)

> [!bug] Las taras son datos de prueba — P-023
> La tabla `tara` tiene **una sola fila** (20 kg) usada por 503 de 505 productos, pero **500 de esos productos pesan menos de 10 kg**: el empaque pesaría el doble o más que el producto. Los otros 5 (1,000–10,000 kg) se llaman "Producto Number 1", "Producto bien five". Existe además una tabla `tarima` aparte, lo que refuerza que ese 20 kg no es un empaque real.
>
> **Esto importa porque el neto es lo que se le paga al proveedor.** Los bultos teóricos van a dar números sin sentido hasta que se cargue el catálogo real.

> [!warning] Inconsistencia deliberada — P-024
> El trigger de BD aplica la tara de empaque **plana**; la fórmula nueva la trata **por bulto**. Conviven a propósito: cambiar el trigger alteraría el neto de pesajes futuros y dejaría inconsistentes los guardados. El indicador nuevo es informativo y no toca el neto.

## Decisiones técnicas que vale la pena recordar

**Por qué no se reutilizó `ProductosViewModel`** — hereda de `RealtimeAwareViewModel`, cuyo **constructor** ya se engancha a `IConexionMonitor`, y en la carga se suscribe a Realtime. Instanciarlo dentro de un modal dejaría suscripciones vivas en cada apertura. El VM del selector es un `ObservableObject` plano con `PageSize=15` y `Dispose()` que cancela el debounce. Esto responde directo al pedido *"que no sobrecargue la memoria"*.

**Estilos de paginación propios en el modal** — `ProductosView` usa `FindResource("PageBtn")`, que resuelve por árbol lógico; esos estilos viven en su propio XAML y no son visibles desde otro modal.

**Trampa encontrada durante el trabajo** — un `replace_all` del renombre "bultos teóricos → declarados" alcanzó también al método `PesajeCalc.BultosTeoricos` recién creado, rompiendo el build. Restaurado. Lección: `replace_all` sobre un término que también es parte de un nombre nuevo es riesgoso.

## Verificación

- `dotnet build --no-incremental` → **0 errores** en cada paso. 45 advertencias únicas (línea base 46: bajó por los archivos eliminados), **ninguna nueva**.
- Paginación contrastada contra SQL directo: CISA con 505 productos, página 2 (offset 15) coincide. Proveedor con fabricante pero sin productos → vacío. Proveedor sin fabricantes → corta antes de consultar.
- Fórmula de bultos teóricos simulada en SQL con datos reales; las 3 guardas verificadas.
- **Pendiente de verificación visual por Fernando** (la app requiere login, no se automatiza): estado vacío → wizard completo → pesar → editar con megamodal → intentar quitar un producto ya pesado (debe estar bloqueado) → cerrar camión.

## Relaciones

- [[Módulo Pesaje]] — documentación del módulo
- [[Deuda Técnica - Pendientes]] — P-023, P-024, P-025
- [[Sesión 2026-07-01 - Pesaje Fase 2 - Persistencia Real]] — implementación anterior
- [[Módulo Productos]] · [[Paginación y Búsqueda - Arquitectura Detallada]]
