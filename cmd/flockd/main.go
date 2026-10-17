//go:build windows

// flockd starts the Flock daemon at sign-in on Windows: the "Flock Daemon" scheduled task (see
// `flock install`) runs it as `flockd daemon --watchdog ...`.
//
// It is built as a GUI program (-H=windowsgui), so Windows starts it without a console window,
// and it runs flock.exe next to it with the same arguments in a console that has no window either.
// It is a separate, signed program because the alternative, a copy of flock.exe with the GUI flag
// set, no longer matches flock.exe's signature, and Smart App Control and App Control policies
// refuse to run it.
//
// flockd puts itself in a job object that ends when it does, so ending it (ending the task,
// `flock restart`) also ends flock.exe and everything the daemon started.
package main

import (
	"errors"
	"os"
	"os/exec"
	"path/filepath"
	"syscall"
	"unsafe"

	"golang.org/x/sys/windows"
)

func main() {
	self, err := os.Executable()
	if err != nil {
		os.Exit(1)
	}
	killWithJob()

	child := exec.Command(filepath.Join(filepath.Dir(self), "flock.exe"), os.Args[1:]...)
	child.SysProcAttr = &syscall.SysProcAttr{CreationFlags: windows.CREATE_NO_WINDOW}
	if err := child.Run(); err != nil {
		if exit, ok := errors.AsType[*exec.ExitError](err); ok {
			os.Exit(exit.ExitCode())
		}
		os.Exit(1)
	}
}

// killWithJob puts this process, and so every process it starts, in a job object whose processes
// all end when the last handle to it closes: when this process ends.
func killWithJob() {
	job, err := windows.CreateJobObject(nil, nil)
	if err != nil {
		return
	}
	info := windows.JOBOBJECT_EXTENDED_LIMIT_INFORMATION{}
	info.BasicLimitInformation.LimitFlags = windows.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
	if _, err := windows.SetInformationJobObject(job, windows.JobObjectExtendedLimitInformation,
		uintptr(unsafe.Pointer(&info)), uint32(unsafe.Sizeof(info))); err != nil {
		_ = windows.CloseHandle(job)
		return
	}
	_ = windows.AssignProcessToJobObject(job, windows.CurrentProcess())
}
