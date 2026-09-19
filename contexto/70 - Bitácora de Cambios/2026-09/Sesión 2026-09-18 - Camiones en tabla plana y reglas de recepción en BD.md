---
title: "Sesión 2026-09-18 — Camiones en tabla plana y reglas de recepción en BD"
tags:
  - sesion
  - pesaje
  - supabase
  - wpf
  - reportes
date: 2026-09-18
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente) con Fernando
---

# Sesión 2026-09-18 — Camiones en tabla plana y reglas de recepción en BD

> [!success] Resultado
> El panel «Camiones de Entrega» dejó de agrupar por placa: ahora es un `DataGrid` plano donde **una fila es una recepción** (placa + proveedor). El tope de 5 y la regla «misma placa solo con otro proveedor» viven en Dominio **y** en la BD, con un trigger sobre `movimientos` y un índice único. El reporte se elige por placa y junta todas sus recepciones en un solo archivo. Fernando lo aprobó visualmente.

---

## Problema / motivo

- El panel mostraba una cabecera por placa (🚚 1212) con sub-filas por proveedor. Esa agrupación **existía solo en la UI**: en la BD cada fila de `movimientos` ya era una placa + un proveedor. Sostenerla costaba ~600 líneas: `GrupoCamionPesaje`, `GruposCamiones`, `SincronizarGrupos`, `EsPlacaVacia`, el modo `soloPlaca` de `CamionModal` y `ActualizarPlacaCamionAsync`. Este último hacía N updates **sin transacción** para propagar la placa a las "hermanas".
- **Inconsistencia de cupo:** el tope de 5 se contaba por *placa distinta* (`PlacasAbiertas`), pero el contador y el botón del modal contaban *filas*.
- **Las reglas eran solo de pantalla.** Ni `registrar_camiones_lote_seguro` ni la edición validaban duplicados ni el cupo, así que dos terminales a la vez podían pasarse de 5 o duplicar placa + proveedor. Anular una recepción con productos tampoco lo rechazaba el servidor.

Pedido de Fernando: tabla con placa y proveedor. Si un camión trae carga de 3 proveedores, se ingresa 3 veces con la misma placa (máximo 5 ingresos). La placa solo se repite con otro proveedor. Cada fila con su propio KG manifestado, sin total por placa. El reporte unifica por placa, con una interfaz para elegir una placa, varias o todas.

## Cambios aplicados

### Base de datos — `supabase/migrations/20260918194910_regla_recepciones_abiertas_camiones.sql`

La migración está aplicada en `Bimbo_Pesaje` y registrada como `20260918194910 regla_recepciones_abiertas_camiones`.

- **Trigger** `trg_validar_recepcion_movimiento`: `BEFORE INSERT OR UPDATE OF placa_vehiculo, id_proveedor, id_estado`. Ejecuta `private.validar_recepcion_movimiento()`, que es `SECURITY DEFINER`, tiene `search_path` fijo y `REVOKE ALL FROM public, anon, authenticated`. La función:
  - normaliza `placa_vehiculo = upper(btrim())`;
  - rechaza anular (→ 9) una recepción con productos en estado 7 u 8 — antes era solo un cerrojo de pantalla;
  - toma `pg_advisory_xact_lock(hashtext('pesaje:recepciones_abiertas'))` y rechaza placa + proveedor repetidos entre recepciones abiertas;
  - rechaza abrir una sexta recepción. El cupo solo lo consume un alta, nunca editar una ya abierta.
- **Índice único parcial** `ux_movimientos_abierto_placa_proveedor` sobre `(upper(btrim(placa_vehiculo)), id_proveedor) WHERE id_estado = 7`: la última red ante cualquier carrera.
- Se eligió **un trigger y no tocar cada RPC**: cubre en una sola pieza el lote, el alta simple, la edición, el alta automática del RPC legado `ingresar_producto_recepcion_…` y cualquier camino futuro. La decisión está en [[ADR-029 - Recepciones de pesaje planas con reglas en trigger de tabla]].
- **No se creó ninguna vista.** Con KG = manifestado por fila y sin total por placa, la carga sigue siendo de 2 consultas sin N+1. Una vista agregaría `security_invoker`, GRANTs y recreación en cada `ALTER` para ahorrar un solo round trip.

