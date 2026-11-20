# ไฟล์นี้: [Dev] ดูอัตราและความนิ่งของแพ็กเก็ตจาก tracker (ปิด Unity ก่อน เพราะใช้พอร์ตเดียวกัน)
"""Listen to tracker packets and report rate/jitter:  python udp_monitor.py [seconds]"""
import socket, sys, time, json, statistics

# ▸ ฟังพอร์ต 5065 ตามจำนวนวินาทีที่กำหนด แล้ววัดจำนวนแพ็กเก็ตและช่วงห่างระหว่างแพ็กเก็ต
secs = float(sys.argv[1]) if len(sys.argv) > 1 else 10
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.bind(("127.0.0.1", 5065)); s.settimeout(0.5)
t0, last, gaps, n, msg = time.perf_counter(), None, [], 0, None
while time.perf_counter() - t0 < secs:
    try:
        msg = json.loads(s.recv(65535)); now = time.perf_counter(); n += 1
        if last: gaps.append((now - last) * 1000)
        last = now
    except socket.timeout:
        pass
if n < 2:
    print("NO DATA"); sys.exit(1)
print(f"packets={n} rate={n / (last - t0):.1f}/s gap_ms mean={statistics.mean(gaps):.1f} "
      f"p95={sorted(gaps)[int(len(gaps) * .95)]:.1f} max={max(gaps):.1f}")
print("last:", msg)
