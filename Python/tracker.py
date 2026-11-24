# ไฟล์นี้: [Tracker] โปรแกรมหลัก: อ่านกล้อง -> YOLO (กรอบคน + ล็อคผู้เล่น 2 คน) -> MediaPipe (โครงกระดูก)
#   -> ส่งข้อมูลให้ Unity ทาง UDP   รับคนเฉพาะระยะ 2–3 ม., ล็อคจนกว่าจะออกจากกล้อง, นับการย่ำเท้า, หน้าต่างพรีวิว
"""2-player tracker -> Unity (UDP JSON, 30 fps).
YOLO finds people (boxes) and locks P1/P2; MediaPipe Pose finds each player's skeleton inside their box.

Run:  python tracker.py                 (Orbbec Gemini via pyorbbecsdk, falls back to UVC)
      python tracker.py --source 1      (OpenCV camera index)
      python tracker.py --source demo.mp4
Keys in the preview window:
      C = calibrate (each player sweeps their hand to all corners for 6 s)
      R = reset player locks     S = swap side split to center     Q/Esc = quit
"""
import argparse, json, socket, threading, time, os, sys
import numpy as np
import cv2

from player_lock import PlayerLock
from filters import OneEuro
from gestures import estimate_distance, full_body_distance, StepDetector
from skeleton_mp import MpSkeleton

HERE = os.path.dirname(os.path.abspath(__file__))
# ▸ ไฟล์เก็บค่าคาลิเบรต (ขอบเขตมือ, ตัวคูณระยะ) และค่าเริ่มต้น
CALIB_PATH = os.path.join(HERE, "calibration.json")
DEFAULT_CALIB = {
    "split": 0.5,
    # hand position relative to the shoulder centre, in shoulder widths
    "1": {"x0": -2.0, "x1": 2.0, "y0": -2.6, "y1": 0.8},
    "2": {"x0": -2.0, "x1": 2.0, "y0": -2.6, "y1": 0.8},
}
KP_LS, KP_RS, KP_LW, KP_RW = 5, 6, 9, 10
# COCO-17 bones drawn for the two players (head, arms, torso, legs)
# ▸ คู่จุดกระดูกที่วาดในหน้าต่างพรีวิว
BONES = [(0, 5), (0, 6), (5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11), (6, 12), (11, 12),
         (11, 13), (13, 15), (12, 14), (14, 16)]


# ▸ วาดเส้นกระดูกและจุดข้อต่อของผู้เล่นลงบนภาพพรีวิว (เฉพาะจุดที่มั่นใจพอ)
def draw_skeleton(img, kps, kc, col, min_conf=0.3):
    for a, b in BONES:
        if kc[a] > min_conf and kc[b] > min_conf:
            cv2.line(img, tuple(kps[a].astype(int)), tuple(kps[b].astype(int)), col, 5, cv2.LINE_AA)
    for i in range(17):
        if kc[i] > min_conf:
            cv2.circle(img, tuple(kps[i].astype(int)), 7, (255, 255, 255), -1, cv2.LINE_AA)
            cv2.circle(img, tuple(kps[i].astype(int)), 7, col, 2, cv2.LINE_AA)


# ---------------------------------------------------------------- camera
# ▸ อ่านภาพจากกล้องใน thread แยก เก็บเฉพาะภาพล่าสุด (ไม่สะสมคิว = ไม่หน่วง)
#   ลองเปิดด้วย pyorbbecsdk ก่อน ถ้าไม่ได้ใช้ OpenCV (UVC/เลขกล้อง/ไฟล์วิดีโอ)
class CameraNotConnected(RuntimeError):
    pass


