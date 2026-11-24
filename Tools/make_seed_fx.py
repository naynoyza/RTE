# ไฟล์นี้: [Tools] สร้าง texture เอฟเฟกต์ของภารกิจที่ 2 ด้วยโค้ด (แทนภาพนิ่ง Ref/20-23)
#   ภาพทั้งหมดเป็นสีขาว/เทา + alpha เพื่อให้ Unity ย้อมสีน้ำ (ฟ้า) หรือแสงแดด (ทอง) ได้เอง
#   fx_soft   จุดแสงนุ่ม            fx_drop   หยดน้ำ             fx_spark  ประกายดาว 4 แฉก
#   fx_streak เส้นแสงยาว (ใช้ยืด)    fx_swirl  น้ำวนเกลียว        fx_rays   รัศมีแสงรอบดวงอาทิตย์
#   fx_water_strip / fx_beam_strip  แถบที่ต่อกันได้ไม่มีรอยต่อ สำหรับสายน้ำ/ลำแสงที่ไหลเลื่อน (LineRenderer)
# รัน: Python/venv/Scripts/python.exe Tools/make_seed_fx.py
import os
import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Game", "Resources", "Sprites", "seed")
rng = np.random.default_rng(3)


def save(name, alpha, rgb=None):
    a = np.clip(alpha, 0, 1)
    if rgb is None:
        rgb = np.ones(a.shape + (3,))
    img = np.dstack([np.clip(rgb, 0, 1), a]) * 255
    Image.fromarray(img.astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))


def grid(n, m=None):
    m = m or n
    y, x = np.mgrid[0:m, 0:n].astype(np.float32)
    return (x + .5) / n * 2 - 1, (y + .5) / m * 2 - 1


def soft():
    x, y = grid(128); r = np.hypot(x, y)
    save("fx_soft", np.clip(1 - r, 0, 1) ** 2.2)


def drop():
    x, y = grid(64)
    # หยดน้ำ: วงกลมด้านล่าง + ปลายแหลมด้านบน
    r = np.hypot(x / np.clip(.62 - .25 * np.clip(-y, 0, 1) * 1.0, .05, 1), (y - .15) / .78)
    body = np.clip((1 - r) * 6, 0, 1)
    shade = .55 + .45 * np.clip(1 - np.hypot(x + .2, y - .05) * 1.4, 0, 1)
    hl = np.clip(1 - np.hypot((x + .2) / .14, (y - .05) / .22), 0, 1)
    rgb = np.dstack([shade + hl] * 3)
    save("fx_drop", body * (.75 + .25 * shade), rgb)


def spark():
    x, y = grid(128)
    r = np.hypot(x, y)
    star = np.exp(-np.abs(x) * 30) * np.exp(-np.abs(y) * 2.2) + np.exp(-np.abs(y) * 30) * np.exp(-np.abs(x) * 2.2)
    star += .5 * np.exp(-(np.abs(x + y) + np.abs(x - y) * 0) * 0)  * 0
    core = np.clip(1 - r * 3.2, 0, 1) ** 2
    save("fx_spark", np.clip(star + core, 0, 1))


def streak():
    x, y = grid(256, 64)
    a = np.exp(-(y * 2.6) ** 2) * np.clip(1 - np.abs(x), 0, 1) ** 1.4
    save("fx_streak", a)


def swirl():
    x, y = grid(256); r = np.hypot(x, y) + 1e-5; th = np.arctan2(y, x)
    arms = .5 + .5 * np.cos(3 * th + np.log(r) * 7)
    arms = arms ** 3
    ring = np.clip(1 - np.abs(r - .55) / .45, 0, 1)
    a = arms * ring + np.clip(1 - r * 2.2, 0, 1) * .6
    a *= np.clip((1 - r) * 4, 0, 1)
    save("fx_swirl", a)


def rays():
    x, y = grid(512); r = np.hypot(x, y) + 1e-5; th = np.arctan2(y, x)
    n = np.zeros_like(r)
    for k, amp in ((7, .5), (11, .35), (17, .25), (29, .15)):
        n += amp * (.5 + .5 * np.cos(k * th + rng.uniform(0, 6.28)))
    a = (n / 1.25) ** 2.5 * np.clip(1 - r, 0, 1) ** 1.5 * np.clip(r * 3, 0, 1)
    save("fx_rays", a)


def strip(name, water):
    """แถบ 512x128 ต่อกันซ้าย-ขวาได้พอดี (คลื่นทุกตัวมีความถี่เป็นจำนวนเต็มรอบ)"""
    W, H = 512, 128
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = x / W * 2 * np.pi, y / H * 2 - 1
    wave = np.zeros((H, W), np.float32)
    for k in range(1, 9):
        wave += np.sin(u * k * (2 if water else 3) + rng.uniform(0, 6.28) + v * rng.uniform(-3, 3)) / k
    if water:
        width = .55 + .12 * np.sin(u * 2) + .06 * np.sin(u * 5 + 1)
        edge = np.clip(1 - np.abs(v) / width, 0, 1)
        body = np.clip(edge * 3, 0, 1) * .55
        foam = np.clip(np.abs(v) / width - .55, 0, 1) * np.clip(edge * 6, 0, 1) * 2.2      # ขอบน้ำสว่าง
        caustic = np.clip(wave * .45 + .2, 0, 1) * edge
        a = body + foam + caustic * .6
        shade = .75 + .25 * np.clip(wave, 0, 1)
        rgb = np.dstack([shade] * 3)
    else:
        edge = np.exp(-(v * 2.2) ** 2)
        streaks = np.clip(wave * .5 + .5, 0, 1) ** 3
        a = edge * (.35 + .9 * streaks) + np.exp(-(v * 7) ** 2) * .8
        rgb = None
    save(name, a, rgb)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    soft(); drop(); spark(); streak(); swirl(); rays()
    strip("fx_water_strip", True); strip("fx_beam_strip", False)
    print("fx textures ->", OUT)
