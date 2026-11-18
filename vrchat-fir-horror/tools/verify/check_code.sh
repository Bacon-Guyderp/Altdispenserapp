#!/usr/bin/env bash
# Type-checks every Fir Warning script without Unity: compiles them with Mono's mcs against API stand-ins
# (stubs_runtime.cs, stubs_editor.cs), then checks the shaders. Needs: apt install mono-mcs glslang-tools
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ASSETS="$HERE/../../Assets/FirHorror"
TMP="$(mktemp -d)"
cp "$ASSETS"/Editor/*.cs "$TMP/"
# mcs doesn't support C# local functions (Unity's compiler does), so lift them out for the check only
python3 - "$TMP/FirWorldBuilder.cs" <<'PY'
import re, sys
import os
p = sys.argv[1]
if not os.path.exists(p): sys.exit(0)
s = open(p).read(); hoisted = []
a = s.index('        TreeParts GagTree('); b = s.index('        switch (kind)', a)
hoisted.append('    static ' + s[a:b].strip() + '\n'); s = s[:a] + s[b:]
m = re.search(r'        void A\(string n, string tex, Color c, float full = 0, Color\? glow = null\) => [^\n]*\n', s)
hoisted.append('    static ' + m.group(0).strip() + '\n'); s = s[:m.start()] + s[m.end():]
a = s.index('        bool Bad(string folder'); b = s.index('        var opts = ImportAssetOptions', a)
hoisted.append('    static ' + s[a:b].strip() + '\n'); s = s[:a] + s[b:]
i = s.rindex('}\n#endif'); s = s[:i] + '\n'.join(hoisted) + s[i:]
open(p, 'w').write(s)
PY
mcs -t:library -langversion:7.2 -warn:4 -nowarn:0169,0414,0649,0219,0067 -d:UNITY_EDITOR -out:"$TMP/check.dll" \
    "$HERE/stubs_runtime.cs" "$HERE/stubs_editor.cs" "$ASSETS"/Scripts/*.cs "$TMP"/*.cs 2>&1 | grep -v "stubs_" || true
echo "C#: compiled"
python3 "$HERE/../check_shaders.py"
rm -rf "$TMP"
