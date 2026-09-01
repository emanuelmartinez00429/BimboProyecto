---
title: Sesión 2026-08-29 — Predicción y medición del contrato de bóveda (Fase 3)
type: sesion
status: vigente
tags:
  - sesion
  - boveda
  - medicion
  - tokens
  - fase-3
date: 2026-08-29
updated: 2026-08-29
summary: "Instrumental de medición en scripts/medir/. Métrica 1 corrida: el contrato de bóveda al arranque cuesta 5.461 tokens reales, no los 6.8K proyectados. Falta la batería."
scope:
  - scripts/medir
symbols: []
branch: feat/fase8-MaquetadodeRoles-B-Fernando
autor_cambios: Fernando (agente)
---

# Sesión 2026-08-29 — Predicción y medición del contrato de bóveda (Fase 3)

> [!success] Resultado
> Instrumental de medición instalado en `scripts/medir/`. Predicciones de F3.1
> fijadas **antes** de medir. **Métrica 1 corrida**: el contrato de bóveda al
> arranque cuesta **5.461 tokens reales** (mediana de 3), no los 6.8K proyectados
> por bytes ÷ 3,6. Falta la batería (métrica 2), que es la que decide.

---

## Problema / motivo

El plan de reestructuración (fases 0-2, commit `7a7c17b`, ver
[[Pasos manuales — fases 0 a 2]] / `PASOS-MANUALES-20260823.md`) bajó la entrada en
frío de 132.161 B a 24.598 B. El "**3,4K tokens que ahorra**" que figura en el plan
es una **proyección** (bytes ÷ 3,6), no una medición con el tokenizador real.

Antes de aplicar **F3.1 (bóveda compilada / índice servido, brazo `con-db`)** hay que
medir de verdad: tokenizador real de Claude, costo que reporta Claude Code, sesiones
nuevas por pregunta. El instrumental lo provee `scripts/medir/` (ver
`scripts/medir/LEEME.md`).

## Predicciones fijadas (pre-registro)

| Métrica | Antes (proyectado) | Se cumple si | Cómo se verifica |
|---|---|---|---|
| Costo fijo de arranque | 6,8K tok | **≤ 4K tok** | `medir-arranque.sh` (`normal − bare`, mediana de 3) |
| Contexto por tarea (batería de 12) | línea base | **−60 % o más** | `comparar.js ab` |
| Respuestas correctas | línea base | **no baja** (guardarraíl, no objetivo) | `comparar.js ab` |
| Tokens por respuesta correcta | línea base | **−40 % o más** | veredicto de `comparar.js ab` |

Regla: si el acierto baja, F3.1 falló aunque los tokens bajen. Bajar tokens rompiendo
respuestas es trivial y no vale nada.

### Resultado métrica 1 — costo fijo de arranque

`bash scripts/medir/medir-arranque.sh hoy` · mediana de 3 · `--model sonnet` ·
crudos en `medicion/arranque-hoy-20260829-211423/`.

| Brazo | contexto (input + cache_creation + cache_read) | costo |
|---|---|---|
| `normal` (en el repo: base + `AGENTS.md` + hook + `INDEX.md`) | 47.326 tok | $0,1024 |
| `piso` (dir vacío: solo base + tools + CLAUDE.md global) | 41.865 tok | $0,0789 |
| **contrato de bóveda** (`normal − piso`) | **5.461 tok** | **$0,0236** |

Varianza mínima entre las 3 corridas (normal 47.321–47.329, piso 41.865–41.870).

**El número honesto es 5.461 tok, no 6.8K.** La proyección del plan (24.610 B ÷ 3,6)
sobreestimaba: el tokenizador real comprime mejor el markdown de los índices.
Contra el target **≤ 4K**, el margen de mejora en arranque es ~1,5K tok — modesto,
y encima se cachea ($0,024/sesión, menos en la práctica). Coincide con lo que
adelanta el LEEME: **el arranque aporta poco; el 80 % de la mejora está en recuperar
(la batería).**

