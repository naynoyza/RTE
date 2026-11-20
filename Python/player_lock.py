# ไฟล์นี้: [Tracker] จับคู่คนในกล้องเข้าช่อง P1 (ฝั่งซ้าย) / P2 (ฝั่งขวา) และล็อคไว้ ไม่ให้สลับคน
"""Two-player slot locking.

Rules:
- Slot 1 (P1) and slot 2 (P2). A slot takes the first person who enters its side
  of the camera (left half -> P1, right half -> P2, split adjustable).
- Once locked, the slot follows ONLY that person (by tracker id) until they leave
  the frame for longer than `lost_grace` seconds. Other people are ignored.
- If the tracker swaps ids while the person is still there (occlusion), the slot
  re-adopts the new id when its box overlaps the last known box.
- One person can never hold both slots (no double tracking).
"""
from dataclasses import dataclass, field
import time


# ▸ ค่าทับซ้อนของ 2 กรอบ (0 = ไม่ทับ, 1 = ทับกันพอดี)
def iou(a, b):
    ax0, ay0, ax1, ay1 = a
    bx0, by0, bx1, by1 = b
    ix = max(0.0, min(ax1, bx1) - max(ax0, bx0))
    iy = max(0.0, min(ay1, by1) - max(ay0, by0))
    inter = ix * iy
    u = (ax1 - ax0) * (ay1 - ay0) + (bx1 - bx0) * (by1 - by0) - inter
    return inter / u if u > 0 else 0.0


@dataclass
# ▸ ช่องผู้เล่น 1 ช่อง: id ของคนที่ล็อค, กรอบล่าสุด, เวลาที่เห็นล่าสุด
class Slot:
    index: int
    track_id: int | None = None
    box: tuple | None = None
    last_seen: float = 0.0
    locked_at: float = 0.0
    data: dict = field(default_factory=dict)

    @property
    def locked(self):
        return self.track_id is not None


# ▸ ตัวจัดผู้เล่นเข้าช่อง P1 (ฝั่งซ้าย) / P2 (ฝั่งขวา)
class PlayerLock:
    def __init__(self, split=0.5, lost_grace=0.8, readopt_iou=0.3, min_box_h=0.25,
                 join_m=2.0, leave_m=2.5, min_m=0.0, lock_until_gone=False, readopt_dist=0.3):
        self.readopt_dist = readopt_dist  # max centre jump (normalized) when re-adopting a new id
        self.split = split
        # distance gate (hysteresis): join only within join_m, kicked out beyond leave_m.
        # people without a distance ("dist" missing/None) are judged by box height only.
        self.join_m, self.leave_m = join_m, leave_m
        self.require_dist = False
        # min_m: people closer than this can't join. lock_until_gone: once locked, distance is ignored
        # and the slot is only freed when the person leaves the camera.
        self.min_m, self.lock_until_gone = min_m, lock_until_gone
        self.lost_grace = lost_grace
        self.readopt_iou = readopt_iou
        self.min_box_h = min_box_h  # ignore far-away/small people (normalized height)
        self.slots = [Slot(1), Slot(2)]

    def reset(self):
        self.slots = [Slot(1), Slot(2)]

    # ▸ รับทุกคนในเฟรม คืน {1: คนของ P1, 2: คนของ P2} ทำตามขั้น 0-3:
    #   0) (ถ้าเปิด) ปล่อยคนที่ถอยไกลเกิน  1) ช่องที่ล็อคตาม id เดิม
    #   2) id หาย: รับ id ใหม่ที่ทับกรอบเดิมหรือใกล้ตำแหน่งเดิม ภายใน lost_grace วินาที ไม่งั้นปล่อยช่อง
    #   3) ช่องว่าง: รับคนใหม่ในฝั่งของตัวเองที่ยืนในระยะ 2-3 ม. (เลือกคนที่ใหญ่/ใกล้สุด)
    def update(self, people, now=None):
        """people: list of dicts {id, box:(x0,y0,x1,y1) normalized, ...}.
        Returns dict slot_index -> person dict (or None)."""
        now = time.monotonic() if now is None else now
        by_id = {p["id"]: p for p in people if p.get("id") is not None}
        result = {1: None, 2: None}

        # 0) a locked player who walks back beyond leave_m is out of range -> free the slot now
        for s in self.slots:
            if s.locked and s.track_id in by_id:
                d = by_id[s.track_id].get("dist")
                if d is not None and d > self.leave_m and not self.lock_until_gone:
                    s.track_id, s.box = None, None
                    s.data["out_of_range"] = now

        # 1) keep locked slots on their own id
        for s in self.slots:
            if s.locked and s.track_id in by_id:
                p = by_id[s.track_id]
                s.box, s.last_seen = p["box"], now
                result[s.index] = p

        used = {s.track_id for s in self.slots if s.locked}

        # 2) locked but missing: try to re-adopt an id-switched track, else expire
        for s in self.slots:
            if not s.locked or result[s.index] is not None:
                continue
            if now - s.last_seen > self.lost_grace:
                s.track_id, s.box = None, None  # player left -> slot free
                continue
            best, best_iou = None, self.readopt_iou
            for p in people:
                if p["id"] in used:
                    continue
                d = p.get("dist")
                if d is not None and d > self.leave_m and not self.lock_until_gone:
                    continue
                v = iou(p["box"], s.box)
                if v > best_iou:
                    best, best_iou = p, v
            if best is None and self.lock_until_gone:
                # stepping very close changes the box a lot (bigger, cut off by the frame edge), so the
                # tracker issues a new id with little overlap. Take the unclaimed person whose centre is
                # nearest the last one. No distance check here: a locked player may come as close as they like.
                lcx, lcy = (s.box[0] + s.box[2]) / 2, (s.box[1] + s.box[3]) / 2
                best_d = self.readopt_dist
                for p in people:
                    if p["id"] in used:
                        continue
                    x0, y0, x1, y1 = p["box"]
                    dc = ((x0 + x1) / 2 - lcx) ** 2 + ((y0 + y1) / 2 - lcy) ** 2
                    if dc ** 0.5 < best_d:
                        best, best_d = p, dc ** 0.5
            if best is not None:
                used.discard(s.track_id)
                s.track_id, s.box, s.last_seen = best["id"], best["box"], now
                used.add(best["id"])
                result[s.index] = best

        # 3) free slots take a new person standing on their side
        for s in self.slots:
            if s.locked:
                continue
            cands = []
            for p in people:
                if p["id"] is None or p["id"] in used:
                    continue
                x0, y0, x1, y1 = p["box"]
                if (y1 - y0) < self.min_box_h:
                    continue
                d = p.get("dist")
                if d is None and self.require_dist:
                    continue  # distance unknown (shoulders not visible) -> can't verify 1 m
                if d is not None and (d > self.join_m or d < self.min_m):
                    continue  # must stand inside the join band
                cx = (x0 + x1) / 2
                on_side = cx < self.split if s.index == 1 else cx >= self.split
                if on_side:
                    cands.append((y1 - y0, p))  # prefer the closest (tallest) person
            if cands:
                cands.sort(key=lambda c: -c[0])
                p = cands[0][1]
                s.track_id, s.box, s.last_seen, s.locked_at = p["id"], p["box"], now, now
                used.add(p["id"])
                result[s.index] = p
        return result
