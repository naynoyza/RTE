# ไฟล์นี้: [Tracker] โปรแกรมหลัก: กล้อง -> YOLO (กรอบ P1/P2 + ตัว/ขา) + MediaPipe (โครงกระดูก 33 จุด + มือ 2 ข้าง) -> Unity (UDP)
"""Run:  python tracker.py [--source 0 | demo.mp4] [--model models/yolo11s-pose.pt]
Keys: C = calibrate aim (sweep the hand to every corner, 6 s)   D = distance calibration (stand at 1 m)
      R = reset player locks   Q/Esc = quit"""
import argparse, json, os, socket, time, traceback
from concurrent.futures import ThreadPoolExecutor
import numpy as np
import cv2

from camera import Camera, CameraNotConnected
from player_lock import PlayerLock
from filters import OneEuro
from gestures import estimate_distance, full_body_distance, StepDetector
from vision import Body, Hands, Pose, appearance, HAND_BONES, POSE_BONES, LS, RS

HERE = os.path.dirname(os.path.abspath(__file__))
CALIB_PATH = os.path.join(HERE, "calibration.json")
AIM0 = {"x0": -2.0, "x1": 2.0, "y0": -2.6, "y1": 0.8}   # aiming hand vs shoulder centre, in shoulder widths
BONES = [(0, 5), (0, 6), (5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11), (6, 12), (11, 12),
         (11, 13), (13, 15), (12, 14), (14, 16)]
COL = {1: (255, 160, 60), 2: (40, 140, 255)}
PREVIEW_W, PREVIEW_H = 1280, 720          # tracker preview window size
# Joints the games use (MediaPipe Pose numbers): nose, shoulders, elbows, wrists, hips, knees, ankles.
# Dropped (no game reads them): eyes 1-6, ears 7-8, mouth 9-10, pinky/index/thumb 17-22, heels 29-30, toes 31-32.
POSE_KEEP = [0, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28]
KEEP_BONES = [(a, b) for a, b in POSE_BONES if a in POSE_KEEP and b in POSE_KEEP]
# YOLO-17 index <- MediaPipe-33 index (same joint). YOLO eyes/ears 1-4 are not sent (no game uses them).
MP_OF = {0: 0, 5: 11, 6: 12, 7: 13, 8: 14, 9: 15, 10: 16, 11: 23, 12: 24, 13: 25, 14: 26, 15: 27, 16: 28}
ARMS = (0, 5, 6, 7, 8, 9, 10)


def fuse_kp(k, c, ps):
    """17 body points sent as "kp": arms + nose from MediaPipe when it sees them (it keeps tracking an arm
    held straight out at the camera, where YOLO loses the wrist), legs from YOLO (MediaPipe only fills gaps)."""
    k, c = k.copy(), c.copy()
    c[1:5] = 0.0
    if ps is None:
        return k, c
    q, v = ps["px"], ps["vis"]
    for i, m in MP_OF.items():
        if v[m] > 0.5 and (i in ARMS or c[i] < 0.3):
            k[i], c[i] = q[m][:2], max(float(v[m]), 0.5)
    return k, c


def load_calib():
    try:
        with open(CALIB_PATH, encoding="utf-8") as f:
            c = json.load(f)
    except Exception:
        c = {}
    for k in ("1", "2"):
        c.setdefault(k, dict(AIM0))
    c.setdefault("split", 0.5)
    return c


def save_calib(c):
    with open(CALIB_PATH, "w", encoding="utf-8") as f:
        json.dump(c, f, indent=2)


def aim_points(k, c, hd):
    """{0/1: (rx, ry)} hand position vs the shoulder centre (in shoulder widths), by screen side.
    MediaPipe palm centre when seen, YOLO wrist otherwise."""
    if c[LS] < 0.35 or c[RS] < 0.35:
        return {}
    sc, sw = (k[LS] + k[RS]) / 2, max(float(np.linalg.norm(k[LS] - k[RS])), 25.0)
    pts = {}
    ws = sorted([k[i] for i in (9, 10) if c[i] >= 0.35], key=lambda q: q[0])
    if len(ws) == 2:
        pts = {0: ws[0], 1: ws[1]}
    elif ws:
        pts = {0 if ws[0][0] < sc[0] else 1: ws[0]}
    pts.update({s: hd[s]["palm"] for s in (0, 1) if hd[s]})
    return {s: tuple((q - sc) / sw) for s, q in pts.items()}


