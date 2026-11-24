# make_g1_layers.py — เตรียมฉากหลังเกม 1 แบบหลายชั้น (parallax) จาก UI Background/Game1-1..4.png
#   layer1 = กองขยะ + ถนน (ใกล้สุด)   layer2 = ตึกพัง + เสาไฟข้างถนน   layer3 = เมืองไกล + สะพานลอย   layer4 = ท้องฟ้า + ดวงอาทิตย์
#   จอของผู้เล่นแต่ละคนเป็นแนวตั้ง (960x1080) ต้องการพื้นใต้เส้นขอบฟ้ามากกว่าในภาพต้นฉบับ (แนวนอน)
#   -> ยืดเฉพาะส่วน "พื้น" ใต้จุดลับตาของ layer1 ลงด้านล่าง (GROUND_STRETCH เท่า) กองขยะ/ตึกไม่ถูกยืด
#   ผลลัพธ์: Assets/Game/Resources/Sprites/g1/layer1..4.png + บันทึกจุดลับตาของแต่ละชั้นใน layers.json
# รัน: python Tools/make_g1_layers.py   แล้วกดเมนู RTE > Setup Intro Assets ใน Unity
import json, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "UI Background")
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Sprites", "g1")
os.makedirs(OUT, exist_ok=True)

# จุดลับตาของแต่ละชั้น (สัดส่วนจากซ้าย, จากบน) วัดจากภาพ: ปลายถนน / ปลายถนนระหว่างตึก / ใต้สะพาน / ดวงอาทิตย์
VANISH = {1: (0.52, 0.78), 2: (0.60, 0.82), 3: (0.60, 0.66), 4: (0.68, 0.52)}
GROUND_STRETCH = 2.2


def main():
    info = {}
    for i in (1, 2, 3, 4):
        im = Image.open(os.path.join(SRC, f"Game1-{i}.png")).convert("RGBA")
        w, h = im.size
        vx, vy = VANISH[i]
        if i == 1:
            cut = int(h * vy)
            top = im.crop((0, 0, w, cut))
            ground = im.crop((0, cut, w, h)).resize((w, int((h - cut) * GROUND_STRETCH)), Image.LANCZOS)
            out = Image.new("RGBA", (w, top.height + ground.height))
            out.paste(top, (0, 0)); out.paste(ground, (0, cut))
            im, vy = out, cut / out.height
        if i == 4:
            im = im.convert("RGB")
        im.save(os.path.join(OUT, f"layer{i}.png"))
        info[f"layer{i}"] = {"w": im.width, "h": im.height, "pivot_x": round(vx, 4), "pivot_y": round(1 - vy, 4)}  # pivot_y จากล่าง (แบบ Unity)
        print(i, info[f"layer{i}"])
    with open(os.path.join(OUT, "layers.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, indent=1)


if __name__ == "__main__":
    main()
