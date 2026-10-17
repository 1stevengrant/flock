package cli

import (
	"errors"
	"os/exec"

	"golang.org/x/sys/windows/registry"
)

// openUpdateLink hands flock://update to the tray app, which the installer registers for the
// scheme. Windows would show its own "no app" dialog for an unknown scheme, so that is checked first.
func openUpdateLink() error {
	key, err := registry.OpenKey(registry.CURRENT_USER, `Software\Classes\flock\shell\open\command`, registry.QUERY_VALUE)
	if err != nil {
		return errors.New("the Flock app isn't installed: download it from https://flockphp.com to get updates")
	}
	key.Close()
	return exec.Command("rundll32", "url.dll,FileProtocolHandler", updateLink).Run()
}
