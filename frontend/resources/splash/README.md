# Launch screen

The app opens on a dark wall: faceted panels and holds in two corners, BoulderTime's logo in the middle. It is native
on both platforms, so it shows before the app's code has loaded, and the splash-screen plugin keeps it up until the
first screen is drawn.

| File | What it is | Used by |
|---|---|---|
| `logo.png` | Climber above the name, transparent, 1200 px wide | iOS `SplashLogo` |
| `corner-top-left.png`, `corner-bottom-right.png` | The wall in the two corners, transparent | iOS `SplashCornerTop`, `SplashCornerBottom` |
| `corner-*.svg` | The same corners as vector, to edit them | — |
| `android-icon.png` | The climber alone, sized for Android's 288dp icon area (it is cut to a circle) | `drawable-*/splash_icon.png` |
| `android-branding.png` | The name, 200×80dp | `drawable-*/splash_branding.png` (Android 12+) |

The logo is cut from the originals in `docs/brand/originals`, not redrawn: the climber from the app icon (its dark tile
removed), the name from the horizontal logo (black turned to white for the dark background).

**iOS** (`ios/App/App/Base.lproj/LaunchScreen.storyboard`): the corners are pinned to their corner and sized to the
screen (39% and 87% of the width, capped by height), the logo is centred at 62% of the width, at most 320pt. Nothing
is cropped or stretched on any iPhone, upright or sideways. iOS caches the launch screen: after changing it, delete
the app and install it again to see the new one.

**Android** (`values/styles.xml`, `values-v31/styles.xml`): since Android 12 the system draws the launch screen and
allows only a background colour, a centred icon and a small image at the bottom. So Android shows the dark background,
the climber and, on Android 12+, the name at the bottom; the wall can't be shown there.

Background colour on both: `#0C0D0F`.
