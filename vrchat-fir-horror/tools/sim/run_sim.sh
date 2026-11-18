#!/usr/bin/env bash
# Runs the real Fir Warning game scripts with simulated players (no Unity needed). Needs: apt install mono-mcs mono-runtime
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(mktemp -d)"
mcs -langversion:7.2 -nowarn:0169,0414,0649,0219,0067,0108,0114 -out:"$OUT/sim.exe" "$HERE/sim_runtime.cs" "$HERE/sim_editor.cs" "$HERE/sim_test.cs" "$HERE/../../legacy/Scripts/"*.cs
mono "$OUT/sim.exe" "$@"
