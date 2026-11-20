# ไฟล์นี้: [Tools] ตัดสไปรต์ของภารกิจที่ 2 (The Last Seed) ออกจากภาพ Ref
#   Ref/19.png  -> พื้นหลัง                Ref/24.png -> ต้นอ่อน (ปกติ / เรืองแสง)
#   Ref/25.png  -> แขนท่าชูรับพลัง 4 ข้าง   Ref/26.png -> แขนท่าผลัก 4 ข้าง
#   Ref/17.png  -> กรอบแถบ น้ำ / ต้นไม้ / แสงแดด (ลบท้องฟ้าด้านหลังและลบสีเกจเดิมออก)
#   ผลลัพธ์: Assets/Game/Resources/Sprites/seed/*.png + layout.json (ตำแหน่งบนจอ 1920x1080)
# รัน: Python/venv/Scripts/python.exe Tools/make_seed_sprites.py
import json, os
import numpy as np, cv2
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REF = os.path.join(ROOT, "Ref")
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Sprites", "seed")
SCALE = 1920 / 4096          # ภาพ Ref ทั้งหมดเป็น 4096x2304 -> จอเกม 1920x1080
layout = {"items": []}


def save(img, name, box=None):
    img.save(os.path.join(OUT, name + ".png"))
    if box is not None:
        x, y, w, h = box  # พิกัดบนจอ 1920x1080 (y นับจากบน)
        layout["items"].append({"name": name, "x": round(float(x), 1), "y": round(float(y), 1), "w": round(float(w), 1), "h": round(float(h), 1)})


def load_scaled(n):
    im = Image.open(os.path.join(REF, f"{n}.png")).convert("RGBA")
    return im.resize((1920, 1080), Image.LANCZOS)


# ▸ แยกชิ้นที่ติดกันในภาพพื้นโปร่งใส เรียงจากซ้ายไปขวา
def components(im, min_area=3000):
    a = np.array(im)[..., 3]
    grouped = cv2.dilate((a > 20).astype(np.uint8), np.ones((9, 9), np.uint8))
    n, lab, stats, _ = cv2.connectedComponentsWithStats(grouped)
    boxes = [tuple(s[:4]) for s in stats[1:] if s[4] >= min_area]
    boxes.sort(key=lambda b: b[0])
    out = []
    for (x, y, w, h) in boxes:
        piece = np.array(im.crop((x, y, x + w, y + h)))
        keep = (lab[y:y + h, x:x + w] > 0)
        piece[..., 3] = np.where(keep, piece[..., 3], 0)
        out.append((Image.fromarray(piece), (x, y, w, h)))
    return out


