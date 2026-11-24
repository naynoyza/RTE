# make_intro_sprites.py — เตรียมรูปของหน้าปก / หน้าเลือกตัวละคร
#   UI/Ui 0.jpg                       -> intro/ui0.png (พื้นดำ = โปร่งใส)
#   UI/การ์ดเลือกตัวละครไซไฟคู่.png      -> intro/card_male.png, intro/card_female.png
#   UI/อินเทอร์เฟซฮอโลแกรมไซไฟนีออน.png -> intro/holo_center/left/right.png, ring_pink/cyan.png
#   UI Background/Background menu.jpg -> intro/bg_menu.jpg
# รัน: python Tools/make_intro_sprites.py
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Sprites", "intro")
os.makedirs(OUT, exist_ok=True)


def black_to_alpha(im):
    im = im.convert("RGB")
    px = list(im.getdata())
    out = []
    for r, g, b in px:
        a = max(r, g, b)
        if a < 18:
            out.append((0, 0, 0, 0)); continue
        k = 255.0 / a
        out.append((min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), min(255, int(a * 1.4))))
    res = Image.new("RGBA", im.size); res.putdata(out)
    return res


def crop_frac(im, box):
    w, h = im.size
    return im.crop((int(box[0] * w), int(box[1] * h), int(box[2] * w), int(box[3] * h)))


ui0 = Image.open(os.path.join(ROOT, "UI", "Ui 0.jpg"))
black_to_alpha(ui0.resize((1920, 1072))).save(os.path.join(OUT, "ui0.png"))

cards = Image.open(os.path.join(ROOT, "UI", "การ์ดเลือกตัวละครไซไฟคู่.png"))
cw, ch = cards.size
crop_frac(cards, (150 / 1774, 70 / 887, 855 / 1774, 820 / 887)).save(os.path.join(OUT, "card_male.png"))
crop_frac(cards, (925 / 1774, 70 / 887, 1630 / 1774, 820 / 887)).save(os.path.join(OUT, "card_female.png"))

holo = Image.open(os.path.join(ROOT, "UI", "อินเทอร์เฟซฮอโลแกรมไซไฟนีออน.png"))
W, H = 1672, 941
for name, box in {
    "holo_left": (240, 180, 552, 390),
    "holo_center": (540, 70, 1132, 495),
    "holo_right": (1122, 180, 1432, 390),
    "ring_pink": (0, 470, 760, 900),
    "ring_cyan": (930, 470, 1672, 900),
}.items():
    crop_frac(holo, (box[0] / W, box[1] / H, box[2] / W, box[3] / H)).save(os.path.join(OUT, name + ".png"))

Image.open(os.path.join(ROOT, "UI Background", "Background menu.jpg")).convert("RGB").save(os.path.join(OUT, "bg_menu.jpg"), quality=92)
print("ok ->", OUT)
