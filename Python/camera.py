# ไฟล์นี้: [Tracker] อ่านภาพจากกล้องใน thread แยก เก็บเฉพาะภาพล่าสุด (ไม่สะสมคิว = ไม่หน่วง)
#   ลองเปิดด้วย pyorbbecsdk ก่อน ถ้าไม่ได้ใช้ OpenCV (UVC / เลขกล้อง / ไฟล์วิดีโอ)
"""Camera grabber: Orbbec SDK -> Orbbec UVC -> any OpenCV source. Always hands out the newest frame."""
import threading, time
import numpy as np
import cv2


class CameraNotConnected(RuntimeError):
    pass


class Camera:
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
                raise   # don't fall back to another webcam: wait for the Orbbec (supervisor retries)
            except Exception as e:
                print(f"[camera] pyorbbecsdk unavailable ({e}); using Orbbec UVC")
                source = "orbbec-uvc"
        if self._ob is None:
            self._open_cv(source, width, height, fps)
        threading.Thread(target=self._loop, daemon=True).start()

    # ▸ เปิดกล้อง Orbbec ผ่าน SDK เลือกรูปแบบภาพสี (RGB/MJPG/YUYV)
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

    # ▸ เปิดกล้องผ่าน OpenCV: หา Orbbec RGB ตามชื่อ / ไล่เลข 0-3 / เปิดไฟล์วิดีโอ
    def _open_cv(self, source, w, h, fps):
        if source == "orbbec-uvc":
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

    # ▸ (thread กล้อง) วนอ่านเฟรมใหม่, ไฟล์วิดีโอเล่นวนที่ 30 fps
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

    def read(self):
        with self.lock:
            return self.frame_id, self.frame

    def close(self):
        self.ok = False
        time.sleep(0.15)
        if self._ob is not None:
            self._ob.stop()
        if self._cap is not None:
            self._cap.release()
