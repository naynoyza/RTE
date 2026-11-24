# sim_presence.py — ส่งแพ็กเก็ตปลอมให้ Unity เพื่อทดสอบการเฝ้าผู้เล่น (PresenceGuard)
# ใช้: python Python/dev/sim_presence.py [วินาทีที่ P2 อยู่] [วินาทีทั้งหมด]
#   P1 อยู่ตลอด, P2 อยู่ช่วงแรกแล้วหายไป -> Unity ควรขึ้นแถบแดง แล้วกลับหน้าปกเมื่อหายครบ 12 วิ
import json, socket, sys, time

p2_seconds = float(sys.argv[1]) if len(sys.argv) > 1 else 4
total = float(sys.argv[2]) if len(sys.argv) > 2 else 25
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
kp = [0.5, 0.5, 0.9] * 17
t0 = time.time()
while time.time() - t0 < total:
    t = time.time() - t0
    players = [
        {"slot": 1, "present": True, "dist": 2.5, "kp": kp},
        {"slot": 2, "present": t < p2_seconds, "dist": 2.5 if t < p2_seconds else -1, "kp": kp},
    ]
    sock.sendto(json.dumps({"fps": 30, "players": players}).encode(), ("127.0.0.1", 5065))
    time.sleep(1 / 30)
print("done")
