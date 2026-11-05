#!/bin/sh
OUTDIR="Temp/particle-vfx"
OUT="$OUTDIR/out.txt"
JOB="$OUTDIR/job.cs"
WAIT="${VFXGEN_WAIT:-180}"
mkdir -p "$OUTDIR"

if [ -z "$1" ]; then
  echo "usage: vfxgen.sh build|preview|check|catalog|compile ..."
  exit 2
fi

if [ "$1" = "compile" ]; then
  unity command --no-banner --result-only --timeout 60 eval --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null 2>&1
  unity command --no-banner --result-only --timeout 60 recompile >/dev/null 2>&1
  sleep 5
  i=0
  while [ $i -lt 180 ]; do
    S="$(unity command --no-banner --result-only --timeout 10 recompile_status 2>&1)"
    if echo "$S" | grep -q '"compilationFailed": true'; then echo "$S" | head -60; exit 1; fi
    if echo "$S" | grep -q '"status": "\(completed\|up_to_date\)"'; then echo "compiled OK"; exit 0; fi
    sleep 3; i=$((i + 3))
  done
  echo "recompile did not finish: $S"; exit 1
fi

ARGS=""
for a in "$@"; do
  esc=$(printf '%s' "$a" | sed 's/"/""/g')
  ARGS="$ARGS${ARGS:+, }@\"$esc\""
done
cat > "$JOB" <<CS
var r = ParticleVfx.Gen.Editor.ParticleVfxCli.Run($ARGS);
System.IO.Directory.CreateDirectory("$OUTDIR");
System.IO.File.WriteAllText("$OUT", r);
return "done";
CS
rm -f "$OUT"
REPLY="$(unity command --no-banner --result-only --timeout 120 eval_file --file "$JOB" 2>&1)"
if echo "$REPLY" | grep -q "Compilation Failed"; then echo "$REPLY" | head -40; exit 1; fi
i=0
while [ ! -f "$OUT" ] && [ $i -lt "$WAIT" ]; do sleep 1; i=$((i + 1)); done
if [ -f "$OUT" ]; then cat "$OUT"; grep -q "^ERROR\|^EXC" "$OUT" && exit 1; exit 0; fi
echo "No result after ${WAIT}s (is the Editor open and not in Play mode?). Reply was:"
echo "$REPLY" | head -20
exit 1
