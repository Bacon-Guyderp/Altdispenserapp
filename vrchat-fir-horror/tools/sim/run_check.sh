#!/usr/bin/env bash
# Builds the systems-check world with FirCheckBuilder inside the simulator and presses every test. Needs: mono-mcs mono-runtime
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(mktemp -d)"
mcs -langversion:7.2 -d:UNITY_EDITOR -nowarn:0169,0414,0649,0219,0067,0108,0114 -out:"$OUT/check.exe" \
    "$HERE/sim_runtime.cs" "$HERE/sim_editor.cs" "$HERE/sim_check.cs" "$HERE/../../Assets/FirHorror/Editor/FirCheckBuilder.cs" "$HERE/../../Assets/FirHorror/Editor/FirExpansion.cs" "$HERE/../../Assets/FirHorror/Editor/FirExpansion2.cs" "$HERE/../../Assets/FirHorror/Editor/FirExpansion3.cs" "$HERE/../../Assets/FirHorror/Editor/FirRooms.cs" "$HERE/../../Assets/FirHorror/Editor/FirOrlando.cs" "$HERE/../../Assets/FirHorror/Editor/FirRooms2.cs" "$HERE/../../Assets/FirHorror/Scripts/"*.cs
FIR_PROJECT="$HERE/../.." mono "$OUT/check.exe" "$@"
