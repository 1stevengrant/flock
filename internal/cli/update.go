package cli

import (
	"github.com/spf13/cobra"
)

// updateLink asks the app to update itself; the app's update brings the CLI and daemon along
// (it runs its bundled `flock install` after an update).
const updateLink = "flock://update"

func newUpdateCommand() *cobra.Command {
	return &cobra.Command{
		Use:   "update",
		Short: "Update Flock: the app checks for a new version and installs it, CLI and daemon included",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			if err := openUpdateLink(); err != nil {
				return err
			}
			cmd.Println("Flock's app is checking for updates.")
			return nil
		},
	}
}
