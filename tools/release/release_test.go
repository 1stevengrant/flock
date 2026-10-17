package main

import (
	"crypto/ed25519"
	"encoding/base64"
	"strings"
	"testing"
	"time"
)

func TestVersionOrder(t *testing.T) {
	order := []string{"1.0.0", "1.0.1-beta.1", "1.0.1", "1.1.0-beta.1", "1.1.0-beta.2", "1.1.0-beta.10", "1.1.0", "2.0.0-beta.1"}
	for i := 1; i < len(order); i++ {
		a, _ := parseVersion(order[i-1])
		b, _ := parseVersion(order[i])
		if a.compare(b) >= 0 || b.compare(a) <= 0 {
			t.Errorf("%s should sort below %s", a, b)
		}
	}
	for _, bad := range []string{"1.0", "1.0.0-beta", "1.0.0-beta.0", "1.0.0-rc.1", "dev-abc123", "v1.0.0-3-gabc"} {
		if _, ok := parseVersion(bad); ok {
			t.Errorf("%q parsed", bad)
		}
	}
}

func TestBuildNumbersSortLikeVersions(t *testing.T) {
	for v, want := range map[string]string{"1.1.0-beta.2": "1.1.0.2", "1.1.0": "1.1.0.1000", "v2.0.0-beta.12": "2.0.0.12"} {
		parsed, _ := parseVersion(v)
		if got := parsed.build(); got != want {
			t.Errorf("build(%s) = %s, want %s", v, got, want)
		}
	}
}

func TestNextBeta(t *testing.T) {
	v := func(s string) *version { p, _ := parseVersion(s); return &p }
	fix := commit{"a", "fix: a bug"}
	feat := commit{"b", "feat(sites): a feature"}
	breaking := commit{"c", "refactor!: drop something"}
	footer := commit{"d", "chore: x\n\nBREAKING CHANGE: it changed"}

	tests := []struct {
		name    string
		last    *version
		commits []commit
		tags    []string
		want    string
		err     string
	}{
		{"first ever", nil, []commit{feat}, nil, "1.0.0-beta.1", ""},
		{"fix", v("1.0.0"), []commit{fix}, []string{"v1.0.0"}, "1.0.1-beta.1", ""},
		{"feature", v("1.0.0"), []commit{fix, feat}, []string{"v1.0.0"}, "1.1.0-beta.1", ""},
		{"breaking", v("1.4.2"), []commit{fix, breaking}, nil, "2.0.0-beta.1", ""},
		{"breaking footer", v("1.4.2"), []commit{footer}, nil, "2.0.0-beta.1", ""},
		{"counts up", v("1.0.0"), []commit{feat}, []string{"v1.0.0", "v1.1.0-beta.1", "v1.1.0-beta.2"}, "1.1.0-beta.3", ""},
		{"after a hotfix", v("1.0.1"), []commit{feat}, []string{"v1.0.0", "v1.1.0-beta.1", "v1.0.1"}, "1.1.0-beta.2", ""},
		{"never back", v("1.0.0"), []commit{fix}, []string{"v1.1.0-beta.1"}, "", "already released"},
		{"maintenance line", v("1.4.2"), []commit{fix}, []string{"v1.4.2", "v2.0.0", "v2.1.0-beta.1"}, "1.4.3-beta.1", ""},
		{"new major past old betas", v("1.4.2"), []commit{breaking}, []string{"v1.5.0-beta.1"}, "2.0.0-beta.1", ""},
		{"nothing", v("1.0.0"), nil, nil, "", "nothing to release"},
	}
	for _, tt := range tests {
		got, err := nextBeta(tt.last, tt.commits, tt.tags)
		switch {
		case tt.err != "" && (err == nil || !strings.Contains(err.Error(), tt.err)):
			t.Errorf("%s: err = %v, want %q", tt.name, err, tt.err)
		case tt.err == "" && err != nil:
			t.Errorf("%s: %v", tt.name, err)
		case tt.err == "" && got.String() != tt.want:
			t.Errorf("%s: got %s, want %s", tt.name, got, tt.want)
		}
	}
}

func TestLastStableIgnoresBetas(t *testing.T) {
	if got := lastStable([]string{"v1.0.0", "v1.1.0-beta.1", "v1.0.1", "updates", "manifest"}); got == nil || got.String() != "1.0.1" {
		t.Fatalf("got %v", got)
	}
	if lastStable([]string{"v1.0.0-beta.1"}) != nil {
		t.Fatal("a beta is not a stable release")
	}
}

