# Medir de verdad si la bóveda compilada mejora tokens y contexto

Todo lo que sigue usa el tokenizador real de Claude y el costo que reporta
Claude Code. Nada de estimar bytes ÷ 3,6 — que es lo que hice yo hasta ahora
y por eso el 3,4K del plan es una **proyección, no una medición**.

## Antes de medir: escribí la predicción

Si no fijás el número de antemano, después racionalizás cualquier resultado.
Anotá esto en `contexto/70 - Bitácora/` **antes** de tocar nada:

| | Predicción | Se cumple si |
|---|---|---|
| Costo fijo de arranque | 6,8K → ≤ 4K tok | `medir-arranque.sh` lo confirma |
| Contexto por tarea (batería) | −60 % o más | `comparar.js ab` |
| Respuestas correctas | **no baja** | guardarraíl, no objetivo |
| Tokens por respuesta correcta | −40 % o más | el veredicto |

Si el acierto baja, el cambio falló aunque los tokens bajen. Bajar tokens
rompiendo respuestas es trivial y no sirve de nada.

## Métrica 1 — el costo fijo de arranque (exacto, hoy, ~$0,05)

```bash
cd "/d/Proyectos/Proyecto de BIMBO/BimboProyecto"
bash scripts/medir/medir-arranque.sh hoy
```

Manda un prompt cuya respuesta cuesta ~5 tokens, así que casi todo lo que se
cobra es overhead fijo: system prompt, herramientas, AGENTS.md y lo que
inyecta el hook `SessionStart`. Lo corre 3 veces normal y 3 veces con `--bare`
(que salta hooks, CLAUDE.md y skills).

**La resta es el número que nunca tuviste**: `normal − bare` = exactamente lo
que te cuesta tu contrato de bóveda al arrancar, con el tokenizador real.

Después del cambio: `bash scripts/medir/medir-arranque.sh con-db` y comparás.

## Métrica 2 — la batería (el experimento de verdad)

12 preguntas reales del proyecto, con respuesta conocida, en `bateria.jsonl`.

```bash
bash scripts/medir/medir-bateria.sh hoy 3        # ANTES  (~$3-6)
# ... aplicás F3.1 ...
bash scripts/medir/medir-bateria.sh con-db 3     # DESPUÉS
node scripts/medir/comparar.js ab \
     medicion/bateria-hoy-*  medicion/bateria-con-db-*  scripts/medir/bateria.jsonl
```

Cada pregunta corre con `claude -p`, que abre una **sesión nueva**: no hay
contexto arrastrado de una pregunta a otra, que es lo que hace comparables las
corridas. 3 repeticiones porque el modelo no es determinista — se reporta la
mediana, no una corrida suelta.

Fijá el modelo (los scripts usan `--model sonnet`). El **ratio** entre antes y
después se traslada a Opus; los tokens de entrada son idénticos.

### Cuidado con el caché
Si corrés las 36 llamadas de un brazo seguidas y después las del otro, el
segundo brazo aprovecha caché caliente y parece más barato de mentira. Por eso
`comparar.js` separa `cacheLeido` de entrada fresca y suma **contexto**
(entrada + caché escrito + caché leído). Si querés ser estricto, corré los dos
brazos intercalados en días distintos.

## Métrica 3 — producción, longitudinal

Para confirmar que el efecto sobrevive al uso real, no solo a la batería:

```bash
export CLAUDE_CODE_ENABLE_TELEMETRY=1
export OTEL_METRICS_EXPORTER=console
```

Claude Code emite `claude_code.token.usage` (atributo `type`:
input / output / cacheRead / cacheCreation) y `claude_code.cost.usage` en USD,
ambos con `model`, `effort` y `agent.name`. Redirigís la consola a un archivo y
tenés el histórico por sesión sin montar nada.

Dentro de una sesión, `/usage` muestra lo mismo en vivo: tokens por modelo
separados en entrada, salida, caché leído y caché escrito, con el costo.

**No parsees `~/.claude/projects/**/*.jsonl`.** Tiene los datos, pero el formato
es interno y cambia entre versiones: un script que lo lea se rompe solo.

### Los tres confundidores reales
- **Modelo.** Opus y Sonnet cuestan 2,5× distinto. Segmentá por modelo o
  compará en dólares, nunca tokens crudos mezclados.
- **Tamaño de tarea.** Una sesión que refactoriza 20 archivos no se compara con
  una que arregla un typo. Por eso la batería tiene tareas fijas: ahí el tamaño
  está controlado por construcción. En producción usá la mediana de ≥30 sesiones.
- **Aprendizaje.** La primera semana con el sistema nuevo no es representativa.
  Descartala.

## Qué esperar de verdad

Sé honesto con lo que cada cosa aporta:

- **El arranque baja poco en dólares.** Se cachea, así que 3,4K tokens menos son
  centavos por sesión. Lo que gana es **presupuesto de ventana**: 3,4K que dejan
  de estar ocupados permanentemente, y eso pesa en sesiones largas.
- **Lo que de verdad cuesta es recuperar.** Hoy contestar «¿qué notas describen
  este archivo?» significa leer `INDEX-codigo.md` entero (20 KB ≈ 5,5K tokens) o
  grepear y abrir varias notas. Con el índice compilado son ~200 tokens.
  **Ahí está el 80 % de la mejora, y por eso la batería es la medición que importa.**

Si la batería no muestra al menos −40 % en tokens por respuesta correcta,
la propuesta no se sostiene y hay que decirlo.
