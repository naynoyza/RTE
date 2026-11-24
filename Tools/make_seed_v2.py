# make_seed_v2.py — ชิ้นส่วนใหม่ของภารกิจที่ 2 (The Last Seed)
#   Ref/Tree.png   -> seed/tree_s1..s4.png  ต้นไม้ 4 ระดับ (เลือกจาก 9 ภาพในแผ่น)
#   Ref/27.jpg     -> seed/arms_push_p1/p2.png  แขนชุดใหม่ (แทนของ Ref/26) + อัปเดต layout.json
#   Ref/28.jpg     -> seed/fx_water_strip2.png (สายน้ำแบบต่อกันได้), fx_water_head.png (หัวสายน้ำสาด)
#   Ref/29.jpg     -> seed/fx_beam_strip2.png  (ลำแสงแบบต่อกันได้), fx_sun_head.png   (หัวลำแสงดาวหาง)
#   Ref/สอนเกม2.jpg -> seed/tut_charge.png, tut_push.png (ภาพประกอบในหน้าต่างสอนเล่น)
# รัน: python Tools/make_seed_v2.py
import json, os
import numpy as np, cv2
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REF = os.path.join(ROOT, "Ref")
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Sprites", "seed")


def white_to_alpha(rgb, tol=10):
    """พื้นขาวที่ต่อกับขอบภาพ = โปร่งใส (flood fill จากขอบ), ขอบนุ่ม"""
    h, w = rgb.shape[:2]
    mask = np.zeros((h + 2, w + 2), np.uint8)
    img = rgb.copy()
    flags = 4 | cv2.FLOODFILL_MASK_ONLY | cv2.FLOODFILL_FIXED_RANGE | (255 << 8)
    for x in range(0, w, 3):
        for y in (0, h - 1):
            if mask[y + 1, x + 1] == 0 and img[y, x].min() > 235:
                cv2.floodFill(img, mask, (x, y), 0, (tol,) * 3, (tol,) * 3, flags)
    for y in range(0, h, 3):
        for x in (0, w - 1):
            if mask[y + 1, x + 1] == 0 and img[y, x].min() > 235:
                cv2.floodFill(img, mask, (x, y), 0, (tol,) * 3, (tol,) * 3, flags)
    bg = mask[1:-1, 1:-1] > 0
    alpha = np.where(bg, 0, 255).astype(np.uint8)
    alpha = cv2.erode(alpha, np.ones((3, 3), np.uint8))
    alpha = cv2.GaussianBlur(alpha, (3, 3), 0)
    return alpha


# ------------------------------------------------------------------ ต้นไม้ 4 ระดับ
def trees():
    rgb = np.asarray(Image.open(os.path.join(REF, "Tree.png")).convert("RGB")).copy()
    alpha = white_to_alpha(rgb, 6)
    grouped = cv2.dilate((alpha > 40).astype(np.uint8), np.ones((7, 7), np.uint8))
    n, lab, stats, _ = cv2.connectedComponentsWithStats(grouped)
    boxes = [tuple(s[:4]) for s in stats[1:] if s[4] > 6000]
    top = sorted([b for b in boxes if b[1] < 430], key=lambda b: b[0])
    bot = sorted([b for b in boxes if b[1] >= 430], key=lambda b: b[0])
    allb = top + bot
    print("trees found:", len(allb))
    # ระดับ: 1 ต้นอ่อน (แถวบน #1), 2 ต้นกล้า (แถวบน #3), 3 ต้นไม้วัยรุ่น (แถวบน #5), 4 โตเต็มวัยเรืองแสง (แถวล่าง #4)
    pick = [allb[0], allb[2], allb[4], allb[8]]
    info = []
    for k, (x, y, w, h) in enumerate(pick, 1):
        piece = np.dstack([rgb, alpha])[y:y + h, x:x + w].copy()
        piece[..., 3] = np.where(lab[y:y + h, x:x + w] > 0, piece[..., 3], 0)
        Image.fromarray(piece, "RGBA").save(os.path.join(OUT, f"tree_s{k}.png"))
        info.append({"name": f"tree_s{k}", "w": int(w), "h": int(h)})
    return info


