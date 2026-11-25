# ไฟล์นี้: [Tracker] YOLO11-pose จับ "ตัว + ขา" (17 จุด + id จาก ByteTrack) และ MediaPipe จับ "มือ 2 ข้าง" (21 จุด/มือ)
#   มือ: ตัดภาพแยกรอบมือแต่ละข้าง แล้วขยายเป็น 256 px (มือได้พิกเซลเยอะแม้ยืน 2-3 ม.) + กรองสั่นทุกจุด + บอกนิ้วที่เหยียด / ท่ามือ
import os, urllib.request
import numpy as np
import cv2
import mediapipe as mp
import torch
from ultralytics import YOLO
from filters import OneEuro

HERE = os.path.dirname(os.path.abspath(__file__))
HAND_TASK = os.path.join(HERE, "models", "hand_landmarker.task")
HAND_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task"
LS, RS, LW, RW = 5, 6, 9, 10
# 21-point hand: 0 wrist | thumb 1-4 | index 5-8 | middle 9-12 | ring 13-16 | pinky 17-20
HAND_BONES = [(0, 1), (1, 2), (2, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8), (5, 9), (9, 10), (10, 11), (11, 12),
              (9, 13), (13, 14), (14, 15), (15, 16), (13, 17), (0, 17), (17, 18), (18, 19), (19, 20)]
GESTURES = {(0, 0, 0, 0): "fist", (1, 0, 0, 0): "point", (1, 1, 0, 0): "peace", (1, 1, 1, 1): "open"}


class Body:
    """BGR frame -> [{"id", "box" (normalized x0,y0,x1,y1), "kps" (17,2) px, "kc" (17,)}]"""

    def __init__(self, weights, conf=0.3, imgsz=(384, 640)):   # 384x640 = 16:9 camera with no wasted padding
        dev = 0 if torch.cuda.is_available() else "cpu"
        self.name = torch.cuda.get_device_name(0) if dev == 0 else "CPU"
        onnx = os.path.splitext(weights)[0] + ".onnx"
        if not os.path.exists(onnx):   # one-time export (.pt is downloaded first): ONNX Runtime is ~2x faster here
            YOLO(weights).export(format="onnx", imgsz=imgsz, simplify=False)
        self.model = YOLO(onnx, task="pose")
        self.args = dict(persist=True, tracker=os.path.join(HERE, "config", "bytetrack_rte.yaml"), conf=conf,
                         imgsz=imgsz, device=dev, classes=[0], verbose=False)
        self(np.zeros((720, 1280, 3), np.uint8))   # warm-up: the first real frame isn't slow

    def __call__(self, bgr):
        h, w = bgr.shape[:2]
        r = self.model.track(bgr, **self.args)[0]
        if r.boxes.id is None:
            return []
        box = (r.boxes.xyxy.cpu().numpy() / [w, h, w, h]).clip(0, 1)
        return [{"id": i, "box": tuple(b), "kps": k, "kc": c} for i, b, k, c in
                zip(r.boxes.id.int().tolist(), box, r.keypoints.xy.cpu().numpy(), r.keypoints.conf.cpu().numpy())]


def hand_shape(p):
    """p: (21,3) landmarks -> fingers [thumb..pinky] 0/1, pinch ratio, gesture name."""
    n = np.linalg.norm
    palm = n(p[9] - p[0]) + 1e-6

    def straight(a, b, c):
        v1, v2 = p[a] - p[b], p[c] - p[b]
        return np.dot(v1, v2) / (n(v1) * n(v2) + 1e-9) < -0.85      # joint angle > ~150 deg
    f = [int(straight(2, 3, 4) and n(p[4] - p[5]) > 0.55 * palm)]
    f += [int(straight(m, m + 1, m + 3) and n(p[m + 3] - p[0]) > n(p[m + 1] - p[0])) for m in (5, 9, 13, 17)]
    pinch = float(n(p[4] - p[8]) / palm)
    return f, pinch, "pinch" if pinch < 0.35 else GESTURES.get(tuple(f[1:]), "other")


