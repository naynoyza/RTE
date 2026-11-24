# Reboot The Earth — โครงสร้างโปรเจกต์

เกม 2 ผู้เล่นควบคุมด้วยร่างกาย (กล้อง + YOLO หาตัวผู้เล่น + MediaPipe จับโครงกระดูก) — ภารกิจที่ 1 **Waste Hunters** เดินเก็บขยะ, ภารกิจที่ 2 **The Last Seed** ปลูกต้นไม้

## ติดตั้งหลัง clone จาก GitHub (สำหรับเพื่อน)
1. ติดตั้ง **Git LFS** ก่อน clone (โมเดล/วิดีโอ/ไฟล์ 3D เก็บใน LFS): `git lfs install` แล้ว `git clone <url>`
2. **Unity**: เปิดโฟลเดอร์โปรเจกต์ด้วย Unity เวอร์ชันตาม `ProjectSettings/ProjectVersion.txt` (ครั้งแรกจะสร้าง `Library/` เองช้าหน่อย)
3. **Python 3.11**: ดับเบิลคลิก `Python/setup_venv.bat` (สร้าง `Python/venv` + ติดตั้ง `requirements.txt` + โหลดโมเดล MediaPipe — ต่อเน็ตครั้งแรก) ครั้งเดียว
4. เล่น: ดับเบิลคลิก `Python/run_tracker.bat` แล้วกด Play ใน Unity

> `venv/`, `Library/`, `Logs/`, `UserSettings/` ไม่ถูกส่งขึ้น GitHub — แต่ละเครื่องสร้างเอง

## วิธีเล่น
1. ดับเบิลคลิก `Python/run_tracker.bat` (เปิดกล้อง)
2. Unity: เปิดฉาก `Assets/Scenes/WasteHunters.unity` แล้วกด Play
   ลำดับ: หน้าปก (วิดีโอ รอครบ 2 คน) → เลือกตัวละคร (ยกแขนค้าง) → สอนเล่นเกม 1 → เกม 1 → เกม 2 (`The Last Seed`, สอนเล่น 2 สเต็ป) → กลับหน้าปก
   มีคนออกจากกล้องนานเกิน 12 วิ (หรือทั้งคู่ 6 วิ) → กลับหน้าปกเอง
3. ไม่มีกล้อง: Space = ข้ามหน้า • เลือกตัวละคร P1 Q/E, P2 U/O • เกม 1 P1 เมาส์เล็ง Z/X เร่ง, P2 WASD เล็ง N/M เร่ง • เกม 2 P1 A/S, P2 K/L, F2 = บอตเล่นเอง
4. ถ้าเปลี่ยนรูป UI / โมเดลตัวละคร: เมนู **RTE > Setup Intro Assets**

## โค้ด Unity — `Assets/Game/Scripts/`
| ไฟล์ | ทำอะไร |
|---|---|
| `Core/TrackingReceiver.cs` | รับข้อมูล P1/P2 จาก tracker ทาง UDP (ไม่มีกล้อง = เมาส์/WASD) |
| `Core/UIKit.cs` | ตัวช่วยสร้าง UI ด้วยโค้ด, โหลดรูป, ฟอนต์ไทย, แก้วรรณยุกต์ |
| `Core/ThaiText.cs` | ข้อความที่แสดงภาษาไทยถูกต้อง |
| `Core/SkeletonView.cs` | วาดโครงกระดูกผู้เล่นในกรอบ UI |
| `Intro/IntroCover.cs` | หน้าปก: วิดีโอ + UI 0 + สถานะผู้เล่นมาแล้ว/รอครบ 2 คน |
| `Intro/CharacterSelect.cs`, `ModelStage.cs`, `RingFx.cs`, `ArmRaise.cs` | หน้าเลือกตัวละคร: ยกแขนเลือก, โมเดล 3D บนวง, แสงวงจุดยืน |
| `Intro/PresenceGuard.cs`, `GameSession.cs` | เฝ้าผู้เล่นหลุดกล้อง (เตือน/กลับหน้าปก), ตัวละครที่เลือก, ลำดับฉาก |
| `WasteHunters/WasteHunters.cs` | **ตัวควบคุมเกม 1**: ปก → เลือกตัวละคร → สอนเล่น → นับถอยหลัง → เล่น → สรุปผล → เกม 2 |
| `WasteHunters/PracticeOverlay.cs` | สอนเล่นเกม 1 ซ้อนบนฉากจริง (ซ้อมดูด + ย่ำเท้า, READY!) |
| `WasteHunters/WasteEvents.cs` | อีเวนต์ DUMP FEVER / พายุฝุ่น (OVERDRIVE, ขยะช็อตไฟ อยู่ใน PlayerLane) |
| `LastSeed/LastSeed.cs` | **ตัวควบคุมเกม 2**: สอนเล่น 2 สเต็ป → นับถอยหลัง → เล่น → สรุปผล |
| `LastSeed/SeedPractice.cs`, `TreeView.cs`, `GuardianView.cs`, `SeedHud.cs` | หน้าสอนเกม 2, ต้นไม้ 4 ระดับ, แขน+สายน้ำ/ลำแสง, HUD |
| `WasteHunters/GameSettings.cs` | ค่าปรับเกมทั้งหมด (เวลา เป้าหมาย ความเร็ว เวลาเล็ง) + สีผู้เล่น |
| `WasteHunters/CoverScreen.cs` | (หน้าปกเดิม ไม่ได้ใช้แล้ว) |
| `WasteHunters/CenterOverlay.cs` | นับ 3-2-1 และหน้าสรุปผล |
| `WasteHunters/RoundTimer.cs` | นาฬิกานับถอยหลัง |
| `WasteHunters/PlayerLane.cs` | จอของผู้เล่น 1 คน: เดิน, เกิดขยะ, เล็ง, ดูด, ถุง, ถังรีไซเคิล |
| `WasteHunters/PlayerHud.cs` | คะแนน / ถุง / เป้าหมาย / แถบความเร็ว / กรอบโครงกระดูก |
| `WasteHunters/WalkController.cs` | เดินอัตโนมัติ + เร่งจากการย่ำเท้า |
| `WasteHunters/RoadScroller.cs` | ภาพถนนซูมเหมือนเดินไปข้างหน้า |
| `WasteHunters/TrashPiece.cs` | ขยะ/ถัง 1 ชิ้น: ตำแหน่งในโลก → ตำแหน่งบนจอ, แอนิเมชันถูกดูด |
| `WasteHunters/TrashCatalog.cs` | รายการขยะ คะแนน โบนัส |
| `WasteHunters/VacuumGun.cs` | ปืนดูดฝุ่น 3D ถือมือข้างที่ยก |
| `../Editor/WasteHuntersSceneBuilder.cs` | เมนูสร้างฉาก |

