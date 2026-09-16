# Brand assets

Source of truth: the official files in [`originals/`](brand/originals) —
`app-icon-original.png` and `logo-horizontal-original.png` — and the *BoulderTime Brand Asset Sheet v1.0*
(Charcoal `#1A1A1A`, Orange `#FF7A2E`, Light `#F8F8F8`, Inter).

**The artwork is never redrawn.** Every file in `frontend/public/assets/` is produced from the originals only by
removing the white background, resizing, and (for the dark logo) recolouring the charcoal ink to white.

| File | Derived from | Processing | Use |
|---|---|---|---|
| `logo-horizontal.png` | logo original | white background → transparent, cropped, 1200 px wide | Light backgrounds (mobile top bar, auth screens) |
| `logo-horizontal-dark.png` | logo original | as above, charcoal ink recoloured to white; orange unchanged | Charcoal backgrounds (desktop sidebar) |
| `app-icon-192.png`, `app-icon-512.png` | icon original | outside of the rounded square → transparent | PWA manifest, in-app icon (`<Logo variant="icon">`) |
| `app-icon-180.png`, `app-icon-1024.png` | icon original | corners filled with the icon's own background colour (full-bleed) | Apple touch icon, app stores — the OS applies its own corner mask |
| `favicon-16.png`, `favicon-32.png`, `/favicon.ico` | icon original | transparent version, downscaled | Browser tabs |

The official set doesn't include a vertical lockup or a standalone mark without the square, so the UI uses the
horizontal logo and the app icon only. If those files are produced later, add them here and extend `<Logo>`.

## UI rules
- Always render logos through `<Logo>`; never substitute a generic climbing icon.
- Interface icons use **lucide-react** exclusively.
- Orange text on white fails contrast — use `--bt-orange-ink` for orange text; bright orange is for fills and accents.
