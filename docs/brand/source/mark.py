import cairosvg
def mark_body(slab="#1A1A1A", accent="#FF7A2E", fig="#1A1A1A"):
    return f'''<path d="M86 428 L 204 98 L 258 104 L 112 448 Z" fill="{slab}" stroke="{slab}" stroke-width="28" stroke-linejoin="round"/>
  <path d="M334 64 L 380 98 L 398 384 L 366 418 L 338 300 Z" fill="{accent}" stroke="{accent}" stroke-width="26" stroke-linejoin="round"/>
  <g fill="none" stroke="{fig}" stroke-linecap="round" stroke-linejoin="round">
    <path stroke-width="24" d="M250 102 L 268 176"/>
    <path stroke-width="58" d="M272 184 C 286 214, 280 244, 266 262"/>
    <path stroke-width="22" d="M268 192 L 304 226 L 324 198"/>
    <path stroke-width="31" d="M266 262 L 320 266 L 334 308"/>
    <path stroke-width="31" d="M262 266 L 238 336 L 288 400"/>
  </g>
  <circle cx="284" cy="150" r="24" fill="{fig}"/>'''
def svg(inner, viewbox):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{viewbox}">{inner}</svg>'
if __name__ == "__main__":
    s = svg(mark_body(), "52 40 370 420")
    open("t.svg","w").write(s)
    cairosvg.svg2png(bytestring=s.encode(), write_to="t.png", output_width=320)
    d = svg('<rect x="52" y="40" width="370" height="420" fill="#1A1A1A"/>'+mark_body("#fff","#FF7A2E","#fff"), "52 40 370 420")
    cairosvg.svg2png(bytestring=d.encode(), write_to="t_dark.png", output_width=320)