class Camera:
    """Grabs frames on a thread and always hands out the newest one."""

    def __init__(self, source, width=1280, height=720, fps=30):
        self.frame, self.frame_id, self.ok = None, 0, True
        self.lock = threading.Lock()
        self.name = ""
        self._ob = None
        self._cap = None
        if source == "orbbec":
            try:
                self._open_orbbec(width, height, fps)
            except CameraNotConnected:
                raise   # don't fall back to another webcam: wait for the Orbbec (main loop / supervisor retries)
            except Exception as e:
                print(f"[camera] pyorbbecsdk unavailable ({e}); using Orbbec UVC")
                source = "orbbec-uvc"
        if self._ob is None:
            self._open_cv(source, width, height, fps)
        threading.Thread(target=self._loop, daemon=True).start()

    # ▸ เปิดกล้อง Orbbec ผ่าน SDK เลือกรูปแบบภาพสี (RGB/MJPG/YUYV) ที่ความละเอียดและ fps ที่ต้องการ
    def _open_orbbec(self, w, h, fps):
        from pyorbbecsdk import Pipeline, Config, OBSensorType, OBFormat, Context
        # with no camera plugged in, Pipeline() kills the whole Python process inside the SDK,
        # so check the device list first and raise a normal error instead
        if Context().query_devices().get_count() == 0:
            raise CameraNotConnected("Orbbec camera not connected - plug it in (retrying)")
        pipe, cfg = Pipeline(), Config()
        plist = pipe.get_stream_profile_list(OBSensorType.COLOR_SENSOR)
        prof = None
        for fmt in (OBFormat.RGB, OBFormat.MJPG, OBFormat.YUYV):
            try:
                prof = plist.get_video_stream_profile(w, 0, fmt, fps)
                break
            except Exception:
                continue
        if prof is None:
            prof = plist.get_default_video_stream_profile()
        cfg.enable_stream(prof)
        pipe.start(cfg)
        self._ob, self._obfmt = pipe, OBFormat
        self.name = f"Orbbec {prof.get_width()}x{prof.get_height()}@{prof.get_fps()} {prof.get_format()}"

    # ▸ เปิดกล้องผ่าน OpenCV: ค้นหากล้องชื่อ Orbbec RGB หรือไล่เลข 0-3 หรือเปิดไฟล์วิดีโอ
    def _open_cv(self, source, w, h, fps):
        if source == "orbbec-uvc":
            # Gemini 2 L colour stream is a normal UVC camera: find its DirectShow index by name
            try:
                from pygrabber.dshow_graph import FilterGraph
                names = FilterGraph().get_input_devices()
                hit = [i for i, n in enumerate(names) if "orbbec" in n.lower() and "rgb" in n.lower()]
                source = str(hit[0]) if hit else "auto"
                print(f"[camera] devices: {names} -> using {source}")
            except Exception as e:
                print(f"[camera] device lookup failed ({e})")
                source = "auto"
        if source == "auto":
            idxs = range(0, 4)
        elif str(source).isdigit():
            idxs = [int(source)]
        else:
            idxs = None
        if idxs is None:
            self._cap = cv2.VideoCapture(source)
        else:
            for i in idxs:
                cap = cv2.VideoCapture(i, cv2.CAP_DSHOW)
                cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
                cap.set(cv2.CAP_PROP_FRAME_WIDTH, w)
                cap.set(cv2.CAP_PROP_FRAME_HEIGHT, h)
                cap.set(cv2.CAP_PROP_FPS, fps)
                cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
                if cap.isOpened() and cap.read()[0]:
                    self._cap = cap
                    break
                cap.release()
        if self._cap is None or not self._cap.isOpened():
            raise RuntimeError(f"cannot open camera source {source}")
        self.name = f"OpenCV {source} {int(self._cap.get(3))}x{int(self._cap.get(4))}"

    # ▸ แปลงเฟรมจาก Orbbec SDK เป็นภาพ BGR ของ OpenCV ตามรูปแบบที่ได้มา
    def _decode_orbbec(self, f):
        data = np.asanyarray(f.get_data())
        fmt, w, h = f.get_format(), f.get_width(), f.get_height()
        F = self._obfmt
        if fmt == F.RGB:
            return cv2.cvtColor(data.reshape(h, w, 3), cv2.COLOR_RGB2BGR)
        if fmt == F.BGR:
            return data.reshape(h, w, 3)
        if fmt == F.MJPG:
            return cv2.imdecode(data, cv2.IMREAD_COLOR)
        if fmt == F.YUYV:
            return cv2.cvtColor(data.reshape(h, w, 2), cv2.COLOR_YUV2BGR_YUYV)
        return None

    # ▸ (thread กล้อง) วนอ่านเฟรมใหม่ เก็บไว้พร้อมเลขเฟรม, ไฟล์วิดีโอเล่นวนที่ 30 fps
    def _loop(self):
        is_file = self._cap is not None and self._cap.get(cv2.CAP_PROP_FRAME_COUNT) > 0
        while self.ok:
            img = None
            if self._ob is not None:
                fs = self._ob.wait_for_frames(100)
                c = fs.get_color_frame() if fs else None
                if c is not None:
                    img = self._decode_orbbec(c)
            else:
                ok, img = self._cap.read()
                if not ok:
                    if is_file:
                        self._cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                        continue
                    time.sleep(0.01)
                    continue
                if is_file:
                    time.sleep(1 / 30)
            if img is not None:
                with self.lock:
                    self.frame, self.frame_id = img, self.frame_id + 1

    # ▸ คืนเลขเฟรมและภาพล่าสุด
    def read(self):
        with self.lock:
            return self.frame_id, self.frame

    # ▸ หยุด thread และปิดกล้อง
    def close(self):
        self.ok = False
        time.sleep(0.15)
        if self._ob is not None:
            self._ob.stop()
        if self._cap is not None:
            self._cap.release()


