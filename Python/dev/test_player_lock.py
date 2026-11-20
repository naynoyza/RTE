# ไฟล์นี้: [Dev] ชุดทดสอบ player_lock.py:  python Python/dev/test_player_lock.py
"""Unit tests for 2-player locking (no camera needed):  python -m pytest -q  or  python test_player_lock.py"""
import os, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))  # player_lock.py lives one folder up
from player_lock import PlayerLock


def P(i, cx, h=0.6, w=0.2):
    return {"id": i, "box": (cx - w / 2, 0.3, cx + w / 2, 0.3 + h)}


# ▸ ทดสอบการล็อคพื้นฐาน: แบ่งฝั่ง, ไม่รับคนใหม่, ข้ามฝั่งได้, ปล่อยเมื่อออก
class T(unittest.TestCase):
    def test_left_right_assign(self):
        L = PlayerLock()
        r = L.update([P(1, 0.25), P(2, 0.75)], 0)
        self.assertEqual(r[1]["id"], 1)
        self.assertEqual(r[2]["id"], 2)

    def test_lock_ignores_newcomer(self):
        L = PlayerLock()
        L.update([P(1, 0.25)], 0)
        r = L.update([P(1, 0.25), P(5, 0.3, h=0.9)], 0.1)  # taller person walks in on P1 side
        self.assertEqual(r[1]["id"], 1)
        self.assertIsNone(r[2])  # newcomer on left side does not steal P2

    def test_locked_player_can_cross_sides(self):
        L = PlayerLock()
        L.update([P(1, 0.25)], 0)
        r = L.update([P(1, 0.8)], 0.1)
        self.assertEqual(r[1]["id"], 1)
        self.assertIsNone(r[2])  # no double tracking

    def test_release_after_leaving(self):
        L = PlayerLock(lost_grace=0.5)
        L.update([P(1, 0.25)], 0)
        r = L.update([], 0.3)
        self.assertIsNone(r[1])
        self.assertTrue(L.slots[0].locked)          # still reserved during grace
        r = L.update([P(9, 0.25)], 0.4)              # newcomer far from old box? same spot -> re-adopt
        self.assertEqual(r[1]["id"], 9)
        L2 = PlayerLock(lost_grace=0.5)
        L2.update([P(1, 0.1, w=0.1)], 0)
        L2.update([], 1.0)                           # expired
        self.assertFalse(L2.slots[0].locked)
        r = L2.update([P(7, 0.3)], 1.1)
        self.assertEqual(r[1]["id"], 7)

    def test_newcomer_not_adopted_if_far(self):
        L = PlayerLock(lost_grace=0.5)
        L.update([P(1, 0.1, w=0.1)], 0)
        r = L.update([P(3, 0.4, w=0.1)], 0.2)        # different spot during grace
        self.assertIsNone(r[1])
        r = L.update([P(3, 0.4, w=0.1)], 0.8)        # after grace -> new player
        self.assertEqual(r[1]["id"], 3)

    def test_small_people_ignored(self):
        L = PlayerLock()
        r = L.update([P(1, 0.25, h=0.1)], 0)
        self.assertIsNone(r[1])

    def test_one_person_never_two_slots(self):
        L = PlayerLock()
        for t in range(50):
            cx = 0.1 + 0.8 * (t / 49)
            r = L.update([P(1, cx)], t * 0.033)
            self.assertFalse(r[1] is not None and r[2] is not None)


# ▸ ทดสอบเกณฑ์ระยะแบบเกมเก่า
class Distance(unittest.TestCase):
    def D(self, i, cx, d):
        p = P(i, cx); p["dist"] = d; return p

    def test_join_only_within_1m(self):
        L = PlayerLock(join_m=1.0, leave_m=1.5)
        self.assertIsNone(L.update([self.D(1, .25, 1.3)], 0)[1])     # too far to join
        self.assertEqual(L.update([self.D(1, .25, 0.95)], .1)[1]["id"], 1)

    def test_stay_between_1_and_1_5m(self):
        L = PlayerLock(join_m=1.0, leave_m=1.5)
        L.update([self.D(1, .25, 0.9)], 0)
        self.assertEqual(L.update([self.D(1, .25, 1.4)], .1)[1]["id"], 1)  # hysteresis keeps them

    def test_leave_beyond_1_5m(self):
        L = PlayerLock(join_m=1.0, leave_m=1.5)
        L.update([self.D(1, .25, 0.9)], 0)
        self.assertIsNone(L.update([self.D(1, .25, 1.6)], .1)[1])
        self.assertFalse(L.slots[0].locked)                                  # freed immediately
        self.assertIsNone(L.update([self.D(1, .25, 1.2)], .2)[1])           # must come back to 1 m
        self.assertEqual(L.update([self.D(1, .25, 1.0)], .3)[1]["id"], 1)

    def test_far_person_not_readopted(self):
        L = PlayerLock(join_m=1.0, leave_m=1.5)
        L.update([self.D(1, .25, 0.9)], 0)
        self.assertIsNone(L.update([self.D(4, .25, 1.8)], .1)[1])


    def test_defaults_2m_join_2_5m_leave(self):
        L = PlayerLock()
        self.assertIsNone(L.update([self.D(1, .25, 2.2)], 0)[1])       # 2.2 m: too far to join
        self.assertEqual(L.update([self.D(1, .25, 1.9)], .1)[1]["id"], 1)
        self.assertEqual(L.update([self.D(1, .25, 2.4)], .2)[1]["id"], 1)  # still in between 2 and 2.5
        self.assertIsNone(L.update([self.D(1, .25, 2.6)], .3)[1])       # beyond 2.5 m -> out


