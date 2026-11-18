#!/usr/bin/env bash
# Builds the real world with FirWorldBuilder inside the simulator and plays it. Needs: mono-mcs mono-runtime
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(mktemp -d)"
python3 "$HERE/build_hoist.py" "$HERE/../../Assets/FirHorror/Editor/FirWorldBuilder.cs" "$OUT/FirWorldBuilder.cs"
mcs -langversion:7.2 -d:UNITY_EDITOR -nowarn:0169,0414,0649,0219,0067,0108,0114 -out:"$OUT/world.exe" \
    "$HERE/sim_runtime.cs" "$HERE/sim_editor.cs" "$OUT/FirWorldBuilder.cs" "$HERE/sim_world.cs" "$HERE/../../Assets/FirHorror/Scripts/"*.cs
FIR_PROJECT="$HERE/../.." mono "$OUT/world.exe" "$@"
