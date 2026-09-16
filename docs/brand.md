# Brand assets

Source of truth: *BoulderTime Brand Asset Sheet v1.0* — Charcoal `#1A1A1A`, Orange `#FF7A2E`, Light `#F8F8F8`, Inter.

`frontend/public/assets/` contains:

| File | Use |
|---|---|
| `logo-horizontal.svg` / `-dark.svg` | Headers, sidebar (dark variant on charcoal) |
| `logo-vertical.svg` / `-dark.svg` | Auth screens, splash |
| `logo-mark.svg` / `-dark.svg` | Compact placements, hero |
| `favicon.svg`, `favicon-16.png`, `favicon-32.png`, `/favicon.ico` | Browser tabs |
| `app-icon.svg`, `app-icon-{180,192,512,1024}.png` | Apple touch icon, PWA manifest, stores |

## ⚠️ These are faithful rebuilds, not the designer's originals

The brand sheet was supplied as a raster image, so these vectors were redrawn from it: the wordmark is Inter
ExtraBold converted to outlines, and the climber mark was re-drawn to match the sheet's composition (dark slab,
orange volume, hanging climber). They are consistent with the sheet but not pixel-identical.

**If you have the original `logo-*.svg` files listed on the sheet, drop them into `frontend/public/assets/` with
the same names.** Every component loads logos by filename through `<Logo>`, so no code changes are needed.

`docs/brand/source/build.py` regenerates all variants and raster sizes from the mark definition in `mark.py`
(`pip install cairosvg fonttools pillow`, Inter WOFF files from `@fontsource/inter`).

## UI rules
- The logo is a product asset; never substitute a generic climbing icon for it.
- Interface icons use **lucide-react** exclusively.
- Orange text on white fails contrast — use `--bt-orange-ink` for orange text; bright orange is for fills and accents.
- The slanted orange bar used for active navigation is lifted from the mark's volume shape; reuse it for "current/selected" states rather than inventing new indicators.
