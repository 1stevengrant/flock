//go:build unix && !darwin

package ports

import (
	"os"
	"strconv"
)

func listeners(port int) []listener { return lsofListeners(port) }

func executable(pid int) string {
	path, _ := os.Readlink("/proc/" + strconv.Itoa(pid) + "/exe")
	return path
}

func special(h Holder) Holder { return h }

func homeDir() string {
	home, _ := os.UserHomeDir()
	return home
}
