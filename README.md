# Reboot The Earth — โครงสร้างโปรเจกต์

เกม 2 ผู้เล่นควบคุมด้วยร่างกาย (กล้อง + YOLO pose) — ภารกิจที่ 1 **Waste Hunters** เดินเก็บขยะ

## ติดตั้งหลัง clone จาก GitHub (สำหรับเพื่อน)
1. ติดตั้ง **Git LFS** ก่อน clone (โมเดล/วิดีโอ/ไฟล์ 3D เก็บใน LFS): `git lfs install` แล้ว `git clone <url>`
2. **Unity**: เปิดโฟลเดอร์โปรเจกต์ด้วย Unity เวอร์ชันตาม `ProjectSettings/ProjectVersion.txt` (ครั้งแรกจะสร้าง `Library/` เองช้าหน่อย)
3. **Python 3.11**: ดับเบิลคลิก `Python/setup_venv.bat` (สร้าง `Python/venv` + ติดตั้ง `requirements.txt`) ครั้งเดียว
4. เล่น: ดับเบิลคลิก `Python/run_tracker.bat` แล้วกด Play ใน Unity

> `venv/`, `Library/`, `Logs/`, `UserSettings/` ไม่ถูกส่งขึ้น GitHub — แต่ละเครื่องสร้างเอง

## วิธีเล่น
1. Unity: เมนู **RTE > Build Waste Hunters Scene** แล้วกด Play (ฉาก `Assets/Scenes/WasteHunters.unity`)
2. ดับเบิลคลิก `Python/run_tracker.bat` (เปิดกล้อง)
3. ไม่มีกล้อง: `python Python/dev/sim_wastehunters.py` หรือกด Space ในเกม (P1 เมาส์เล็ง Z/X เร่ง, P2 WASD เล็ง N/M เร่ง)

## โค้ด Unity — `Assets/Game/Scripts/`
| ไฟล์ | ทำอะไร |
|---|---|
| `Core/TrackingReceiver.cs` | รับข้อมูล P1/P2 จาก tracker ทาง UDP (ไม่มีกล้อง = เมาส์/WASD) |
| `Core/UIKit.cs` | ตัวช่วยสร้าง UI ด้วยโค้ด, โหลดรูป, ฟอนต์ไทย, แก้วรรณยุกต์ |
| `Core/ThaiText.cs` | ข้อความที่แสดงภาษาไทยถูกต้อง |
| `Core/SkeletonView.cs` | วาดโครงกระดูกผู้เล่นในกรอบ UI |
| `WasteHunters/WasteHunters.cs` | **ตัวควบคุมเกม**: สลับหน้า ปก → จุดยืน → นับถอยหลัง → เล่น → สรุปผล |
| `WasteHunters/GameSettings.cs` | ค่าปรับเกมทั้งหมด (เวลา เป้าหมาย ความเร็ว เวลาเล็ง) + สีผู้เล่น |
| `WasteHunters/CoverScreen.cs` | หน้าปก: วิดีโอ + บอกให้เข้าบริเวณกล้อง |
| `WasteHunters/StandbyScreen.cs` | หน้าจุดยืน: รอครบ 2 คน, บอกใกล้/ไกลเกินไป, โครงกระดูก |
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
| `tracker.py` | โปรแกรมหลัก: กล้อง → YOLO → ล็อค P1/P2 (2–3 ม.) → ส่งให้ Unity |
| `pose_onnx.py` | รันโมเดล YOLOv8-pose (`models/yolov8n-pose.onnx`) + ติดตาม id คน |
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

