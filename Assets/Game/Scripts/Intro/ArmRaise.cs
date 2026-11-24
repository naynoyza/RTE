// =====================================================================================================
// ArmRaise — ตรวจว่าผู้เล่นยกแขนข้างไหน (ใช้เลือกตัวละคร)
// -----------------------------------------------------------------------------------------------------
// ยกแขน = ข้อมือสูงกว่าไหล่ข้างเดียวกันชัดเจน (เทียบความกว้างไหล่) และอีกข้างไม่ได้ยก
// ด้าน "ซ้าย" = ข้างที่อยู่ซ้ายบนจอ (ภาพกล้องแบบกระจก) -> เลือก MALE, ด้าน "ขวา" -> FEMALE
// ไม่มีกล้อง: P1 กด Q (ซ้าย) / E (ขวา), P2 กด U (ซ้าย) / O (ขวา)
// =====================================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTE
{
    public static class ArmRaise
    {
        const int LS = 5, RS = 6, LW = 9, RW = 10;
        const float MinConf = .3f;

        // ชูแขนทั้งสองข้าง (ข้อมือทั้งคู่สูงกว่าไหล่) — ใช้เป็นท่า "พร้อมออกภารกิจ"
        // ไม่มีกล้อง: P1 กด Q+E พร้อมกัน, P2 กด U+O พร้อมกัน
        public static bool BothUp(TrackedPlayer p, int slot, bool online)
        {
            if (!online)
            {
                var k = Keyboard.current; if (k == null) return false;
                return slot == 0 ? k.qKey.isPressed && k.eKey.isPressed : k.uKey.isPressed && k.oKey.isPressed;
            }
            var kp = p.kp;
            if (!p.present || kp == null || kp.Length < 51) return false;
            bool Ok(int i) => kp[i * 3 + 2] > MinConf;
            if (!Ok(LS) || !Ok(RS) || !Ok(LW) || !Ok(RW)) return false;
            float margin = Mathf.Max(.02f, Mathf.Abs(kp[LS * 3] - kp[RS * 3]) * .25f);
            return kp[LS * 3 + 1] - kp[LW * 3 + 1] > margin && kp[RS * 3 + 1] - kp[RW * 3 + 1] > margin;
        }

        // -1 = ไม่ยก, 0 = ยกแขนด้านซ้ายของจอ, 1 = ด้านขวาของจอ
        public static int Side(TrackedPlayer p, int slot, bool online)
        {
            if (!online)
            {
                var k = Keyboard.current; if (k == null) return -1;
                if (slot == 0) return k.qKey.isPressed ? 0 : k.eKey.isPressed ? 1 : -1;
                return k.uKey.isPressed ? 0 : k.oKey.isPressed ? 1 : -1;
            }
            var kp = p.kp;
            if (!p.present || kp == null || kp.Length < 51) return -1;
            bool Ok(int i) => kp[i * 3 + 2] > MinConf;
            if (!Ok(LS) || !Ok(RS)) return -1;
            float shoulderW = Mathf.Abs(kp[LS * 3] - kp[RS * 3]);
            float margin = Mathf.Max(.02f, shoulderW * .35f);
            bool Up(int w, int s) => Ok(w) && kp[s * 3 + 1] - kp[w * 3 + 1] > margin;   // y ลงล่าง
            bool lUp = Up(LW, LS), rUp = Up(RW, RS);
            if (lUp == rUp) return -1;                                                   // ไม่ยก หรือยกทั้งสองข้าง
            int w2 = lUp ? LW : RW;
            float midX = (kp[LS * 3] + kp[RS * 3]) * .5f;
            return kp[w2 * 3] < midX ? 0 : 1;
        }
    }
}
