# ไฟล์นี้: [Tracker] จับคู่คนในกล้องเข้าช่อง P1 / P2 และล็อคไว้กับ "คนเดิม" เท่านั้น (คนที่ 3 ไม่ถูกนับ)
"""Two-player slot locking.

Rules:
- Exactly two slots: P1 and P2. Anybody else in the camera is never counted (no 3rd player).
- Joining ("scan"): with zones on, a slot only takes a person standing in ITS stand zone (like the rings
  on the character-select screen: P1 left of centre, P2 right of centre, `zone_x` metres from the centre
  line each) at `join_near`..`join_m` metres from the camera, held still there for `scan_s` seconds.
  Without zones the old rule applies: left half of the image -> P1, right half -> P2.
- Once locked, the slot follows ONLY that person: by tracker id, checked against the person's
  appearance (clothes colour signature, "sig"). If the tracker hands the id to someone else, or the two
  players' ids swap, the signature puts them back. A lost player is re-adopted only by a person who
  looks the same; the slot is freed after `lost_grace` seconds out of the camera.
- Occlusion: someone walking in front of a locked player never takes the slot. While anybody covers the
  spot where the player was last seen, the slot is HELD (up to `hold_max` s); it is freed only when that
  spot is empty (the player is really gone) for `lost_grace` seconds.
- One person can never hold both slots.
"""
from dataclasses import dataclass, field
import time
import numpy as np


# ▸ ค่าทับซ้อนของ 2 กรอบ (0 = ไม่ทับ, 1 = ทับกันพอดี)
def iou(a, b):
    ax0, ay0, ax1, ay1 = a
    bx0, by0, bx1, by1 = b
    ix = max(0.0, min(ax1, bx1) - max(ax0, bx0))
    iy = max(0.0, min(ay1, by1) - max(ay0, by0))
    inter = ix * iy
    u = (ax1 - ax0) * (ay1 - ay0) + (bx1 - bx0) * (by1 - by0) - inter
    return inter / u if u > 0 else 0.0


# ▸ ความเหมือนของสีเสื้อผ้า 2 คน (Bhattacharyya: 1 = เหมือนเป๊ะ, 0 = ต่างกันหมด). ไม่มีข้อมูล = None
def similarity(a, b):
    if a is None or b is None:
        return None
    return float(np.sum(np.sqrt(np.asarray(a) * np.asarray(b))))


def _centre(box):
    return (box[0] + box[2]) / 2, (box[1] + box[3]) / 2


# ▸ กรอบ b บังกรอบ a ไปกี่ส่วน (0..1 ของพื้นที่ a)
def cover(a, b):
    ix = max(0.0, min(a[2], b[2]) - max(a[0], b[0]))
    iy = max(0.0, min(a[3], b[3]) - max(a[1], b[1]))
    area = (a[2] - a[0]) * (a[3] - a[1])
    return ix * iy / area if area > 0 else 0.0


@dataclass
# ▸ ช่องผู้เล่น 1 ช่อง: id ของคนที่ล็อค, กรอบล่าสุด, เวลาที่เห็นล่าสุด, สีเสื้อผ้า, สถานะสแกน
class Slot:
    index: int
    track_id: int | None = None
    box: tuple | None = None
    last_seen: float = 0.0
    locked_at: float = 0.0
    sig: object = None               # appearance signature of the locked person
    scan_id: int | None = None       # person being scanned in this slot's zone
    scan_t0: float = 0.0
    scan: float = 0.0                # scan progress 0..1 (1 = locked)
    spot_seen: float = 0.0           # last time somebody still stood on the player's spot (occlusion)
    hidden: bool = False             # locked but not visible this frame (covered / missed)
    data: dict = field(default_factory=dict)

    @property
    def locked(self):
        return self.track_id is not None


