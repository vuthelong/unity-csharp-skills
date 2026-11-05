#!/bin/sh
FILE="$1"
WAIT="${2:-300}"
DIR="${PARTICLE_ATLAS_OUT_DIR:-Temp/particle-atlas}"
OUT="$DIR/out.txt"
if [ -z "$FILE" ] || [ ! -f "$FILE" ]; then echo "usage: sh run.sh <snippet.cs> [timeout_seconds]"; exit 2; fi
mkdir -p "$DIR"
rm -f "$OUT"
REPLY="$(unity command --no-banner --result-only --timeout 120 eval_file --file "$FILE" 2>&1)"
if echo "$REPLY" | grep -q "Compilation Failed\|error CS"; then echo "$REPLY" | head -40; exit 1; fi
i=0
while [ ! -f "$OUT" ] && [ $i -lt "$WAIT" ]; do sleep 2; i=$((i + 2)); done
if [ -f "$OUT" ]; then cat "$OUT"; grep -q "^EXC" "$OUT" && exit 1; exit 0; fi
echo "No result after ${WAIT}s. Is the Editor open and focused enough to run? Reply was:"
echo "$REPLY" | head -20
exit 1
