# ไฟล์นี้: [Tools] อ่านแผ่นรวมภาพ UI แล้วตัดเป็นชิ้น (กล่องคะแนน/เวลา/ถุง/เป้าหมาย/ขยะ 7 ชนิด) + ย่อภาพถนน -> บันทึกลง Resources/Sprites/wh
"""Build Waste Hunters sprites from the UI sheet + road picture. Number areas are blanked so Unity draws live values."""
import os, sys, numpy as np, cv2
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
ROOT = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(ROOT, "Assets/Game/Resources/Sprites/wh")
os.makedirs(OUT, exist_ok=True)
SL = os.path.join(os.path.dirname(__file__), "_s12")
from slice_sprites import cut
if not os.path.isdir(SL): cut(os.path.join(ROOT, "Ref/12.png"), SL, "s12")

# ▸ เปิดชิ้นภาพที่ตัดแล้วตามลำดับ
def load(i): return np.array(Image.open(os.path.join(SL, f"s12_{i:02d}.png")))
# ▸ ระบายสีทับช่องตัวเลขเดิมด้วยสีพื้นของกรอบ (เกมจะเขียนตัวเลขจริงทับเอง)
def blank(img, x0, y0, x1, y1):
    reg = img[y0:y1, x0:x1, :3]
    col = np.median(img[y0:y1, x0:x0+6, :3].reshape(-1, 3), axis=0)
    img[y0:y1, x0:x1, :3] = col.astype(np.uint8)
    return img
# ▸ บันทึกเป็น png ลง Resources/Sprites/wh
def save(img, name): Image.fromarray(img).save(os.path.join(OUT, name + ".png")); print(name, img.shape)

# coordinates relative to each slice (sheet coords minus slice origin)
# ▸ กรอบ GOAL, นาฬิกา, คะแนน P1/P2, ถุง P1/P2 (แยกจากชิ้นที่ติดกัน), รูปขยะ 7 ชนิด และภาพถนน
g = load(1);   g = blank(g, 250-24, 255-137, 470-24, 330-137); g[:42, 390:, 3] = 0; save(g, "goal")  # GOAL panel (drop neighbour scrap top-right)
t = load(2);   save(blank(t, 790-588, 225-164, 1015-588, 315-164), "timer")      # 01:42 panel
p1 = load(3);  save(blank(p1, 1325-1135, 280-165, 1460-1135, 345-165), "score_p1")
p2 = load(4);  save(blank(p2, 1720-1532, 280-162, 1850-1532, 345-162), "score_p2")
inv = load(5)
a, b = inv[:230].copy(), inv[230:].copy()
save(blank(a, 235-24, 480-394, 340-24, 575-394), "inv_p1")
save(blank(b, 235-24, 705-624, 340-24, 800-624), "inv_p2")
for i, n in zip(range(6, 13), ["t_aircon", "t_tv", "t_phone", "t_laptop", "t_battery", "t_tube", "t_board"]):
    save(load(i), n)
bg = Image.open(os.path.join(ROOT, "Ref/Background.jpg")).convert("RGB"); bg.thumbnail((2048, 2048))
bg.save(os.path.join(OUT, "road.jpg"), quality=92)
