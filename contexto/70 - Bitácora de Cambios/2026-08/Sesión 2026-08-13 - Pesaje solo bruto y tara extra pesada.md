---
title: "Sesión 2026-08-13 — Pesaje: solo peso bruto y tara extra pesada"
tags:
  - sesion
  - bimbo
  - pesaje
  - wpf
date: 2026-08-13
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude
---

# Sesión 2026-08-13 — Pesaje: solo peso bruto y tara extra pesada

> [!success] Resultado
> El modal de pesaje pasó a capturar **un solo dato**: el peso bruto. Los bultos ahora se estiman desde el peso, y la tara extra dejó de pedirse por adelantado en el wizard — se **pesa** (nunca se calcula) y se puede cargar en cualquier momento, por pesada o como total repartido entre las pesadas. Los camiones cerrados salieron de la pantalla.

---

## Problema / motivo

El flujo pedía dos datos por pesada (bruto + bultos) y exigía la tara extra como paso 3 del wizard, antes de la primera pesada. Nada de eso coincidía con la operación real:

1. **Los bultos no se cuentan a mano** — se deducen del peso. Si se pesan 20 kg y cada unidad (producto + embalaje) pesa 2 kg, son ~10 bultos. Es un indicador aproximado, no un dato medido.
2. **La tara extra no se puede calcular: se pesa.** El peso de una tarima varía (madera, plástico). Las tarimas se juntan, se pesan en bloque, y ese peso se resta. Si no se pueden pesar todas juntas, se pesan por partes y se suman.
3. **No se conoce al empezar.** Pedirla en el paso 3 del wizard obligaba a inventar un número o dejarla en 0.
4. Los camiones cerrados seguían en pantalla sin necesidad, hasta que exista la sección de históricos.

## El hallazgo que habilitó el cambio

El trigger `calcular_pesos_entrada` **no usa `numero_bultos_recibido`**: calcula `peso_tara_individual` plana desde el catálogo, y de ahí `peso_tara_total` y `peso_neto` a partir del bruto y la tara extra. El **único** punto donde los bultos afectaban el neto guardado era el prorrateo que hacía la app (`TaraExtraPorBulto × bultos`). Al eliminar ese prorrateo, dejar de capturar bultos no toca el neto — que es lo que se le paga al proveedor.

## Decisiones de diseño

| Tema | Decisión |
|---|---|
| Fuente de verdad de la tara extra | `entradas_producto.peso_tara_extra` (columna que ya existía). El total **nunca se persiste**: es `Σ` de las entradas. Sin migraciones de esquema. |
| `movimientos.peso_tara_extra` | **Congelada**: se lee para reconocer camiones legado, no se escribe nunca más. |
| Reparto | Promedio simple, con el residuo del redondeo en la última cuota para que `Σ cuotas == total` exacto. |
| Alcance del reparto | Producto o camión entero — la misma operación sobre distinto conjunto. El promedio uniforme ya reparte proporcionalmente a la cantidad de pesadas, que es el proxy natural de cantidad de tarimas; no hizo falta inventar una regla de asignación entre productos. |
| Bultos | `numero_bultos_recibido` queda **NULL** en las entradas nuevas: no se persiste una estimación como si fuera un dato medido. |

## Cambios aplicados

### Cálculo — `Modelos/PesajeModels.cs`
- `PesajeCalc.BultosTeoricos` cambió de firma y fórmula: `(bruto − tara_extra) / (peso_teorico + tara_empaque)`.
- Nuevos: `BultosSonAproximados`, `RepartirTaraExtra` (reparto exacto), `TaraExtraMaximaRepartible` (techo antes de violar el CHECK).
- Se eliminaron `TaraExtraPorBulto` y `CalcTaraInd` (esta última ya era código muerto).
- `EntradaPesaje`: se fue `Bultos`; entraron `BultosTeoricos` (calculado), `BultosCapturados` (`int?`, dato del flujo viejo), `BultosTexto` y `BultosAproximados`.
- `ProductoCamion`: nuevos `TaraExtraRegistrada`, `PesadasSinTaraExtra`, `FaltaTaraExtra`, `BultosEstimados`.
- `CamionPesaje`: `TaraExtraTotal` → `TaraExtraLegado` (solo lectura) + `EsLegado`; se fueron `BultosDeclaradosTotal`, `TaraExtraPorBulto`, `FaltaTaraExtra` y `NotificarTaraExtra()`.

