# ไฟล์นี้: [Tools] รวมเฟรมต้นไม้โต (จาก blender_tree_grow.py) เป็น
#   1) Assets/Game/Resources/Sprites/seed/tree_atlas.png  — sprite sheet 12 x 8 ช่อง (เกมเลื่อนเฟรมตาม Growth Bar)
#   2) Media/TheLastSeed/tree_grow.mov                      — วิดีโอ ProRes 4444 มีพื้นโปร่งใส (ใช้ตัดต่อ/After Effects)
#   3) Media/TheLastSeed/tree_grow.webm                     — VP8 + alpha (เล่นใน Unity VideoPlayer ได้ ถ้าต้องการ)
# รัน: Python/venv/Scripts/python.exe Tools/pack_tree_frames.py
import glob, os, subprocess
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MEDIA = os.path.join(ROOT, "Media", "TheLastSeed")
FRAMES = os.path.join(MEDIA, "tree_frames")
ATLAS = os.path.join(ROOT, "Assets", "Game", "Resources", "Sprites", "seed", "tree_atlas.png")
COLS = 12

files = sorted(glob.glob(os.path.join(FRAMES, "tree_*.png")))
assert files, "no frames in " + FRAMES
size = Image.open(files[0]).size[0]
rows = (len(files) + COLS - 1) // COLS
atlas = Image.new("RGBA", (COLS * size, rows * size), (0, 0, 0, 0))
for i, f in enumerate(files):
    atlas.paste(Image.open(f).convert("RGBA"), ((i % COLS) * size, (i // COLS) * size))
atlas.save(ATLAS, optimize=True)
print(f"atlas {len(files)} frames {COLS}x{rows} @ {size}px -> {ATLAS}")

pattern = os.path.join(FRAMES, "tree_%04d.png")
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", pattern,
                "-c:v", "prores_ks", "-profile:v", "4444", "-pix_fmt", "yuva444p10le",
                os.path.join(MEDIA, "tree_grow.mov")], check=True)
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", pattern,
                "-c:v", "libvpx", "-pix_fmt", "yuva420p", "-auto-alt-ref", "0", "-b:v", "3M",
                os.path.join(MEDIA, "tree_grow.webm")], check=True)
print("video ->", MEDIA)