### Dominio — `CapaDominio/Reglas/ReglasEntidades.cs`

`ReglasCamion` suma:
- `MaxRecepcionesAbiertas = 5`;
- `NormalizarPlaca`;
- `record RecepcionClave(int? Id, string Placa, int IdProveedor, int Fila)`;
- `ValidarRecepciones(abiertas, propuestas)`: una función pura para R4 y R5, con mensajes que llevan el número de fila. Las filas editadas reemplazan a su original, así que no chocan consigo mismas.

El `5` queda duplicado a propósito con el trigger, con un comentario cruzado en ambos lados, como los largos de ADR-021.

### Datos — `CapaDatos/Repositories/Pesaje/PesajeRepository.cs`

- `ConMensajeDelServidor` desenvuelve la `PostgrestException` para que el operador vea el mensaje del trigger y no `{"code":"P0001",...}`. Un `23505` del índice se traduce al mismo texto de negocio.
- `RegistrarCamionesLoteAsync` y `ActualizarCamionAsync` normalizan la placa con `ReglasCamion.NormalizarPlaca`.
- Se eliminó `CrearCamionAsync`, también de `IPesajeRepository`, porque quedó sin uso.

### UI — `CapaUI/Formularios/Principal/Pantallas/Pesaje/`

- **`PesajeView.xaml`:** el `ItemsControl` anidado se reemplazó por `DataGrid x:Name="DgCamiones"`.
  - Columnas: `#` (`PlantillaCeldaNumeroFila`), PLACA (96), PROVEEDOR (`*`), KG (80), ESTADO (90, en gris si está cerrado) y basurero (`BasureroCelda`).
  - Sigue el Informe de DataGrids: anchos fijos o `*`, `RowHeight=40`, virtualización con `Recycling` y `ScrollUnit=Item`, fondo blanco y `ClearTypeHint`.
  - `SelectedItem` va `OneWay` con `SelectedCamion`.
- **`PesajeView.xaml.cs`:**
  - se eliminaron `GrupoCamion_Click`, `RecepcionProveedor_Click`, `BtnQuitarGrupo_Click`, `QuitarGrupoFlujo` y `AbrirCamionModal`;
  - se agregaron `DgCamiones_SelectionChanged` y `DgCamiones_DoubleClick`;
  - «Agregar», «Editar» y el doble clic abren `RegistroCamionesModal`;
  - el botón dice siempre «Cerrar camión».
- **`PesajeViewModel.cs`:**
  - se eliminaron `GruposCamiones`, `HayPlacaCompartida`, `RecalcularRecepcionesPorPlaca`, `SincronizarGrupos`, `ActualizarSeleccionGrupos`, `ActualizarPlacaCamionAsync`, `QuitarCamionCompletoAsync` y la rama «cascarón» de `QuitarCamionAsync`;
  - el cupo pasa a contar filas (`CamionesActivos`) y `MaxCamiones = ReglasCamion.MaxRecepcionesAbiertas`;
  - se agregó `Page => 1`, que es el contrato de la columna `#`;
  - `GuardarCamionAsync` edita solo esa fila.
- **`Modelos/PesajeModels.cs`:** se eliminaron `GrupoCamionPesaje`, `RecepcionesEnPlaca`, `PlacaCompartida` e `IsSelected`.
- **`Modales/RegistroCamionesModal`:**
  - el cupo y los duplicados delegan en `ReglasCamion.ValidarRecepciones`;
  - se eliminaron `placaFija`, el aviso ámbar (`RevisarPlacas`/`PanelAviso`), `PlacasNuevas` y `SinDuplicados`;
  - `Modificada` pasó a `ChangeTracker<Snapshot>` (regla 14 de `AGENTS.md`);
  - el selector de proveedor sigue atenuando los proveedores que esa placa ya tiene.
- **`Modales/CamionModal` eliminado.** Tras el cambio nada lo abría.
- **`Modales/ReporteModal`:**
  - el alcance se elige **por placa**, con un `CheckBox` por placa (`OpcionPlacaReporte`, `[ObservableProperty] Incluida`) más «Todas» y «Ninguna»;
  - muestra un resumen del tipo «Se incluyen 2 camiones (3 recepciones)»;
  - PDF y Excel se deshabilitan si no hay ninguna placa marcada.

  Se abre con la placa seleccionada ya marcada («Imprimir reporte») o con las placas cerradas («Cerrar todos»).
