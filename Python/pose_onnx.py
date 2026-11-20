# ไฟล์นี้: [Tracker] รัน YOLOv8-pose ด้วย onnxruntime (GPU ผ่าน DirectML) + ตัวติดตาม id คนแบบ IoU
"""YOLOv8/11-pose inference with onnxruntime (no torch needed) + a light IoU tracker."""
import os
import numpy as np
import cv2
import onnxruntime as ort

from player_lock import iou


# ▸ โมเดล YOLOv8-pose: รับภาพ คืนรายการคน (กรอบ, คะแนน, จุดกระดูก 17 จุด + ความมั่นใจ)
class PoseModel:
    def __init__(self, path, conf=0.45, nms=0.5):
        opts = ort.SessionOptions()
        opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
        opts.intra_op_num_threads = max(2, (os.cpu_count() or 4) - 2)
        self.sess, self.provider = self._fastest_session(path, opts)
        i = self.sess.get_inputs()[0]
        self.inp = i.name
        self.size = int(i.shape[2]) if isinstance(i.shape[2], int) else 640
        self.conf, self.nms = conf, nms

    @staticmethod
    # ▸ ลองรันโมเดลบนการ์ดจอทุกตัว (DirectML) และ CPU วัดความเร็ว แล้วเลือกตัวที่เร็วที่สุด
    def _fastest_session(path, opts):
        """Try every DirectML adapter (laptops have iGPU + dGPU) and CPU; keep the fastest."""
        import time
        cands = []
        if "DmlExecutionProvider" in ort.get_available_providers():
            cands += [("DmlExecutionProvider", {"device_id": d}) for d in range(3)]
        cands.append("CPUExecutionProvider")
        best = None
        for prov in cands:
            try:
                s = ort.InferenceSession(path, opts, providers=[prov])
                i = s.get_inputs()[0]
                x = np.zeros((1, 3, 640, 640), np.float32)
                for _ in range(3):
                    s.run(None, {i.name: x})
                t = time.perf_counter()
                for _ in range(10):
                    s.run(None, {i.name: x})
                ms = (time.perf_counter() - t) * 100
                name = prov if isinstance(prov, str) else f"DML gpu{prov[1]['device_id']}"
                print(f"[yolo] {name}: {ms:.1f} ms")
                if best is None or ms < best[2]:
                    best = (s, name, ms)
            except Exception:
                continue
        return best[0], f"{best[1]} ({best[2]:.1f} ms)"

    # ▸ ย่อภาพลงกรอบ 640x640 (เติมขอบเทา) -> รันโมเดล -> คัดคนที่คะแนนเกิน conf ->
    #   ตัดกรอบซ้อนกัน (NMS) -> แปลงกรอบและจุดกระดูกกลับเป็นพิกัดภาพจริง
    def __call__(self, bgr):
        h, w = bgr.shape[:2]
        s = self.size / max(h, w)
        nw, nh = int(round(w * s)), int(round(h * s))
        px, py = (self.size - nw) // 2, (self.size - nh) // 2
        canvas = np.full((self.size, self.size, 3), 114, np.uint8)
        canvas[py:py + nh, px:px + nw] = cv2.resize(bgr, (nw, nh), interpolation=cv2.INTER_LINEAR)
        blob = canvas[:, :, ::-1].transpose(2, 0, 1)[None].astype(np.float32) / 255.0
        out = self.sess.run(None, {self.inp: blob})[0][0].T  # (8400, 56)
        scores = out[:, 4]
        keep = scores > self.conf
        out, scores = out[keep], scores[keep]
        if not len(out):
            return []
        cx, cy, bw, bh = out[:, 0], out[:, 1], out[:, 2], out[:, 3]
        x0 = (cx - bw / 2 - px) / s
        y0 = (cy - bh / 2 - py) / s
        rects = np.stack([x0, y0, bw / s, bh / s], 1)
        idx = cv2.dnn.NMSBoxes(rects.tolist(), scores.tolist(), self.conf, self.nms)
        idx = np.array(idx).flatten() if len(idx) else []
        people = []
        for i in idx:
            kp = out[i, 5:].reshape(17, 3)
            kxy = np.stack([(kp[:, 0] - px) / s, (kp[:, 1] - py) / s], 1)
            bx, by, bww, bhh = rects[i]
            box = (max(0, bx / w), max(0, by / h), min(1, (bx + bww) / w), min(1, (by + bhh) / h))
            people.append({"box": tuple(float(v) for v in box), "score": float(scores[i]),
                           "kps": kxy, "kc": kp[:, 2]})
        return people


# ▸ ให้ id เดิมกับคนเดิมข้ามเฟรม: จับคู่กรอบเฟรมนี้กับเฟรมก่อนด้วยค่าทับซ้อน (IoU)
#   คนที่หายเกิน max_missing เฟรมถูกลบ, คนใหม่ได้ id ใหม่
class IouTracker:
    """Gives each person a persistent id across frames (greedy IoU + centre distance)."""

    def __init__(self, max_missing=15, min_iou=0.2):
        self.tracks = {}  # id -> (box, missing)
        self.next_id = 1
        self.max_missing, self.min_iou = max_missing, min_iou

    def update(self, people):
        pairs = []
        for tid, (tb, _) in self.tracks.items():
            for j, p in enumerate(people):
                v = iou(tb, p["box"])
                if v >= self.min_iou:
                    pairs.append((v, tid, j))
        pairs.sort(reverse=True)
        used_t, used_p = set(), set()
        for v, tid, j in pairs:
            if tid in used_t or j in used_p:
                continue
            used_t.add(tid); used_p.add(j)
            people[j]["id"] = tid
            self.tracks[tid] = (people[j]["box"], 0)
        for tid in list(self.tracks):
            if tid not in used_t:
                b, m = self.tracks[tid]
                if m + 1 > self.max_missing:
                    del self.tracks[tid]
                else:
                    self.tracks[tid] = (b, m + 1)
        for j, p in enumerate(people):
            if j not in used_p:
                p["id"] = self.next_id
                self.tracks[self.next_id] = (p["box"], 0)
                self.next_id += 1
        return people
