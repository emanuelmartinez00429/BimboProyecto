#!/usr/bin/env bash
# Mide el costo FIJO de arranque: lo que pagás antes de que el agente
# haga nada. Usa el tokenizador real, no una estimación por bytes.
#
# Truco: se manda un prompt trivial cuya respuesta cuesta ~5 tokens de
# salida. Todo lo demás que se cobre es overhead fijo: system prompt,
# herramientas, CLAUDE.md/AGENTS.md y lo que inyecte el hook SessionStart.
#
# uso:  bash medir-arranque.sh [etiqueta]      (ej: "hoy" o "con-db")
#
# NOTA (2026-08-29): en esta maquina `--bare` NO carga el login OAuth
# ("Not logged in · Please run /login"), asi que el brazo "piso" se corre
# como `claude -p` NORMAL pero desde un directorio vacio sin proyecto:
# sin AGENTS.md, sin CLAUDE.md local, y el hook SessionStart no dispara.
#   normal (en el repo)  −  piso (dir vacio)  =  el contrato de boveda.
set -euo pipefail

ETIQUETA="${1:-sin-etiqueta}"
SALIDA="medicion/arranque-${ETIQUETA}-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$SALIDA"
PROMPT='Responde exactamente esta palabra y nada mas: ok'
REPO="$(pwd)"
PISO="$(mktemp -d)"; trap 'rm -rf "$PISO"' EXIT

echo "== arranque: $ETIQUETA =="
for i in 1 2 3; do
  echo -n "  normal  $i/3 ... "
  ( cd "$REPO" && claude -p "$PROMPT" --output-format json --model sonnet ) \
    > "$SALIDA/normal-$i.json" 2>"$SALIDA/normal-$i.err" || echo "(fallo)"
  echo "listo"

  echo -n "  piso    $i/3 ... "
  ( cd "$PISO" && claude -p "$PROMPT" --output-format json --model sonnet ) \
    > "$SALIDA/bare-$i.json" 2>"$SALIDA/bare-$i.err" || echo "(fallo)"
  echo "listo"
done

node "$(dirname "$0")/comparar.js" arranque "$SALIDA"
echo
echo "Crudos en: $SALIDA"
