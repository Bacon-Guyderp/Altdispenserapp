//go:build !windows

package main

// On Linux and macOS the program is a command-line tool (the window is Windows-only).

import (
	"context"
	"flag"
	"fmt"
	"os"
	"strings"
)

func main() {
	o := DefaultOptions()
	out := flag.String("o", "", "output folder (default: next to each .blend)")
	blender := flag.String("blender", "", "path to the Blender executable")
	flag.StringVar(&o.UnityFolder, "unity-folder", o.UnityFolder, "folder inside the Unity project")
	noTransform := flag.Bool("no-apply-transform", false, "don't bake the axis conversion")
	noModifiers := flag.Bool("no-modifiers", false, "don't apply modifiers")
	noAnimation := flag.Bool("no-animation", false, "don't export animation")
	noTextures := flag.Bool("no-textures", false, "don't include textures")
	flag.BoolVar(&o.IncludeBlend, "include-blend", false, "also include the original .blend")
	flag.BoolVar(&o.BakeMaterials, "bake", false, "bake node-based materials (procedural textures, bump) to image textures")
	flag.IntVar(&o.BakeSize, "bake-size", o.BakeSize, "baked texture size in pixels")
	verbose := flag.Bool("v", false, "show Blender's output")
	flag.Usage = func() {
		fmt.Fprintln(os.Stderr, "usage: blend2unity [options] file.blend [more.blend ...]")
		flag.PrintDefaults()
	}
	flag.Parse()
	if flag.NArg() == 0 {
		flag.Usage()
		os.Exit(2)
	}
	o.ApplyTransform, o.ApplyModifiers = !*noTransform, !*noModifiers
	o.IncludeAnimation, o.IncludeTextures = !*noAnimation, !*noTextures
	if *blender == "" {
		*blender = FindBlender()
	}
	if *blender == "" {
		fmt.Fprintln(os.Stderr, "Blender not found; pass -blender or set BLENDER_PATH")
		os.Exit(2)
	}

	failures := 0
	for i, f := range flag.Args() {
		show := func(frac float64, msg string) {
			bar := strings.Repeat("#", int(frac*30))
			fmt.Printf("\r[%d/%d] [%-30s] %3.0f%% %-40.40s", i+1, flag.NArg(), bar, frac*100, msg)
		}
		log := func(msg string) {
			if *verbose || !strings.HasPrefix(msg, "  ") {
				fmt.Printf("\r%-100s\r%s\n", "", msg)
			}
		}
		if _, err := Convert(context.Background(), f, *out, *blender, o, show, log); err != nil {
			failures++
			fmt.Printf("\nFailed: %s: %v\n", f, err)
		} else {
			fmt.Println()
		}
	}
	if failures > 0 {
		os.Exit(1)
	}
}