# ---------------------------------------------------------------- hand mapping
# ▸ อ่าน calibration.json (ถ้าไม่มีใช้ค่าเริ่มต้น)
def load_calib():
    try:
        with open(CALIB_PATH, "r", encoding="utf-8") as f:
            c = json.load(f)
        for k in ("1", "2"):
            c.setdefault(k, DEFAULT_CALIB[k])
        c.setdefault("split", 0.5)
        return c
    except Exception:
        return json.loads(json.dumps(DEFAULT_CALIB))


# ▸ บันทึกค่าคาลิเบรตลง calibration.json
def save_calib(c):
    with open(CALIB_PATH, "w", encoding="utf-8") as f:
        json.dump(c, f, indent=2)


# ▸ ดึงข้อมือทั้ง 2 ข้างจากจุดกระดูก แยกซ้าย/ขวาตามตำแหน่งบนภาพ
#   คืนตำแหน่งเทียบไหล่ + บอกว่ามือนั้นยกขึ้นเล็งอยู่ไหม
def both_hands(kps, conf, min_conf=0.35, raised_below=1.5):
    """Both wrists relative to the shoulder centre (in shoulder widths), split by SCREEN side, not by YOLO's
    left/right label (the preview is mirrored, so screen-left = the player's own left hand).
    Returns {0: left, 1: right} -> (rx, ry, wrist_px, raised) or None. raised = hand lifted to aim
    (wrist higher than raised_below shoulder widths under the shoulder line; a hanging arm is ~2.3)."""
    ls, rs = kps[KP_LS], kps[KP_RS]
    if conf[KP_LS] < min_conf or conf[KP_RS] < min_conf:
        return {0: None, 1: None}
    sc = (ls + rs) / 2
    sw = max(np.linalg.norm(ls - rs), 25.0)
    out = {0: None, 1: None}
    ws = [w for w in (KP_LW, KP_RW) if conf[w] >= min_conf]
    for w in ws:
        rel = (kps[w] - sc) / sw
        side = 0 if kps[w][0] < sc[0] else 1
        if len(ws) == 2:  # both visible: the one further left is the left hand even if it crossed the middle
            other = KP_RW if w == KP_LW else KP_LW
            side = 0 if kps[w][0] < kps[other][0] else 1
        out[side] = (float(rel[0]), float(rel[1]), kps[w], bool(rel[1] < raised_below))
    return out


