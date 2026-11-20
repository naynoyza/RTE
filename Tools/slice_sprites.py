# ไฟล์นี้: [Tools] ฟังก์ชันตัดสไปรต์ออกจากภาพพื้นขาว (ใช้โดย make_wh_sprites.py และเกมเก่า)
"""Cut sprites out of the reference sheets: remove white background, split by connected components."""
import sys, os, numpy as np, cv2
from PIL import Image

# ▸ ตัดรูปออกจากภาพพื้นขาว: ทำพื้นขาวที่ติดขอบเป็นโปร่งใส แล้วแยกแต่ละชิ้นตามกลุ่มพิกเซลที่ติดกัน
def cut(path, out_dir, prefix, min_area=4000, thr=235, merge=25):
    img = np.array(Image.open(path).convert("RGBA"))
    rgb = img[..., :3].astype(np.int32)
    fg = ~((rgb[..., 0] > thr) & (rgb[..., 1] > thr) & (rgb[..., 2] > thr))
    if img[..., 3].min() < 255:
        fg &= img[..., 3] > 10
    # flood-fill only background connected to border, so white inside sprites stays
    bg = (~fg).astype(np.uint8)
    h, w = bg.shape
    ff = bg.copy(); mask = np.zeros((h + 2, w + 2), np.uint8)
    for x in range(0, w, 8):
        for y in (0, h - 1):
            if ff[y, x] == 1: cv2.floodFill(ff, mask, (x, y), 2)
    for y in range(0, h, 8):
        for x in (0, w - 1):
            if ff[y, x] == 1: cv2.floodFill(ff, mask, (x, y), 2)
    alpha = np.where(ff == 2, 0, 255).astype(np.uint8)
    alpha = cv2.GaussianBlur(alpha, (3, 3), 0)
    img[..., 3] = alpha
    grouped = cv2.dilate((alpha > 128).astype(np.uint8), np.ones((merge, merge), np.uint8))
    n, lab, stats, _ = cv2.connectedComponentsWithStats(grouped)
    boxes = [s for s in stats[1:] if s[4] >= min_area]
    boxes.sort(key=lambda s: (round(s[1] / (h / 6)), s[0]))
    os.makedirs(out_dir, exist_ok=True)
    for i, (x, y, bw, bh, a) in enumerate(boxes):
        Image.fromarray(img[y:y + bh, x:x + bw]).save(os.path.join(out_dir, f"{prefix}_{i:02d}.png"))
        print(prefix, i, x, y, bw, bh)

if __name__ == "__main__":
    ref, out = sys.argv[1], sys.argv[2]
    cut(os.path.join(ref, "1.jpg"), out, "hands", min_area=20000, merge=5)
    cut(os.path.join(ref, "2.png"), out, "icons")
    cut(os.path.join(ref, "3.png"), out, "ui")
