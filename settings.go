package main

import (
	"os"
	"path/filepath"
	"sort"
	"strings"
)

// Settings is a key=value file in %APPDATA%\Blend2Unity\settings.ini.
type Settings map[string]string

func settingsPath() string {
	dir, err := os.UserConfigDir()
	if err != nil {
		dir = "."
	}
	return filepath.Join(dir, "Blend2Unity", "settings.ini")
}

func LoadSettings() Settings {
	s := Settings{}
	data, err := os.ReadFile(settingsPath())
	if err != nil {
		return s
	}
	for _, line := range strings.Split(string(data), "\n") {
		if k, v, ok := strings.Cut(strings.TrimRight(line, "\r"), "="); ok {
			s[strings.TrimSpace(k)] = v
		}
	}
	return s
}

func (s Settings) Save() {
	keys := make([]string, 0, len(s))
	for k := range s {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	var b strings.Builder
	for _, k := range keys {
		b.WriteString(k + "=" + s[k] + "\n")
	}
	p := settingsPath()
	os.MkdirAll(filepath.Dir(p), 0o755)
	os.WriteFile(p, []byte(b.String()), 0o644)
}

func (s Settings) Get(key, fallback string) string {
	if v, ok := s[key]; ok {
		return v
	}
	return fallback
}

func (s Settings) GetBool(key string, fallback bool) bool {
	if v, ok := s[key]; ok {
		return v == "1"
	}
	return fallback
}

func (s Settings) Set(key, value string) {
	s[key] = strings.NewReplacer("\r", "", "\n", "").Replace(value)
}

func (s Settings) SetBool(key string, value bool) {
	if value {
		s[key] = "1"
	} else {
		s[key] = "0"
	}
}