# ▸ ทดสอบการประมาณระยะจากความกว้างไหล่
class Gestures(unittest.TestCase):
    def kp(self, lw, rw):
        import numpy as np
        k = np.zeros((17, 2)); c = np.ones(17)
        k[5], k[6] = (300, 300), (200, 300)      # mirrored image: left shoulder on the right
        k[11], k[12] = (290, 432), (210, 432)    # torso ~1.3x shoulder width, like a real body
        k[9], k[10] = lw, rw
        return k, c

    def test_distance(self):
        from gestures import estimate_distance
        k, c = self.kp((320, 480), (180, 480))
        d = estimate_distance(k, c, 1280, 640)    # 100 px shoulders at f=640 -> 2.43 m
        self.assertAlmostEqual(d, 640 * .38 / 100, delta=0.05)


# ▸ ทดสอบเกมใหม่: เข้าได้ 2-3 ม., เข้าใกล้แล้วไม่หลุด, ไม่ขโมยคนอีกฝั่ง
class CloseRange(unittest.TestCase):
    """Waste Hunters rules: join only at 2-3 m, then stay locked even very close, until gone from the camera."""

    def L(self):
        return PlayerLock(min_m=2.0, join_m=3.0, lock_until_gone=True, lost_grace=1.5)

    def person(self, i, cx, d, w=0.2, h=0.6, y0=0.3):
        return {"id": i, "box": (cx - w / 2, y0, cx + w / 2, min(1.0, y0 + h)), "dist": d}

    def test_too_close_cannot_join(self):
        L = self.L()
        r = L.update([self.person(1, .25, 1.2)], now=0)
        self.assertIsNone(r[1])

    def test_too_far_cannot_join(self):
        L = self.L()
        self.assertIsNone(L.update([self.person(1, .25, 3.6)], now=0)[1])

    def test_locked_player_walks_very_close_stays(self):
        L = self.L()
        self.assertEqual(L.update([self.person(1, .25, 2.5)], now=0)[1]["id"], 1)
        # walks up to 0.8 m: same id, much bigger box -> still P1
        self.assertEqual(L.update([self.person(1, .25, 0.8, w=.45, h=.9, y0=.1)], now=.1)[1]["id"], 1)

    def test_close_up_new_id_is_readopted(self):
        L = self.L()
        L.update([self.person(1, .25, 2.5)], now=0)
        # tracker lost id 1 and created id 7: big cut-off box, almost no overlap with the old one
        r = L.update([self.person(7, .30, 0.7, w=.5, h=.7, y0=0)], now=.4)
        self.assertEqual(r[1]["id"], 7)

    def test_missing_frames_within_grace_keep_slot(self):
        L = self.L()
        L.update([self.person(1, .25, 2.5)], now=0)
        L.update([], now=1.2)                       # YOLO missed the close-up body for 1.2 s
        self.assertEqual(L.update([self.person(1, .25, 0.9)], now=1.3)[1]["id"], 1)

    def test_gone_frees_slot(self):
        L = self.L()
        L.update([self.person(1, .25, 2.5)], now=0)
        L.update([], now=2.0)                       # out of the camera longer than the grace
        self.assertIsNone(L.update([self.person(1, .25, 1.0)], now=2.1)[1])   # back too close: not re-joined

    def test_readopt_does_not_steal_other_side(self):
        L = self.L()
        L.update([self.person(1, .25, 2.5), self.person(2, .75, 2.5)], now=0)
        r = L.update([self.person(2, .75, 2.5), self.person(9, .80, 0.9)], now=.3)
        self.assertIsNone(r[1])                     # P1's new id would have to jump 0.55 across: refused
        self.assertEqual(r[2]["id"], 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
