//go:build !windows

package config

import "os"

// readFile reads config.json (see file_windows.go for why Windows needs more).
func readFile(path string) ([]byte, error) { return os.ReadFile(path) }

// replaceFile moves tmp over path: atomic, even while the file is open.
func replaceFile(tmp, path string) error { return os.Rename(tmp, path) }