def hand_json(hd, w, h):
    if hd is None:
        return {"ok": False}
    return {"ok": True, "g": hd["g"], "f": hd["f"], "pinch": round(hd["pinch"], 3),
            "px": round(float(hd["palm"][0] / w), 4), "py": round(float(hd["palm"][1] / h), 4),
            "lm": [round(float(v), 4) for q in hd["px"] for v in (q[0] / w, q[1] / h, q[2] / w)]}


def _dashed_rect(img, p0, p1, col, th=2, dash=12):
    (x0, y0), (x1, y1) = p0, p1
    for a, b in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
        n = max(1, int(np.hypot(b[0] - a[0], b[1] - a[1]) / dash))
        for i in range(0, n, 2):
            q0 = (int(a[0] + (b[0] - a[0]) * i / n), int(a[1] + (b[1] - a[1]) * i / n))
            q1 = (int(a[0] + (b[0] - a[0]) * min(n, i + 1) / n), int(a[1] + (b[1] - a[1]) * min(n, i + 1) / n))
            cv2.line(img, q0, q1, col, th, cv2.LINE_AA)


def draw_skeleton(vis, p, f, col):
    """The game's 13 MediaPipe joints when available (YOLO's otherwise) + hands if --hands is on."""
    ps = p.get("pose")
    if ps is not None:
        q, v = (ps["px"][:, :2] * f).astype(int), ps["vis"]
        for a, b in KEEP_BONES:
            if v[a] > 0.5 and v[b] > 0.5:
                cv2.line(vis, tuple(q[a]), tuple(q[b]), col, 3, cv2.LINE_AA)
        for i in POSE_KEEP:
            if v[i] > 0.5:
                cv2.circle(vis, tuple(q[i]), 5, (255, 255, 255), -1, cv2.LINE_AA)
                cv2.circle(vis, tuple(q[i]), 5, col, 2, cv2.LINE_AA)
    else:
        k, c = (p["kps"] * f).astype(int), p["kc"]
        for a, b in BONES:
            if c[a] > 0.3 and c[b] > 0.3:
                cv2.line(vis, tuple(k[a]), tuple(k[b]), col, 3, cv2.LINE_AA)
    for sd, hc in ((0, (80, 255, 120)), (1, (255, 80, 220))):
        hd = p["hands"][sd]
        if hd:
            q = (hd["px"][:, :2] * f).astype(int)
            for a, b in HAND_BONES:
                cv2.line(vis, tuple(q[a]), tuple(q[b]), hc, 2, cv2.LINE_AA)
            cv2.putText(vis, hd["g"], tuple(q[12] - [20, 12]), cv2.FONT_HERSHEY_SIMPLEX, 0.6, hc, 2)


