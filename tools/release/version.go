package main

import (
	"fmt"
	"regexp"
	"slices"
	"strconv"
	"strings"
)

// version is a release: 1.2.3, or 1.2.3-beta.4 (beta > 0). A beta sorts below its own release
// and above the release before it.
type version struct {
	major, minor, patch, beta int
}

var versionPattern = regexp.MustCompile(`^v?(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$`)

func parseVersion(s string) (version, bool) {
	m := versionPattern.FindStringSubmatch(s)
	if m == nil {
		return version{}, false
	}
	n := func(s string) int { i, _ := strconv.Atoi(s); return i }
	v := version{n(m[1]), n(m[2]), n(m[3]), 0}
	if m[4] != "" {
		if v.beta = n(m[4]); v.beta == 0 {
			return version{}, false
		}
	}
	return v, true
}

func (v version) String() string {
	s := fmt.Sprintf("%d.%d.%d", v.major, v.minor, v.patch)
	if v.beta > 0 {
		s += fmt.Sprintf("-beta.%d", v.beta)
	}
	return s
}

func (v version) isBeta() bool { return v.beta > 0 }

// release is the version without its beta part.
func (v version) release() version { return version{v.major, v.minor, v.patch, 0} }

func (v version) compare(o version) int {
	for _, d := range []int{v.major - o.major, v.minor - o.minor, v.patch - o.patch} {
		if d != 0 {
			return d
		}
	}
	// A release (beta 0) sorts above its betas.
	switch {
	case v.beta == o.beta:
		return 0
	case v.beta == 0:
		return 1
	case o.beta == 0:
		return -1
	}
	return v.beta - o.beta
}

// build is the number the updaters compare on macOS (CFBundleVersion, sparkle:version):
// "1.2.3.4" for 1.2.3-beta.4 and "1.2.3.1000" for 1.2.3. macos/build-app.sh computes the same.
func (v version) build() string {
	n := v.beta
	if n == 0 {
		n = 1000
	}
	return fmt.Sprintf("%d.%d.%d.%d", v.major, v.minor, v.patch, n)
}

// conventional is the type of a conventional commit message and whether it breaks compatibility.
var conventional = regexp.MustCompile(`^(\w+)(?:\(([^)]*)\))?(!)?: (.+)`)

type commit struct {
	hash, message string
}

// bump is the release after last that the commits make, as release-please counts them: a breaking
// change raises the major version, a feature the minor one, anything else the patch.
func bump(last version, commits []commit) version {
	level := 0
	for _, c := range commits {
		m := conventional.FindStringSubmatch(c.message)
		switch {
		case m != nil && m[3] == "!", strings.Contains(c.message, "\nBREAKING CHANGE:"), strings.Contains(c.message, "\nBREAKING-CHANGE:"):
			level = max(level, 3)
		case m != nil && m[1] == "feat":
			level = max(level, 2)
		default:
			level = max(level, 1)
		}
	}
	switch level {
	case 3:
		return version{last.major + 1, 0, 0, 0}
	case 2:
		return version{last.major, last.minor + 1, 0, 0}
	}
	return version{last.major, last.minor, last.patch + 1, 0}
}

// nextBeta is the beta to release after the stable release last (nil before the first one, which
// makes 1.0.0) with commits since then, given every tag in the repository. Betas count up per
// release, and a beta never goes below one already released, which the updaters wouldn't offer.
func nextBeta(last *version, commits []commit, tags []string) (version, error) {
	if len(commits) == 0 {
		return version{}, fmt.Errorf("nothing to release: no commits since v%s", last)
	}
	next := version{1, 0, 0, 0}
	if last != nil {
		next = bump(*last, commits)
	}

	var newest *version
	base := next
	for _, t := range tags {
		v, ok := parseVersion(t)
		if !ok || !v.isBeta() {
			continue
		}
		if v.release() == base {
			next.beta = max(next.beta, v.beta)
		}
		if newest == nil || v.compare(*newest) > 0 {
			newest = &v
		}
	}
	next.beta++
	if newest != nil && next.compare(*newest) <= 0 {
		return version{}, fmt.Errorf("the next beta would be %s, but %s is already released and updaters never go back; "+
			"release %s first or add the commits that make the version higher", next, newest, newest.release())
	}
	return next, nil
}

// lastStable is the highest stable release among tags (nil when there is none).
func lastStable(tags []string) *version {
	var last *version
	for _, t := range tags {
		if v, ok := parseVersion(t); ok && !v.isBeta() && (last == nil || v.compare(*last) > 0) {
			last = &v
		}
	}
	return last
}

// sortVersions sorts newest first.
func sortVersions[T any](items []T, v func(T) version) {
	slices.SortFunc(items, func(a, b T) int { return v(b).compare(v(a)) })
}
