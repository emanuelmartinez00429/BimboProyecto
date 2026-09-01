#!/usr/bin/env bash
# Corre la bateria de preguntas fijas y guarda el uso real de cada corrida.
# Cada invocacion con -p es una sesion NUEVA: no hay contexto arrastrado
# entre preguntas, que es lo que hace comparables las corridas.
#
# uso:  bash medir-bateria.sh <etiqueta> [repeticiones]
#       bash medir-bateria.sh hoy 3
set -euo pipefail

ETIQUETA="${1:?falta la etiqueta, ej: hoy | con-db}"
REPS="${2:-3}"
DIR="$(cd "$(dirname "$0")" && pwd)"
SALIDA="medicion/bateria-${ETIQUETA}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$SALIDA"

TOTAL=$(grep -c . "$DIR/bateria.jsonl")
echo "== bateria: $ETIQUETA · $TOTAL preguntas x $REPS repeticiones =="

for r in $(seq 1 "$REPS"); do
  n=0
  while IFS= read -r linea; do
    [ -z "$linea" ] && continue
    n=$((n+1))
    id=$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).id)' "$linea")
    preg=$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).pregunta)' "$linea")
    printf "  rep %s · %-5s (%s/%s) ... " "$r" "$id" "$n" "$TOTAL"
    claude -p "$preg" --output-format json --model sonnet \
      > "$SALIDA/${id}-r${r}.json" 2>"$SALIDA/${id}-r${r}.err" || echo -n "(fallo) "
    echo "ok"
  done < "$DIR/bateria.jsonl"
done

node "$DIR/comparar.js" bateria "$SALIDA" "$DIR/bateria.jsonl"
echo
echo "Crudos en: $SALIDA"
