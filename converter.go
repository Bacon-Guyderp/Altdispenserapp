package main

import (
	"bufio"
	"context"
	_ "embed"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"sort"
	"strconv"
	"strings"
	"sync"
)

// The script Blender runs to export the FBX and textures. It is built into
// the executable, so nothing besides Blender needs to be installed.
//
//go:embed blender_export.py
var exportScript []byte

const progressTag = "@@B2U"

// BlenderShare is the part of a file's progress spent in Blender; the rest is packaging.
const BlenderShare = 0.85

// ErrCancelled is returned when a conversion is cancelled.
var ErrCancelled = errors.New("cancelled")

type Options struct {
	UnityFolder      string // {name} = .blend file name without extension
	ApplyTransform   bool   // bake axis conversion (avoids -90° X rotation)
	ApplyModifiers   bool
	IncludeAnimation bool
	IncludeTextures  bool
	IncludeBlend     bool // also ship the original .blend
	BakeMaterials    bool // render node-based materials (procedural, mixes, bump) to textures
	BakeSize         int  // baked texture size in pixels
}

func DefaultOptions() Options {
	return Options{UnityFolder: "Assets/{name}", ApplyTransform: true, ApplyModifiers: true,
		IncludeAnimation: true, IncludeTextures: true, BakeSize: 1024}
}

// FindBlender looks for a Blender executable in the usual places.
func FindBlender() string {
	var candidates []string
	if env := os.Getenv("BLENDER_PATH"); env != "" {
		candidates = append(candidates, env)
	}
	if p, err := exec.LookPath("blender"); err == nil {
		candidates = append(candidates, p)
	}
	switch runtime.GOOS {
	case "windows":
		seen := map[string]bool{}
		for _, v := range []string{"ProgramFiles", "ProgramW6432", "ProgramFiles(x86)", "LOCALAPPDATA"} {
			base := os.Getenv(v)
			if base == "" || seen[strings.ToLower(base)] {
				continue
			}
			seen[strings.ToLower(base)] = true
			found, _ := filepath.Glob(filepath.Join(base, "Blender Foundation", "Blender*", "blender.exe"))
			sortByVersionDesc(found)
			candidates = append(candidates, found...)
			candidates = append(candidates, filepath.Join(base, "Steam", "steamapps", "common", "Blender", "blender.exe"))
		}
	case "darwin":
		candidates = append(candidates, "/Applications/Blender.app/Contents/MacOS/Blender")
	default:
		candidates = append(candidates, "/snap/bin/blender", "/usr/bin/blender", "/usr/local/bin/blender")
	}
	for _, c := range candidates {
		if st, err := os.Stat(c); err == nil && !st.IsDir() {
			return c
		}
	}
	return ""
}

var digits = regexp.MustCompile(`\d+`)

func sortByVersionDesc(paths []string) {
	key := func(s string) []int {
		var k []int
		for _, d := range digits.FindAllString(s, -1) {
			n, _ := strconv.Atoi(d)
			k = append(k, n)
		}
		return k
	}
	sort.SliceStable(paths, func(i, j int) bool {
		a, b := key(paths[i]), key(paths[j])
		for n := 0; n < len(a) && n < len(b); n++ {
			if a[n] != b[n] {
				return a[n] > b[n]
			}
		}
		return len(a) > len(b)
	})
}

// BlenderVersion returns e.g. "Blender 4.2.3 LTS", or "" if unknown.
func BlenderVersion(blender string) string {
	cmd := exec.Command(blender, "--version")
	hideWindow(cmd)
	out, _ := cmd.Output()
	for _, line := range strings.Split(string(out), "\n") {
		if line = strings.TrimSpace(line); strings.HasPrefix(line, "Blender ") {
			return line
		}
	}
	return ""
}

var unsafeChars = regexp.MustCompile(`[<>:"\\|?*\x00-\x1f]`)