func TestNotes(t *testing.T) {
	got := notes("flock-php/flock", []commit{
		{"1111111aaaa", "fix(dns): resolve faster"},
		{"2222222bbbb", "feat: update the app\n\nlonger body"},
		{"3333333cccc", "chore: tidy"},
		{"4444444dddd", "not conventional"},
	})
	want := "### Features\n\n* update the app ([2222222](https://github.com/flock-php/flock/commit/2222222bbbb))\n\n" +
		"### Bug Fixes\n\n* **dns:** resolve faster ([1111111](https://github.com/flock-php/flock/commit/1111111aaaa))\n\n"
	if got != want {
		t.Fatalf("got\n%s\nwant\n%s", got, want)
	}
	if notes("", []commit{{"x", "chore: tidy"}}) == "" {
		t.Fatal("notes without visible changes must still say something")
	}
}

func TestFeedKeepsWhatUpdatersPick(t *testing.T) {
	f := &feed{}
	add := func(v string) {
		f.add(release{Version: v, Date: time.Date(2026, 10, 17, 12, 0, 0, 0, time.UTC),
			MacOS: &download{URL: "https://example.com/" + v + ".dmg", MinimumSystemVersion: minimumMacOS}})
	}
	versions := func() string {
		var vs []string
		for _, r := range f.Releases {
			vs = append(vs, r.Version)
		}
		return strings.Join(vs, " ")
	}

	add("1.0.0")
	add("1.1.0-beta.1")
	add("1.1.0-beta.2")
	if got := versions(); got != "1.1.0-beta.2 1.0.0" {
		t.Fatalf("betas: %s", got)
	}
	if f.Stable.Version != "1.0.0" || f.Beta.Version != "1.1.0-beta.2" {
		t.Fatalf("summaries: %+v %+v", f.Stable, f.Beta)
	}

	add("1.0.1") // a hotfix while 1.1.0 is in beta
	if got := versions(); got != "1.1.0-beta.2 1.0.1" {
		t.Fatalf("hotfix: %s", got)
	}

	add("1.1.0")
	if got := versions(); got != "1.1.0" || f.Beta.Version != "1.1.0" {
		t.Fatalf("release: %s, beta channel %s", got, f.Beta.Version)
	}

	add("2.0.0-beta.1")
	add("2.0.0")
	add("1.1.1") // a fix for systems 2.x no longer supports
	if got := versions(); got != "2.0.0 1.1.1" || f.Stable.Version != "2.0.0" {
		t.Fatalf("lines: %s", got)
	}

	data, err := f.json()
	if err != nil {
		t.Fatal(err)
	}
	again, err := parseFeed(data)
	if err != nil || len(again.Releases) != 2 {
		t.Fatalf("round trip: %v %+v", err, again)
	}
}

func TestAppcast(t *testing.T) {
	f := &feed{}
	f.add(release{
		Version: "1.1.0-beta.2", Date: time.Date(2026, 10, 17, 12, 0, 0, 0, time.UTC),
		NotesURL: "https://github.com/flock-php/flock/releases/tag/v1.1.0-beta.2", NotesHTML: "<p>Fixes &amp; more ]]> end</p>",
		MacOS:   &download{URL: "https://x/Flock-1.1.0-beta.2-macOS.dmg", Length: 10, SHA256: "aa", EdSignature: "sig", MinimumSystemVersion: "15.0"},
		Windows: &download{URL: "https://x/Flock-Setup-1.1.0-beta.2-x64.exe", Length: 20, SHA256: "bb", MinimumSystemVersion: "10.0.19041"},
	})
	data, err := f.appcast()
	if err != nil {
		t.Fatal(err)
	}
	out := string(data)
	for _, want := range []string{
		"<sparkle:version>1.1.0.2</sparkle:version>",
		"<sparkle:shortVersionString>1.1.0-beta.2</sparkle:shortVersionString>",
		"<sparkle:channel>beta</sparkle:channel>",
		`sparkle:os="macos" sparkle:edSignature="sig" flock:sha256="aa"/>`,
		`sparkle:os="windows" flock:sha256="bb"/>`,
		"<sparkle:minimumSystemVersion>10.0.19041</sparkle:minimumSystemVersion>",
		"<![CDATA[<p>Fixes &amp; more ]]]]><![CDATA[> end</p>]]>",
		"Sat, 17 Oct 2026 12:00:00 +0000",
	} {
		if !strings.Contains(out, want) {
			t.Errorf("appcast lacks %q:\n%s", want, out)
		}
	}
	if strings.Count(out, "<item>") != 2 {
		t.Errorf("want an item per platform:\n%s", out)
	}
}

func TestSignVerifiesWithPublicKey(t *testing.T) {
	public, private, _ := ed25519.GenerateKey(nil)
	sig, err := sign([]byte("update"), base64.StdEncoding.EncodeToString(private.Seed()))
	if err != nil {
		t.Fatal(err)
	}
	raw, _ := base64.StdEncoding.DecodeString(sig)
	if !ed25519.Verify(public, []byte("update"), raw) {
		t.Fatal("signature doesn't verify")
	}
	if _, err := sign(nil, "bm90IGEga2V5"); err == nil {
		t.Fatal("a short key must fail")
	}
}
