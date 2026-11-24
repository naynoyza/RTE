# ไฟล์นี้: [Tracker] คำนวณจากโครงร่างกาย: ประมาณระยะห่างจากกล้อง (ไหล่ / ลำตัว / ทั้งตัว) และตัวนับการย่ำเท้า
"""Distance estimate + step counter from COCO-17 keypoints (now from MediaPipe, see skeleton_mp.py)."""
import numpy as np

NOSE, LS, RS, LE, RE, LW, RW, LH, RH = 0, 5, 6, 7, 8, 9, 10, 11, 12
SHOULDER_M = 0.38   # typical adult shoulder width (m)
TORSO_M = 0.50      # shoulder-centre to hip-centre (m)


# ▸ ประมาณระยะจากกล้อง: ความกว้างไหล่ในภาพ (พิกเซล) และความยาวลำตัว เทียบขนาดจริงของคน
def estimate_distance(kps, conf, img_w, focal_px, scale=1.0, min_conf=0.35):
    """Monocular distance (m) from body size in pixels. scale is set by the 1 m calibration."""
    est = []
    if conf[LS] > min_conf and conf[RS] > min_conf:
        sw = np.linalg.norm(kps[LS] - kps[RS])
        if sw > 5:
            est.append(focal_px * SHOULDER_M / sw)
    if min(conf[LS], conf[RS], conf[LH], conf[RH]) > min_conf:
        tl = np.linalg.norm((kps[LS] + kps[RS]) / 2 - (kps[LH] + kps[RH]) / 2)
        if tl > 5:
            est.append(focal_px * TORSO_M / tl)
    if not est:
        return None
    # people turned sideways have narrow shoulders -> take the smaller (closer) estimate
    return float(min(est) * scale)


LK, RK, LA, RA = 13, 14, 15, 16
BODY_M = 1.50  # nose to ankle-centre for an adult (m)


# ▸ ประมาณระยะจากความสูงจมูกถึงข้อเท้า (ใช้ได้เมื่อเห็นทั้งตัว แม่นกว่า)
def full_body_distance(kps, conf, focal_px, min_conf=0.35):
    """Distance from whole-body height (nose -> ankles). Needs head and both ankles visible."""
    if min(conf[NOSE], conf[LA], conf[RA]) < min_conf:
        return None
    hpx = (kps[LA][1] + kps[RA][1]) / 2 - kps[NOSE][1]
    return float(focal_px * BODY_M / hpx) if hpx > 20 else None


# ▸ นับการย่ำเท้า: ดูว่าเข่า/ข้อเท้าข้างไหนยกสูงกว่า เมื่อสลับข้าง = 1 ก้าว
class StepDetector:
    """Walking in place: counts a step each time the higher knee (or ankle) swaps sides.
    Lift is relative to torso length, so it does not depend on distance."""

    def __init__(self, thresh=0.10, window=2.0):
        self.thresh, self.window = thresh, window
        self.reset()

    def reset(self):
        self.side, self.steps, self.times = 0, 0, []

    def __call__(self, kps, conf, now, min_conf=0.3):
        if min(conf[LS], conf[RS], conf[LH], conf[RH]) >= min_conf:
            torso = np.linalg.norm((kps[LS] + kps[RS]) / 2 - (kps[LH] + kps[RH]) / 2)
            diffs = []
            if torso > 10 and conf[LK] > min_conf and conf[RK] > min_conf:
                diffs.append((kps[RK][1] - kps[LK][1]) / torso)   # >0: left knee higher
            if torso > 10 and conf[LA] > min_conf and conf[RA] > min_conf:
                diffs.append((kps[RA][1] - kps[LA][1]) / torso)
            if diffs:
                d = max(diffs, key=abs)
                if abs(d) > self.thresh:
                    side = 1 if d > 0 else -1
                    if side != self.side:
                        if self.side != 0:
                            self.steps += 1
                            self.times.append(now)
                        self.side = side
        return self.rate(now)

    # ▸ จำนวนก้าวต่อวินาทีใน 2 วินาทีล่าสุด
    def rate(self, now):
        """steps per second over the last `window` seconds."""
        self.times = [t for t in self.times if now - t <= self.window]
        return len(self.times) / self.window
