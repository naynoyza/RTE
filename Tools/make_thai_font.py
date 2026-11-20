# ไฟล์นี้: [Tools] สร้างฟอนต์ ThaiFontWH.ttf (Leelawadee UI Bold + วรรณยุกต์ยกสูง) ให้ Unity แสดงภาษาไทยถูก
"""Patch Leelawadee UI Bold for Unity's legacy Text (which ignores OpenType mark positioning):
adds raised copies of the Thai tone marks at U+F70A..U+F70E. UIKit.FixThai() swaps a tone mark that follows an
upper vowel (ั ิ ี ึ ื ็) or sits on ำ for its raised copy, so it no longer hides inside the vowel."""
import os, sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen

src = sys.argv[1] if len(sys.argv) > 1 else r"C:\Windows\Fonts\LeelaUIb.ttf"
dst = os.path.join(os.path.dirname(__file__), "..", "Assets", "Game", "Resources", "Fonts", "ThaiFontWH.ttf")
f = TTFont(src)
cmap = f.getBestCmap()
gs = f.getGlyphSet()
glyf, hmtx = f["glyf"], f["hmtx"]


# ▸ หาความสูงต่ำสุด/สูงสุดของตัวอักษร
def bounds(cp):
    g = glyf[cmap[cp]]
    g.recalcBounds(glyf)
    return g.yMin, g.yMax


# how high upper vowels reach vs. where tone marks start
# ▸ วัดความสูงของสระบน แล้วคัดลอกวรรณยุกต์ 5 ตัว ยกขึ้นให้อยู่เหนือสระ ใส่ที่รหัส U+F70A..U+F70E
top = max(bounds(cp)[1] for cp in (0x0E34, 0x0E35, 0x0E36, 0x0E37, 0x0E31))
order = f.getGlyphOrder()
for i, cp in enumerate(range(0x0E48, 0x0E4D)):
    name = cmap[cp]
    ymin, _ = bounds(cp)
    shift = max(0, top - ymin + 40)
    rec = DecomposingRecordingPen(gs)
    gs[name].draw(rec)
    pen = TTGlyphPen(None)
    rec.replay(TransformPen(pen, (1, 0, 0, 1, 0, shift)))
    new = f"thai_tone_hi{i}"
    glyf[new] = pen.glyph()
    hmtx[new] = hmtx[name]
    if new not in order: order.append(new)
    for t in f["cmap"].tables:
        if t.isUnicode():
            t.cmap[0xF70A + i] = new
    print(f"U+{cp:04X} -> U+{0xF70A + i:04X} raised {shift}")
f.setGlyphOrder(order)
f["maxp"].numGlyphs = len(order)
# ▸ ลบตารางจัดตำแหน่งขั้นสูงที่ Unity ไม่ใช้ แล้วบันทึกฟอนต์ใหม่
for k in ("GPOS", "GSUB", "DSIG"):
    if k in f:
        del f[k]
f.save(dst)
print("saved", os.path.abspath(dst))
