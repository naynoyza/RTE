# ไฟล์นี้: [Tracker] ฟิลเตอร์ One-Euro ลดอาการสั่นของตำแหน่งมือ
import math


# ▸ ฟิลเตอร์ลดอาการสั่น: นิ่ง = กรองมาก, ขยับเร็ว = ตอบสนองไว
class OneEuro:
    """One-Euro filter: smooth when still, responsive when moving."""

    def __init__(self, min_cutoff=1.2, beta=0.02, d_cutoff=1.0):
        self.min_cutoff, self.beta, self.d_cutoff = min_cutoff, beta, d_cutoff
        self.x = self.dx = self.t = None

    @staticmethod
    def _a(cutoff, dt):
        r = 2 * math.pi * cutoff * dt
        return r / (r + 1)

    def reset(self):
        self.x = self.dx = self.t = None

    def __call__(self, x, t):
        if self.t is None:
            self.x, self.dx, self.t = x, 0.0, t
            return x
        dt = max(1e-3, t - self.t)
        self.t = t
        dx = (x - self.x) / dt
        self.dx += self._a(self.d_cutoff, dt) * (dx - self.dx)
        cutoff = self.min_cutoff + self.beta * abs(self.dx)
        self.x += self._a(cutoff, dt) * (x - self.x)
        return self.x
