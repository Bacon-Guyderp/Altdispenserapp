package main

import (
	"archive/tar"
	"compress/gzip"
	"crypto/md5"
	"encoding/hex"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"time"
)

// A .unitypackage is a gzip-compressed tar archive. Every asset (file or
// folder) lives in a directory named after its GUID:
//
//	<guid>/pathname    project path, e.g. "Assets/Foo/bar.fbx"
//	<guid>/asset       file contents (missing for folders)
//	<guid>/asset.meta  Unity .meta file holding the same GUID
//
// GUIDs are derived from the project path, so importing a newer package for
// the same model updates the existing assets instead of duplicating them.

const folderMeta = "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n" +
	"  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"

// Unity fills in default importer settings for anything not listed.
const fileMeta = "fileFormatVersion: 2\nguid: %s\n"

// Asset is a file on disk and where it goes in the Unity project.
type Asset struct {
	Source    string
	UnityPath string
}

func guidFor(unityPath string) string {
	sum := md5.Sum([]byte("blend2unity:" + unityPath))
	return hex.EncodeToString(sum[:])
}

func normalizeUnityPath(p string) (string, error) {
	var parts []string
	for _, s := range strings.Split(strings.ReplaceAll(p, "\\", "/"), "/") {
		if s == "" || s == "." {
			continue
		}
		if s == ".." {
			return "", fmt.Errorf("Unity path may not contain '..': %s", p)
		}
		parts = append(parts, s)
	}
	if len(parts) == 0 || parts[0] != "Assets" {
		parts = append([]string{"Assets"}, parts...)
	}
	return strings.Join(parts, "/"), nil
}

// BuildUnityPackage writes a .unitypackage. progress (may be nil) is called
// with 0..1 as file data is written.
func BuildUnityPackage(output string, assets []Asset, progress func(float64)) (err error) {
	if len(assets) == 0 {
		return fmt.Errorf("no assets to package")
	}
	files := make([]Asset, len(assets))
	folders := map[string]bool{}
	var total int64
	for i, a := range assets {
		p, err := normalizeUnityPath(a.UnityPath)
		if err != nil {
			return err
		}
		files[i] = Asset{a.Source, p}
		parts := strings.Split(p, "/")
		for n := len(parts) - 1; n > 1; n-- {
			folders[strings.Join(parts[:n], "/")] = true
		}
		st, err := os.Stat(a.Source)
		if err != nil {
			return err
		}
		total += st.Size()
	}
	if total == 0 {
		total = 1
	}
	folderList := make([]string, 0, len(folders))
	for f := range folders {
		folderList = append(folderList, f)
	}
	sort.Strings(folderList)

	if dir := filepath.Dir(output); dir != "" {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return err
		}
	}
	tmp := output + ".part"
	out, err := os.Create(tmp)
	if err != nil {
		return err
	}
	defer func() {
		if out != nil {
			out.Close()
		}
		if err != nil {
			os.Remove(tmp)
		}
	}()

	gz, _ := gzip.NewWriterLevel(out, gzip.BestCompression)
	tw := tar.NewWriter(gz)
	mtime := time.Now().Truncate(time.Second)

	addDir := func(name string) error {
		return tw.WriteHeader(&tar.Header{Name: name + "/", Typeflag: tar.TypeDir, Mode: 0o755, ModTime: mtime, Format: tar.FormatUSTAR})
	}
	addText := func(name, text string) error {
		if err := tw.WriteHeader(&tar.Header{Name: name, Typeflag: tar.TypeReg, Mode: 0o644, Size: int64(len(text)), ModTime: mtime, Format: tar.FormatUSTAR}); err != nil {
			return err
		}
		_, err := io.WriteString(tw, text)
		return err
	}

	for _, f := range folderList {
		g := guidFor(f)
		if err = addDir(g); err != nil {
			return err
		}
		if err = addText(g+"/pathname", f); err != nil {
			return err
		}
		if err = addText(g+"/asset.meta", fmt.Sprintf(folderMeta, g)); err != nil {
			return err
		}
	}

	var written int64
	for _, f := range files {
		g := guidFor(f.UnityPath)
		if err = addDir(g); err != nil {
			return err
		}
		if err = addText(g+"/pathname", f.UnityPath); err != nil {
			return err
		}
		if err = addText(g+"/asset.meta", fmt.Sprintf(fileMeta, g)); err != nil {
			return err
		}
		if err = addFile(tw, g+"/asset", f.Source, mtime, func(n int64) {
			written += n
			if progress != nil {
				progress(float64(written) / float64(total))
			}
		}); err != nil {
			return err
		}
	}
	if err = tw.Close(); err != nil {
		return err
	}
	if err = gz.Close(); err != nil {
		return err
	}
	if err = out.Close(); err != nil {
		out = nil
		return err
	}
	out = nil
	os.Remove(output)
	return os.Rename(tmp, output)
}

func addFile(tw *tar.Writer, name, src string, mtime time.Time, progress func(int64)) error {
	f, err := os.Open(src)
	if err != nil {
		return err
	}
	defer f.Close()
	st, err := f.Stat()
	if err != nil {
		return err
	}
	if err := tw.WriteHeader(&tar.Header{Name: name, Typeflag: tar.TypeReg, Mode: 0o644, Size: st.Size(), ModTime: mtime, Format: tar.FormatUSTAR}); err != nil {
		return err
	}
	buf := make([]byte, 1<<16)
	var done int64
	for done < st.Size() {
		n, err := f.Read(buf[:min64(int64(len(buf)), st.Size()-done)])
		if n > 0 {
			if _, werr := tw.Write(buf[:n]); werr != nil {
				return werr
			}
			done += int64(n)
			progress(int64(n))
		}
		if err == io.EOF {
			break
		}
		if err != nil {
			return err
		}
	}
	if done != st.Size() {
		return fmt.Errorf("file changed while packaging: %s", src)
	}
	return nil
}

func min64(a, b int64) int64 {
	if a < b {
		return a
	}
	return b
}

// ReadUnityPackage returns {unity path: contents, or nil for folders}. Used by tests.
func ReadUnityPackage(path string) (map[string][]byte, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer f.Close()
	gz, err := gzip.NewReader(f)
	if err != nil {
		return nil, err
	}
	tr := tar.NewReader(gz)
	entries := map[string][]byte{}
	for {
		h, err := tr.Next()
		if err == io.EOF {
			break
		}
		if err != nil {
			return nil, err
		}
		if h.Typeflag == tar.TypeDir {
			continue
		}
		data, err := io.ReadAll(tr)
		if err != nil {
			return nil, err
		}
		entries[h.Name] = data
	}
	result := map[string][]byte{}
	for name, data := range entries {
		if guid, ok := strings.CutSuffix(name, "/pathname"); ok {
			pathname := strings.SplitN(string(data), "\n", 2)[0]
			result[pathname] = entries[guid+"/asset"]
		}
	}
	return result, nil
}