// Convert turns one .blend file into a .unitypackage and returns its path.
// An empty outputDir writes next to the .blend file. progress and log may be nil.
func Convert(ctx context.Context, blend, outputDir, blender string, o Options,
	progress func(float64, string), log func(string)) (string, error) {
	if progress == nil {
		progress = func(float64, string) {}
	}
	if log == nil {
		log = func(string) {}
	}
	blend, _ = filepath.Abs(blend)
	if st, err := os.Stat(blend); err != nil || st.IsDir() {
		return "", fmt.Errorf("file not found: %s", blend)
	}
	if st, err := os.Stat(blender); blender == "" || err != nil || st.IsDir() {
		return "", fmt.Errorf("Blender executable not found; set its location first")
	}

	name := strings.TrimSuffix(filepath.Base(blend), filepath.Ext(blend))
	if outputDir == "" {
		outputDir = filepath.Dir(blend)
	}
	outPath := filepath.Join(outputDir, name+".unitypackage")
	safe := strings.TrimSpace(unsafeChars.ReplaceAllString(name, "_"))
	if safe == "" {
		safe = "Model"
	}
	unityFolder := strings.Trim(strings.ReplaceAll(o.UnityFolder, "{name}", safe), "/\\ ")
	if unityFolder == "" {
		unityFolder = "Assets"
	}

	work, err := os.MkdirTemp("", "blend2unity_")
	if err != nil {
		return "", err
	}
	defer os.RemoveAll(work)
	exportDir := filepath.Join(work, "export")
	if err := os.Mkdir(exportDir, 0o755); err != nil {
		return "", err
	}
	script := filepath.Join(work, "blender_export.py")
	if err := os.WriteFile(script, exportScript, 0o644); err != nil {
		return "", err
	}

	if err := runBlender(ctx, blender, blend, script, exportDir, name, o, progress, log); err != nil {
		return "", err
	}
	if ctx.Err() != nil {
		return "", ErrCancelled
	}
	progress(BlenderShare, "Building .unitypackage")

	var assets []Asset
	hasFBX := false
	err = filepath.Walk(exportDir, func(p string, info os.FileInfo, err error) error {
		if err != nil || info.IsDir() {
			return err
		}
		rel, _ := filepath.Rel(exportDir, p)
		assets = append(assets, Asset{p, unityFolder + "/" + filepath.ToSlash(rel)})
		if strings.EqualFold(filepath.Ext(p), ".fbx") {
			hasFBX = true
		}
		return nil
	})
	if err != nil {
		return "", err
	}
	if !hasFBX {
		return "", fmt.Errorf("Blender did not produce an FBX file")
	}
	if o.IncludeBlend {
		assets = append(assets, Asset{blend, unityFolder + "/" + filepath.Base(blend)})
	}
	if err := os.MkdirAll(outputDir, 0o755); err != nil {
		return "", err
	}
	err = BuildUnityPackage(outPath, assets, func(f float64) {
		progress(BlenderShare+(1-BlenderShare)*f, "Building .unitypackage")
	})
	if err != nil {
		return "", err
	}
	progress(1, "Done")
	log(fmt.Sprintf("Wrote %s (%d asset(s))", outPath, len(assets)))
	return outPath, nil
}

func runBlender(ctx context.Context, blender, blend, script, exportDir, name string, o Options,
	progress func(float64, string), log func(string)) error {
	args := []string{"-b", "--factory-startup", blend, "--python-exit-code", "1", "--python", script,
		"--", "--out", exportDir, "--name", name}
	if o.ApplyTransform {
		args = append(args, "--apply-transform")
	}
	if o.ApplyModifiers {
		args = append(args, "--modifiers")
	}
	if o.IncludeAnimation {
		args = append(args, "--animation")
	}
	if o.IncludeTextures {
		args = append(args, "--textures")
	}
	if o.BakeMaterials {
		size := o.BakeSize
		if size < 64 || size > 8192 {
			size = 1024
		}
		args = append(args, "--bake", "--bake-size", strconv.Itoa(size))
	}

	cmd := exec.Command(blender, args...)
	hideWindow(cmd)
	r, w, err := os.Pipe()
	if err != nil {
		return err
	}
	cmd.Stdout, cmd.Stderr = w, w
	progress(0.02, "Starting Blender")
	if err := cmd.Start(); err != nil {
		r.Close()
		w.Close()
		return fmt.Errorf("could not start Blender (%s): %v", blender, err)
	}
	w.Close()

	done := make(chan struct{})
	var killed sync.Once
	go func() {
		select {
		case <-ctx.Done():
			killed.Do(func() { cmd.Process.Kill() })
		case <-done:
		}
	}()

	var errs []string
	lastLine := ""
	sc := bufio.NewScanner(r)
	sc.Buffer(make([]byte, 64*1024), 4*1024*1024)
	for sc.Scan() {
		line := strings.TrimRight(sc.Text(), "\r")
		if strings.TrimSpace(line) == "" {
			continue
		}
		if !strings.HasPrefix(line, progressTag+" ") {
			// e.g. `Error: File format is not supported in file "x.blend"`
			if strings.HasPrefix(strings.ToLower(line), "error") {
				errs = append(errs, strings.TrimSpace(line))
			}
			lastLine = line
			log("  " + line)
			continue
		}
		parts := strings.SplitN(line, " ", 3)
		kind, text := "", ""
		if len(parts) > 1 {
			kind = parts[1]
		}
		if len(parts) > 2 {
			text = parts[2]
		}
		switch kind {
		case "PROGRESS":
			num, msg, _ := strings.Cut(text, " ")
			if f, err := strconv.ParseFloat(num, 64); err == nil {
				progress(f*BlenderShare, msg)
			}
		case "ERROR":
			errs = append(errs, text)
			log("ERROR: " + text)
		case "WARN":
			log("Warning: " + text)
		case "INFO":
			log(text)
		case "DONE":
			log("Exported " + filepath.Base(text))
		}
	}
	r.Close()
	waitErr := cmd.Wait()
	close(done)

	if ctx.Err() != nil {
		return ErrCancelled
	}
	if waitErr != nil {
		detail := "no output"
		if len(errs) > 0 {
			detail = errs[len(errs)-1]
		} else if lastLine != "" {
			detail = lastLine
		}
		code := -1
		if ee, ok := waitErr.(*exec.ExitError); ok {
			code = ee.ExitCode()
		}
		return fmt.Errorf("Blender failed (exit code %d): %s", code, detail)
	}
	return nil
}
