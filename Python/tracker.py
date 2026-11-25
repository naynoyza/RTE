# ไฟล์นี้: [Tracker] โปรแกรมหลัก: กล้อง -> YOLO (ตัว + ขา) + MediaPipe (มือ 2 ข้าง) -> ล็อคผู้เล่น 2 คน -> Unity (UDP)
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
from vision import Body, Hands, HAND_BONES, LS, RS

HERE = os.path.dirname(os.path.abspath(__file__))
CALIB_PATH = os.path.join(HERE, "calibration.json")
AIM0 = {"x0": -2.0, "x1": 2.0, "y0": -2.6, "y1": 0.8}   # aiming hand vs shoulder centre, in shoulder widths
BONES = [(0, 5), (0, 6), (5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11), (6, 12), (11, 12),
         (11, 13), (13, 15), (12, 14), (14, 16)]
COL = {1: (255, 160, 60), 2: (40, 140, 255)}


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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default="orbbec", help="orbbec | auto | <index> | <video file>")
    ap.add_argument("--model", default=os.path.join(HERE, "models", "yolo11n-pose.pt"))
    ap.add_argument("--port", type=int, default=5065)
    ap.add_argument("--min-m", type=float, default=2.0, help="closest distance to join (m)")
    ap.add_argument("--join-m", type=float, default=3.0, help="farthest distance to join (m)")
    ap.add_argument("--hfov", type=float, default=90.0, help="camera horizontal FOV (deg)")
    ap.add_argument("--no-mirror", action="store_true")
    ap.add_argument("--no-show", action="store_true")
    ap.add_argument("--seconds", type=float, default=0, help="auto-exit (tests)")
    args = ap.parse_args()

    body = Body(args.model)
    hands = {1: Hands(), 2: Hands()}
    pool = ThreadPoolExecutor(2)              # both players' hands run at the same time
    cam = Camera(args.source)
    print(f"[tracker] YOLO on {body.name} | MediaPipe hands | camera {cam.name}")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    calib = load_calib()
    lock = PlayerLock(split=calib["split"], lost_grace=1.5, join_m=args.join_m, min_m=args.min_m,
                      leave_m=99.0, lock_until_gone=True)
    lock.require_dist = True
    steps = {s: StepDetector() for s in (1, 2)}
    smooth = {s: OneEuro(1.0, 0.01) for s in (1, 2)}   # body keypoints, per player
    tid = {1: -1, 2: -1}
    jobs, last_hd = {1: None, 2: None}, {1: (None, None), 2: (None, None)}
    use, use_t = {1: -1, 2: -1}, {1: 0.0, 2: 0.0}
    calib_rec = dist_rec = None
    calib_end = dist_end = 0.0
    last_fid, last_frame, t0 = -1, time.monotonic(), time.monotonic()
    fps, n, fps_t = 0.0, 0, time.monotonic()

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

        # ▸ YOLO: everybody (box, 17 body/leg points, id) + distance
        people = body(frame)
        focal = 0.5 * w / np.tan(np.radians(args.hfov) / 2)
        for p in people:
            raw, fb = estimate_distance(p["kps"], p["kc"], w, focal), full_body_distance(p["kps"], p["kc"], focal)
            raw = fb if raw is None else (raw if fb is None else 0.6 * fb + 0.4 * raw)
            p["raw"] = raw
            p["dist"] = None if raw is None else raw * calib.get("dist_scale", 1.0)
        players = lock.update(people, now)

        # ▸ smooth the players' points. Hands (MediaPipe, CPU) run in the background and are never waited for:
        #   body + aim go out at full camera rate, each frame takes the newest finished hand result.
        for s, p in players.items():
            if p is None or tid[s] != p["id"]:
                smooth[s].reset(); hands[s].reset()
                if tid[s] == -1:
                    steps[s].reset()     # new player only (an id switch keeps the step count)
            tid[s] = -1 if p is None else p["id"]
            if p is not None:
                p["kraw"] = p["kps"]                       # step counter wants raw points (smoothing flattens fast steps)
                p["kps"] = smooth[s](p["kps"], now)
                if jobs[s] is None or jobs[s][1].done():
                    if jobs[s]:
                        last_hd[s] = (jobs[s][0], jobs[s][1].result())
                    jobs[s] = (p["id"], pool.submit(hands[s], frame, p, now * 1000.0))
                pid, hd = last_hd[s]
                p["hands"] = hd if pid == p["id"] else {0: None, 1: None}

        used = {sl.track_id for sl in lock.slots if sl.locked}
        approach = {1: None, 2: None}
        for p in people:
            if p["id"] not in used and p["dist"] is not None:
                side = 1 if (p["box"][0] + p["box"][2]) / 2 < lock.split else 2
                approach[side] = min(p["dist"], approach[side] or 99)

        out = []
        for s in (1, 2):
            p = players[s]
            e = {"slot": s, "present": p is not None, "dist": -1.0, "steps": 0, "hand": False, "use": -1,
                 "x": 0.5, "y": 0.5, "approach": round(approach[s], 2) if approach[s] else -1.0,
                 "handL": {"ok": False}, "handR": {"ok": False}}
            if p is not None:
                k, c, hd = p["kps"], p["kc"], p["hands"]
                steps[s](p["kraw"], c, now, h)
                e.update(steps=steps[s].steps, dist=round(p["dist"], 2) if p["dist"] else -1.0,
                         kp=[round(float(v), 3) for i in range(17) for v in (k[i][0] / w, k[i][1] / h, c[i])],
                         handL=hand_json(hd[0], w, h), handR=hand_json(hd[1], w, h))
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
        sock.sendto(json.dumps({"fps": round(fps, 1), "players": out}, separators=(",", ":")).encode(),
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

        # ▸ preview: 960 px wide like before, drawn on the small image (cheap = smooth)
        if not args.no_show:
            f = 960 / w
            vis = cv2.resize(frame, (960, int(h * f)), interpolation=cv2.INTER_AREA)
            sx = int(lock.split * 960)
            cv2.line(vis, (sx, 0), (sx, vis.shape[0]), (200, 200, 200), 1)
            for s, p in players.items():
                if p is None:
                    continue
                k, c = (p["kps"] * f).astype(int), p["kc"]
                for a, b in BONES:
                    if c[a] > 0.3 and c[b] > 0.3:
                        cv2.line(vis, tuple(k[a]), tuple(k[b]), COL[s], 4, cv2.LINE_AA)
                for sd, hc in ((0, (80, 255, 120)), (1, (255, 80, 220))):
                    hd = p["hands"][sd]
                    if hd:
                        q = (hd["px"][:, :2] * f).astype(int)
                        for a, b in HAND_BONES:
                            cv2.line(vis, tuple(q[a]), tuple(q[b]), hc, 2, cv2.LINE_AA)
                        cv2.putText(vis, hd["g"], tuple(q[12] - [20, 12]), cv2.FONT_HERSHEY_SIMPLEX, 0.6, hc, 2)
                x0, y0 = int(p["box"][0] * 960), int(p["box"][1] * vis.shape[0])
                cv2.putText(vis, f"P{s} {p['dist'] or 0:.1f}m aim:{'LRN'[use[s]]} steps:{steps[s].steps}", (x0, max(20, y0 - 8)),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, COL[s], 2)
            free = any(not sl.locked for sl in lock.slots)
            for p in people:
                if free and p["id"] not in used and p["dist"]:
                    x0, y1 = int(p["box"][0] * 960), int(p["box"][3] * vis.shape[0])
                    ok = args.min_m <= p["dist"] <= args.join_m
                    cv2.putText(vis, f"{p['dist']:.2f} m", (x0, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.8,
                                (80, 220, 80) if ok else (60, 60, 255), 2)
            msg = f"{fps:4.1f} fps" + (f"  CALIBRATING {calib_end - now:.1f}s" if calib_rec else "")
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
