# make_fx_sprites.py — ตัดเอฟเฟกต์จาก UI/11.jpg (พื้นเทาตาราง) เป็น PNG โปร่งใส + สร้างแสงสำหรับหน้าเลือกตัวละคร
#   -> Assets/Game/Resources/Sprites/fx/*.png
# วิธีลบพื้น: หาค่าพื้นเทารอบ ๆ ชิ้น แล้ว alpha = ความต่างจากพื้น, สีถูกคืนค่าแบบ un-premultiply (แสงเรืองยังสวย)
# รัน: python Tools/make_fx_sprites.py
import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Sprites", "fx")
os.makedirs(OUT, exist_ok=True)

sheet = np.asarray(Image.open(os.path.join(ROOT, "UI", "11.jpg")).convert("RGB")).astype(np.float32)
H, W, _ = sheet.shape
S = W / 2000.0  # พิกัดด้านล่างวัดจากภาพย่อ 2000 px

# ชิ้น: ชื่อ -> (x0, y0, x1, y1) ในพิกัดภาพย่อ
PIECES = {
    "drone":        (425, 45, 735, 275),
    "fever_sign":   (675, 200, 1350, 400),
    "danger_text":  (1415, 265, 1935, 425),
    "overdrive_tx": (55, 448, 412, 562),
    "vortex":       (15, 572, 475, 945),
    "aim_blue":     (762, 842, 948, 1028),
    "suck_blue":    (515, 670, 955, 1035),
    "suck_orange":  (955, 655, 1365, 1015),
    "aim_orange":   (1170, 850, 1330, 1010),
    "hand":         (905, 435, 1075, 585),
    "swipe_tx":     (725, 585, 1295, 665),
    "visor_tx":     (1325, 575, 1745, 655),
    "lightning":    (1495, 685, 1955, 1085),
    "stunned_tx":   (1385, 980, 1695, 1050),
}


# พื้นที่ที่ต้องลบทิ้งในบางชิ้น (มีชิ้นอื่นติดมา) พิกัดภาพย่อ
CLEAR = {
    "drone": [(672, 198, 800, 400)],
    "lightning": [(1380, 960, 1700, 1100)],
}


SOLID = {"drone", "fever_sign"}


def cut(name, box):
    x0, y0, x1, y1 = [int(v * S) for v in box]
    c = sheet[y0:y1, x0:x1]
    # พื้น = ค่ากลาง (median) ของขอบชิ้น
    bg = np.array([60.0, 60.0, 60.0])                            # พื้นเทาของแผ่นรวม (วัดจากมุมว่าง)
    diff = np.abs(c - bg).max(axis=2)
    a = np.clip((diff - 26) / 70.0, 0, 1)                    # ต่างจากพื้นนิดเดียว (เส้นตาราง) = โปร่งใส
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(.8))).astype(np.float32) / 255
    if name in SOLID:                                          # วัตถุทึบ (โดรน, ป้าย): ใช้หน้ากากทึบ + อุดรู แทนความต่างของสี
        m = Image.fromarray(((diff > (45 if name == "fever_sign" else 24)) * 255).astype(np.uint8))
        m = m.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.MinFilter(7))
        inv = Image.fromarray(255 - np.asarray(m)).copy()
        from PIL import ImageDraw
        iw, ih = inv.size                                       # เติมพื้นจากทุกจุดบนขอบ (พื้นนอกวัตถุ = 128, รูข้างใน = 255)
        for px, py in [(x, 0) for x in range(0, iw, 4)] + [(x, ih - 1) for x in range(0, iw, 4)] +                       [(0, y) for y in range(0, ih, 4)] + [(iw - 1, y) for y in range(0, ih, 4)]:
            if inv.getpixel((px, py)) == 255:
                ImageDraw.floodfill(inv, (px, py), 128)
        solid = np.asarray(inv) != 128
        m = Image.fromarray((solid * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
        a = np.asarray(m).astype(np.float32) / 255
        for cx0, cy0, cx1, cy1 in CLEAR.get(name, []):
            a[max(0, int(cy0 * S) - y0):max(0, int(cy1 * S) - y0), max(0, int(cx0 * S) - x0):max(0, int(cx1 * S) - x0)] = 0
        Image.fromarray(np.dstack([c, a * 255]).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
        return
    for cx0, cy0, cx1, cy1 in CLEAR.get(name, []):
        a[max(0, int(cy0 * S) - y0):max(0, int(cy1 * S) - y0), max(0, int(cx0 * S) - x0):max(0, int(cx1 * S) - x0)] = 0
    if name.startswith("aim_"):                               # เป้าเล็ง: ตัดลมหมุนรอบ ๆ ออก เหลือวงกลม + ทำรูดำตรงกลางให้โปร่งใส
        hh, ww = a.shape
        yy, xx = np.mgrid[0:hh, 0:ww]
        rr = np.sqrt(((xx - ww / 2) / (ww / 2)) ** 2 + ((yy - hh / 2) / (hh / 2)) ** 2)
        a *= np.clip((1.0 - rr) / .08, 0, 1)
        lum = c.mean(axis=2)
        a *= np.clip((lum - 45) / 40, 0, 1)
    rgb = (c - bg * (1 - a[..., None])) / np.maximum(a[..., None], 1e-3)
    rgb = np.clip(rgb, 0, 255)
    out = np.dstack([rgb, a * 255]).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(os.path.join(OUT, name + ".png"))


for n, b in PIECES.items():
    cut(n, b)

# ---- แสงสำหรับวงจุดยืนหน้าเลือกตัวละคร (สร้างเอง ขาวล้วน ไว้ย้อมสีใน Unity) ----
# ลำแสงพุ่งขึ้น: กลางสว่าง ขอบจาง, ล่างสว่าง บนจาง
w, h = 256, 512
x = np.linspace(-1, 1, w)[None, :]
y = np.linspace(1, 0, h)[:, None]           # บน = 0
beam = np.exp(-(x ** 2) / .12) * (y ** 1.6) * (1 - np.exp(-(1 - y) * 30))
img = np.dstack([np.full((h, w), 255)] * 3 + [np.clip(beam * 255, 0, 255)]).astype(np.uint8)
Image.fromarray(img, "RGBA").save(os.path.join(OUT, "light_beam.png"))

# วงแหวนเรืองแสงแบบมีขีด (หมุนได้)
n = 512
yy, xx = np.mgrid[-1:1:n * 1j, -1:1:n * 1j]
r = np.sqrt(xx ** 2 + yy ** 2); ang = np.arctan2(yy, xx)
ring = np.exp(-((r - .82) ** 2) / .0012) + .6 * np.exp(-((r - .62) ** 2) / .0006) * (np.sin(ang * 12) > 0)
ring += .35 * np.exp(-((r - .95) ** 2) / .0004) * (np.sin(ang * 3) > -.2)
img = np.dstack([np.full((n, n), 255)] * 3 + [np.clip(ring * 255, 0, 255)]).astype(np.uint8)
Image.fromarray(img, "RGBA").save(os.path.join(OUT, "tech_ring.png"))

# แสงวาบ (ดาว 4 แฉก) ตอนเลือกตัวละครสำเร็จ
star = np.exp(-(r ** 2) / .01) + .8 * np.exp(-(np.abs(xx) * 40 + yy ** 2 * 3)) + .8 * np.exp(-(np.abs(yy) * 40 + xx ** 2 * 3))
img = np.dstack([np.full((n, n), 255)] * 3 + [np.clip(star * 255, 0, 255)]).astype(np.uint8)
Image.fromarray(img, "RGBA").save(os.path.join(OUT, "flare.png"))
print("ok ->", OUT)
