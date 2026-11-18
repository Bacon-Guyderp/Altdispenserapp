#!/usr/bin/env bash
# Builds the systems-check world in the simulator, plays a scripted visit with two players, renders it
# in 3D with three.js and writes an MP4. Usage: bash tools/sim/run_flythrough.sh [out.mp4]
# Needs: mono-mcs mono-runtime, node + playwright (Chromium), ffmpeg, and three.module.js (three@0.160)
# next to flythrough.html, or set THREE_JS=/path/to/three.module.js.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(mktemp -d)"
OUTFILE="$(realpath -m "${1:-fir_warning_flythrough.mp4}")"
mcs -langversion:7.2 -d:UNITY_EDITOR -nowarn:0169,0414,0649,0219,0067,0108,0114 -out:"$OUT/fly.exe" \
    "$HERE/sim_runtime.cs" "$HERE/sim_editor.cs" "$HERE/sim_flythrough.cs" "$HERE/../../Assets/FirHorror/Editor/FirCheckBuilder.cs" "$HERE/../../Assets/FirHorror/Scripts/"*.cs
FIR_PROJECT="$HERE/../.." mono "$OUT/fly.exe" > "$OUT/scene.json"
cp "$HERE/flythrough.html" "$OUT/"
cp "${THREE_JS:-$HERE/three.module.js}" "$OUT/three.module.js"
(cd "$OUT" && python3 -m http.server 8778 >/dev/null 2>&1 & echo $! > "$OUT/server.pid")
sleep 1
NODE_PATH="$(npm root -g)" node "$HERE/render_flythrough.js" "$OUT/frames" 8778
kill "$(cat "$OUT/server.pid")" || true
python3 "$HERE/mix_audio.py" "$OUT/frames" "$HERE/../../Assets/FirHorror/Audio" "$OUTFILE"
