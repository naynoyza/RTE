# ไฟล์นี้: [Dev] กล้องปลอมสำหรับเทสเกม Waste Hunters (คนเข้า-ออก, ระยะ, ย่ำเท้า) — รันตอน Unity กด Play
"""Fake tracker for the Waste Hunters scene (no camera needed).
Timeline: nobody -> P1 too close -> P1 joins at 2.5 m, P2 too far -> both in -> both walk in place
(P1 boosts, P2 auto-walk only) while aiming with the right / left hand while swaying left/right -> one leaves -> both leave.
Run while Unity is in Play mode:  python sim_wastehunters.py [speedup]"""
import json, math, socket, sys, time

port = 5065
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
# ▸ ลำดับเหตุการณ์จำลอง (วินาที, ช่วง): ไม่มีคน -> ใกล้ไป -> P1 เข้า -> ครบ 2 คน -> เดิน/เล็ง -> P2 ออก
T = [(0, "nobody"), (4, "p1_close"), (7, "p1_in_p2_far"), (10, "both_in"), (16, "walk"), (60, "p2_leaves"), (66, "nobody")]
speed = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
steps = [0, 0]
acc = [0.0, 0.0]
t0, last = time.time(), time.time()


# ▸ สร้างจุดกระดูกปลอม 17 จุด: เข่ายกสลับตามจังหวะก้าว, แขนข้างที่ใช้ยกขึ้น
def skeleton(cx, t, rate, use):
    """Fake COCO-17 pose (normalized, y down): knees lift with the step rate, the aiming arm is raised."""
    ph = math.sin(t * math.pi * max(rate, 0.01))
    lift = 0.05 * abs(ph) if rate > 0 else 0
    lk, rk = (lift, 0) if ph > 0 else (0, lift)
    P = {0: (cx, .22), 1: (cx - .01, .21), 2: (cx + .01, .21), 3: (cx - .02, .22), 4: (cx + .02, .22),
         5: (cx - .05, .30), 6: (cx + .05, .30), 7: (cx - .07, .42), 8: (cx + .07, .42), 9: (cx - .07, .52), 10: (cx + .07, .52),
         11: (cx - .04, .55), 12: (cx + .04, .55), 13: (cx - .04, .70 - lk), 14: (cx + .04, .70 - rk),
         15: (cx - .04, .85 - lk * 1.5), 16: (cx + .04, .85 - rk * 1.5)}
    if use == 0: P[7], P[9] = (cx - .10, .27), (cx - .12, .16)
    if use == 1: P[8], P[10] = (cx + .10, .27), (cx + .12, .16)
    return [round(v, 3) for i in range(17) for v in (P[i][0], P[i][1], 0.9)]


# ▸ สร้างข้อมูลผู้เล่น 1 คนในรูปแบบเดียวกับ tracker จริง
def player(slot, present, dist=-1.0, approach=-1.0, rate=0.0, bx=0.5, use=-1, x=.5, y=.5):
    return {"slot": slot, "present": present, "hand": use >= 0, "x": x, "y": y, "use": use, "tid": slot if present else -1,
            "dist": dist, "approach": approach, "bothUp": False, "cross": False,
            "stepRate": rate, "steps": steps[slot - 1], "fullBody": present, "bx": bx,
            "kp": skeleton(0.25 if slot == 1 else 0.75, time.time() - t0, rate, use) if present else []}


# ▸ วนส่งข้อมูล 30 ครั้ง/วินาทีไปที่ Unity ตามช่วงเวลาปัจจุบัน
while True:
    now = time.time()
    t = (now - t0) * speed
    dt = (now - last) * speed
    last = now
    phase = [p for s, p in T if t >= s][-1]
    if t > T[-1][0] + 4:
        break
    if phase == "nobody":
        ps = [player(1, False), player(2, False)]
    elif phase == "p1_close":
        ps = [player(1, False, approach=1.4), player(2, False)]
    elif phase == "p1_in_p2_far":
        ps = [player(1, True, 2.5), player(2, False, approach=3.6)]
    elif phase == "both_in":
        ps = [player(1, True, 2.4), player(2, True, 2.7)]
    else:
        rates = [3.0, 0.0]   # P1 steps fast (boost), P2 does not step (auto-walk only)
        for i in range(2):
            acc[i] += rates[i] * dt
            while acc[i] >= 1:
                acc[i] -= 1
                steps[i] += 1
        sway = [0.5 + 0.35 * math.sin(t * 0.7), 0.5 + 0.35 * math.sin(t * 0.5 + 1)]
        p2 = phase != "p2_leaves"
        # aim: sweep the crosshair slowly over the road; P1 right hand, P2 left hand, swapped after 35 s
        swap = t > 35
        aim = [(0.5 + 0.3 * math.sin(t * 0.9), 0.38 + 0.08 * math.sin(t * 1.7)),
               (0.5 + 0.3 * math.sin(t * 0.7 + 2), 0.38 + 0.08 * math.sin(t * 1.3))]
        ps = [player(1, True, 2.4, rate=rates[0], bx=sway[0], use=0 if swap else 1, x=aim[0][0], y=aim[0][1]),
              player(2, p2, 2.7 if p2 else -1, rate=rates[1] if p2 else 0, bx=sway[1], use=(1 if swap else 0) if p2 else -1,
                     x=aim[1][0], y=aim[1][1])]
    msg = {"t": now, "fps": 30.0, "players": ps}
    sock.sendto(json.dumps(msg).encode(), ("127.0.0.1", port))
    time.sleep(1 / 30)
print("done")
