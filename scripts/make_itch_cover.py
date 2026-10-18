import math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

F = r"C:\dev\zeus-rc4\godot\assets\fonts"
W, H = 1260, 1000
GREEN = (46, 74, 62)
CHALK, CREAM, INK = (226, 232, 220), (236, 224, 200), (36, 32, 30)
RED, BLUE, YEL = (196, 62, 44), (52, 100, 168), (242, 201, 76)

img = Image.new("RGB", (W, H), GREEN)
d = ImageDraw.Draw(img)
for i in range(0, W, 140):
    if (i // 140) % 2: d.rectangle([i, 0, i + 140, H], fill=(42, 69, 58))

def line(p, w=5, c=CHALK):
    d.line(p, fill=c, width=w)

m = 60
line([(m, m), (W - m, m), (W - m, H - m), (m, H - m), (m, m)])
line([(W // 2, m), (W // 2, H - m)])
d.ellipse([W // 2 - 120, H // 2 - 120, W // 2 + 120, H // 2 + 120], outline=CHALK, width=5)
for x0, x1 in ((m, m + 230), (W - m - 230, W - m)):
    d.rectangle([x0, H // 2 - 260, x1, H // 2 + 260], outline=CHALK, width=5)
    gx = (m, m + 90) if x0 == m else (W - m - 90, W - m)
    d.rectangle([gx[0], H // 2 - 120, gx[1], H // 2 + 120], outline=CHALK, width=5)

def dot(x, y, c, r=22):
    d.ellipse([x - r + 4, y - r + 6, x + r + 4, y + r + 6], fill=(20, 36, 30))
    d.ellipse([x - r, y - r, x + r, y + r], fill=c, outline=(250, 246, 236), width=4)

def arrow(pts, c):
    for a, b in zip(pts, pts[1:]):
        L = math.dist(a, b); n = max(1, int(L // 26))
        for k in range(0, n, 2):
            t0, t1 = k / n, min(1, (k + 0.7) / n)
            d.line([(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0),
                    (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], fill=c, width=7)
    (x0, y0), (x1, y1) = pts[-2], pts[-1]
    ang = math.atan2(y1 - y0, x1 - x0)
    for s in (-0.45, 0.45):
        d.line([(x1, y1), (x1 - 34 * math.cos(ang + s), y1 - 34 * math.sin(ang + s))], fill=c, width=7)

red = [(180, 640), (330, 560), (330, 780), (480, 520), (480, 700), (480, 860), (640, 640), (760, 560), (760, 800), (940, 700), (1060, 640)]
blue = [(1090, 520), (960, 820), (900, 560), (840, 700), (1010, 760), (700, 880), (640, 520), (560, 760)]
arrow([(330, 560), (480, 470), (700, 470), (900, 520)], RED)
arrow([(330, 780), (500, 840), (760, 880), (940, 820)], RED)
arrow([(640, 640), (800, 650), (960, 620)], RED)
arrow([(940, 700), (1040, 640), (1130, 600)], RED)
for p in red: dot(*p, RED)
for p in blue: dot(*p, BLUE)
d.ellipse([1090, 600, 1124, 634], fill=(250, 246, 236), outline=INK, width=4)

ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
od=ImageDraw.Draw(ov)
for y in range(0, 620):
    od.line([(0, y), (W, y)], fill=(20, 36, 30, int(130 * (1 - y / 620))))
img = Image.alpha_composite(img.convert("RGBA"), ov).convert("RGB")

page = Image.new("RGBA", (1000, 400), CREAM + (255,))
pd = ImageDraw.Draw(page)
for y in range(70, 400, 44):
    pd.line([(0, y), (1000, y)], fill=(205, 192, 164), width=2)
pd.line([(90, 0), (90, 400)], fill=(214, 120, 104), width=3)
big = ImageFont.truetype(F + r"\BigShouldersDisplay-ExtraBold.ttf", 150)
pd.text((126, 28), "GAFFER'S", font=big, fill=INK)
pd.text((126, 168), "NOTEBOOK", font=big, fill=INK)
pd.rectangle([126, 340, 900, 354], fill=YEL)
hand = ImageFont.truetype(F + r"\Kalam-Regular.ttf", 38)
pd.text((126, 352), "draw the plan. coach the match.", font=hand, fill=(70, 62, 54))
shadow = Image.new("RGBA", (1000, 400), (0, 0, 0, 150)).filter(ImageFilter.GaussianBlur(12))
page = page.rotate(-2.2, expand=True, resample=Image.BICUBIC)
shadow = shadow.rotate(-2.2, expand=True, resample=Image.BICUBIC)
base = img.convert("RGBA")
base.alpha_composite(shadow, (138, 86))
base.alpha_composite(page, (130, 70))
img = base.convert("RGB")
d = ImageDraw.Draw(img)
small = ImageFont.truetype(F + r"\BarlowCondensed-Bold.ttf", 40)
d.text((W - 90, H - 80), "FOOTBALL TACTICS SANDBOX  -  WINDOWS  -  FREE DEMO", font=small, fill=(236, 232, 220), anchor="rs")
img.save("cover-1260x1000.png")
img.resize((630, 500), Image.LANCZOS).save("cover-630x500.png")
print("ok")