# ▸ ลบท้องฟ้ารอบ ๆ กรอบ HUD: flood fill จากขอบภาพ แบบเทียบสีกับพิกเซลข้างเคียง (ฟ้าไล่สีเรียบ ขอบกรอบเข้ม)
def cut_from_sky(rgb, tol=4):
    h, w = rgb.shape[:2]
    img = cv2.GaussianBlur(rgb, (3, 3), 0)
    mask = np.zeros((h + 2, w + 2), np.uint8)
    flags = 4 | cv2.FLOODFILL_MASK_ONLY | (255 << 8)
    for x in range(0, w, 6):
        for y in (0, h - 1):
            if mask[y + 1, x + 1] == 0:
                cv2.floodFill(img, mask, (x, y), 0, (tol,) * 3, (tol,) * 3, flags)
    for y in range(0, h, 6):
        for x in (0, w - 1):
            if mask[y + 1, x + 1] == 0:
                cv2.floodFill(img, mask, (x, y), 0, (tol,) * 3, (tol,) * 3, flags)
    sky = mask[1:-1, 1:-1] > 0
    sky = cv2.morphologyEx(sky.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
    alpha = np.where(sky, 0, 255).astype(np.uint8)
    # ลบเศษเล็ก ๆ ที่หลุดลอย เก็บเฉพาะก้อนใหญ่
    n, lab, stats, _ = cv2.connectedComponentsWithStats((alpha > 0).astype(np.uint8))
    big = np.zeros_like(alpha)
    for i in range(1, n):
        if stats[i][4] > 1500:
            big[lab == i] = 255
    big = cv2.GaussianBlur(big, (3, 3), 0)
    return big


# ▸ ทาช่องเกจให้ว่าง (สีเข้มไล่เฉด) เกมจะวาดสีเกจจริงทับในตำแหน่งนี้
def clear_slot(rgba, x0, y0, x1, y1, radius):
    h = y1 - y0
    slot = np.zeros((h, x1 - x0, 3), np.float32)
    for yy in range(h):
        t = yy / max(1, h - 1)
        slot[yy] = np.array([28, 32, 40]) * (1.15 - .35 * t)
    m = np.zeros((h, x1 - x0), np.uint8)
    cv2.rectangle(m, (radius, 0), (x1 - x0 - radius, h - 1), 255, -1)
    cv2.rectangle(m, (0, radius), (x1 - x0 - 1, h - radius), 255, -1)
    for cx, cy in ((radius, radius), (x1 - x0 - radius - 1, radius), (radius, h - radius - 1), (x1 - x0 - radius - 1, h - radius - 1)):
        cv2.circle(m, (cx, cy), radius, 255, -1)
    region = rgba[y0:y1, x0:x1, :3]
    region[m > 0] = slot[m > 0].astype(np.uint8)
    rgba[y0:y1, x0:x1, 3][m > 0] = 255


def hud():
    src = np.array(Image.open(os.path.join(REF, "17.png")).convert("RGB"))
    # (ชื่อ, กรอบที่ตัดในภาพ 4096, ช่องเกจในภาพ 4096, มุมโค้งช่อง)
    bars = [
        ("hud_water", (64, 112, 1110, 352), (330, 190, 946, 284), 30),
        ("hud_tree", (1250, 70, 2850, 545), (1522, 300, 2660, 428), 26),
        ("hud_sun", (3010, 112, 4000, 352), (3266, 198, 3894, 280), 26),
    ]
    for name, (x0, y0, x1, y1), (sx0, sy0, sx1, sy1), r in bars:
        crop = src[y0:y1, x0:x1].copy()
        alpha = cut_from_sky(crop)
        rgba = np.dstack([crop, alpha])
        clear_slot(rgba, sx0 - x0, sy0 - y0, sx1 - x0, sy1 - y0, r)
        im = Image.fromarray(rgba).resize((round((x1 - x0) * SCALE), round((y1 - y0) * SCALE)), Image.LANCZOS)
        save(im, name, (x0 * SCALE, y0 * SCALE, (x1 - x0) * SCALE, (y1 - y0) * SCALE))
        # ตำแหน่งช่องเกจ (เทียบกับมุมบนซ้ายของกรอบ, หน่วยพิกเซลจอ 1920)
        layout["items"].append({"name": name + "_slot", "x": round((sx0 - x0) * SCALE, 1), "y": round((sy0 - y0) * SCALE, 1),
                                "w": round((sx1 - sx0) * SCALE, 1), "h": round((sy1 - sy0) * SCALE, 1)})


def main():
    os.makedirs(OUT, exist_ok=True)
    # พื้นหลัง
    Image.open(os.path.join(REF, "19.png")).convert("RGB").resize((1920, 1080), Image.LANCZOS) \
        .save(os.path.join(OUT, "bg.png"))

    # ต้นอ่อน: ซ้าย = ปกติ, ขวา = เรืองแสง
    for (im, box), name in zip(components(load_scaled(24)), ["sprout", "sprout_glow"]):
        save(im, name, box)

    # แขน: ตัดเป็นคู่ต่อผู้เล่น (ครึ่งซ้ายของภาพ = P1 น้ำ, ครึ่งขวา = P2 แสงแดด)
    for n, pose in ((25, "charge"), (26, "push")):
        full = load_scaled(n)
        for slot, (hx0, hx1) in ((1, (0, 960)), (2, (960, 1920))):
            half = Image.new("RGBA", full.size, (0, 0, 0, 0))
            half.paste(full.crop((hx0, 0, hx1, 1080)), (hx0, 0))
            x, y, x1, y1 = half.getbbox()
            save(half.crop((x, y, x1, y1)), f"arms_{pose}_p{slot}", (x, y, x1 - x, y1 - y))

    hud()
    with open(os.path.join(OUT, "layout.json"), "w", encoding="utf-8") as f:
        json.dump(layout, f, indent=1)
    for it in layout["items"]:
        print(it)


if __name__ == "__main__":
    main()