# ▸ ตัวจัดผู้เล่นเข้าช่อง P1 / P2
class PlayerLock:
    def __init__(self, split=0.5, lost_grace=0.8, readopt_iou=0.3, min_box_h=0.25,
                 join_m=2.0, leave_m=2.5, min_m=0.0, lock_until_gone=False, readopt_dist=0.3,
                 join_near=None, zone_x=None, zone_r=0.5, scan_s=0.0,
                 sig_min=0.65, sig_bad=0.55, swap_margin=0.15, hold_max=0.0, cover_min=0.25):
        self.readopt_dist = readopt_dist  # max centre jump (normalized) when re-adopting a new id
        self.split = split
        # distance gate (hysteresis): join only within join_m, kicked out beyond leave_m.
        # people without a distance ("dist" missing/None) are judged by box height only.
        self.join_m, self.leave_m = join_m, leave_m
        self.require_dist = False
        # min_m: people closer than this can't join. join_near: nearest join distance (default min_m).
        # lock_until_gone: once locked, distance is ignored; the slot is freed only when the person leaves.
        self.min_m, self.lock_until_gone = min_m, lock_until_gone
        self.join_near = min_m if join_near is None else join_near
        # stand zones: slot 1 centre at -zone_x m, slot 2 at +zone_x m (person "lx" = metres from the
        # camera's centre line). None = old left/right-half rule.
        self.zone_x, self.zone_r, self.scan_s = zone_x, zone_r, scan_s
        # appearance: same person >= sig_min; the locked id looking like < sig_bad = probably someone else
        self.sig_min, self.sig_bad, self.swap_margin = sig_min, sig_bad, swap_margin
        self.lost_grace = lost_grace
        # occlusion hold: a slot whose spot is covered by someone (>= cover_min of its box) is kept up to
        # hold_max seconds. 0 = off (old behaviour).
        self.hold_max, self.cover_min = hold_max, cover_min
        self.readopt_iou = readopt_iou
        self.min_box_h = min_box_h  # ignore far-away/small people (normalized height)
        self.slots = [Slot(1), Slot(2)]

    def reset(self):
        self.slots = [Slot(1), Slot(2)]

    # ▸ ศูนย์กลางจุดยืนของช่อง (เมตรจากเส้นกลางกล้อง, ลบ = ซ้ายจอ)
    def zone_centre(self, index):
        return -self.zone_x if index == 1 else self.zone_x

    # ▸ คนนี้ยืนอยู่ในจุดยืนของช่องนี้ไหม (ระยะห่างกล้อง + ซ้าย/ขวา)
    def in_zone(self, s, p):
        x0, y0, x1, y1 = p["box"]
        if (y1 - y0) < self.min_box_h:
            return False
        d = p.get("dist")
        if d is None and (self.require_dist or self.zone_x is not None):
            return False      # distance unknown (shoulders not visible) -> can't verify the band
        if d is not None and (d > self.join_m or d < self.join_near):
            return False      # must stand inside the join band
        if self.zone_x is not None:
            lx = p.get("lx")
            return lx is not None and abs(lx - self.zone_centre(s.index)) <= self.zone_r
        cx = (x0 + x1) / 2
        return cx < self.split if s.index == 1 else cx >= self.split

    def _keep(self, s, p, now, result):
        s.track_id, s.box, s.last_seen, s.spot_seen = p["id"], p["box"], now, now
        sim = similarity(s.sig, p.get("sig"))
        if s.sig is None:
            s.sig = p.get("sig")
        elif sim is not None and sim >= self.sig_min:     # follow slow lighting changes, only when sure
            s.sig = 0.95 * np.asarray(s.sig) + 0.05 * np.asarray(p["sig"])
        result[s.index] = p

    # ▸ รับทุกคนในเฟรม คืน {1: คนของ P1, 2: คนของ P2} ทำตามขั้น 0-3:
    #   0) (ถ้าเปิด) ปล่อยคนที่ถอยไกลเกิน  1) ช่องที่ล็อคตาม id เดิม + เช็คสีเสื้อผ้า (id สลับคน / P1-P2 สลับกัน)
    #   2) id หาย: รับเฉพาะคนที่หน้าตา(สีเสื้อผ้า)เหมือนเดิม ภายใน lost_grace วินาที ไม่งั้นปล่อยช่อง
    #   3) ช่องว่าง: สแกนคนที่ยืนนิ่งในจุดยืนของช่องนั้น ครบ scan_s วินาที -> ล็อค
    def update(self, people, now=None):
        """people: list of dicts {id, box:(x0,y0,x1,y1) normalized, dist?, lx?, sig?}.
        Returns dict slot_index -> person dict (or None)."""
        now = time.monotonic() if now is None else now
        by_id = {p["id"]: p for p in people if p.get("id") is not None}
        result = {1: None, 2: None}

        # 0) a locked player who walks back beyond leave_m is out of range -> free the slot now
        for s in self.slots:
            if s.locked and s.track_id in by_id:
                d = by_id[s.track_id].get("dist")
                if d is not None and d > self.leave_m and not self.lock_until_gone:
                    s.track_id, s.box, s.sig = None, None, None
                    s.data["out_of_range"] = now

        # 1a) both players seen but their ids swapped (crossed paths): appearance says so -> swap back
        a, b = self.slots
        if a.locked and b.locked and a.track_id in by_id and b.track_id in by_id:
            pa, pb = by_id[a.track_id], by_id[b.track_id]
            sims = [similarity(a.sig, pa.get("sig")), similarity(b.sig, pb.get("sig")),
                    similarity(a.sig, pb.get("sig")), similarity(b.sig, pa.get("sig"))]
            if None not in sims and sims[2] + sims[3] > sims[0] + sims[1] + 2 * self.swap_margin:
                a.track_id, b.track_id = b.track_id, a.track_id

        # 1b) keep locked slots on their own id
        used = {s.track_id for s in self.slots if s.locked}
        for s in self.slots:
            if s.locked and s.track_id in by_id:
                p = by_id[s.track_id]
                sim = similarity(s.sig, p.get("sig"))
                if sim is not None and sim < self.sig_bad:
                    # the id now sits on someone else (a passer-by took it / stands in front): look for our
                    # player nearby, else HOLD the slot on its last box and do not follow the stranger
                    alt = self._match(s, people, used, strict=True)
                    if alt is None:
                        used.discard(s.track_id)
                        s.track_id = -s.index          # locked, no id: step 2 re-adopts the same person
                        continue
                    used.discard(s.track_id); used.add(alt["id"])
                    p = alt
                self._keep(s, p, now, result)

        used = {s.track_id for s in self.slots if s.locked}

        # 2) locked but missing: re-adopt an id-switched track of the SAME person, else expire
        for s in self.slots:
            if not s.locked or result[s.index] is not None:
                continue
            best = self._match(s, people, used)
            if best is not None:
                used.discard(s.track_id)
                used.add(best["id"])
                self._keep(s, best, now, result)
                continue
            if self.hold_max > 0 and any(cover(s.box, p["box"]) >= self.cover_min for p in people):
                s.spot_seen = now                       # somebody stands on / in front of the spot: hold
            gone = now - max(s.last_seen, s.spot_seen) > self.lost_grace
            if gone or (self.hold_max > 0 and now - s.last_seen > self.hold_max):
                s.track_id, s.box, s.sig = None, None, None  # player left -> slot free

        for s in self.slots:
            s.hidden = s.locked and result[s.index] is None

        # 3) free slots scan a new person standing in their zone
        for s in self.slots:
            if s.locked:
                s.scan, s.scan_id = 1.0, None
                continue
            cands = [p for p in people if p.get("id") is not None and p["id"] not in used and self.in_zone(s, p)]
            if not cands:
                s.scan, s.scan_id = 0.0, None
                continue
            p = max(cands, key=lambda q: q["box"][3] - q["box"][1])   # prefer the closest (tallest) person
            if p["id"] != s.scan_id:
                s.scan_id, s.scan_t0 = p["id"], now
            s.scan = 1.0 if self.scan_s <= 0 else min(1.0, (now - s.scan_t0) / self.scan_s)
            if s.scan >= 1.0:
                s.track_id, s.box, s.last_seen, s.locked_at, s.sig = p["id"], p["box"], now, now, p.get("sig")
                s.scan_id = None
                used.add(p["id"])
                result[s.index] = p
        return result

    # ▸ หาคนที่น่าจะเป็นผู้เล่นของช่องนี้ (id ใหม่ของคนเดิม) จากคนที่ยังไม่มีเจ้าของ
    def _match(self, s, people, used, strict=False):
        free = [p for p in people if p.get("id") is not None and p["id"] not in used]
        if s.sig is not None and any(p.get("sig") is not None for p in free):
            # appearance known: only a person who looks the same, the nearest one to the last position
            lcx, lcy = _centre(s.box)
            best, best_d = None, 0.35 if strict else 0.6
            for p in free:
                sim = similarity(s.sig, p.get("sig"))
                if sim is None or sim < self.sig_min:
                    continue
                cx, cy = _centre(p["box"])
                dc = ((cx - lcx) ** 2 + (cy - lcy) ** 2) ** 0.5
                if dc < best_d:
                    best, best_d = p, dc
            return best
        if strict:
            return None
        best, best_iou = None, self.readopt_iou
        for p in free:
            d = p.get("dist")
            if d is not None and d > self.leave_m and not self.lock_until_gone:
                continue
            v = iou(p["box"], s.box)
            if v > best_iou:
                best, best_iou = p, v
        if best is None and self.lock_until_gone:
            # stepping very close changes the box a lot (bigger, cut off by the frame edge), so the
            # tracker issues a new id with little overlap. Take the unclaimed person whose centre is
            # nearest the last one.
            lcx, lcy = _centre(s.box)
            best_d = self.readopt_dist
            for p in free:
                cx, cy = _centre(p["box"])
                dc = ((cx - lcx) ** 2 + (cy - lcy) ** 2) ** 0.5
                if dc < best_d:
                    best, best_d = p, dc
        return best
