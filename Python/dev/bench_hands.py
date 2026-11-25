# bench_hands.py — วัดว่ามือ (MediaPipe) หลุดบ่อยแค่ไหน: เทียบ vision.Hands กับไฟล์ vision อีกตัว บนภาพกล้องชุดเดียวกัน
# ใช้: python dev/bench_hands.py [วินาที] [path/to/old_vision.py]
#   ยืนหน้ากล้อง 2-3 ม. แล้วยกมือ ขยับไปมา; นับเฉพาะเฟรมที่ข้อมือ (YOLO) อยู่สูงกว่าศอก = "ควรเห็นมือ"
import importlib.util, os, sys, time
import numpy as np
import cv2

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
from camera import Camera
from vision import Body, Hands, LS, RS

secs = float(sys.argv[1]) if len(sys.argv) > 1 else 30
impls = {"new": Hands}
if len(sys.argv) > 2:
    spec = importlib.util.spec_from_file_location("vision_old", sys.argv[2])
    old = importlib.util.module_from_spec(spec); spec.loader.exec_module(old)
    impls = {"old": old.Hands, "new": Hands}

body = Body(os.path.join(HERE, "models", "yolo11n-pose.pt"))
cam = Camera("orbbec")
inst = {}
stat = {n: {"want": 0, "hit": 0, "ms": 0.0, "calls": 0} for n in impls}
frames = people_frames = 0
snap = sys.argv[3] if len(sys.argv) > 3 else None
next_snap = 0
t0, last = time.monotonic(), -1
print(f"recording {secs:.0f} s - raise and move your hands")
while time.monotonic() - t0 < secs:
    fid, frame = cam.read()
    if frame is None or fid == last:
        time.sleep(0.002); continue
    last = fid
    frame = cv2.flip(frame, 1)
    now = time.monotonic() * 1000
    people = body(frame)
    frames += 1; people_frames += bool(people)
    vis = frame.copy() if snap and now > next_snap else None
    for p in people:
        k, c = p["kps"], p["kc"]
        if c[LS] < 0.3 or c[RS] < 0.3:
            continue
        raised = [0 if k[wr][0] < (k[LS][0] + k[RS][0]) / 2 else 1
                  for wr, el in ((9, 7), (10, 8)) if c[wr] > 0.3 and c[el] > 0.3 and k[wr][1] < k[el][1]]
        for name, cls in impls.items():
            half = int((p["box"][0] + p["box"][2]) / 2 > 0.5)       # one instance per side of the camera
            h = inst.setdefault((name, half), cls())
            t = time.perf_counter()
            out = h(frame, p, now)
            st = stat[name]
            st["ms"] += (time.perf_counter() - t) * 1000; st["calls"] += 1
            for s in raised:
                st["want"] += 1; st["hit"] += out[s] is not None
            if vis is not None and name == "new":
                for x0, y0, x1, y1 in (h.crop or []):
                    cv2.rectangle(vis, (x0, y0), (x1, y1), (0, 255, 255), 2)
                for o in out.values():
                    if o is not None:
                        for q in o["px"][:, :2].astype(int):
                            cv2.circle(vis, tuple(q), 2, (0, 255, 0), -1)
        if vis is not None:
            for q, cf in zip(k.astype(int), c):
                if cf > 0.3:
                    cv2.circle(vis, tuple(q), 4, (255, 0, 255), -1)
    if vis is not None:
        cv2.imwrite(snap, vis); next_snap = now + 3000
cam.close()
print(f"frames {frames}, with a person {people_frames}, frame {frame.shape[1]}x{frame.shape[0]}")
for n, st in stat.items():
    rate = 100 * st["hit"] / max(1, st["want"])
    print(f"{n:4s}: hand seen {rate:5.1f}% of {st['want']} raised-hand frames | {st['ms'] / max(1, st['calls']):.1f} ms/call")
