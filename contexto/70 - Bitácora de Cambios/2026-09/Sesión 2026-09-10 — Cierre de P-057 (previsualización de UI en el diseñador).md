---
title: "Sesión 2026-09-10 — Cierre de P-057 (previsualización de UI en el diseñador)"
tags:
  - sesion
  - wpf
  - xaml
  - disenador
date: 2026-09-10
branch: (rama de trabajo actual)
autor_cambios: Claude Fernando (agente)
---

# Sesión 2026-09-10 — Cierre de P-057 (previsualización de UI en el diseñador)

> [!success] Resultado
> Los 17 controles de tipo modal + 2 pantallas + 13 vistas se previsualizan en el diseñador de Visual Studio — **verificado por Fernando en el diseñador el 2026-09-10**. Arné de instanciación: **33/33 a tamaño real, 0 colapsos, 0 excepciones**. Los 6 converters salieron de `App.xaml`. Nodo nuevo de convenciones de UI + skill `ui-conventions`. Solución 0/0, suite 344/344. **P-057 cerrado.**

---

## Problema / motivo

P-057: solo `ProductosCargaModal` y `FormatoReporteModal` se previsualizaban. El resto (modales y vistas) mostraba lienzo en blanco o recuadro vacío. Fernando pidió cerrarlo completo y "seguir las buenas prácticas a rajatabla".

## Cambios aplicados

### Modales (17) — pasos de [[Anatomia compartida de los modales]]
- **Raíz sin `MaxWidth`/`MaxHeight`** con `AncestorType` en los 14 que lo tenían. El límite lo pone el host:
  - Pesaje (5): `PesajeView.MostrarModal` ya llamaba `LimitarAlOverlay` — sin cambio de runtime.
  - Catálogo (9) + Config: helper nuevo `CapaUI/Core/ModalLayout.cs` → `LimitarAlOverlay(modal, ModalOverlay)`, llamado al inicio de cada `MostrarModal`.
- **`{StaticResource RestarMargen}` → `{x:Static conv:RestarMargenConverter.Instancia}`**.
- **Merge de `Styles.xaml`** donde faltaba (`SelectorCatalogoModal`).
- **Constructor sin parámetros** en los que no lo tenían (servicios en `null!`). En `Camion`/`Registro`/`Fabricante`/`Producto` reemplazó el parche `DesignerProperties.GetIsInDesignMode` metido por un intento anterior.
- `ConfiguracionEmpresaModal`: `BoolToVisibility` → `{x:Static ...Instancia}` (se le agregó el singleton al converter).

### Pantallas
- `WelcomeScreen.xaml` / `ConstructionScreen.xaml`: `mc:Ignorable="d"` + `d:DesignWidth/Height` (contenido centrado ⇒ alto natural 0).
- `WelcomeScreen.xaml.cs`: guard `GetIsInDesignMode` antes de enganchar el `Loaded` que llama a `App.Services`.

### Vistas (13)
- **Merge de `Styles.xaml`**: 4 sin `<UserControl.Resources>` lo reciben directo; 8 con recursos locales se envolvieron en `<ResourceDictionary>` + `MergedDictionaries`.
- **Converters de `App.xaml` → `{x:Static}`**: `AnchoMinimoAVisibilidad`, `TextoVacioConverter`, `BoolToVisibility`, `InverseBoolToVisibility`, `RestarMargen`. Se agregó `.Instancia` a `AnchoMinimoAVisibilidadConverter` y `TextoVacioConverter`.

### Limpieza de `App.xaml` (rajatabla)
- **Los 6 `<conv:...>` salieron de `App.xaml`** y de `DesignTimeResources.xaml`. Todos tienen singleton `Instancia`.
- Migrados los usos que quedaban por clave: `MainWindow.xaml` (3× `BoolToVisibility` + 5× `AnchoMinimoAVisibilidad`), `RegistroCamionesModal.xaml` (1× `InverseBoolToVisibility`), `RolesResources.xaml` (1×), `RolesView.xaml` (19×).
- Grep repo-wide final: **cero** `{StaticResource <converter>}` en `.xaml`; cero referencias por string en `.cs`.

### Documentación
- Nodo nuevo: [[Convenciones de UI (WPF) — leer antes de tocar XAML]] en `20 - Patrones/`. Compila todas las reglas de UI con su evidencia.
- Skill nueva: `.claude/skills/ui-conventions/SKILL.md`.
- `AGENTS.md` (raíz) y `contexto/AGENTS.md`: entrada de lectura obligatoria antes de tocar XAML.
- ADR-028: Addendums 3 y 4. [[Anatomia compartida de los modales]]: estado. Este ítem (P-057) → `[x]` Resuelto.

## Verificación