- **`GenerarReportePesajesAsync`:**
  - ordena por placa, luego proveedor, luego producto;
  - los metadatos hablan de camiones (placas) y recepciones;
  - la auditoría registra `cantidad_camiones` (placas) y `cantidad_recepciones`.

  El motor de reportes no se tocó: no tiene filas de subtotal.

> [!note] Desvío consciente del plan
> «Cerrar camión» **no** abre el reporte automáticamente. Ya se había separado a propósito en una sesión anterior («Imprimir reporte» es independiente). Si se quiere, es una línea en `BtnCamionCerrar_Click`.

## Reglas vigentes (R1–R9)

| # | Regla | Dónde |
|---|---|---|
| R1 | La placa es obligatoria, de 20 caracteres como máximo, y se normaliza con `upper(trim)` | Dominio · UI · BD |
| R2 | El proveedor es obligatorio y debe estar activo | Dominio · UI · RPC |
| R3 | La descripción tiene 500 caracteres como máximo | Dominio · UI · BD |
| R4 | Como máximo 5 recepciones abiertas, **por fila** | Dominio · trigger con advisory lock |
| R5 | Placa + proveedor únicos entre las abiertas; misma placa con otro proveedor ✅ | Dominio · trigger · índice único |
| R6 | Editar cambia solo esa fila y usa `ChangeTracker` | UI · trigger |
| R7 | Quitar solo sin productos | UI · trigger |
| R8 | Cerrar cierra solo esa fila | RPC existente |
| R9 | El reporte se elige por placa y sale en un solo archivo | `ReporteModal` · VM |

## Verificación

- **SQL en base viva:** pre-chequeo sin duplicados abiertos. Se hizo una prueba en un bloque `DO` con rollback, sin dejar datos:
  - `' zz-test '` se guarda como `ZZ-TEST`;
  - misma placa con otro proveedor → ok;
  - duplicado → «La placa ZZ-TEST ya tiene una recepción abierta con ese proveedor»;
  - 6.ª alta → «Ya hay 5 camiones abiertos…»;
  - editar hacia un duplicado → rechazado;
  - editar observaciones → ok.
- Registro verificado en `supabase_migrations.schema_migrations`, `pg_trigger` y `pg_indexes`. `get_advisors` (security) no marca los objetos nuevos.
- `dotnet build BimboProyecto.sln` → **0 errores, 0 advertencias**. `dotnet test` → **565 tests en verde**, 12 de ellos nuevos en `BimboProyecto.Tests/Dominio/ReglasCamionRecepcionesTests.cs`.
- **Arné de instanciación** (§8 de Convenciones de UI): `PesajeView`, `RegistroCamionesModal` y `ReporteModal` cargan sin colapsar.
  - `DgCamiones` con datos de ejemplo renderiza 3 filas con #, placa repetida, proveedor, KG y estado.
  - `ReporteModal` con datos marca la placa inicial, actualiza el resumen y deshabilita PDF con «Ninguna».
  - El arné detectó y se corrigió un plural mal armado («camiónes»).
- **Prueba visual y aprobación de Fernando** en la app real.

## Lo que NO cambió

- El modelo de datos: `movimientos` ya era una fila por placa + proveedor.
- Los RPC de alta, edición y estado conservan su firma y comportamiento; las reglas nuevas las aplica el trigger.
- El cálculo de KG (manifestado) y el motor de reportes (sin subtotales por placa).
- El reporte solo junta recepciones que están en pantalla. No busca recepciones cerradas de días anteriores con la misma placa.

---

## Relaciones

- [[Módulo Pesaje]]
- [[ADR-029 - Recepciones de pesaje planas con reglas en trigger de tabla]]
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]
- [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] — el alta en tabla que esta sesión simplifica
- [[Informe de Optimización de DataGrids y Bug de Salto de Columnas]] — reglas de columnas y virtualización aplicadas a `DgCamiones`
- [[Columna de Numero de Fila en DataGrid]]
- [[Arquitectura Actual]]
