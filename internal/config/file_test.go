package config

import (
	"os"
	"sync"
	"testing"
)

// The daemon saves config.json while other code reads it; on Windows a read used to make the save
// fail, or the read itself, with a sharing violation.
func TestSavesWhileTheConfigIsRead(t *testing.T) {
	root := t.TempDir()
	if err := Default().Save(root); err != nil {
		t.Fatal(err)
	}
	var wg sync.WaitGroup
	done := make(chan struct{})
	for range 4 {
		wg.Go(func() {
			for {
				select {
				case <-done:
					return
				default:
				}
				if _, err := Load(root); err != nil {
					t.Error(err)
					return
				}
			}
		})
	}
	for i := range 300 {
		cfg := Default()
		cfg.DefaultPHP = "8." + string(rune('0'+i%10))
		if err := cfg.Save(root); err != nil {
			t.Errorf("save %d: %v", i, err)
			break
		}
	}
	close(done)
	wg.Wait()

	// The last save is what's there, and its temporary file is gone.
	if cfg, err := Load(root); err != nil || cfg.DefaultPHP != "8.9" {
		t.Fatalf("after the saves: %v %v", cfg, err)
	}
	if _, err := os.Stat(file(root) + ".tmp"); !os.IsNotExist(err) {
		t.Fatalf("config.json.tmp is left: %v", err)
	}
}