def draw_preview(frame, players, people, lock, rng, dsm, use, steps, focal, args, now):
    """Preview: the two stand zones (scan), P1 / P2 boxes, full skeletons. Nobody else is drawn."""
    h, w = frame.shape[:2]
    f = np.array([PREVIEW_W / w, PREVIEW_H / h])          # x / y scale camera px -> preview px
    vis = cv2.resize(frame, (PREVIEW_W, PREVIEW_H), interpolation=cv2.INTER_AREA if w > PREVIEW_W else cv2.INTER_LINEAR)
    H = PREVIEW_H
    by_id = {p["id"]: p for p in people}
    dmid = (args.join_near + args.join_m) / 2

    # ▸ stand zones (like the rings of the character-select screen), shown at the middle of the 2.5-3 m band
    for sl in lock.slots:
        col = COL[sl.index]
        zc = lock.zone_centre(sl.index)
        xa = int((w / 2 + (zc - lock.zone_r) * focal / dmid) * f[0])
        xb = int((w / 2 + (zc + lock.zone_r) * focal / dmid) * f[0])
        if not sl.locked:
            ya, xa2, xb2 = int(0.08 * H), max(0, xa), min(PREVIEW_W, xb)
            if xb2 > xa2:
                roi = vis[ya:H - 4, xa2:xb2]
                cv2.addWeighted(roi, 0.88, np.full_like(roi, col), 0.12, 0, roi)
            _dashed_rect(vis, (xa, int(0.08 * H)), (xb, H - 4), col, 2)
            cv2.putText(vis, f"P{sl.index} ZONE {args.join_near:.1f}-{args.join_m:.1f}m", (xa + 4, int(0.08 * H) - 6),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.55, col, 2)
        # scanning: sweep line over the person standing in the zone + progress
        cand = by_id.get(sl.scan_id) if not sl.locked else None
        if cand is not None:
            x0, y0, x1, y1 = (np.array(cand["box"]) * [PREVIEW_W, H, PREVIEW_W, H]).astype(int)
            _dashed_rect(vis, (x0, y0), (x1, y1), col, 2)
            sy = int(y0 + (y1 - y0) * (0.5 + 0.5 * np.sin(now * 6)))
            cv2.line(vis, (x0, sy), (x1, sy), (255, 255, 255), 2, cv2.LINE_AA)
            cv2.rectangle(vis, (x0, y1 + 4), (x1, y1 + 12), (60, 60, 60), -1)
            cv2.rectangle(vis, (x0, y1 + 4), (x0 + int((x1 - x0) * sl.scan), y1 + 12), col, -1)
            cv2.putText(vis, f"SCAN P{sl.index} {int(sl.scan * 100)}%  {cand['dist']:.2f}m", (x0, max(20, y0 - 8)),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, col, 2)

    # ▸ free slot: guide people standing outside the band (distance in red)
    if any(not sl.locked for sl in lock.slots):
        used = {sl.track_id for sl in lock.slots if sl.locked} | {sl.scan_id for sl in lock.slots}
        for p in people:
            if p["id"] not in used and p["dist"]:
                x0, y1 = int(p["box"][0] * PREVIEW_W), int(p["box"][3] * H)
                cv2.putText(vis, f"{p['dist']:.2f} m", (x0, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (60, 60, 255), 2)

    # ▸ the two players: own box + skeleton (only inside 2-3 m)
    for s, p in players.items():
        sl = lock.slots[s - 1]
        if p is None:
            if sl.locked and sl.box is not None:     # covered by someone / missed: slot held on the last spot
                x0, y0, x1, y1 = (np.array(sl.box) * [PREVIEW_W, H, PREVIEW_W, H]).astype(int)
                _dashed_rect(vis, (x0, y0), (x1, y1), COL[s], 2)
                cv2.putText(vis, f"P{s} HOLD (covered)", (x0 + 3, max(18, y0 - 7)), cv2.FONT_HERSHEY_SIMPLEX,
                            0.55, COL[s], 2)
            continue
        ok = rng[s] == "ok"
        col = COL[s] if ok else (150, 150, 150)
        x0, y0, x1, y1 = (np.array(p["box"]) * [PREVIEW_W, H, PREVIEW_W, H]).astype(int)
        if ok:
            cv2.rectangle(vis, (x0, y0), (x1, y1), col, 3)
            draw_skeleton(vis, p, f, COL[s])
            label = f"P{s} {dsm[s] or 0:.2f}m aim:{'LRN'[use[s]]} steps:{steps[s].steps}"
        else:
            _dashed_rect(vis, (x0, y0), (x1, y1), col, 2)
            label = f"P{s} {dsm[s] or 0:.2f}m " + ("TOO CLOSE <%.1fm" % args.min_m if rng[s] == "near"
                                                    else "TOO FAR >%.1fm" % args.track_m)
        cv2.rectangle(vis, (x0, max(0, y0 - 26)), (x0 + 11 * len(label), y0), col, -1)
        cv2.putText(vis, label, (x0 + 3, max(18, y0 - 7)), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2)
    return vis


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default="orbbec", help="orbbec | auto | <index> | <video file>")
    ap.add_argument("--model", default=os.path.join(HERE, "models", "yolo11n-pose.pt"))
    ap.add_argument("--port", type=int, default=5065)
    ap.add_argument("--min-m", type=float, default=2.0, help="closer than this: no skeleton (m)")
    ap.add_argument("--join-near", type=float, default=2.5, help="scan zone: nearest distance (m)")
    ap.add_argument("--join-m", type=float, default=3.0, help="scan zone: farthest distance (m)")
    ap.add_argument("--track-m", type=float, default=3.0, help="farther than this: no skeleton (m)")
    ap.add_argument("--zone-x", type=float, default=1.0, help="each stand zone's distance from the centre line (m)")
    ap.add_argument("--zone-r", type=float, default=0.5, help="stand zone half-width (m)")
    ap.add_argument("--scan-s", type=float, default=1.0, help="stand still this long in the zone to lock (s)")
    ap.add_argument("--lost-grace", type=float, default=3.0, help="slot kept for the same player this long after leaving (s)")
    ap.add_argument("--hold-max", type=float, default=15.0, help="slot held this long while someone covers the player (s)")
    ap.add_argument("--pose-hz", type=float, default=20.0, help="MediaPipe skeleton updates per second per player")
    ap.add_argument("--hands", action="store_true", help="also run MediaPipe Hands (21 points/hand; no game uses it)")
    ap.add_argument("--hfov", type=float, default=90.0, help="camera horizontal FOV (deg)")
    ap.add_argument("--no-mirror", action="store_true")
    ap.add_argument("--no-show", action="store_true")
    ap.add_argument("--seconds", type=float, default=0, help="auto-exit (tests)")
    args = ap.parse_args()

    cv2.setNumThreads(2)                      # leave CPU cores for MediaPipe: steadier frame times
    body = Body(args.model)
    hands = {1: Hands(), 2: Hands()} if args.hands else None
    poses = {1: Pose(), 2: Pose()}            # MediaPipe Pose per player
    pool = ThreadPoolExecutor(4 if args.hands else 2)   # both players run at the same time
    cam = Camera(args.source)
    print(f"[tracker] YOLO on {body.name} | MediaPipe pose{' + hands' if args.hands else ''} | camera {cam.name}")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    calib = load_calib()
    lock = PlayerLock(split=calib["split"], lost_grace=args.lost_grace, join_m=args.join_m, min_m=args.min_m,
                      join_near=args.join_near, zone_x=args.zone_x, zone_r=args.zone_r, scan_s=args.scan_s,
                      leave_m=99.0, lock_until_gone=True, hold_max=args.hold_max)
    lock.require_dist = True
    steps = {s: StepDetector() for s in (1, 2)}
    smooth = {s: OneEuro(1.0, 0.01) for s in (1, 2)}   # body keypoints, per player
    tid, lock_t = {1: -1, 2: -1}, {1: -1.0, 2: -1.0}
    pose_t = {1: 0.0, 2: 0.0}
    sig_cache = {}                            # id -> (time, clothes signature)
    no_hands = {0: None, 1: None}
    jobs, last_hd = {1: None, 2: None}, {1: (None, None), 2: (None, None)}
    pjobs, last_pose = {1: None, 2: None}, {1: (None, None), 2: (None, None)}
    dsm, rng = {1: None, 2: None}, {1: "?", 2: "?"}   # smoothed distance + range state per player
    use, use_t = {1: -1, 2: -1}, {1: 0.0, 2: 0.0}
    calib_rec = dist_rec = None
    calib_end = dist_end = 0.0
    last_fid, last_frame, t0 = -1, time.monotonic(), time.monotonic()
    fps, n, fps_t = 0.0, 0, time.monotonic()
    proc_ms = 0.0                             # smoothed processing time per frame (ms)

    while True:
        fid, frame = cam.read()
        now = time.monotonic()
        if frame is None or fid == last_fid:        # no new frame yet (don't sleep long: lowest latency)
            if now - last_frame > 4.0:
                print("[camera] no frames for 4 s -> reopening")
                cam.close()
                cam, last_frame = Camera(args.source), now
            cv2.waitKey(1) if not args.no_show else time.sleep(0.002)
            continue
        last_fid, last_frame = fid, now
        if not args.no_mirror:
            frame = cv2.flip(frame, 1)
        h, w = frame.shape[:2]

        # ▸ YOLO: everybody (box, 17 body/leg points, id) + distance, side position (m) and clothes colour
        people = body(frame)
        focal = 0.5 * w / np.tan(np.radians(args.hfov) / 2)
        for p in people:
            raw, fb = estimate_distance(p["kps"], p["kc"], w, focal), full_body_distance(p["kps"], p["kc"], focal)
            raw = fb if raw is None else (raw if fb is None else 0.6 * fb + 0.4 * raw)
            p["raw"] = raw
            p["dist"] = None if raw is None else raw * calib.get("dist_scale", 1.0)
            cx = (p["box"][0] + p["box"][2]) / 2 * w
            p["lx"] = None if p["dist"] is None else (cx - w / 2) * p["dist"] / focal   # m from the centre line
        # clothes colour: fresh every frame for the two players' ids (catches a stranger taking the id),
        # every 0.4 s for everybody else (cheaper = steadier fps)
        mine = {sl.track_id for sl in lock.slots} | {sl.scan_id for sl in lock.slots}
        for p in people:
            hit = sig_cache.get(p["id"])
            if p["id"] in mine or hit is None or now - hit[0] > 0.4:
                hit = sig_cache[p["id"]] = (now, appearance(frame, p))
            p["sig"] = hit[1]
        if len(sig_cache) > 64:
            alive = {p["id"] for p in people}
            sig_cache = {i: v for i, v in sig_cache.items() if i in alive}
        players = lock.update(people, now)      # only P1 / P2: a 3rd person is never counted

        # ▸ range per player: skeleton only between min_m and track_m (0.1 m hysteresis: no flicker)
        for s, p in players.items():
            if p is None:
                if not lock.slots[s - 1].locked:
                    dsm[s], rng[s] = None, "?"
                continue
            if p["dist"] is not None:
                dsm[s] = p["dist"] if dsm[s] is None else dsm[s] + 0.25 * (p["dist"] - dsm[s])
            if dsm[s] is None:
                continue                       # distance unknown this moment: keep the last state
            hy = 0.1 if rng[s] == "ok" else 0.0     # leaving needs 0.1 m past the limit, coming back is exact
            rng[s] = "near" if dsm[s] < args.min_m - hy else "far" if dsm[s] > args.track_m + hy else "ok"

        # ▸ smooth the players' points. MediaPipe (CPU) runs in the background at --pose-hz and is never
        #   waited for: body + aim go out at full camera rate, each frame takes the newest finished skeleton.
        for s, p in players.items():
            sl = lock.slots[s - 1]
            if sl.locked and sl.locked_at != lock_t[s]:
                steps[s].reset(); lock_t[s] = sl.locked_at      # new player only (covered / id switch keeps steps)
            if p is None or tid[s] != p["id"] or rng[s] != "ok":
                smooth[s].reset(); poses[s].reset()
                if hands:
                    hands[s].reset()
            tid[s] = -1 if p is None else p["id"]
            if p is None:
                continue
            p["kraw"] = p["kps"]                           # step counter wants raw points (smoothing flattens fast steps)
            p["kps"] = smooth[s](p["kps"], now)
            p["hands"], p["pose"] = no_hands, None
            if rng[s] != "ok":                             # closer than 2 m / beyond 3 m: no skeleton
                last_hd[s] = last_pose[s] = (None, None)
                continue
            if hands and (jobs[s] is None or jobs[s][1].done()):
                if jobs[s]:
                    last_hd[s] = (jobs[s][0], jobs[s][1].result())
                jobs[s] = (p["id"], pool.submit(hands[s], frame, p, now * 1000.0))
            if pjobs[s] is not None and pjobs[s][1].done():
                last_pose[s], pjobs[s] = (pjobs[s][0], pjobs[s][1].result()), None
            if pjobs[s] is None and now - pose_t[s] >= 1.0 / args.pose_hz:   # paced: no CPU spikes
                pose_t[s] = now
                others = [q["box"] for q in people if q["id"] != p["id"]]
                pjobs[s] = (p["id"], pool.submit(poses[s], frame, p, others, now * 1000.0))
            if last_hd[s][0] == p["id"]:
                p["hands"] = last_hd[s][1]
            if last_pose[s][0] == p["id"]:
                p["pose"] = last_pose[s][1]

        used = {sl.track_id for sl in lock.slots if sl.locked}
        approach = {1: None, 2: None}
        for p in people:       # nearest not-yet-player in each half (Unity says "too close / too far")
            if p["id"] not in used and p["dist"] is not None:
                side = 1 if p["lx"] < 0 else 2
                approach[side] = min(p["dist"], approach[side] or 99)

        out = []
        for s in (1, 2):
            p = players[s]
            held = p is None and lock.slots[s - 1].locked      # covered by someone / missed: still the player
            e = {"slot": s, "present": p is not None or held, "track": False,
                 "range": "hidden" if held else rng[s], "dist": -1.0,
                 "steps": steps[s].steps if p is not None or held else 0, "hand": False, "use": -1, "x": 0.5, "y": 0.5,
                 "scan": round(lock.slots[s - 1].scan, 2), "approach": round(approach[s], 2) if approach[s] else -1.0,
                 "handL": {"ok": False}, "handR": {"ok": False}}
            if (p is not None or held) and dsm[s]:
                e["dist"] = round(dsm[s], 2)
            if p is not None and rng[s] == "ok":
                steps[s](p["kraw"], p["kc"], now, h)
                k, c = fuse_kp(p["kps"], p["kc"], p["pose"])
                hd = p["hands"]
                e.update(track=True, steps=steps[s].steps,
                         kp=[round(float(v), 3) for i in range(17) for v in (k[i][0] / w, k[i][1] / h, c[i])],
                         handL=hand_json(hd[0], w, h), handR=hand_json(hd[1], w, h))
                if p["pose"] is not None:   # 33 slots [x, y, z, visibility]; only POSE_KEEP filled, the rest 0
                    q, v = p["pose"]["px"], p["pose"]["vis"]
                    pz = [0] * 132
                    for i in POSE_KEEP:
                        pz[i * 4:i * 4 + 4] = [round(float(q[i][0] / w), 3), round(float(q[i][1] / h), 3),
                                               round(float(q[i][2] / w), 3), round(float(v[i]), 2)]
                    e["pose"] = pz
                # aiming hand = the raised one (wrist/palm above 1.5 shoulder widths under the shoulders)
                aim = aim_points(k, c, hd)
                up = [sd for sd, (_, ry) in aim.items() if ry < 1.5]
                if use[s] in up:
                    use_t[s] = now
                elif up:
                    use[s], use_t[s] = min(up, key=lambda sd: aim[sd][1]), now
                elif now - use_t[s] > 0.4:
                    use[s] = -1
                e["use"] = use[s]
                if use[s] in aim:
                    rx, ry = aim[use[s]]
                    if calib_rec:
                        r = calib_rec[s]
                        r[:] = [min(r[0], rx), max(r[1], rx), min(r[2], ry), max(r[3], ry)]
                    cal = calib[str(s)]
                    e.update(hand=True, x=round(float(np.clip((rx - cal["x0"]) / (cal["x1"] - cal["x0"]), 0, 1)), 4),
                             y=round(float(np.clip(1 - (ry - cal["y0"]) / (cal["y1"] - cal["y0"]), 0, 1)), 4))
            out.append(e)

        n += 1
        if now - fps_t >= 1.0:
            fps, n, fps_t = n / (now - fps_t), 0, now
        proc_ms += 0.1 * ((time.monotonic() - now) * 1000 - proc_ms)
        sock.sendto(json.dumps({"fps": round(fps, 1), "ms": round(proc_ms, 1), "players": out}, separators=(",", ":")).encode(),
                    ("127.0.0.1", args.port))

        # ▸ calibrations finish
        if calib_rec and now > calib_end:
            for s in (1, 2):
                x0, x1, y0, y1 = calib_rec[s]
                if x1 - x0 > 0.8 and y1 - y0 > 0.8:
                    mx, my = (x1 - x0) * 0.08, (y1 - y0) * 0.08
                    calib[str(s)] = {"x0": x0 + mx, "x1": x1 - mx, "y0": y0 + my, "y1": y1 - my}
            save_calib(calib); calib_rec = None
            print("[calib] aim saved")
        if dist_rec is not None:
            dist_rec += [p["raw"] for p in people if p["raw"]]
            if now > dist_end:
                if len(dist_rec) > 10:
                    calib["dist_scale"] = round(1.0 / float(np.median(dist_rec)), 4)
                    save_calib(calib); lock.reset()
                    print(f"[calib] dist_scale = {calib['dist_scale']}")
                dist_rec = None

        # ▸ preview window 1280 x 720
        if not args.no_show:
            vis = draw_preview(frame, players, people, lock, rng, dsm, use, steps, focal, args, now)
            msg = f"{fps:4.1f} fps  {proc_ms:4.1f} ms" + (f"  CALIBRATING {calib_end - now:.1f}s" if calib_rec else "")
            cv2.putText(vis, msg, (10, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
            cv2.imshow("RTE tracker", vis)
            key = cv2.waitKey(1) & 0xFF
            if key in (27, ord("q")):
                break
            if key == ord("c"):
                calib_rec, calib_end = {1: [9, -9, 9, -9], 2: [9, -9, 9, -9]}, now + 6.0
            if key == ord("d"):
                dist_rec, dist_end = [], now + 2.0
            if key == ord("r"):
                lock.reset()
        if args.seconds and now - t0 > args.seconds:
            break

    cam.close()
    cv2.destroyAllWindows()


# ▸ ถ้าพัง (ยังไม่เสียบกล้อง ฯลฯ) รอ 3 วินาทีแล้วเริ่มใหม่ — ปิดจริงด้วย Q / Esc / Ctrl+C
if __name__ == "__main__":
    while True:
        try:
            main()
            break
        except KeyboardInterrupt:
            break
        except CameraNotConnected as e:
            print(f"[camera] {e}")
        except Exception:
            traceback.print_exc()
            print("[tracker] restarting in 3 s")
            cv2.destroyAllWindows()
        time.sleep(3)
