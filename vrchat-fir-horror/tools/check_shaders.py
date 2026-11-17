"""Type-checks the world's shaders without Unity: compiles each CGPROGRAM block as HLSL with glslang
(apt install glslang-tools), against a small stand-in for UnityCG.cginc (shadercheck_UnityCG.cginc).

  python check_shaders.py
"""
import os, re, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__))
src_dir = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Shaders")
import tempfile, shutil
out_dir = tempfile.mkdtemp()
shutil.copyfile(os.path.join(HERE, "shadercheck_UnityCG.cginc"), os.path.join(out_dir, "UnityCG.cginc"))
ok = True
for name in sorted(os.listdir(src_dir)):
    if not name.endswith('.shader'):
        continue
    text = open(os.path.join(src_dir, name)).read()
    body = re.search(r'CGPROGRAM(.*?)ENDCG', text, re.S).group(1)
    body = re.sub(r'#pragma.*', '', body)
    body = body.replace('sampler2D _MainTex;', 'Texture2D _MainTexT; SamplerState sampler_MainTexT;')
    body = body.replace('tex2D(_MainTex, i.uv)', '_MainTexT.Sample(sampler_MainTexT, i.uv)')
    hlsl = os.path.join(out_dir, name.replace('.shader', '.hlsl'))
    open(hlsl, 'w').write(body)
    for stage, entry in (('vert', 'vert'), ('frag', 'frag')):
        r = subprocess.run(['glslangValidator', '-D', '-V', '-S', stage, '-e', entry, '-I' + out_dir, '-o', os.devnull, hlsl],
                           capture_output=True, text=True)
        status = 'OK' if r.returncode == 0 else 'FAILED'
        if r.returncode: ok = False
        print(f'{name:22s} {stage}: {status}')
        if r.returncode: print(r.stdout + r.stderr)
sys.exit(0 if ok else 1)