- **Arné de instanciación** (`new Application()` de `Resources` vacío + ctor sin parámetros + `Measure`/`Arrange`): 33/33 OK, 0 colapsos, 0 excepciones. Incluye `RolesView` (regresión por el swap de converters).
- `dotnet build BimboProyecto.sln` → 0/0. (Durante la sesión el `bin` estuvo bloqueado por la app y VS 2022 abiertos; se compiló con `-p:UseAppHost=false` y se corrió el arné contra `obj/.../CapaUI.dll` — el `MarkupCompilePass` no reportó ningún error `MC`/`CS`.)
- Suite: **344/344**.
- **Prueba manual de Fernando (2026-09-10): OK.** Probó todos los controles en el diseñador de Visual Studio y renderizan. P-057 cerrado.

## Errores y caminos equivocados (para no repetirlos)

1. **El parche `DesignerProperties.GetIsInDesignMode` en el ctor con parámetros.** Un intento previo (Antigravity / Engram #221) lo metió creyendo que salvaba el diseñador. Es **código muerto**: el diseñador instancia por el ctor **sin parámetros** (y ni corre el code-behind raíz), así que ese guard nunca se ejecuta para lo que fue puesto — y encima deja `_repo` en `null!` si alguna vez `GetIsInDesignMode` diera `true` en runtime. La cura real es un ctor sin parámetros.
2. **Misdiagnóstico de "una sola capa".** El bug tenía dos: (a) `{StaticResource}` en atributo raíz revienta el parseo; (b) aun arreglado eso, el binding `AncestorType=Border` engancha un `Border` de VS que mide 0 y colapsa el control a 0×0 **sin excepción**. Dos intentos anteriores atacaron solo (a). Detalle en [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]].
3. **Arné que confirma la hipótesis previa = no es evidencia.** El primer arné del ADR armaba el `Border` ancestro ya dimensionado → daba verde mientras el lienzo seguía vacío. La condición a reproducir era "con ancestro **todavía sin medir**".
4. **Asumir "ya se ven" sin verificar.** Esta sesión dio por hecho que las 12 vistas renderizaban (porque "no tenían el problema del raíz"). Fernando lo corrigió con un screenshot. Al verificar: no mergeaban `Styles.xaml` **y** usaban converters de `App.xaml`. Regla: verificar con el arné, no asumir.
5. **El barrido de 2026-09-09 subestimó el trabajo de las vistas.** Dijo "solo les falta el merge (paso 3)". Faltaba también el paso 2 (converters). Causa: el parseo XAML **corta en la primera clave que falta**, así que un barrido que instancia y anota el primer error ve `IconTag` y nunca llega a `AnchoMinimoAVisibilidad`. Hay que iterar: arreglar, recorrer de nuevo.
6. **Punto ciego del arné: `{StaticResource}` en templates diferidos.** `RegistroCamionesModal` pasó el arné (880×620) teniendo `{StaticResource InverseBoolToVisibility}` roto — estaba dentro de un `DataTrigger` en un template que un `Measure`/`Arrange` sin datos no instancia. Se detectó recién con el `grep` repo-wide al sacar los converters de `App.xaml`. Mitigación permanente: grep exhaustivo + `{x:Static}` (que no puede faltar).

## Lecciones

- **Verificar > asumir.** Un "no debería tener el problema" no es una verificación. Correr el arné.
- **Iterar el barrido.** El parser corta en el primer error; un solo pase subestima. Arreglar → recorrer de nuevo hasta cero.
- **El arné cubre carga + layout, no todo.** Ciego a templates diferidos y a runtime con datos. Combinar con `grep` repo-wide y prueba manual.
- **Estado medio-migrado es el peor.** Dos formas de hacer lo mismo (clave de `App.xaml` vs singleton) confunden al próximo. Cerrar la migración entera o no empezarla.
- **Un parche que "arregla por un camino que no se lee en el código" es deuda.** El `GetIsInDesignMode` muerto y el binding de ancestro invisible son el mismo antipatrón.

## Lo que NO cambió

- Ninguna lógica de negocio, ViewModel, repositorio ni migración.
- El comportamiento de runtime de los modales: `LimitarAlOverlay` referencia el **mismo** `ModalOverlay` que el binding de ancestro encontraba.
- Los colores `Empresa*` siguen solo en `App.xaml` + `DesignTimeResources.xaml`.
- `BimboPesaje` (fuera de la solución).

---

## Relaciones

- [[Convenciones de UI (WPF) — leer antes de tocar XAML]] — el nodo que salió de esta sesión
- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] — Addendums 3 y 4
- [[Anatomia compartida de los modales]] — la receta
- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]]
- [[Deuda Técnica - Pendientes]] — P-057 cerrado
- [[Arquitectura Actual]]