### Persistencia
- `CrearEntradaAsync` perdió el parámetro `bultos` y escribe `numero_bultos_recibido = null`.
- Nuevo `ActualizarTaraExtraEntradaAsync`: **UPDATE en el lugar** (no anular+insertar — eso reemitiría id, fecha, hora y usuario de N pesadas y mataría la auditoría).
- `CrearCamionAsync`/`ActualizarCamionAsync` perdieron `taraExtraTotal`. **`ActualizarCamionAsync` ya no escribe `peso_tara_extra`**: sin esto, editar un camión legado le borraba su tara histórica.
- `EntradaDto.Bultos` → `BultosCapturados` (`int?`), leído sin `?? 0` para distinguir "entrada nueva" de "entrada vieja con 0".
- `GetCamionesActivosAsync` filtra solo `Abierto`.

### UI
- **`PesajeModal`**: `TxtBultos` → `TxtTaraExtraEntrada` (opcional). Nuevo `TxtTaraTotal` y `TxtBultosEstimados`. `Valido` ahora exige `neto > 0` (el CHECK de la BD llegaba como excepción cruda de Postgrest). Dos banners: ámbar "bultos aproximados", rojo "neto en cero o negativo". Al editar se precarga la tara extra que la pesada ya tenía.
- **`TaraExtraTotalModal`** (nuevo): selector de alcance (producto / camión) y de modo (total de una vez / sumar parciales), tarjeta de impacto en vivo y pre-vuelo del CHECK que deshabilita el botón antes de intentar escribir.
- **`ProcesoDescargaModal`**: wizard de 3 → 2 pasos; se eliminó la sección de tara extra y `ResultadoProceso` perdió `TaraExtraTotal`.
- **`PesajeView`**: botón «Tara extra» en la barra de productos (la botonera del camión es un `UniformGrid` 2×2 y un quinto botón la rompía), aviso ámbar con las pesadas sin tara, columna «BULTOS (EST.)» con `DataTrigger` que la pinta en ámbar cuando es aproximada. Se eliminaron `BtnVerCerrados`, `_verHistorico`, `HayCerrados` y `CerradosCount`.
- Se borró el código muerto `PesajeViewModel.RegistrarCamionAsync` / `ActualizarCamionAsync` (sin llamadores desde que se unificó todo en `GuardarProcesoAsync`).

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**, con el modal nuevo incluido.
- `dotnet build CapaDatos/CapaDatos.csproj` → **0 errores**.
- Se cruzaron todos los `StaticResource` de `TaraExtraTotalModal.xaml` contra `PesajeModalStyles.xaml`: los 8 existen. Es la comprobación que evita el `XamlParseException` en runtime que documenta este mismo módulo (`ModalSegBtn` vs `MSegBtn`) — un build verde no la cubre.
- Sin referencias muertas: `TaraExtraPorBulto`, `CalcTaraInd`, `BultosDeclaradosTotal`, `NotificarTaraExtra`, `HayCerrados`, `_verHistorico` ya no aparecen en el código.

> [!warning] Sin verificación en runtime
> No se ejecutó la aplicación (bloqueaba los DLL durante la sesión). Falta probar de punta a punta: pesar con y sin tara extra, repartir un total por producto y por camión, forzar el pre-vuelo con una pesada de bruto chico, y confirmar que editar un camión legado **no** cambia `movimientos.peso_tara_extra`.

## Lo que NO cambió

- **Nada del histórico**: los camiones ya cerrados y sus pesadas quedan intactos, con su tara prorrateada y sus bultos capturados.
- No se tocó el esquema de Supabase ni el trigger — el rediseño no necesitó ninguna migración.
- `ProductoCamion.BultosRecibidos` / `BultosRestantes` siguen calculándose como proporción del manifiesto sobre el **peso** recibido. No dependían del input que se quitó, así que se dejaron como estaban; convive con el indicador nuevo de bultos estimados.
- Las observaciones por pesada se mantienen (dato de auditoría, no de cálculo).

## Deuda generada

- **P-032** 🔴 — el reparto de tara extra son N updates sin transacción. Mitigado con pre-vuelo y con que la operación es auto-reparable, pero la solución de fondo es un RPC.
- **P-033** — falta verificar en la BD si el trigger cubre `UPDATE`, si `peso_tara_total`/`peso_neto` son `GENERATED ALWAYS`, y si RLS permite `UPDATE` de `entradas_producto`. Mitigado escribiendo los derivados desde el cliente.
- **P-024** no se cerró: cambió de forma y ahora es más visible (ver la nota actualizada en Deuda Técnica).

---

## Relaciones

- [[Módulo Pesaje]] — nota del módulo, actualizada con el flujo nuevo
- [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] — el rediseño anterior, que introdujo el prorrateo que acá se elimina
- [[Deuda Técnica - Pendientes]] — P-032, P-033; P-024 y P-023 siguen abiertos
