"""Server icon (512x512) and banner (960x540) for the Discord server, in the same notebook style as the itch cover.
Output: <out>/discord-icon.png and <out>/discord-banner.png. Needs Pillow; fonts come from godot/assets/fonts (OFL)."""
import os, sys
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "godot", "assets", "fonts")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "local", "discord")
os.makedirs(OUT, exist_ok=True)
GREEN, GREEN2 = (46, 74, 62), (42, 69, 58)
CHALK, CREAM, INK = (226, 232, 220), (236, 224, 200), (36, 32, 30)
RED, BLUE, YEL, RULE = (196, 62, 44), (52, 100, 168), (242, 201, 76), (190, 176, 150)


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def pitch(w, h, stripe):
    img = Image.new("RGB", (w, h), GREEN)
    d = ImageDraw.Draw(img)
    for i in range(0, w, stripe):
        if (i // stripe) % 2:
            d.rectangle([i, 0, i + stripe, h], fill=GREEN2)
    return img, d


def dot(d, x, y, c, r):
    d.ellipse([x - r + 3, y - r + 4, x + r + 3, y + r + 4], fill=(20, 36, 30))
    d.ellipse([x - r, y - r, x + r, y + r], fill=c, outline=(250, 246, 236), width=max(2, r // 6))


def paper(w, h, lines_at, margin_x):
    page = Image.new("RGBA", (w, h), CREAM + (255,))
    pd = ImageDraw.Draw(page)
    for y in lines_at:
        pd.line([(0, y), (w, y)], fill=RULE, width=2)
    pd.line([(margin_x, 0), (margin_x, h)], fill=(214, 110, 98), width=3)
    return page, pd


def tilt_onto(img, page, center, angle):
    shadow = Image.new("RGBA", page.size, (10, 20, 16, 150))
    for layer, off in ((shadow, 8), (page, 0)):
        rot = layer.rotate(angle, expand=True, resample=Image.BICUBIC)
        img.paste(rot, (center[0] - rot.width // 2 + off, center[1] - rot.height // 2 + off), rot)


# Icon: field markings, tilted notebook page with "GN", a few players.
icon, d = pitch(512, 512, 64)
d.rectangle([24, 24, 488, 488], outline=CHALK, width=4)
d.line([(256, 24), (256, 488)], fill=CHALK, width=4)
d.ellipse([176, 176, 336, 336], outline=CHALK, width=4)
for p, c in (((90, 400), RED), ((190, 440), RED), ((330, 420), RED), ((420, 380), RED), ((120, 330), BLUE), ((380, 330), BLUE), ((250, 460), BLUE)):
    dot(d, *p, c, 18)
page, pd = paper(380, 300, range(40, 300, 34), 52)
f = font("BigShouldersDisplay-ExtraBold.ttf", 190)
pd.text(((380 - pd.textlength("GN", font=f)) / 2 + 20, 10), "GN", font=f, fill=INK)
pd.rectangle([84, 236, 340, 254], fill=YEL)
tilt_onto(icon, page, (256, 230), 5)
icon.save(os.path.join(OUT, "discord-icon.png"))

# Banner: wide pitch with the title page and a tagline; the title font shrinks until it fits the page.
W, H = 960, 540
ban, d = pitch(W, H, 80)
m = 30
d.rectangle([m, m, W - m, H - m], outline=CHALK, width=4)
d.line([(W // 2, m), (W // 2, H - m)], fill=CHALK, width=4)
d.ellipse([W // 2 - 90, H // 2 - 90, W // 2 + 90, H // 2 + 90], outline=CHALK, width=4)
for p in ((120, 420), (250, 340), (250, 470), (400, 400), (560, 450), (700, 400), (820, 460), (860, 360)):
    dot(d, *p, RED, 17)
for p in ((330, 440), (610, 360), (760, 480), (480, 330)):
    dot(d, *p, BLUE, 17)
page, pd = paper(780, 270, range(50, 270, 36), 70)
size = 150
while pd.textlength("GAFFER'S NOTEBOOK", font=font("BigShouldersDisplay-ExtraBold.ttf", size)) > 640:
    size -= 4
pd.text((100, 10), "GAFFER'S NOTEBOOK", font=font("BigShouldersDisplay-ExtraBold.ttf", size), fill=INK)
pd.rectangle([100, 190, 700, 204], fill=YEL)
pd.text((100, 208), "draw the plan. coach the match.", font=font("Kalam-Regular.ttf", 34), fill=INK)
tilt_onto(ban, page, (W // 2, 230), -2)
ban.save(os.path.join(OUT, "discord-banner.png"))
print("Wrote", OUT)
