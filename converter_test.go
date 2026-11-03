package main

// go test ./...                                   package-format tests
// BLENDER_PATH=/path/to/blender go test ./...     plus end-to-end conversions

import (
	"context"
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"sort"
	"strings"
	"testing"
	"time"
)

func TestPackageRoundTrip(t *testing.T) {
	d := t.TempDir()
	os.WriteFile(filepath.Join(d, "a.fbx"), []byte("fbx"), 0o644)
	os.WriteFile(filepath.Join(d, "b.png"), []byte("png"), 0o644)
	pkg := filepath.Join(d, "x.unitypackage")
	err := BuildUnityPackage(pkg, []Asset{
		{filepath.Join(d, "a.fbx"), "Assets/M/a.fbx"},
		{filepath.Join(d, "b.png"), "M/Textures/b.png"},
	}, nil)
	if err != nil {
		t.Fatal(err)
	}
	c, err := ReadUnityPackage(pkg)
	if err != nil {
		t.Fatal(err)
	}
	keys := []string{}
	for k := range c {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	want := "Assets/M Assets/M/Textures Assets/M/Textures/b.png Assets/M/a.fbx"
	if strings.Join(keys, " ") != want {
		t.Fatalf("entries = %v, want %s", keys, want)
	}
	if c["Assets/M"] != nil || string(c["Assets/M/a.fbx"]) != "fbx" || string(c["Assets/M/Textures/b.png"]) != "png" {
		t.Fatal("wrong contents")
	}
	if _, err := os.Stat(pkg + ".part"); err == nil {
		t.Fatal("temp file left behind")
	}
	out, err := exec.Command("tar", "tzf", pkg).Output()
	if err != nil || !strings.Contains(string(out), guidFor("Assets/M/a.fbx")+"/asset.meta") {
		t.Fatalf("not readable by tar: %v", err)
	}
}

func TestGuidsAndPaths(t *testing.T) {
	g := guidFor("Assets/x.fbx")
	if g != guidFor("Assets/x.fbx") || len(g) != 32 {
		t.Fatal("bad guid", g)
	}
	if _, err := normalizeUnityPath("Assets/../x"); err == nil {
		t.Fatal("accepted ..")
	}
	if p, _ := normalizeUnityPath(`Models\a.fbx`); p != "Assets/Models/a.fbx" {
		t.Fatal(p)
	}
}

const makeScene = `
import bpy, sys
out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.mesh.primitive_monkey_add()
ob = bpy.context.active_object
mat = bpy.data.materials.new('Mat'); mat.use_nodes = True
img = bpy.data.images.new('Painted', 16, 16)
node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = img
ob.data.materials.append(mat)
ob.keyframe_insert('location', frame=1); ob.location.x = 2; ob.keyframe_insert('location', frame=10)
bpy.ops.wm.save_as_mainfile(filepath=out)
`

func testBlend(t *testing.T) (blender, blend string) {
	blender = os.Getenv("BLENDER_PATH")
	if blender == "" {
		t.Skip("set BLENDER_PATH to run Blender tests")
	}
	d := t.TempDir()
	blend = filepath.Join(d, "Test 'Model' v2.blend")
	script := filepath.Join(d, "make.py")
	os.WriteFile(script, []byte(makeScene), 0o644)
	if out, err := exec.Command(blender, "-b", "--factory-startup", "--python", script, "--", blend).CombinedOutput(); err != nil {
		t.Fatalf("making test file: %v\n%s", err, out)
	}
	return
}

func TestConvert(t *testing.T) {
	blender, blend := testBlend(t)
	var seen []float64
	var logs []string
	out, err := Convert(context.Background(), blend, filepath.Join(t.TempDir(), "out dir"), blender, DefaultOptions(),
		func(f float64, _ string) { seen = append(seen, f) }, func(m string) { logs = append(logs, m) })
	if err != nil {
		t.Fatal(err)
	}
	c, err := ReadUnityPackage(out)
	if err != nil {
		t.Fatal(err)
	}
	name := "Test 'Model' v2"
	for _, p := range []string{"Assets/" + name + "/" + name + ".fbx", "Assets/" + name + "/Textures/Painted.png"} {
		if len(c[p]) == 0 {
			t.Errorf("missing %s (have %v)", p, c)
		}
	}
	if !sort.Float64sAreSorted(seen) || seen[len(seen)-1] != 1 {
		t.Errorf("progress not monotonic to 1: %v", seen)
	}
	if !strings.HasPrefix(logs[len(logs)-1], "Wrote ") {
		t.Errorf("last log %q", logs[len(logs)-1])
	}
}

func TestIncludeBlendAndFolder(t *testing.T) {
	blender, blend := testBlend(t)
	o := DefaultOptions()
	o.IncludeBlend, o.IncludeTextures, o.UnityFolder = true, false, "Assets/Models/{name}"
	out, err := Convert(context.Background(), blend, t.TempDir(), blender, o, nil, nil)
	if err != nil {
		t.Fatal(err)
	}
	c, _ := ReadUnityPackage(out)
	name := "Test 'Model' v2"
	if c["Assets/Models/"+name+"/"+name+".blend"] == nil {
		t.Error("missing .blend")
	}
	for k := range c {
		if strings.Contains(k, "Textures") {
			t.Error("textures included:", k)
		}
	}
}

func TestBadFile(t *testing.T) {
	blender, _ := testBlend(t)
	bad := filepath.Join(t.TempDir(), "bad.blend")
	os.WriteFile(bad, []byte("not a blend file"), 0o644)
	_, err := Convert(context.Background(), bad, "", blender, DefaultOptions(), nil, nil)
	if err == nil || !strings.Contains(err.Error(), "not supported") {
		t.Fatalf("err = %v", err)
	}
}

func TestCancel(t *testing.T) {
	blender, blend := testBlend(t)
	out := t.TempDir()
	ctx, cancel := context.WithTimeout(context.Background(), 200*time.Millisecond)
	defer cancel()
	start := time.Now()
	_, err := Convert(ctx, blend, out, blender, DefaultOptions(), nil, nil)
	if !errors.Is(err, ErrCancelled) {
		t.Fatalf("err = %v", err)
	}
	if time.Since(start) > 5*time.Second {
		t.Error("cancel was slow")
	}
	if entries, _ := os.ReadDir(out); len(entries) != 0 {
		t.Error("output written after cancel")
	}
}

const makeProcedural = `
import bpy, sys
out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3); rock = bpy.context.active_object; rock.name = 'Rock'
m = bpy.data.materials.new('RockMat'); m.use_nodes = True; nt = m.node_tree; p = nt.nodes['Principled BSDF']
noise = nt.nodes.new('ShaderNodeTexNoise'); bump = nt.nodes.new('ShaderNodeBump')
nt.links.new(noise.outputs['Color'], p.inputs['Base Color'])
nt.links.new(noise.outputs['Fac'], bump.inputs['Height']); nt.links.new(bump.outputs['Normal'], p.inputs['Normal'])
rock.data.materials.append(m)
bpy.ops.mesh.primitive_cylinder_add(location=(3, 0, 0)); mix = bpy.context.active_object; mix.name = 'Mixed'
while mix.data.uv_layers: mix.data.uv_layers.remove(mix.data.uv_layers[0])
m = bpy.data.materials.new('MixMat'); m.use_nodes = True; nt = m.node_tree
nt.nodes.remove(nt.nodes['Principled BSDF'])
d = nt.nodes.new('ShaderNodeBsdfDiffuse'); g = nt.nodes.new('ShaderNodeBsdfGlossy'); s = nt.nodes.new('ShaderNodeMixShader')
nt.links.new(d.outputs[0], s.inputs[1]); nt.links.new(g.outputs[0], s.inputs[2])
nt.links.new(s.outputs[0], nt.nodes['Material Output'].inputs['Surface'])
mix.data.materials.append(m)
bpy.ops.wm.save_as_mainfile(filepath=out)
`

func TestBakeMaterials(t *testing.T) {
	blender := os.Getenv("BLENDER_PATH")
	if blender == "" {
		t.Skip("set BLENDER_PATH to run Blender tests")
	}
	d := t.TempDir()
	blend := filepath.Join(d, "Procedural.blend")
	script := filepath.Join(d, "make.py")
	os.WriteFile(script, []byte(makeProcedural), 0o644)
	if out, err := exec.Command(blender, "-b", "--factory-startup", "--python", script, "--", blend).CombinedOutput(); err != nil {
		t.Fatalf("making test file: %v\n%s", err, out)
	}
	o := DefaultOptions()
	o.BakeMaterials, o.BakeSize = true, 128
	var logs []string
	out, err := Convert(context.Background(), blend, filepath.Join(d, "out"), blender, o, nil, func(m string) { logs = append(logs, m) })
	if err != nil {
		t.Fatal(err)
	}
	c, _ := ReadUnityPackage(out)
	for _, p := range []string{
		"Assets/Procedural/Textures/Rock_RockMat_BaseColor.png",
		"Assets/Procedural/Textures/Rock_RockMat_Normal.png",
		"Assets/Procedural/Textures/Mixed_MixMat_BaseColor.png",
	} {
		if len(c[p]) == 0 {
			t.Errorf("missing baked texture %s", p)
		}
	}
	fbx := string(c["Assets/Procedural/Procedural.fbx"])
	if !strings.Contains(fbx, "Rock_RockMat_BaseColor.png") || !strings.Contains(fbx, "Rock_RockMat_Normal.png") {
		t.Error("FBX does not reference the baked textures")
	}
	if !strings.Contains(strings.Join(logs, "\n"), "Unwrapped UVs for Mixed") {
		t.Error("object without UVs was not unwrapped")
	}
}
