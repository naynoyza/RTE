// =====================================================================================================
// PoseDetector — ตรวจท่าของผู้เล่น 1 คนจากจุดกระดูก 17 จุด (YOLO Pose) ที่ tracker ส่งมา
// -----------------------------------------------------------------------------------------------------
// ท่าสะสมพลัง "ชูแขนรับพลัง" (Charge): ข้อมือทั้งสองสูงกว่าจมูก และห่างกันมากกว่าความกว้างไหล่ (รูปตัว V)
// ท่าส่งพลัง "ผลักฝ่ามือ"   (Push)  : ข้อมือทั้งสองอยู่ระดับไหล่ถึงอก และแขนเหยียด (มุมศอก > 150°)
//                                     หรือแขนชี้ตรงเข้าหากล้อง (ภาพ 2D ไหล่-ข้อมือจะสั้นลง)
// Hysteresis: ท่าต้องค้างครบ PoseEnterSeconds จึงนับว่าเริ่ม และหายไปครบ PoseExitSeconds จึงนับว่าเลิก
// ไม่มีกล้อง: P1 กด A = ชูแขน, S = ผลัก | P2 กด K = ชูแขน, L = ผลัก
// ส่งออก  : Pose (ท่าปัจจุบัน), Lost (หลุดจากกล้อง), Reach (ตำแหน่งมือเฉลี่ยเทียบไหล่ ไว้ขยับแขนบนจอ)
// =====================================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTE
{
    public enum SeedPose { None, Charge, Push }

    public class PoseDetector
    {
        // เลขจุดของ YOLO pose
        const int Nose = 0, LS = 5, RS = 6, LE = 7, RE = 8, LW = 9, RW = 10;
        const float MinConf = .3f;

        readonly int slot;
        readonly SeedSettings s;
        SeedPose raw = SeedPose.None;
        float rawTime;

        public SeedPose Pose { get; private set; }
        public bool Lost { get; private set; }
        public Vector2 Reach { get; private set; }     // มือเฉลี่ยเทียบกลางไหล่ (หน่วย = ความกว้างไหล่, y ขึ้น = บวก)

        public PoseDetector(int slot, SeedSettings settings) { this.slot = slot; s = settings; }

        public void Reset() { Pose = raw = SeedPose.None; rawTime = 0; Reach = Vector2.zero; }

        /// Auto-play (F2): ถ้าไม่ใช่ None จะใช้ท่านี้แทนกล้อง/คีย์บอร์ด
        public SeedPose Forced = SeedPose.None;

        public SeedPose Tick(TrackedPlayer p, bool online, float dt)
        {
            SeedPose now;
            if (Forced != SeedPose.None) { Lost = false; now = Forced; Reach = Vector2.Lerp(Reach, now == SeedPose.Charge ? new Vector2(0, 1.6f) : new Vector2(0, .3f), .2f); }
            else if (!online) { Lost = false; now = FromKeys(); }
            else
            {
                Lost = !p.present || p.kp == null || p.kp.Length < 51;
                now = Lost ? SeedPose.None : FromSkeleton(p.kp);
            }

            // Hysteresis: เปลี่ยนท่าก็ต่อเมื่อท่าใหม่ค้างนานพอ
            if (now != raw) { raw = now; rawTime = 0; }
            rawTime += dt;
            float need = raw == SeedPose.None ? s.PoseExitSeconds : s.PoseEnterSeconds;
            if (raw != Pose && rawTime >= need) Pose = raw;
            if (Lost) Pose = SeedPose.None;           // หลุดกล้อง = หยุดทันที
            return Pose;
        }

        SeedPose FromKeys()
        {
            var k = Keyboard.current;
            if (k == null) return SeedPose.None;
            bool charge = slot == 1 ? k.aKey.isPressed : k.kKey.isPressed;
            bool push = slot == 1 ? k.sKey.isPressed : k.lKey.isPressed;
            Reach = Vector2.Lerp(Reach, charge ? new Vector2(0, 1.6f) : push ? new Vector2(0, .3f) : new Vector2(0, -1f), .2f);
            return charge ? SeedPose.Charge : push ? SeedPose.Push : SeedPose.None;
        }

        SeedPose FromSkeleton(float[] kp)
        {
            Vector2 P(int i) => new Vector2(kp[i * 3], -kp[i * 3 + 1]);   // กลับแกน y ให้ "ขึ้น" เป็นบวก
            bool Ok(int i) => kp[i * 3 + 2] > MinConf;
            if (!Ok(LS) || !Ok(RS) || !Ok(LW) || !Ok(RW)) return SeedPose.None;

            Vector2 ls = P(LS), rs = P(RS), lw = P(LW), rw = P(RW);
            float sw = Mathf.Max(.02f, Vector2.Distance(ls, rs));
            Vector2 sc = (ls + rs) * .5f;
            Reach = ((lw + rw) * .5f - sc) / sw;

            // ---- ท่า V เหนือศีรษะ
            float headY = Ok(Nose) ? P(Nose).y : sc.y + .8f * sw;
            if (lw.y > headY && rw.y > headY && Mathf.Abs(lw.x - rw.x) > sw * s.ChargeSpread)
                return SeedPose.Charge;

            // ---- ท่าผลัก: ข้อมือระดับไหล่ถึงอก + แขนเหยียด (หรือชี้เข้ากล้อง)
            bool Level(Vector2 w) => w.y < sc.y + .5f * sw && w.y > sc.y - 1.3f * sw;
            bool Extended(Vector2 sh, int elbow, Vector2 w)
            {
                if (Vector2.Distance(sh, w) < sw * s.PushForeshorten) return true;           // ชี้เข้ากล้อง
                if (!Ok(elbow)) return false;
                Vector2 e = P(elbow);
                return Vector2.Angle(sh - e, w - e) > s.PushElbowAngle;                      // แขนเหยียดตรง
            }
            if (Level(lw) && Level(rw) && Extended(ls, LE, lw) && Extended(rs, RE, rw))
                return SeedPose.Push;
            return SeedPose.None;
        }
    }
}
