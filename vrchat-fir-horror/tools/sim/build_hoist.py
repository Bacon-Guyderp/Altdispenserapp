# mcs doesn't support C# local functions; lift FirWorldBuilder's out (same as tools/verify/check_code.sh)
import re, sys
src, dst = sys.argv[1], sys.argv[2]
s = open(src).read(); hoisted = []
a = s.index('        TreeParts GagTree('); b = s.index('        switch (kind)', a)
hoisted.append('    static ' + s[a:b].strip() + '\n'); s = s[:a] + s[b:]
m = re.search(r'        void A\(string n, string tex, Color c, float full = 0, Color\? glow = null\) => [^\n]*\n', s)
hoisted.append('    static ' + m.group(0).strip() + '\n'); s = s[:m.start()] + s[m.end():]
a = s.index('        bool Bad(string folder'); b = s.index('        var opts = ImportAssetOptions', a)
hoisted.append('    static ' + s[a:b].strip() + '\n'); s = s[:a] + s[b:]
i = s.rindex('}\n#endif'); s = s[:i] + '\n'.join(hoisted) + s[i:]
open(dst, 'w').write(s)
