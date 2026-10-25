# Provisional badge art for BoulderTime (ADR-069). One SVG per art key, 96x96, transparent, no text.
import os, sys
OUT = sys.argv[1]
TIERS = {
  "bronze":   ("#E7A766", "#A8642C", "#7A4419"),
  "silver":   ("#EEF1F5", "#AEB7C2", "#6E7884"),
  "gold":     ("#FFE07A", "#E0A21C", "#9A6A06"),
  "platinum": ("#C8F0FF", "#5FB4D9", "#2D6F92"),
  "legend":   ("#FFB36B", "#FF6A1A", "#A9330A"),
  "violet":   ("#D8C2FF", "#8B5CF6", "#4C1D95"),
  "teal":     ("#9FF0DF", "#18A98B", "#0B5E4D"),
  "founder":  ("#FFE9A8", "#F2B632", "#8A5A00"),
}
def grad(gid, tier):
    a, b, c = TIERS[tier]
    return f'''<linearGradient id="{gid}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{a}"/><stop offset="0.55" stop-color="{b}"/><stop offset="1" stop-color="{c}"/></linearGradient>'''
def wrap(defs, body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" width="96" height="96"><defs>{defs}</defs>{body}</svg>\n'

# ---- frames, one shape per category ----
def hexagon(t):   # progression
    _, _, c = TIERS[t]
    return (grad("f", t), f'<path d="M48 4 86 26v44L48 92 10 70V26z" fill="url(#f)" stroke="{c}" stroke-width="3" stroke-linejoin="round"/><path d="M48 14 77 31v34L48 82 19 65V31z" fill="none" stroke="#fff" stroke-opacity=".45" stroke-width="2"/>')
def circle(t):    # consistency
    _, _, c = TIERS[t]
    ticks = "".join(f'<rect x="47" y="7" width="2" height="6" rx="1" fill="#fff" fill-opacity=".55" transform="rotate({i*30} 48 48)"/>' for i in range(12))
    return (grad("f", t), f'<circle cx="48" cy="48" r="43" fill="url(#f)" stroke="{c}" stroke-width="3"/>{ticks}<circle cx="48" cy="48" r="32" fill="none" stroke="#fff" stroke-opacity=".4" stroke-width="2"/>')
def diamond(t):   # experience
    _, _, c = TIERS[t]
    return (grad("f", t), f'<rect x="16" y="16" width="64" height="64" rx="14" transform="rotate(45 48 48)" fill="url(#f)" stroke="{c}" stroke-width="3"/><rect x="24" y="24" width="48" height="48" rx="10" transform="rotate(45 48 48)" fill="none" stroke="#fff" stroke-opacity=".4" stroke-width="2"/>')
def shield(t):    # exploration
    _, _, c = TIERS[t]
    return (grad("f", t), f'<path d="M48 5 84 17v28c0 23-15 38-36 46C27 83 12 68 12 45V17z" fill="url(#f)" stroke="{c}" stroke-width="3" stroke-linejoin="round"/><path d="M48 14 75 23v22c0 17-11 29-27 35-16-6-27-18-27-35V23z" fill="none" stroke="#fff" stroke-opacity=".4" stroke-width="2"/>')
def laurel(t):    # competition
    _, _, c = TIERS[t]
    leaves = ""
    for side in (-1, 1):
        for i in range(6):
            ang = -60 + i * 22
            x = 48 + side * 36 * __import__("math").sin(__import__("math").radians(ang + 90))
            y = 52 - 36 * __import__("math").cos(__import__("math").radians(ang + 90))
            rot = ang * side + (90 if side > 0 else -90)
            leaves += f'<ellipse cx="{x:.1f}" cy="{y:.1f}" rx="4.5" ry="9" fill="#3E9B4F" stroke="#24692F" stroke-width="1.2" transform="rotate({rot:.0f} {x:.1f} {y:.1f})"/>'
    return (grad("f", t), f'{leaves}<circle cx="48" cy="48" r="29" fill="url(#f)" stroke="{c}" stroke-width="3"/><circle cx="48" cy="48" r="22" fill="none" stroke="#fff" stroke-opacity=".4" stroke-width="2"/>')
def star(t):      # special
    _, _, c = TIERS[t]
    import math
    pts = []
    for i in range(16):
        r = 44 if i % 2 == 0 else 36
        a = math.radians(i * 22.5 - 90)
        pts.append(f"{48 + r*math.cos(a):.1f},{48 + r*math.sin(a):.1f}")
    return (grad("f", t), f'<polygon points="{" ".join(pts)}" fill="url(#f)" stroke="{c}" stroke-width="3" stroke-linejoin="round"/><circle cx="48" cy="48" r="27" fill="none" stroke="#fff" stroke-opacity=".45" stroke-width="2"/>')

# ---- pictograms (white with a dark outline, readable on every tier) ----
def ink(w=2.5): return f'fill="#fff" stroke="#1A1A1A" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"'
INK = ink()
def line(w=3): return f'fill="none" stroke="#1A1A1A" stroke-width="{w}" stroke-linecap="round" stroke-linejoin="round"'
LINE = line()
def mountain(flag=False, peaks=1):
    p = f'<path d="M22 66 40 38l7 10 9-16 18 34z" {INK}/><path d="M36 44l4-6 4 6-4 2z" fill="#1A1A1A"/>'
    if flag: p += f'<path d="M56 32V18" {LINE}/><path d="M56 18h11l-3 4 3 4H56" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="2.2" stroke-linejoin="round"/>'
    return p
def chevrons(n):
    out = ""
    for i in range(n):
        y = 74 - i * 5
        out += f'<path d="M40 {y}l8-4 8 4" {line(2.6)}/>'
    return out
def hold(cx=48, cy=48, s=1.0, color="#fff"):
    # A climbing hold seen from the front: an irregular jug with its bolt and a darker grip edge.
    return (f'<g transform="translate({cx} {cy}) scale({s})">'
            f'<path d="M-17 4c-3-10 3-19 13-21 8-2 17 1 21 8 4 8 1 17-6 20-9 4-25 3-28-7z" fill="{color}" stroke="#1A1A1A" stroke-width="2.6" stroke-linejoin="round"/>'
            f'<path d="M-12 9c7 4 18 4 24-1" fill="none" stroke="#1A1A1A" stroke-width="2.4" stroke-linecap="round"/>'
            f'<circle cx="1" cy="-5" r="3.6" fill="#1A1A1A"/><circle cx="1" cy="-5" r="1.4" fill="#9AA1AA"/></g>')
def flame():
    return f'<path d="M48 22c3 9 13 13 13 26a13 13 0 0 1-26 0c0-7 4-11 7-15 1 5 3 7 6 7-2-6-2-12 0-18z" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="2.5" stroke-linejoin="round"/><path d="M48 48c2 3 5 4 5 8a5 5 0 0 1-10 0c0-3 2-5 5-8z" fill="#FFD25E"/>'
def calendar_dots(n):
    import math
    out = f'<rect x="30" y="30" width="36" height="34" rx="6" {INK}/><path d="M30 41h36" {line(2.5)}/><path d="M38 26v8M58 26v8" {LINE}/>'
    for i in range(min(n, 6)):
        x = 37 + (i % 3) * 11; y = 49 + (i // 3) * 8
        out += f'<circle cx="{x}" cy="{y}" r="3" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="1.4"/>'
    return out
def repeat_arrows():
    return f'<path d="M30 56a19 19 0 0 1 32-19" {LINE}/><path d="M64 28v10H54" {LINE}/><path d="M66 42a19 19 0 0 1-32 19" {LINE}/><path d="M32 70V60h10" {LINE}/>'
def target_check():
    return f'<circle cx="48" cy="48" r="22" {INK}/><circle cx="48" cy="48" r="13" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="2.5"/><circle cx="48" cy="48" r="5" fill="#fff" stroke="#1A1A1A" stroke-width="2"/><path d="M54 30 62 38 76 22" fill="none" stroke="#1A1A1A" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/><path d="M54 30 62 38 76 22" fill="none" stroke="#3E9B4F" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"/>'
def pin(n):
    out = f'<path d="M22 70 36 50l6 8 8-12 16 24z" {ink(2.2)}/>'
    out += f'<path d="M48 18c-9 0-15 6-15 15 0 11 15 24 15 24s15-13 15-24c0-9-6-15-15-15z" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="2.6" stroke-linejoin="round"/><circle cx="48" cy="33" r="5.5" fill="#fff" stroke="#1A1A1A" stroke-width="2"/>'
    for i in range(n - 1):
        x = 30 + i * 12 if n <= 4 else 26 + i * 9
        out += f'<circle cx="{x + (0 if n<=4 else 0)}" cy="78" r="3" fill="#fff" stroke="#1A1A1A" stroke-width="1.6"/>'
    return out
def compass():
    return f'<circle cx="48" cy="48" r="20" {INK}/><path d="M48 31l6 17-6 17-6-17z" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="2.2" stroke-linejoin="round"/><path d="M48 48l6 0-6 17z" fill="#fff"/><circle cx="48" cy="48" r="3" fill="#1A1A1A"/>'
def trophy(mark=None):
    out = f'<path d="M36 28h24v10c0 9-5 15-12 15s-12-6-12-15z" {INK}/><path d="M36 32h-6c0 7 3 11 8 11M60 32h6c0 7-3 11-8 11" {line(2.6)}/><path d="M48 53v8" {LINE}/><path d="M38 66h20l-2-5H40z" {INK}/>'
    if mark == "moon": out += '<path d="M51 33a6 6 0 1 0 2 10 5 5 0 1 1-2-10z" fill="#1A1A1A"/>'
    if mark == "star": out += '<path d="M48 32l2.4 5 5.4.6-4 3.7 1.1 5.3-4.9-2.7-4.9 2.7 1.1-5.3-4-3.7 5.4-.6z" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="1.4" stroke-linejoin="round"/>'
    return out
def stars(n):
    out = ""
    xs = {2: [38, 58], 3: [32, 48, 64]}[n]
    for i, x in enumerate(xs):
        y = 44 if (n == 3 and i == 1) else 50
        s = 1.25 if (n == 3 and i == 1) else 1.05
        out += f'<path transform="translate({x} {y}) scale({s})" d="M0-10l3 6.2 6.8.8-5 4.6 1.4 6.7L0 4.9-6.2 8.3l1.4-6.7-5-4.6 6.8-.8z" fill="#fff" stroke="#1A1A1A" stroke-width="2" stroke-linejoin="round"/>'
    if n == 3: out += f'<path d="M34 66h28l3-12-9 6-8-10-8 10-9-6z" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="2.2" stroke-linejoin="round"/>'
    else: out += f'<path d="M36 66h24" {LINE}/>'
    return out
def crown():
    return f'<path d="M28 60l-3-24 13 10 10-16 10 16 13-10-3 24z" fill="#FFE07A" stroke="#1A1A1A" stroke-width="2.6" stroke-linejoin="round"/><path d="M29 66h38" {LINE}/><circle cx="48" cy="48" r="3.5" fill="#FF6A1A" stroke="#1A1A1A" stroke-width="1.5"/>'
def flask():
    return f'<path d="M42 24h12M44 24v14L30 64a5 5 0 0 0 4.5 7h27a5 5 0 0 0 4.5-7L52 38V24" {INK}/><path d="M35 58h26l4 8a3 3 0 0 1-3 4H34a3 3 0 0 1-3-4z" fill="#8B5CF6"/>' + hold(46, 60, 0.42, "#FF6A1A") + '<circle cx="56" cy="50" r="2.5" fill="#fff" stroke="#1A1A1A" stroke-width="1.2"/><circle cx="52" cy="44" r="1.8" fill="#fff" stroke="#1A1A1A" stroke-width="1"/>'
def founder():
    # Carabiner around a peak with a star on top: the one who opened the route.
    return (f'<path d="M37 26c-8 0-12 6-12 14v18c0 9 6 15 15 15h16c9 0 15-6 15-15V40c0-8-4-14-12-14z" fill="none" stroke="#1A1A1A" stroke-width="9" stroke-linejoin="round"/>'
            f'<path d="M37 26c-8 0-12 6-12 14v18c0 9 6 15 15 15h16c9 0 15-6 15-15V40c0-8-4-14-12-14z" fill="none" stroke="#C7CFD8" stroke-width="5" stroke-linejoin="round"/>'
            f'<path d="M59 26v14" stroke="#1A1A1A" stroke-width="7" stroke-linecap="round"/><path d="M59 26v14" stroke="#FF6A1A" stroke-width="3.5" stroke-linecap="round"/>'
            f'<path d="M31 64 43 46l5 7 6-10 11 21z" fill="#1F2A44" stroke="#1A1A1A" stroke-width="2" stroke-linejoin="round"/>'
            f'<path d="M48 22l3 6.5 7 .8-5.2 4.8 1.4 7-6.2-3.6-6.2 3.6 1.4-7-5.2-4.8 7-.8z" fill="#FFE07A" stroke="#1A1A1A" stroke-width="2" stroke-linejoin="round"/>')
def placeholder():
    return ('', '<circle cx="48" cy="48" r="42" fill="#D6D9DE" stroke="#9AA1AA" stroke-width="3" stroke-dasharray="6 5"/>' + hold(48, 50, 1.0, "#F4F5F7"))

ART = {
  # Progression — hexagon, tier by threshold
  "progression-1":   (hexagon("bronze"),   mountain()),
  "progression-10":  (hexagon("silver"),   mountain() + chevrons(1)),
  "progression-50":  (hexagon("gold"),     mountain() + chevrons(2)),
  "progression-100": (hexagon("platinum"), mountain(flag=True) + chevrons(2)),
  "progression-500": (hexagon("legend"),   mountain(flag=True) + chevrons(3)),
  # Consistency — sun-dial circle
  "consistency-3":   (circle("bronze"),   calendar_dots(3)),
  "consistency-10":  (circle("silver"),   calendar_dots(6)),
  "consistency-30":  (circle("gold"),     flame()),
  "consistency-100": (circle("legend"),   flame()),
  # Experience — rounded diamond
  "experience-attempts-10":  (diamond("bronze"), hold(48, 48, 1.15, "#FF6A1A")),
  "experience-attempts-50":  (diamond("silver"), repeat_arrows() + hold(48, 48, 0.85, "#FF6A1A")),
  "experience-attempts-100": (diamond("gold"),   repeat_arrows() + hold(48, 48, 0.85, "#FF6A1A")),
  "experience-project-closed": (diamond("teal"), target_check()),
  # Exploration — shield
  "exploration-1":  (shield("bronze"),   pin(1)),
  "exploration-3":  (shield("silver"),   pin(3)),
  "exploration-5":  (shield("gold"),     compass()),
  "exploration-10": (shield("platinum"), compass()),
  # Competition — laurel wreath
  "competition-month":        (laurel("silver"), trophy("moon")),
  "competition-year":         (laurel("gold"),   trophy("star")),
  "competition-double":       (laurel("gold"),   stars(2)),
  "competition-dominator":    (laurel("legend"), stars(3)),
  "competition-gym-legend":   (laurel("platinum"), crown()),
  # Special — sixteen-point star
  "special-beta-tester": (star("violet"),  flask()),
  "special-founder":     (star("founder"), founder()),
}
os.makedirs(OUT, exist_ok=True)
for key, ((defs, frame), body) in ART.items():
    open(os.path.join(OUT, key + ".svg"), "w").write(wrap(defs, frame + body))
d, f = placeholder()
open(os.path.join(OUT, "placeholder.svg"), "w").write(wrap(d, f))
print(len(ART) + 1, "files")
