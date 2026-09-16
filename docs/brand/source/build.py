import cairosvg, os
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from mark import mark_body

CH, OR, LT = "#1A1A1A", "#FF7A2E", "#F8F8F8"

def text_path(fontfile, text, size, x, y, tracking=0.0):
    f = TTFont(fontfile); gs = f.getGlyphSet(); cmap = f.getBestCmap()
    upm = f['head'].unitsPerEm; sc = size/upm; hmtx = f['hmtx']
    kern = {}
    pen = SVGPathPen(gs); cx = 0
    for ch in text:
        g = cmap[ord(ch)]
        tp = TransformPen(pen, (sc, 0, 0, -sc, x + cx, y))
        gs[g].draw(tp)
        cx += hmtx[g][0]*sc + tracking*size
    return pen.getCommands(), cx - tracking*size

B = "package/files/inter-latin-800-normal.woff"
M = "package/files/inter-latin-600-normal.woff"
MARK_VB = (52, 40, 370, 420)

def wordmark(x, y, size, boulder_col, time_col, tag_col, tagline=True, center=False):
    d1, w1 = text_path(B, "Boulder", size, 0, 0, -0.035)
    d2, w2 = text_path(B, "Time", size, 0, 0, -0.035)
    total = w1 + w2 - 0.035*size
    tsize = size*0.235
    d3, w3 = text_path(M, "CLIMB MORE. TOGETHER.", tsize, 0, 0, 0.42)
    ox = x - total/2 if center else x
    out = f'<path transform="translate({ox:.1f} {y})" fill="{boulder_col}" d="{d1}"/>'
    out += f'<path transform="translate({ox + w1 - 0.035*size:.1f} {y})" fill="{time_col}" d="{d2}"/>'
    if tagline:
        tx = (x - w3/2) if center else ox + (total - w3)/2
        out += f'<path transform="translate({tx:.1f} {y + size*0.52:.1f})" fill="{tag_col}" d="{d3}"/>'
    return out, total

def mark_at(x, y, h, cols):
    s = h / MARK_VB[3]
    return f'<g transform="translate({x} {y}) scale({s:.4f}) translate({-MARK_VB[0]} {-MARK_VB[1]})">{mark_body(*cols)}</g>'

def save(name, w, h, inner, bg=None, radius=0):
    rect = f'<rect width="{w}" height="{h}" rx="{radius}" fill="{bg}"/>' if bg else ''
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}">{rect}{inner}</svg>'
    open(f"out/{name}.svg", "w").write(svg)
    return svg

os.makedirs("out", exist_ok=True)
light = (CH, OR, CH); dark = ("#FFFFFF", OR, "#FFFFFF")

for variant, cols, bcol, tag in [("", light, CH, CH), ("-dark", dark, "#FFFFFF", "#FFFFFF")]:
    bg = CH if variant else None
    # horizontal
    wm, tw = wordmark(0, 0, 100, bcol, OR, tag)
    W = 30 + 132 + 26 + tw + 30
    inner = mark_at(30, 22, 150, cols) + f'<g transform="translate({30+132+26} 112)">{wm}</g>'
    save("logo-horizontal"+variant, round(W), 194, inner, bg, 24 if bg else 0)
    # vertical
    wm, tw = wordmark(0, 0, 88, bcol, OR, tag, center=True)
    W = round(tw + 60)
    inner = mark_at(W/2 - 118, 24, 270, cols) + f'<g transform="translate({W/2} 380)">{wm}</g>'
    save("logo-vertical"+variant, W, 450, inner, bg, 28 if bg else 0)
    # mark
    save("logo-mark"+variant, 420, 420, mark_at(34, 18, 384, cols), bg, 72 if bg else 0)

# app icon + favicon: charcoal rounded square, white slab/climber, orange volume
icon = save("app-icon", 1024, 1024, mark_at(218, 190, 644, dark), CH, 0)  # iOS/Android apply their own mask
fav = save("favicon", 64, 64, mark_at(10, 8, 48, dark), CH, 14)
cairosvg.svg2png(bytestring=icon.encode(), write_to="out/app-icon-1024.png", output_width=1024)
for n in (512, 192, 180):
    cairosvg.svg2png(bytestring=icon.encode(), write_to=f"out/app-icon-{n}.png", output_width=n)
for n in (32, 16, 48):
    cairosvg.svg2png(bytestring=fav.encode(), write_to=f"out/favicon-{n}.png", output_width=n)
from PIL import Image
Image.open("out/favicon-48.png").save("out/favicon.ico", sizes=[(16,16),(32,32),(48,48)])
print("done")
