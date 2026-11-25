# ไฟล์นี้: [Tracker] คำนวณจากโครงร่างกาย: ประมาณระยะห่างจากกล้อง (ไหล่ / ลำตัว / ทั้งตัว) และตัวนับการย่ำเท้า
"""Distance estimate + step counter from YOLO keypoints (COCO-17)."""
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


# ▸ นับการย่ำเท้า/วิ่งอยู่กับที่ 2 แบบ (ใช้แบบไหนก็ได้ที่เห็น):
#   1) เข่า/ข้อเท้าข้างไหนสูงกว่า สลับข้าง = 1 ก้าว (ลบค่าเอียงประจำตัวออกก่อน เพราะยืนปกติเข่า 2 ข้างก็ไม่เท่ากัน)
#   2) ตัวเด้งขึ้น-ลง (สะโพก/ไหล่) 1 ครั้ง = 1 ก้าว  -> ใช้ตอนขาหลุดขอบล่างของภาพ
class StepDetector:
    """Walking / running in place. Values are in torso lengths, so they do not depend on distance.
    Give it RAW keypoints (not smoothed): smoothing flattens fast steps. Points stuck on the bottom
    edge of the image (legs cut off) are ignored."""

    def __init__(self, thresh=0.045, bob=0.022, window=2.0):
        self.thresh, self.bob_th, self.window = thresh, bob, window
        self.reset()

    def reset(self):
        self.side, self.steps, self.times = 0, 0, []
        self.base = self.hip = self.t = self.torso = self.prev_d = None
        self.bob_up, self.last_leg = False, -9.0
        self.dbg = (0.0, 0.0)

    def __call__(self, kps, conf, now, img_h=None, min_conf=0.3):
        if min(conf[LS], conf[RS], conf[LH], conf[RH]) < min_conf:
            return self.rate(now)
        tl = float(np.linalg.norm((kps[LS] + kps[RS]) / 2 - (kps[LH] + kps[RH]) / 2))
        if tl < 10:
            return self.rate(now)
        dt = 0.033 if self.t is None else min(0.5, max(1e-3, now - self.t))
        self.t = now
        self.torso = tl if self.torso is None else self.torso + (tl - self.torso) * min(1.0, dt / 1.0)
        torso = self.torso                                   # slow: a noisy scale would look like movement
        ok = lambda i: conf[i] > min_conf and (img_h is None or kps[i][1] < img_h - 4)

        # 1) legs: which knee (or ankle) is higher, minus the slow baseline (stance offset)
        d = None
        if ok(LK) and ok(RK):
            d = (kps[RK][1] - kps[LK][1]) / torso          # >0: left knee higher
        if ok(LA) and ok(RA):
            a = (kps[RA][1] - kps[LA][1]) / torso
            d = a if d is None or abs(a) > abs(d) else d
        x = 0.0
        if d is not None:
            d, self.prev_d = (d if self.prev_d is None else (d + self.prev_d) / 2), d   # 2-frame average: less jitter
            self.base = d if self.base is None else self.base + (d - self.base) * min(1.0, dt / 1.5)
            x = d - self.base
            side = 1 if x > self.thresh else -1 if x < -self.thresh else 0
            if side and side != self.side:
                if self.side != 0:
                    self._step(now); self.last_leg = now
                self.side = side

        # 2) body bounce: hip height minus its slow average; one up-down = one step.
        #    Only counts while the legs are not giving steps (no double counting).
        hy = float(kps[LH][1] + kps[RH][1] + kps[LS][1] + kps[RS][1]) / 4   # px (4 points: less jitter)
        self.hip = hy if self.hip is None else self.hip + (hy - self.hip) * min(1.0, dt / 0.8)
        b = (self.hip - hy) / torso                          # >0: body higher than usual
        if b > self.bob_th and not self.bob_up:
            self.bob_up = True
            if now - self.last_leg > 0.8:
                self._step(now)
        elif b < -self.bob_th * 0.5:
            self.bob_up = False
        self.dbg = (x, b)
        return self.rate(now)

    def _step(self, now):
        self.steps += 1
        self.times.append(now)

    # ▸ จำนวนก้าวต่อวินาทีใน 2 วินาทีล่าสุด
    def rate(self, now):
        """steps per second over the last `window` seconds."""
        self.times = [t for t in self.times if now - t <= self.window]
        return len(self.times) / self.window