# ------------------------------------------------------------------ แขน (27.jpg)
def arms(layout):
    rgb = np.asarray(Image.open(os.path.join(REF, "27.jpg")).convert("RGB").resize((1920, 1080), Image.LANCZOS)).copy()
    alpha = white_to_alpha(rgb, 5)
    full = np.dstack([rgb, alpha])
    items = [it for it in layout["items"] if not it["name"].startswith("arms_push")]
    for slot, (hx0, hx1) in ((1, (0, 960)), (2, (960, 1920))):
        half = np.zeros_like(full); half[:, hx0:hx1] = full[:, hx0:hx1]
        im = Image.fromarray(half, "RGBA")
        x, y, x1, y1 = im.getbbox()
        im.crop((x, y, x1, y1)).save(os.path.join(OUT, f"arms_push_p{slot}.png"))
        items.append({"name": f"arms_push_p{slot}", "x": float(x), "y": float(y), "w": float(x1 - x), "h": float(y1 - y)})
    layout["items"] = items


# ------------------------------------------------------------------ สายน้ำ / ลำแสง
def rotate_to_horizontal(img, angle_deg):
    h, w = img.shape[:2]
    m = cv2.getRotationMatrix2D((w / 2, h / 2), angle_deg, 1)
    cos, sin = abs(m[0, 0]), abs(m[0, 1])
    nw, nh = int(h * sin + w * cos), int(h * cos + w * sin)
    m[0, 2] += nw / 2 - w / 2; m[1, 2] += nh / 2 - h / 2
    return cv2.warpAffine(img, m, (nw, nh), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0))


def tileable(band):
    """ทำให้ต่อซ้ำแนวนอนได้ไม่เห็นรอย: ผสมครึ่งหลังเข้ากับครึ่งหน้า"""
    w = band.shape[1]; half = w // 2
    a = band[:, :half].astype(np.float32); b = band[:, half:half * 2].astype(np.float32)
    t = np.linspace(0, 1, half)[None, :, None]
    return (a * t + b * (1 - t)).clip(0, 255).astype(np.uint8)


def strip(src, angle, x0, x1, name, out_h=128, boost=1.0):
    img = np.asarray(Image.open(os.path.join(REF, src)).convert("RGB"))
    rot = rotate_to_horizontal(img, angle)
    # หาแถวที่สว่างที่สุด (แกนกลางสาย)
    lum = rot[:, int(rot.shape[1] * x0):int(rot.shape[1] * x1)].mean(axis=(1, 2))
    cy = int(np.argmax(cv2.GaussianBlur(lum.reshape(-1, 1), (1, 61), 0)))
    bh = int(rot.shape[0] * .09)
    band = rot[max(0, cy - bh):cy + bh, int(rot.shape[1] * x0):int(rot.shape[1] * x1)]
    band = tileable(band)
    # ขอบบน/ล่างจางเป็นดำ (ใช้แบบ additive: ดำ = โปร่ง)
    v = np.sin(np.linspace(0, np.pi, band.shape[0]))[:, None, None] ** .8
    band = (band * v * boost).clip(0, 255).astype(np.uint8)
    band = cv2.resize(band, (int(band.shape[1] * out_h / band.shape[0]), out_h), interpolation=cv2.INTER_AREA)
    Image.fromarray(band).save(os.path.join(OUT, name + ".png"))


def head(src, name, max_side=768):
    img = Image.open(os.path.join(REF, src)).convert("RGB")
    img.thumbnail((max_side, max_side), Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))


# ------------------------------------------------------------------ ภาพประกอบหน้าต่างสอน (สอนเกม2.jpg, พิกัดภาพย่อ 2000 กว้าง)
def tutorial_pics():
    im = Image.open(os.path.join(REF, "สอนเกม2.jpg")).convert("RGB")
    s = im.width / 2000
    for name, (x0, y0, x1, y1) in {"tut_charge": (676, 402, 982, 590), "tut_push": (1057, 402, 1362, 590)}.items():
        im.crop((int(x0 * s), int(y0 * s), int(x1 * s), int(y1 * s))).save(os.path.join(OUT, name + ".png"))


def main():
    path = os.path.join(OUT, "layout.json")
    layout = json.load(open(path, encoding="utf-8"))
    info = trees()
    arms(layout)
    layout["items"] = [it for it in layout["items"] if not it["name"].startswith("tree_s")] + info
    json.dump(layout, open(path, "w", encoding="utf-8"), indent=1)
    # น้ำ: สายไหลจากซ้ายบนลงขวาล่าง (~32°), แสง: ลำแสงเฉียงขึ้นขวา (~32°)
    strip("28.jpg", 32.6, .22, .78, "fx_water_strip2", boost=1.3)
    strip("29.jpg", -32.2, .18, .82, "fx_beam_strip2", boost=1.15)
    head("28.jpg", "fx_water_head")
    head("29.jpg", "fx_sun_head")
    tutorial_pics()
    print("ok ->", OUT)


if __name__ == "__main__":
    main()
