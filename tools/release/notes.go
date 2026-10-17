package main

import (
	"fmt"
	"strings"
)

// The changelog sections shown, in order, as release-please-config.json lists them; other types
// (docs, test, chore, ci, build) are left out there too.
var sections = []struct{ kind, title string }{
	{"feat", "Features"},
	{"fix", "Bug Fixes"},
	{"perf", "Performance"},
	{"refactor", "Refactor"},
	{"style", "Style"},
	{"revert", "Reverts"},
}

// notes are a beta's release notes in release-please's changelog format: the commits since the
// last stable release, by section.
func notes(repo string, commits []commit) string {
	var b strings.Builder
	for _, s := range sections {
		var lines []string
		for _, c := range commits {
			m := conventional.FindStringSubmatch(strings.SplitN(c.message, "\n", 2)[0])
			if m == nil || m[1] != s.kind {
				continue
			}
			line := "* "
			if m[2] != "" {
				line += "**" + m[2] + ":** "
			}
			line += m[4]
			if repo != "" && len(c.hash) >= 7 {
				line += fmt.Sprintf(" ([%s](https://github.com/%s/commit/%s))", c.hash[:7], repo, c.hash)
			}
			lines = append(lines, line)
		}
		if len(lines) > 0 {
			fmt.Fprintf(&b, "### %s\n\n%s\n\n", s.title, strings.Join(lines, "\n"))
		}
	}
	if b.Len() == 0 {
		return "Maintenance changes only.\n"
	}
	return b.String()
}
