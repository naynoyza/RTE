# ไฟล์นี้: [Dev] ทดสอบ YOLO (กรอบคน) + MediaPipe (โครงกระดูก) กับรูป/วิดีโอ โดยไม่ต้องมีกล้องและไม่ต้องเปิด Unity
#   python Python/dev/test_yolo_mediapipe.py <รูปหรือวิดีโอ> [ไฟล์ผลลัพธ์.jpg]
#   พิมพ์เวลาที่ใช้ต่อเฟรม + จำนวนคน/จุดกระดูก แล้วบันทึกภาพที่วาดกรอบ (เทา) และโครงกระดูก 2 คนแรก (ฟ้า/ส้ม)
import os, sys, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import numpy as np
import cv2
from yolo_detect import PersonDetector, IouTracker
from skeleton_mp import MpSkeleton
from tracker import draw_skeleton

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main():
    src = sys.argv[1]
    out_path = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "Log", "test_yolo_mediapipe.jpg")
    det = PersonDetector(os.path.join(HERE, "models", "yolov8n-pose.onnx"), conf=0.3)
    ids, skel = IouTracker(), MpSkeleton()
    cap = cv2.VideoCapture(src)
    frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT)) or 1
    t_yolo = t_mp = 0.0
    n = found = 0
    vis = None
    for i in range(min(frames, 60)):
        ok, img = cap.read()
        if not ok:
            break
        h, w = img.shape[:2]
        t = time.perf_counter(); people = ids.update(det(img)); t_yolo += time.perf_counter() - t
        people.sort(key=lambda p: -(p["box"][3] - p["box"][1]))
        assigned = {1: people[0] if people else None, 2: people[1] if len(people) > 1 else None}
        rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        t = time.perf_counter(); sk = skel.players(rgb, assigned); t_mp += time.perf_counter() - t
        n += 1
        found += sum(1 for v in sk.values() if v is not None)
        vis = img.copy()
        for p in people:
            x0, y0, x1, y1 = (np.array(p["box"]) * [w, h, w, h]).astype(int)
            cv2.rectangle(vis, (x0, y0), (x1, y1), (150, 150, 150), 2)
        for s, col in ((1, (255, 160, 60)), (2, (40, 140, 255))):
            if sk[s] is not None:
                draw_skeleton(vis, sk[s][0], sk[s][1], col)
    skel.close()
    if vis is None:
        print("no frames"); return 1
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    cv2.imwrite(out_path, vis)
    print(f"frames {n}  people(last) {len(people)}  skeletons {found}/{n * min(2, max(1, len(people)))}")
    print(f"YOLO {t_yolo / n * 1000:.1f} ms/frame   MediaPipe 2 players {t_mp / n * 1000:.1f} ms/frame   -> {out_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
