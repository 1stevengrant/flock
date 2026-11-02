package config

import (
	"errors"
	"io"
	"os"
	"path/filepath"
	"time"
	"unsafe"

	"golang.org/x/sys/windows"
)

// readFile reads config.json without standing in the way of Save. os.ReadFile opens files without
// FILE_SHARE_DELETE on Windows, so a save that replaced the file while it was read failed.
func readFile(path string) ([]byte, error) {
	h, err := open(path, windows.GENERIC_READ)
	if err != nil {
		return nil, err
	}
	f := os.NewFile(uintptr(h), path)
	defer f.Close()
	return io.ReadAll(f)
}

// replaceFile moves tmp over path. os.Rename (MoveFileEx) refuses while anything has path open,
// even a reader that allows it; a POSIX-style rename (Windows 10 1607 and later, on NTFS) replaces
// it all the same, and the reader keeps reading the old file. Elsewhere, a refused rename is tried
// again for up to a second.
func replaceFile(tmp, path string) error {
	err := posixRename(tmp, path)
	if err == nil || !unsupported(err) {
		return err
	}
	for range 20 {
		if err = os.Rename(tmp, path); err == nil || !busy(err) {
			return err
		}
		time.Sleep(50 * time.Millisecond)
	}
	return err
}

// fileRenameInfo is FILE_RENAME_INFO with the Flags of FileRenameInfoEx; FileName runs on past the
// struct.
type fileRenameInfo struct {
	Flags          uint32
	RootDirectory  windows.Handle
	FileNameLength uint32
	FileName       [1]uint16
}

func posixRename(tmp, path string) error {
	target, err := filepath.Abs(path)
	if err != nil {
		return err
	}
	name, err := windows.UTF16FromString(target)
	if err != nil {
		return err
	}
	h, err := open(tmp, windows.DELETE|windows.SYNCHRONIZE)
	if err != nil {
		return err
	}
	defer windows.CloseHandle(h)

	size := int(unsafe.Offsetof(fileRenameInfo{}.FileName)) + len(name)*2
	buf := make([]byte, size)
	info := (*fileRenameInfo)(unsafe.Pointer(&buf[0]))
	info.Flags = windows.FILE_RENAME_REPLACE_IF_EXISTS | windows.FILE_RENAME_POSIX_SEMANTICS
	info.FileNameLength = uint32((len(name) - 1) * 2) // in bytes, without the terminating NUL
	copy(unsafe.Slice(&info.FileName[0], len(name)), name)
	if err := windows.SetFileInformationByHandle(h, windows.FileRenameInfoEx, &buf[0], uint32(size)); err != nil {
		return &os.LinkError{Op: "rename", Old: tmp, New: path, Err: err}
	}
	return nil
}

func open(path string, access uint32) (windows.Handle, error) {
	name, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return 0, &os.PathError{Op: "open", Path: path, Err: err}
	}
	h, err := windows.CreateFile(name, access, windows.FILE_SHARE_READ|windows.FILE_SHARE_WRITE|windows.FILE_SHARE_DELETE,
		nil, windows.OPEN_EXISTING, windows.FILE_ATTRIBUTE_NORMAL, 0)
	if err != nil {
		return 0, &os.PathError{Op: "open", Path: path, Err: err}
	}
	return h, nil
}

// unsupported: the file system or Windows version has no POSIX-style rename.
func unsupported(err error) bool {
	return errors.Is(err, windows.ERROR_INVALID_PARAMETER) || errors.Is(err, windows.ERROR_NOT_SUPPORTED) ||
		errors.Is(err, windows.ERROR_INVALID_FUNCTION)
}

func busy(err error) bool {
	return errors.Is(err, windows.ERROR_SHARING_VIOLATION) || errors.Is(err, windows.ERROR_ACCESS_DENIED)
}