> Nota: `--bare` no se pudo usar — en esta máquina no carga el login OAuth
> (`"Not logged in · Please run /login"`). El brazo `piso` se cambió a un
> `claude -p` normal desde un `mktemp -d` vacío. El script y `comparar.js` quedaron
> parcheados con esa lógica (comentario con fecha en `medir-arranque.sh`).

## Cambios aplicados

- `scripts/medir/` — instrumental movido acá desde `medir-boveda/` (venía de
  `medir-boveda.zip`, ya eliminado). Los `.sh` se autolocalizan con `dirname "$0"`,
  así que las rutas del LEEME (`scripts/medir/...`) ahora son correctas.
  - `medir-arranque.sh` — costo fijo de arranque. 3× `normal` + 3× `--bare`, resta.
  - `medir-bateria.sh` — 12 preguntas fijas (`bateria.jsonl`), N repeticiones, sesión
    nueva por pregunta (`claude -p`).
  - `comparar.js` — modos `arranque` / `bateria` / `ab`. Autodetecta los campos de
    tokens; verificado contra el JSON real de `claude` 2.1.251 (`usage.input_tokens`,
    `usage.cache_creation_input_tokens`, `usage.cache_read_input_tokens`,
    `total_cost_usd` en raíz). Etiquetas del modo `arranque` cambiadas de "--bare" a
    "piso (dir vacío)".
  - `LEEME.md`, `bateria.jsonl`.
- **Parche `medir-arranque.sh`** — `--bare` no carga el login OAuth en esta máquina.
  El brazo `piso` pasa a ser `claude -p` normal desde un `mktemp -d` vacío
  (`cd "$REPO"` para el brazo `normal`, `cd "$PISO"` para el `piso`). Comentario con
  fecha en el encabezado del script.

### Estado del contrato de arranque hoy (bytes, para referencia)

Lo que inyecta el hook `session-context.js` + lo que se lee como project instructions:

| Archivo | Bytes |
|---|---|
| `contexto/.control/handshake.md` | 1.753 |
| `contexto/INDEX.md` | 15.366 |
| `AGENTS.md` | 7.479 |
| **Total contrato** | **~24.598** |
| (no en arranque) `contexto/INDEX-codigo.md` | 20.054 |
| (no en arranque) `contexto/INDEX-bitacora.md` | 14.921 |
| (no en arranque) `contexto/CLAUDE.md` | 12.096 |

## Verificación

- Login de la máquina estaba vencido (`refreshToken` expiró 2026-08-23). Re-`/login`
  y `claude -p 'di ok' --output-format json` → `is_error: false`, `total_cost_usd`
  0,07. `claude -p` funciona en terminal y anidado.
- `bash -n` sobre los dos `.sh` → sintaxis ok.
- **Métrica 1 corrida** (ver sección arriba): 6/6 llamadas `is_error: false`.
- `node scripts/build-index.js` regenerado, `node scripts/verificar-boveda.js` →
  `Errores : 0`.
- **Métrica 2 (batería) pendiente** — la corre Fernando en terminal dedicada,
  separada en el tiempo por la higiene de caché del LEEME.

## Lo que NO cambió

- No se aplicó F3.1. La bóveda, los índices y el hook siguen como los dejó el commit
  `7a7c17b`.
- No se editó el LEEME (sus rutas ya eran correctas tras el `mv`).
- La batería (`medir-bateria.sh`, ~$5+, 36 llamadas) se corre en terminal dedicada y
  separada en el tiempo, por la higiene de caché que marca el LEEME — no dentro de una
  sesión de agente.

---

## Relaciones

- [[Arquitectura Actual]]
- `PASOS-MANUALES-20260823.md` — fases 0-2 del mismo plan
- `scripts/medir/LEEME.md` — spec de la medición
