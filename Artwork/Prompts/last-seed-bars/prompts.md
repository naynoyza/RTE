# The Last Seed — แถบพลัง 3 อัน (เจนใหม่)

ภาพเรฟอยู่ในโฟลเดอร์ `refs/`:
- `hud_water.png`, `hud_tree.png`, `hud_sun.png` = แถบเดิมที่ใช้ในเกม (ยึดรูปทรง/สัดส่วน/ตำแหน่งไอคอน)
- `Cartoon Vine-Wrapped Timer Panel.png` = สไตล์ใหม่ที่ต้องการให้เข้าชุด (ไม้ + เหล็กยึดน็อต + เถาวัลย์ใบไม้)

แนบภาพเรฟ 2 ภาพต่อการเจน 1 อัน: แถบเดิมของอันนั้น + Timer Panel

กติการ่วมทุกอัน (ต้องได้แบบนี้ ไม่งั้นใส่ในเกมไม่ได้):
- พื้นหลังโปร่งใส (PNG) ไม่มีเงา/ฉากหลัง
- ช่องเติมพลังตรงกลางต้อง **ว่าง สีเข้มเรียบ** (เกมเติมสีเอง) ไม่มีตัวเลข/ตัวหนังสือ/ขีดแบ่ง
- ช่องเป็นสี่เหลี่ยมผืนผ้าแนวนอน ขอบตรง เติมจากซ้ายไปขวา
- มุมมองตรงหน้า ไม่เอียง

---

## 1) แถบน้ำ (Water) — แทน `hud_water.png` สัดส่วนกว้าง:สูง ≈ 4.4 : 1

```
Cartoon mobile-game HUD energy bar frame, horizontal, front view, transparent background.
Same art style as the attached vine-wrapped timer panel: chunky warm wooden frame with grey
steel corner brackets and screws, thick dark outlines, soft cel shading, glossy highlights,
a few fresh green leaves and curly vines wrapping the frame.
On the LEFT end: a round badge holding a glossy blue water droplet icon, with two small
splash droplets.
The long bar slot in the middle is EMPTY, flat dark navy-brown, rounded rectangle, no fill,
no numbers, no text, no segment lines.
Wide format, about 4.4:1, frame centered, nothing cut off, clean edges for game UI, PNG.
Use the attached old water bar only for layout and proportions; follow the timer panel for style.
```

## 2) แถบต้นไม้ (Tree growth) — แทน `hud_tree.png` สัดส่วน ≈ 3.4 : 1 (อันกลาง ใหญ่สุด)

```
Cartoon mobile-game HUD growth bar frame, horizontal, front view, transparent background.
Same art style as the attached vine-wrapped timer panel: warm wooden frame with grey steel
brackets and screws, thick dark outlines, soft cel shading, lush green leaves and vines
growing around the frame, slightly more leafy and grand than the other two bars (this is
the main bar).
On the LEFT end: a small round-crowned green tree standing on a floating grassy soil
platform, overlapping the frame edge.
The long bar slot is EMPTY, flat dark charcoal, rounded rectangle, no fill, no numbers,
no text, no segment lines.
Wide format, about 3.4:1, centered, nothing cut off, clean edges for game UI, PNG.
Use the attached old tree bar only for layout and proportions; follow the timer panel for style.
```

## 3) แถบแสงแดด (Sun) — แทน `hud_sun.png` สัดส่วน ≈ 4.1 : 1

```
Cartoon mobile-game HUD energy bar frame, horizontal, front view, transparent background.
Same art style as the attached vine-wrapped timer panel: chunky warm wooden frame with grey
steel corner brackets and screws, thick dark outlines, soft cel shading, glossy highlights,
a few green leaves and vines.
On the LEFT end: a round badge holding a bright glossy golden sun icon with short rays.
On the RIGHT end: a small solar panel mounted on the frame.
The long bar slot is EMPTY, flat dark brown, rounded rectangle, no fill, no numbers,
no text, no segment lines.
Wide format, about 4.1:1, centered, nothing cut off, clean edges for game UI, PNG.
Use the attached old sun bar only for layout and proportions; follow the timer panel for style.
```

---

ได้ภาพแล้ววางไว้ที่ `Ref/` แล้วบอก — ต้องวัดตำแหน่งช่องเติมใหม่ใน `seed/layout.json`
(`hud_water_slot`, `hud_tree_slot`, `hud_sun_slot`) ให้แถบสีเติมตรงช่อง