class Hands:
    """MediaPipe HandLandmarker for ONE player. Call -> {0: screen-left hand, 1: screen-right hand} (None = not seen).

    Each arm gets its own small crop centred on the hand (from YOLO wrist + elbow, or the last MediaPipe palm),
    enlarged to HAND_PX. At 2-3 m a hand is ~40 px in the camera image: inside one big crop of both arms it
    shrank to ~18 px for the palm detector and was missed often; alone in its crop it fills ~1/3 of the image."""

    HAND_PX = 256       # each hand crop is resized to this (MediaPipe palm detector input is 192)
    CROP_SW = 1.7       # crop side, in shoulder widths (a hand is ~0.5 shoulder width: room to move)
    HOLD_S = 0.25       # a hand missed for less than this keeps its last pose (moved with the wrist): no flicker

    def __init__(self):
        if not os.path.exists(HAND_TASK):
            os.makedirs(os.path.dirname(HAND_TASK), exist_ok=True)
            urllib.request.urlretrieve(HAND_URL, HAND_TASK)
        V = mp.tasks.vision
        with open(HAND_TASK, "rb") as f:   # pass bytes: MediaPipe can't open paths with Thai characters
            model = f.read()
        # one landmarker per arm (VIDEO mode keeps tracking its own hand between frames)
        self.lm = [V.HandLandmarker.create_from_options(V.HandLandmarkerOptions(
            base_options=mp.tasks.BaseOptions(model_asset_buffer=model), running_mode=V.RunningMode.VIDEO,
            num_hands=1, min_hand_detection_confidence=0.35, min_hand_presence_confidence=0.35,
            min_tracking_confidence=0.35)) for _ in range(2)]
        self.ts, self.crop, self.want_reset = 0, None, True

    def reset(self):   # safe while a background call is running: applied at the start of the next call
        self.want_reset = True

    def __call__(self, frame, p, t_ms):
        if self.want_reset:
            self.filt = {0: OneEuro(1.5, 0.02), 1: OneEuro(1.5, 0.02)}
            self.last = {LW: None, RW: None}     # arm -> (raw px, wrist at that time, time ms)
            self.want_reset = False
        h, w = frame.shape[:2]
        k, c = p["kps"], p["kc"]
        out = {0: None, 1: None}
        if c[LS] < 0.2 or c[RS] < 0.2:
            return out
        sw = max(float(np.linalg.norm(k[LS] - k[RS])), 30.0)
        self.ts = max(self.ts + 1, int(t_ms))
        side = int(self.CROP_SW * sw)
        crops, found = [], {}
        for n, (wr, el) in enumerate(((LW, 7), (RW, 8))):
            last = self.last[wr]
            fresh = last is not None and t_ms - last[2] < 300
            if c[wr] > 0.2:
                centre = k[wr] + (0.35 * (k[wr] - k[el]) if c[el] > 0.2 else 0)   # hand sits past the wrist
                if fresh:   # MediaPipe saw it a moment ago: trust its palm more than YOLO's wrist
                    centre = 0.5 * centre + 0.5 * last[0][[0, 5, 9, 13, 17], :2].mean(0)
            elif fresh:
                centre = last[0][[0, 5, 9, 13, 17], :2].mean(0)
            else:
                continue
            x0, y0 = int(centre[0] - side / 2), int(centre[1] - side / 2)
            x1, y1 = x0 + side, y0 + side
            if x1 <= 0 or y1 <= 0 or x0 >= w or y0 >= h:
                continue
            # pad when the crop leaves the image so the hand keeps its size and place in the crop
            crop = cv2.copyMakeBorder(frame[max(y0, 0):min(y1, h), max(x0, 0):min(x1, w)],
                                      max(0, -y0), max(0, y1 - h), max(0, -x0), max(0, x1 - w), cv2.BORDER_CONSTANT)
            crop = cv2.resize(crop, (self.HAND_PX, self.HAND_PX),
                              interpolation=cv2.INTER_LINEAR if side < self.HAND_PX else cv2.INTER_AREA)
            img = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(crop[:, :, ::-1]))
            res = self.lm[n].detect_for_video(img, self.ts)
            crops.append((x0, y0, x1, y1))
            if res.hand_landmarks:
                px = np.array([[q.x * side + x0, q.y * side + y0, q.z * side] for q in res.hand_landmarks[0]])
                ref = k[wr] if c[wr] > 0.2 else centre
                if np.linalg.norm(px[0, :2] - ref) < 1.0 * sw:      # its own hand, not the other one / the other player's
                    found[wr] = px
        self.crop = crops

        # both crops locked onto the same hand (hands together): keep it for the arm whose wrist is nearer
        if len(found) == 2 and np.linalg.norm(found[LW][9, :2] - found[RW][9, :2]) < 0.25 * sw:
            far = max(found, key=lambda a: np.linalg.norm(found[a][0, :2] - k[a]))
            del found[far]
        for wr in (LW, RW):
            if wr in found:
                self.last[wr] = (found[wr], k[wr].copy(), t_ms)
            elif self.last[wr] is not None and t_ms - self.last[wr][2] < self.HOLD_S * 1000:
                px0, w0, _ = self.last[wr]          # short miss: keep the pose, follow the wrist
                shift = (k[wr] - w0) if c[wr] > 0.2 else 0
                found[wr] = px0 + np.append(shift, 0) if c[wr] > 0.2 else px0

        hands = sorted(found.values(), key=lambda a: a[0, 0])
        if len(hands) == 2:
            sides = {0: hands[0], 1: hands[1]}
        elif hands:
            sides = {0 if hands[0][0, 0] < (k[LS][0] + k[RS][0]) / 2 else 1: hands[0]}
        else:
            sides = {}
        for s in (0, 1):
            if s not in sides:
                self.filt[s].reset()
                continue
            px = self.filt[s](sides[s], t_ms / 1000.0)
            f, pinch, g = hand_shape(px)
            out[s] = {"px": px, "palm": px[[0, 5, 9, 13, 17], :2].mean(0), "f": f, "pinch": pinch, "g": g}
        return out