# ▸ แปลงตำแหน่งมือเทียบไหล่ -> พิกัดจอ 0..1 ตามขอบเขตที่คาลิเบรตไว้
def map_to_screen(rx, ry, cal):
    x = (rx - cal["x0"]) / max(1e-3, cal["x1"] - cal["x0"])
    y = 1.0 - (ry - cal["y0"]) / max(1e-3, cal["y1"] - cal["y0"])  # 1 = top
    return float(np.clip(x, 0, 1)), float(np.clip(y, 0, 1))


# ---------------------------------------------------------------- main
# ▸ โปรแกรมหลัก: อ่านค่า argument, โหลดโมเดล YOLO, เปิดกล้อง, เปิด socket แล้ววนทำงานทีละเฟรม
def main():
    # ▸ ค่าตั้งที่ปรับได้จากบรรทัดคำสั่ง (กล้อง, พอร์ต, ระยะเข้าเล่น 2-3 ม., ความไวการนับก้าว ฯลฯ)
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default="orbbec", help="orbbec | auto | <index> | <video file>")
    ap.add_argument("--model", default=os.path.join(HERE, "models", "yolov8n-pose.onnx"))
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5065)
    ap.add_argument("--fps", type=float, default=30.0)
    ap.add_argument("--imgsz", type=int, default=640)
    ap.add_argument("--no-mirror", action="store_true")
    ap.add_argument("--no-show", action="store_true")
    ap.add_argument("--join-m", type=float, default=3.0, help="max distance to be picked up (m)")
    ap.add_argument("--min-m", type=float, default=2.0, help="people closer than this are not picked up (m)")
    ap.add_argument("--leave-m", type=float, default=99.0, help="only used with --drop-far")
    ap.add_argument("--drop-far", action="store_true", help="drop locked players beyond --leave-m (default: keep the lock until they leave the camera)")
    ap.add_argument("--step-thresh", type=float, default=0.10, help="knee lift (x torso length) that counts as a step")
    ap.add_argument("--hfov", type=float, default=90.0, help="colour camera horizontal FOV (deg)")
    ap.add_argument("--seconds", type=float, default=0, help="auto-exit (for tests)")
    ap.add_argument("--conf", type=float, default=0.30, help="YOLO person confidence (lower keeps close-up / partly cut-off bodies)")
    ap.add_argument("--mp-complexity", type=int, default=0, choices=(0, 1, 2), help="MediaPipe Pose model: 0 lite (fast, default), 1 full (more accurate, ~2x slower), 2 heavy")
    ap.add_argument("--lost-grace", type=float, default=1.5, help="seconds a locked player may be unseen before the slot is freed")
    ap.add_argument("--stall-s", type=float, default=4.0, help="reopen the camera after this many seconds without frames")
    args = ap.parse_args()

    # ▸ เตรียมระบบ: YOLO หากรอบคน, ตัวติดตาม id, MediaPipe โครงกระดูก, กล้อง, socket, ตัวล็อคผู้เล่น, ตัวนับก้าว
    from yolo_detect import PersonDetector, IouTracker, AsyncDetector
    model = PersonDetector(args.model, conf=args.conf)
    yolo = AsyncDetector(model, IouTracker())      # YOLO on the GPU in its own thread, parallel to MediaPipe
    print(f"[yolo] {os.path.basename(args.model)} on {model.provider} (person boxes only)")
    skel = MpSkeleton(complexity=args.mp_complexity)
    print(f"[mediapipe] pose model complexity {skel.complexity} ({'lite' if skel.complexity == 0 else 'full' if skel.complexity == 1 else 'heavy'})")
    known = {}       # track id -> last skeleton {"kps", "kc", "t"} (players: every frame, others: round robin)

    cam = Camera(args.source, fps=int(args.fps))
    print(f"[camera] {cam.name}")
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    calib = load_calib()
    lock = PlayerLock(split=calib["split"], lost_grace=args.lost_grace)
    filt = {s: (OneEuro(), OneEuro()) for s in (1, 2)}
    active = {1: -1, 2: -1}          # which hand each player aims with: 0 left, 1 right, -1 none
    active_lost = {1: 0.0, 2: 0.0}
    lock.join_m, lock.leave_m, lock.min_m = args.join_m, args.leave_m, args.min_m
    lock.lock_until_gone = not args.drop_far
    lock.require_dist = True
    steps = {1: StepDetector(args.step_thresh), 2: StepDetector(args.step_thresh)}
    slot_tid = {1: -1, 2: -1}
    calibrating_until, calib_rec = 0.0, None
    dist_until, dist_rec = 0.0, None

    period = 1.0 / args.fps
    last_id, next_t, t_start = -1, time.perf_counter(), time.perf_counter()
    fps_s, fps_n, fps_t = 0.0, 0, time.perf_counter()
    last_frame_t, n_err = time.perf_counter(), 0

    # ▸ วนทุกเฟรม: รอภาพใหม่ (ไม่มีภาพนานเกิน stall_s = เปิดกล้องใหม่)
    while True:
        fid, frame = cam.read()
        if frame is None or fid == last_id:
            # camera stopped delivering frames (unplugged / driver hiccup): reopen it instead of hanging
            if time.perf_counter() - last_frame_t > args.stall_s:
                print(f"[camera] no frames for {args.stall_s:.0f} s -> reopening")
                try:
                    cam.close()
                    cam = Camera(args.source, fps=int(args.fps))
                    print(f"[camera] {cam.name}")
                except Exception as e:
                    print(f"[camera] reopen failed: {e}")
                last_frame_t = time.perf_counter()
            # ▸ หน้าต่างพรีวิว: เส้นแบ่งฝั่ง, คนที่ยังไม่เป็นผู้เล่น (เฉพาะตอนมีช่องว่าง), โครงกระดูก P1/P2, fps
            #   ปุ่ม Q/Esc ออก, C คาลิเบรตมือ, R ปลดล็อค, D คาลิเบรตระยะ, S รีเซ็ตเส้นแบ่ง
            if not args.no_show:
                cv2.waitKey(1)
            else:
                time.sleep(0.002)
            continue
        last_frame_t = time.perf_counter()
        try:
            last_id = fid
            if not args.no_mirror:
                frame = cv2.flip(frame, 1)
            h, w = frame.shape[:2]

            # ▸ 1) YOLO (thread แยก): กรอบคนทุกคน + id ต่อเนื่อง (ไม่ใช้จุดกระดูกของ YOLO)
            yolo.submit(frame)
            _, people = yolo.latest()
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            focal = 0.5 * w / np.tan(np.radians(args.hfov) / 2)
            now = time.monotonic()

            # ▸ ระยะของแต่ละคน: คำนวณจากโครงกระดูก MediaPipe (ความกว้างไหล่ / ลำตัว / ทั้งตัว) แบบเดิม
            def measure(p, kps, kc):
                p["kps"], p["kc"] = kps, kc
                raw = estimate_distance(kps, kc, w, focal)
                fb = full_body_distance(kps, kc, focal)
                if fb is not None:  # whole body visible: blend in the steadier full-height estimate
                    raw = fb if raw is None else 0.6 * fb + 0.4 * raw
                p["raw_dist"] = raw
                p["dist"] = None if raw is None else raw * calib.get("dist_scale", 1.0)

            # คนที่ยังไม่เป็นผู้เล่น: MediaPipe ทีละคนต่อเฟรม (วนกัน) เพื่อรู้ระยะ 2-3 ม. ก่อนรับเข้าเล่น
            #   ทำเฉพาะตอนที่ยังมีช่องผู้เล่นว่าง (ครบ 2 คนแล้วไม่ต้องวัดคนอื่น = เร็วขึ้น)
            player_ids = {lk.track_id for lk in lock.slots if lk.locked}
            free_slot = any(not lk.locked for lk in lock.slots)
            others = [p for p in people if free_slot and p["id"] not in player_ids
                      and (p["box"][3] - p["box"][1]) >= lock.min_box_h]
            for tid, (kps, kc) in skel.candidates(rgb, others, now).items():
                known[tid] = {"kps": kps, "kc": kc, "t": now}
            for p in people:
                k = known.get(p["id"])
                if k is not None and now - k["t"] < 0.6:
                    measure(p, k["kps"], k["kc"])
                else:
                    p["kps"] = p["kc"] = None
                    p["raw_dist"] = p["dist"] = None

            # ▸ 2) เลือกว่าใครเป็น P1 / P2 (เข้าได้เฉพาะระยะ 2-3 ม. ล็อคจนออกจากกล้อง) คนอื่นไม่ใช้
            assigned = lock.update(people, now)

            # ▸ 3) MediaPipe: โครงกระดูกของผู้เล่นทั้งสอง (ทุกเฟรม พร้อมกัน 2 thread)
            #      หาไม่เจอชั่วครู่ -> ใช้ของเดิมไม่เกิน 0.3 วินาที
            for s, res in skel.players(rgb, assigned).items():
                p = assigned[s]
                if p is None:
                    continue
                if res is not None:
                    known[p["id"]] = {"kps": res[0], "kc": res[1], "t": now}
                    measure(p, *res)
                elif p["id"] in known and now - known[p["id"]]["t"] < 0.3:
                    measure(p, known[p["id"]]["kps"], known[p["id"]]["kc"])
                else:
                    p["kps"] = p["kc"] = None
            alive = {p["id"] for p in people}
            known = {k: v for k, v in known.items() if k in alive}

            # 1 m distance calibration: everyone tracked stands at 1.0 m for 2 s
            # ▸ คาลิเบรตระยะ: เก็บระยะดิบ 2 วินาทีตอนยืนที่ 1 ม. แล้วคำนวณตัวคูณ dist_scale
            if dist_rec is not None:
                dist_rec += [p["raw_dist"] for p in people if p["raw_dist"]]
                if now > dist_until:
                    if len(dist_rec) > 10:
                        calib["dist_scale"] = round(1.0 / float(np.median(dist_rec)), 4)
                        lock.reset()
                        save_calib(calib)
                        print(f"[calib] dist_scale = {calib['dist_scale']}")
                    else:
                        print("[calib] distance: nobody seen, kept old value")
                    dist_rec = None

            # nearest person on each side who is NOT a player yet (for "step closer" hints)
            # ▸ หาระยะคนที่ใกล้ที่สุดในแต่ละฝั่งที่ยังไม่เป็นผู้เล่น (ให้ Unity บอก ใกล้/ไกลเกินไป)
            approach = {1: None, 2: None}
            used = {lk.track_id for lk in lock.slots if lk.locked}
            for p in people:
                if p["id"] in used or p["dist"] is None:
                    continue
                side = 1 if (p["box"][0] + p["box"][2]) / 2 < lock.split else 2
                if approach[side] is None or p["dist"] < approach[side]:
                    approach[side] = p["dist"]

            # ▸ สร้างข้อมูลของ P1/P2: ระยะ, ก้าว, ท่าทาง, มือซ้าย-ขวา, มือที่ใช้เล็ง, จุดกระดูก
            out = []
            for s in (1, 2):
                p = assigned[s]
                entry = {"slot": s, "present": p is not None, "dist": -1.0, "steps": 0,
                         "hand": False, "use": -1, "x": 0.5, "y": 0.5,
                         "approach": -1.0 if approach[s] is None else round(approach[s], 2)}
                if p is not None:
                    if slot_tid[s] == -1:
                        steps[s].reset()
                    slot_tid[s] = p["id"]
                    entry["dist"] = -1.0 if p.get("dist") is None else round(p["dist"], 2)
                    if p.get("kps") is None:      # locked by YOLO but no skeleton this moment
                        entry["steps"] = steps[s].steps
                        out.append(entry)
                        continue
                    steps[s](p["kps"], p["kc"], now)
                    entry["steps"] = steps[s].steps
                    entry["kp"] = [round(float(v), 3) for i in range(17)
                                   for v in (p["kps"][i][0] / w, p["kps"][i][1] / h, p["kc"][i])]
                    hands = both_hands(p["kps"], p["kc"])
                    # active hand: the raised one; if both are raised keep the current one (no flicker)
                    up = [sd for sd in (0, 1) if hands[sd] is not None and hands[sd][3]]
                    prev = active[s]
                    if prev in up:
                        active_lost[s] = now
                    elif up:
                        active[s], active_lost[s] = (up[0] if len(up) == 1 else min(up, key=lambda sd: hands[sd][1])), now
                    elif now - active_lost[s] > 0.4:
                        active[s] = -1
                    if active[s] != prev:
                        filt[s][0].reset(); filt[s][1].reset()
                    entry["use"] = active[s]
                    hb = hands[active[s]] if active[s] >= 0 else None
                    if hb is not None:
                        rx, ry, wpx = hb[0], hb[1], hb[2]
                        if calib_rec is not None:
                            r = calib_rec[s]
                            r[0], r[1] = min(r[0], rx), max(r[1], rx)
                            r[2], r[3] = min(r[2], ry), max(r[3], ry)
                        x, y = map_to_screen(rx, ry, calib[str(s)])
                        fx, fy = filt[s]
                        entry.update(hand=True, x=round(fx(x, now), 4), y=round(fy(y, now), 4))
                        p["wrist"] = wpx
                else:
                    filt[s][0].reset(); filt[s][1].reset()
                    slot_tid[s] = -1
                    active[s] = -1
                out.append(entry)

            # finish calibration
            # ▸ จบการคาลิเบรตมือ: ใช้ขอบเขตที่ผู้เล่นกวาดมือได้เป็นขอบจอ
            if calib_rec is not None and now > calibrating_until:
                for s in (1, 2):
                    x0, x1, y0, y1 = calib_rec[s]
                    if x1 - x0 > 0.8 and y1 - y0 > 0.8:  # only accept a real sweep
                        mx, my = (x1 - x0) * 0.08, (y1 - y0) * 0.08
                        calib[str(s)] = {"x0": x0 + mx, "x1": x1 - mx, "y0": y0 + my, "y1": y1 - my}
                        print(f"[calib] P{s} -> {calib[str(s)]}")
                    else:
                        print(f"[calib] P{s}: not enough movement, kept old values")
                save_calib(calib)
                calib_rec = None

            # fps
            fps_n += 1
            if time.perf_counter() - fps_t >= 1.0:
                fps_s, fps_n, fps_t = fps_n / (time.perf_counter() - fps_t), 0, time.perf_counter()
                if args.no_show and int(time.perf_counter() - t_start) % 5 == 0:
                    print(f"[tracker] {fps_s:4.1f} fps  yolo {yolo.ms:.0f} ms  people {len(people)}  "
                          f"P1 {'in' if assigned[1] else '-'}  P2 {'in' if assigned[2] else '-'}")

            # ▸ ส่ง JSON ทั้งหมดไปที่ Unity ทาง UDP (127.0.0.1:5065)
            msg = {"t": round(time.time(), 3), "fps": round(fps_s, 1),
                   "players": out}
            sock.sendto(json.dumps(msg).encode("utf-8"), (args.host, args.port))

            if not args.no_show:
                vis = frame.copy()
                sx = int(lock.split * w)
                cv2.line(vis, (sx, 0), (sx, h), (200, 200, 200), 1)
                # other people are NOT players: nothing is drawn for them once both slots are taken.
                # While a slot is free, show only a thin box + distance so a newcomer knows where to stand.
                players_ids = {lk.track_id for lk in lock.slots if lk.locked}
                free = any(not lk.locked for lk in lock.slots)
                for p in people:
                    if p["id"] in players_ids or not free:
                        continue
                    x0, y0, x1, y1 = (np.array(p["box"]) * [w, h, w, h]).astype(int)
                    cv2.rectangle(vis, (x0, y0), (x1, y1), (90, 90, 90), 1)
                    if p.get("dist") is not None:
                        d = p["dist"]
                        col = (80, 220, 80) if lock.min_m <= d <= lock.join_m else ((0, 200, 255) if d < lock.min_m else (60, 60, 255))
                        cv2.putText(vis, f"{d:.2f} m", (x0 + 4, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.9, col, 2)
                for s, col in ((1, (255, 160, 60)), (2, (40, 140, 255))):
                    p = assigned[s]
                    if p is None:
                        continue
                    x0, y0, x1, y1 = (np.array(p["box"]) * [w, h, w, h]).astype(int)
                    cv2.rectangle(vis, (x0, y0), (x1, y1), col, 2)
                    if p.get("kps") is not None:
                        draw_skeleton(vis, p["kps"], p["kc"], col)
                    cv2.putText(vis, f"P{s} id{p['id']} {steps[s].rate(now):.1f} st/s {'LRN'[active[s]]}-hand", (x0, max(20, y0 - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.8, col, 2)
                    if "wrist" in p:
                        cv2.circle(vis, tuple(p["wrist"].astype(int)), 12, col, -1)
                status = f"{fps_s:4.1f} fps  {cam.name}  YOLO+MediaPipe"
                if calib_rec is not None:
                    status += f"  CALIBRATING {calibrating_until - now:3.1f}s - sweep hand to every corner"
                cv2.putText(vis, status, (10, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
                cv2.imshow("RTE tracker", cv2.resize(vis, (960, int(960 * h / w))))
                k = cv2.waitKey(1) & 0xFF
                if k in (27, ord("q")):
                    break
                if k == ord("c"):
                    calib_rec = {1: [9, -9, 9, -9], 2: [9, -9, 9, -9]}
                    calibrating_until = now + 6.0
                    print("[calib] started")
                if k == ord("r"):
                    lock.reset()
                if k == ord("d"):
                    dist_rec, dist_until = [], now + 2.0
                    print("[calib] distance: stand at exactly 1 m")
                if k == ord("s"):
                    lock.split = calib["split"] = 0.5

            if args.seconds and time.perf_counter() - t_start > args.seconds:
                break
            # hold a steady 30 Hz
            # ▸ รอให้ครบรอบ 30 Hz เพื่อส่งข้อมูลสม่ำเสมอ
            next_t += period
            delay = next_t - time.perf_counter()
            if delay > 0:
                time.sleep(delay)
            else:
                next_t = time.perf_counter()
        except Exception:
            # one bad frame must never stop the tracker: log it and carry on with the next frame
            import traceback
            n_err += 1
            if n_err <= 5 or n_err % 100 == 0:
                print(f"[tracker] frame error #{n_err} (continuing):")
                traceback.print_exc()
            if not args.no_show:
                cv2.waitKey(1)   # keep the preview window responsive (otherwise Windows shows "Not Responding")

    cam.close()
    yolo.close()
    skel.close()
    sock.close()
    cv2.destroyAllWindows()


# ▸ ตัวคุม: ถ้าโปรแกรมพังตอนเริ่ม (เช่นยังไม่เสียบกล้อง) รอ 3 วินาทีแล้วเริ่มใหม่
if __name__ == "__main__":
    # supervisor: if anything outside the frame loop fails (camera not plugged in yet, model/GPU error),
    # wait and start again instead of closing. Q / Esc in the preview window (or Ctrl+C) quits for real.
    import traceback
    while True:
        try:
            main()
            break
        except KeyboardInterrupt:
            break
        except SystemExit:
            raise
        except CameraNotConnected as e:
            print(f"[camera] {e}")
            time.sleep(3)
        except Exception:
            traceback.print_exc()
            print("[tracker] crashed -> restarting in 3 s (Ctrl+C to stop)")
            try:
                cv2.destroyAllWindows()
            except Exception:
                pass
            time.sleep(3)
