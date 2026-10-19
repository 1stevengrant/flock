package share

import (
	"bytes"
	"encoding/base64"
	"image"
	"image/color"
	"image/png"
	"strings"

	"rsc.io/qr"
)

// quiet is the white border around a QR code, in modules, that scanners need.
const quiet = 4

// QRPNG renders text as a QR code, a PNG with scale pixels per module, base64-encoded for JSON.
func QRPNG(text string, scale int) (string, error) {
	code, err := qr.Encode(text, qr.M)
	if err != nil {
		return "", err
	}
	side := (code.Size + 2*quiet) * scale
	img := image.NewGray(image.Rect(0, 0, side, side))
	for y := range side {
		for x := range side {
			c := color.Gray{Y: 0xFF}
			if code.Black(x/scale-quiet, y/scale-quiet) {
				c = color.Gray{}
			}
			img.SetGray(x, y, c)
		}
	}
	var buf bytes.Buffer
	if err := png.Encode(&buf, img); err != nil {
		return "", err
	}
	return base64.StdEncoding.EncodeToString(buf.Bytes()), nil
}

// QRText renders text as a QR code for a terminal: two modules per character with half blocks,
// dark on light, whatever the terminal's colours (scanners need the light border).
func QRText(text string) (string, error) {
	code, err := qr.Encode(text, qr.L)
	if err != nil {
		return "", err
	}
	var b strings.Builder
	for y := -quiet; y < code.Size+quiet; y += 2 {
		// Black foreground on a white background; the half blocks draw the dark modules.
		b.WriteString("  \x1b[30;107m")
		for x := -quiet; x < code.Size+quiet; x++ {
			switch top, bottom := code.Black(x, y), code.Black(x, y+1); {
			case top && bottom:
				b.WriteString("█")
			case top:
				b.WriteString("▀")
			case bottom:
				b.WriteString("▄")
			default:
				b.WriteString(" ")
			}
		}
		b.WriteString("\x1b[0m\n")
	}
	return b.String(), nil
}