ไฟล์อื่นใน `Assets/Game/Resources`: `Sprites/wh/` (UI ขยะ ถนน), `bin.png`, `fx_*.png` (แสง/วงแหวน), `Fonts/ThaiFontWH.ttf` • วิดีโอปก `Assets/StreamingAssets/cover.mp4`

## โค้ด Python — `Python/` (รันคู่กับกล้อง)
| ไฟล์ | ทำอะไร |
|---|---|
| `tracker.py` | โปรแกรมหลัก: กล้อง → YOLO (กรอบคน) → ล็อค P1/P2 (2–3 ม.) → MediaPipe (กระดูก) → ส่งให้ Unity |
| `yolo_detect.py` | YOLO หากรอบคน (`models/yolov8n-pose.onnx`, ไม่ใช้จุดกระดูก) + ติดตาม id คน, รันใน thread แยกบนการ์ดจอ |
| `skeleton_mp.py` | MediaPipe Pose: โครงกระดูกของผู้เล่นแต่ละคนจากภาพในกรอบ → 17 จุดแบบเดิม |
| `player_lock.py` | จับคนเข้าช่อง P1/P2 และล็อคจนกว่าจะออกจากกล้อง |
| `gestures.py` | ประมาณระยะ และนับการย่ำเท้า |
| `filters.py` | ลดอาการสั่นของตำแหน่ง |
| `dev/` | เครื่องมือเทสไม่ต้องใช้กล้อง: กล้องปลอม, unit test, ดูแพ็กเก็ต UDP |

## เครื่องมือสร้างไฟล์ภาพ — `Tools/`
| ไฟล์ | ทำอะไร |
|---|---|
| `make_wh_sprites.py` | ตัดแผ่นรวมภาพ UI เป็นชิ้น (คะแนน เวลา ถุง เป้าหมาย ขยะ) และย่อภาพถนน |
| `make_thai_font.py` | สร้างฟอนต์ไทยสำหรับ Unity |
| `slice_sprites.py` | ฟังก์ชันตัดสไปรต์ออกจากพื้นขาว |
| `make_intro_sprites.py` | UI หน้าปก/เลือกตัวละคร (Ui 0, การ์ด, ฮอโลแกรม, พื้นหลังเมนู) |
| `make_fx_sprites.py` | ตัดเอฟเฟกต์เกม 1 จาก `UI/11.jpg` + แสงวงจุดยืน |
| `make_seed_v2.py` | เกม 2: ต้นไม้ 4 ระดับ (`Ref/Tree.png`), แขน (`Ref/27.jpg`), สายน้ำ/ลำแสง (`Ref/28-29.jpg`), ภาพสอนเล่น |

> หลังรันสคริปต์ใน `Tools/` ให้กดเมนู **RTE > Setup Intro Assets** ใน Unity (ตั้งค่า import รูป/โมเดล)

