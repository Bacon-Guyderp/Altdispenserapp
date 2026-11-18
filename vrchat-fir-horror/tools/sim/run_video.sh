#!/usr/bin/env bash
# Plays Fir Warning in the simulator with walking players and renders it as a top-down video.
# Usage: bash tools/sim/run_video.sh [out.mp4]   Needs: mono-mcs mono-runtime ffmpeg python3-pil
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(mktemp -d)"
mcs -langversion:7.2 -nowarn:0169,0414,0649,0219,0067,0108,0114 -out:"$OUT/sim.exe" "$HERE/sim_runtime.cs" "$HERE/sim_editor.cs" "$HERE/sim_test.cs" "$HERE/../../legacy/Scripts/"*.cs
FIR_TRACE="$OUT/trace.jsonl" mono "$OUT/sim.exe" | tail -1
python3 "$HERE/render_video.py" "$OUT/trace.jsonl" "${1:-fir_warning_playthrough.mp4}"
echo "wrote ${1:-fir_warning_playthrough.mp4}"
