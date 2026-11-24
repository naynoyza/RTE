# ไฟล์นี้: [Tracker] จับโครงกระดูกผู้เล่นด้วย MediaPipe Pose (แทนจุดกระดูกของ YOLO)
#   YOLO หาแค่ "กรอบคน" + ล็อค P1/P2 -> ไฟล์นี้ตัดภาพเฉพาะกรอบของผู้เล่นแต่ละคนไปให้ MediaPipe
#   แล้วแปลงจุด 33 จุดของ MediaPipe เป็น 17 จุดแบบ COCO (ลำดับเดิมที่ Unity / gestures.py ใช้อยู่)
"""MediaPipe Pose on each locked player's crop -> COCO-17 keypoints in full-frame pixels.

One MediaPipe instance per player slot (video mode: it tracks between frames, so it is fast and steady).
A separate single-image instance measures people who are not players yet (needed for the 2-3 m join rule).
"""
from concurrent.futures import ThreadPoolExecutor
import time
import numpy as np
import cv2
import mediapipe as mp

# COCO-17 index -> MediaPipe Pose (BlazePose 33) index
COCO_FROM_MP = [0, 2, 5, 7, 8, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28]


def crop_box(box, w, h, pad_x=0.25, pad_y=0.12):
    """Normalized box (x0,y0,x1,y1) -> padded pixel crop (x0,y0,x1,y1) inside the image."""
    x0, y0, x1, y1 = box
    bw, bh = (x1 - x0) * w, (y1 - y0) * h
    X0 = int(max(0, x0 * w - bw * pad_x)); X1 = int(min(w, x1 * w + bw * pad_x))
    Y0 = int(max(0, y0 * h - bh * pad_y)); Y1 = int(min(h, y1 * h + bh * pad_y))
    return X0, Y0, X1, Y1


def to_coco(landmarks, crop):
    """MediaPipe landmarks (normalized to the crop) -> (kps (17,2) frame pixels, conf (17,))."""
    X0, Y0, X1, Y1 = crop
    cw, ch = X1 - X0, Y1 - Y0
    kps = np.zeros((17, 2), np.float32)
    kc = np.zeros(17, np.float32)
    for i, m in enumerate(COCO_FROM_MP):
        lm = landmarks[m]
        kps[i] = (X0 + lm.x * cw, Y0 + lm.y * ch)
        c = float(lm.visibility)
        if not (-0.05 <= lm.x <= 1.05 and -0.05 <= lm.y <= 1.05):
            c *= 0.3                      # guessed outside the crop (cut off by the frame) -> low confidence
        kc[i] = c
    return kps, kc


class MpSkeleton:
    def __init__(self, complexity=0, min_det=0.5, min_track=0.5):
        # complexity 1 (full) comes with the pip package; 0 (lite, ~2x faster) and 2 (heavy) are downloaded
        # by MediaPipe on first use (setup_venv.bat does it). No internet and no file -> fall back to 1.
        try:
            mp.solutions.pose.Pose(static_image_mode=True, model_complexity=complexity).close()
        except Exception as e:
            print(f"[mediapipe] model complexity {complexity} unavailable ({e}) -> using 1")
            complexity = 1
        self.complexity = complexity
        self.args = dict(model_complexity=complexity, min_detection_confidence=min_det,
                         min_tracking_confidence=min_track, smooth_landmarks=True, enable_segmentation=False)
        self.slots = {1: None, 2: None}       # slot -> (track_id, mp.Pose)
        self.single = mp.solutions.pose.Pose(static_image_mode=True, model_complexity=complexity, min_detection_confidence=0.4)
        self.pool = ThreadPoolExecutor(max_workers=2)
        self.cache = {}                       # track id -> (time, kps, kc)  for people who are not players
        self._rr = 0

    def _pose_for(self, slot, track_id):
        cur = self.slots[slot]
        if cur is None or cur[0] != track_id:     # new person in this slot -> fresh tracker state
            if cur is not None:
                cur[1].close()
            cur = (track_id, mp.solutions.pose.Pose(static_image_mode=False, **self.args))
            self.slots[slot] = cur
        return cur[1]

    def release(self, slot):
        if self.slots[slot] is not None:
            self.slots[slot][1].close()
            self.slots[slot] = None

    @staticmethod
    def _run(pose, rgb, box):
        h, w = rgb.shape[:2]
        crop = crop_box(box, w, h)
        X0, Y0, X1, Y1 = crop
        if X1 - X0 < 20 or Y1 - Y0 < 40:
            return None
        img = rgb[Y0:Y1, X0:X1]
        k = 384.0 / max(img.shape[:2])        # MediaPipe works at 256 px inside: a smaller crop = less copying, same result
        if k < 1:
            img = cv2.resize(img, (int(img.shape[1] * k), int(img.shape[0] * k)), interpolation=cv2.INTER_AREA)
        r = pose.process(np.ascontiguousarray(img))
        if r.pose_landmarks is None:
            return None
        return to_coco(r.pose_landmarks.landmark, crop)

    def players(self, rgb, assigned):
        """assigned: {slot: person or None}. Runs both players in parallel. Returns {slot: (kps, kc) or None}."""
        jobs = {}
        for s in (1, 2):
            p = assigned.get(s)
            if p is None:
                self.release(s)
                continue
            jobs[s] = self.pool.submit(self._run, self._pose_for(s, p["id"]), rgb, p["box"])
        return {s: (jobs[s].result() if s in jobs else None) for s in (1, 2)}

    def candidates(self, rgb, people, now, max_age=0.6):
        """Skeleton of people who are not players (one person per frame, round robin), cached for max_age s.
        Returns {track_id: (kps, kc)} of fresh entries."""
        if people:
            self._rr = (self._rr + 1) % len(people)
            p = people[self._rr]
            res = self._run(self.single, rgb, p["box"])
            if res is not None:
                self.cache[p["id"]] = (now, *res)
        self.cache = {k: v for k, v in self.cache.items() if now - v[0] <= max_age}
        return {k: (v[1], v[2]) for k, v in self.cache.items()}

    def close(self):
        for s in (1, 2):
            self.release(s)
        self.single.close()
        self.pool.shutdown(wait=False)
