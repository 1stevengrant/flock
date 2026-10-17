package cli

import (
	"errors"
	"os/exec"
)

// openUpdateLink hands flock://update to the app; `open` fails when no app handles the scheme.
func openUpdateLink() error {
	if exec.Command("open", updateLink).Run() != nil {
		return errors.New("the Flock app isn't installed: download it from https://flockphp.com to get updates")
	}
	return nil
}
