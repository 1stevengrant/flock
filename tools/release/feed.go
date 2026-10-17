package main

import (
	"bytes"
	"crypto/ed25519"
	"encoding/base64"
	"encoding/json"
	"encoding/xml"
	"errors"
	"fmt"
	"strings"
	"text/template"
	"time"
)

// The oldest systems the builds run on: LSMinimumSystemVersion in macos/build-app.sh and
// SupportedOSPlatformVersion in windows/FlockTray/FlockTray.csproj.
const (
	minimumMacOS   = "15.0"
	minimumWindows = "10.0.19041"
)

// feed is releases.json, kept in the "updates" release. It is the record appcast.xml is made
// from, and the website reads it for its download links and the version it shows.
type feed struct {
	// The newest stable release, and what the beta channel offers (a beta, or the stable
	// release once it is newer).
	Stable *summary `json:"stable,omitempty"`
	Beta   *summary `json:"beta,omitempty"`
	// Newest first: per major version, its newest release and a newer beta if there is one.
	Releases []release `json:"releases"`
}

type summary struct {
	Version string    `json:"version"`
	Date    time.Time `json:"date"`
	Notes   string    `json:"notes"`
	MacOS   string    `json:"macos,omitempty"`
	Windows string    `json:"windows,omitempty"`
}

type release struct {
	Version   string    `json:"version"`
	Date      time.Time `json:"date"`
	NotesURL  string    `json:"notes_url"`
	NotesHTML string    `json:"notes_html"`
	MacOS     *download `json:"macos,omitempty"`
	Windows   *download `json:"windows,omitempty"`
}

type download struct {
	URL                  string `json:"url"`
	Length               int64  `json:"length"`
	SHA256               string `json:"sha256"`
	EdSignature          string `json:"ed_signature,omitempty"` // Sparkle's EdDSA signature (macOS)
	MinimumSystemVersion string `json:"minimum_system_version"`
}

func (r release) version() version {
	v, _ := parseVersion(r.Version)
	return v
}

func parseFeed(data []byte) (*feed, error) {
	f := &feed{}
	if len(bytes.TrimSpace(data)) == 0 {
		return f, nil
	}
	if err := json.Unmarshal(data, f); err != nil {
		return nil, fmt.Errorf("releases.json: %w", err)
	}
	for _, r := range f.Releases {
		if _, ok := parseVersion(r.Version); !ok {
			return nil, fmt.Errorf("releases.json: bad version %q", r.Version)
		}
	}
	return f, nil
}

// add puts a release in the feed (replacing one of the same version) and drops what no updater
// would pick anymore. Each major version keeps its newest release, so a system too old for the
// next major still gets that line's fixes, and its newest beta while that is newer.
func (f *feed) add(r release) {
	releases := []release{r}
	for _, old := range f.Releases {
		if old.Version != r.Version {
			releases = append(releases, old)
		}
	}
	sortVersions(releases, release.version)

	stable := map[int]bool{}
	beta := map[int]bool{}
	f.Releases = nil
	for _, r := range releases {
		v := r.version()
		switch {
		case !v.isBeta() && !stable[v.major]:
			stable[v.major] = true
		case v.isBeta() && !beta[v.major] && !stable[v.major]:
			beta[v.major] = true
		default:
			continue
		}
		f.Releases = append(f.Releases, r)
	}

	f.Stable, f.Beta = nil, nil
	for _, r := range f.Releases {
		if f.Beta == nil {
			f.Beta = r.summary()
		}
		if f.Stable == nil && !r.version().isBeta() {
			f.Stable = r.summary()
		}
	}
}

func (r release) summary() *summary {
	s := &summary{Version: r.Version, Date: r.Date, Notes: r.NotesURL}
	if r.MacOS != nil {
		s.MacOS = r.MacOS.URL
	}
	if r.Windows != nil {
		s.Windows = r.Windows.URL
	}
	return s
}

func (f *feed) json() ([]byte, error) {
	data, err := json.MarshalIndent(f, "", "  ")
	return append(data, '\n'), err
}

// appcast is the Sparkle feed both apps read: an item per release and platform (Sparkle skips
// enclosures for another sparkle:os). Betas are in the "beta" channel; stable releases in none.
var appcast = template.Must(template.New("appcast").Funcs(template.FuncMap{
	"x":     xmlText,
	"cdata": cdata,
}).Parse(`<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle" xmlns:flock="https://flockphp.com/xml-namespaces/update">
  <channel>
    <title>Flock</title>
    <link>https://flockphp.com</link>
{{- range .}}{{$r := .}}{{range .Downloads}}
    <item>
      <title>Flock {{x $r.Version}}</title>
      <pubDate>{{$r.Date.Format "Mon, 02 Jan 2006 15:04:05 -0700"}}</pubDate>
      <sparkle:version>{{x $r.Build}}</sparkle:version>
      <sparkle:shortVersionString>{{x $r.Version}}</sparkle:shortVersionString>
      {{- if $r.Beta}}
      <sparkle:channel>beta</sparkle:channel>
      {{- end}}
      <sparkle:minimumSystemVersion>{{x .MinimumSystemVersion}}</sparkle:minimumSystemVersion>
      <sparkle:fullReleaseNotesLink>{{x $r.NotesURL}}</sparkle:fullReleaseNotesLink>
      <description>{{cdata $r.NotesHTML}}</description>
      <enclosure url="{{x .URL}}" length="{{.Length}}" type="application/octet-stream" sparkle:os="{{.OS}}"
        {{- if .EdSignature}} sparkle:edSignature="{{x .EdSignature}}"{{end}} flock:sha256="{{x .SHA256}}"/>
    </item>
{{- end}}{{end}}
  </channel>
</rss>
`))

type appcastRelease struct {
	release
	Build     string
	Beta      bool
	Downloads []appcastDownload
}

type appcastDownload struct {
	download
	OS string
}

func (f *feed) appcast() ([]byte, error) {
	var items []appcastRelease
	for _, r := range f.Releases {
		v := r.version()
		item := appcastRelease{release: r, Build: v.build(), Beta: v.isBeta()}
		if r.MacOS != nil {
			item.Downloads = append(item.Downloads, appcastDownload{*r.MacOS, "macos"})
		}
		if r.Windows != nil {
			item.Downloads = append(item.Downloads, appcastDownload{*r.Windows, "windows"})
		}
		items = append(items, item)
	}
	var b bytes.Buffer
	err := appcast.Execute(&b, items)
	return b.Bytes(), err
}

func xmlText(s string) string {
	var b strings.Builder
	_ = xml.EscapeText(&b, []byte(s))
	return b.String()
}

func cdata(s string) string {
	return "<![CDATA[" + strings.ReplaceAll(s, "]]>", "]]]]><![CDATA[>") + "]]>"
}

// sign is Sparkle's EdDSA signature of an update: Ed25519 over the file, base64. The key is the
// base64 seed `keygen` printed (SPARKLE_PRIVATE_KEY); the public half is SUPublicEDKey.
func sign(data []byte, key string) (string, error) {
	raw, err := base64.StdEncoding.DecodeString(strings.TrimSpace(key))
	if err != nil {
		return "", fmt.Errorf("signing key: %w", err)
	}
	var private ed25519.PrivateKey
	switch len(raw) {
	case ed25519.SeedSize:
		private = ed25519.NewKeyFromSeed(raw)
	case ed25519.PrivateKeySize:
		private = ed25519.PrivateKey(raw)
	default:
		return "", errors.New("signing key: not an Ed25519 key")
	}
	return base64.StdEncoding.EncodeToString(ed25519.Sign(private, data)), nil
}
