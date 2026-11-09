#!/usr/bin/env bash
# Compile the real alien generator with Mono (no Unity needed) and draw every alien.
#   bash tools/preview_aliens/preview.sh [output.png]
# Needs: mono-mcs (apt install mono-mcs), python3 with numpy and Pillow.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$(cd "$HERE/../.." && pwd)"
TMP="$(mktemp -d)"
OUT="${1:-$PROJECT/aliens.png}"
mcs -nologo -langversion:Latest -define:UNITY_EDITOR -out:"$TMP/preview.exe" \
    "$HERE/UnityStub.cs" "$HERE/Preview.cs" "$PROJECT/Assets/XenoDial/Editor/XenoDialAliens.cs"
mono "$TMP/preview.exe" "$PROJECT/Assets/XenoDial/Data/aliens.txt" "$TMP/aliens.json"
python3 "$HERE/render.py" "$TMP/aliens.json" "$OUT"
rm -rf "$TMP"
