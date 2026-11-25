// =====================================================================================================
// PresenceGuard — เฝ้าว่าผู้เล่นทั้ง 2 คนยังอยู่หน้ากล้อง ตั้งแต่หน้าเลือกตัวละครจนจบเกม 2
// -----------------------------------------------------------------------------------------------------
// หลุดเกิน WarnAfter วิ      -> แถบแดงด้านบน "P1 ออกจากระยะกล้อง! กลับมายืนที่เดิม (กลับหน้าแรกใน X วิ)"
// คนใดคนหนึ่งหายครบ OneLostSeconds วิ  หรือ  หายทั้งคู่ครบ BothLostSeconds วิ -> Tick() คืน true (กลับหน้าปก)
//   * 12 วิ: พอให้คนที่เดินหลุดเฟรม/ก้มเก็บของ/tracker ล็อคตัวใหม่ (~1–2 วิ) กลับมาได้ทัน
//   * 6 วิ : ถ้าหายไปทั้งคู่ แปลว่าเลิกเล่นแล้ว ไม่ต้องรอนาน
// ไม่มีกล้อง (โหมดทดสอบ) = ไม่เช็ค
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class PresenceGuard
    {
        public const float WarnAfter = .6f, OneLostSeconds = 12f, BothLostSeconds = 6f;

        readonly GameObject root;
        readonly Text text;
        readonly float[] lost = new float[2];
        float bothLost;

        public PresenceGuard(Transform canvas)
        {
            var band = UIKit.Panel("PresenceWarning", canvas, new Vector2(1500, 120), new Color(.75f, .05f, .1f, .92f));
            UIKit.Anchor(band.rectTransform, new Vector2(.5f, 1), new Vector2(0, -20));
            root = band.gameObject;
            text = UIKit.Label("Text", band.transform, "", 40, Color.white);
            root.SetActive(false);
        }

        public void Reset() { lost[0] = lost[1] = bothLost = 0; root.SetActive(false); }
        public void Hide() => root.SetActive(false);

        // คืน true เมื่อควรยกเลิกเกมแล้วกลับหน้าปก
        public bool Tick(TrackedPlayer[] players, bool online)
        {
            if (!online) { Reset(); return false; }
            float dt = Time.deltaTime;
            for (int i = 0; i < 2; i++) lost[i] = players[i].present ? 0 : lost[i] + dt;
            bothLost = lost[0] > 0 && lost[1] > 0 ? bothLost + dt : 0;

            bool w0 = lost[0] > WarnAfter, w1 = lost[1] > WarnAfter;
            bool wasWarning = root.activeSelf;
            root.SetActive(w0 || w1);
            if (w0 || w1)
            {
                if (!wasWarning) Sfx.Play("alert", .9f);                // แถบเตือนเพิ่งโผล่
                float left = Mathf.Min(OneLostSeconds - Mathf.Max(lost[0], lost[1]), bothLost > 0 ? BothLostSeconds - bothLost : 99);
                string who = w0 && w1 ? "P1 และ P2" : w0 ? "P1" : "P2";
                text.text = $"{who} ออกจากระยะกล้อง!  กลับมายืนที่เดิม  (กลับหน้าแรกใน {Mathf.Max(0, Mathf.CeilToInt(left))} วิ)";
                root.transform.localScale = Vector3.one * (1 + .02f * Mathf.Sin(Time.time * 8));
            }
            return lost[0] >= OneLostSeconds || lost[1] >= OneLostSeconds || bothLost >= BothLostSeconds;
        }
    }
}
