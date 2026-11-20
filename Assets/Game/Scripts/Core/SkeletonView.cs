// =====================================================================================================
// SkeletonView — วาดโครงกระดูกของผู้เล่นในกรอบ UI
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : รับจุดกระดูก 17 จุด แล้ววาดเป็น วงหัว + เส้นแขน/ลำตัว/ขา + จุดข้อต่อ
//           ย่อขยายให้พอดีกรอบเสมอ (ยืนใกล้หรือไกลก็เห็นเต็มกรอบ)
// ดึงข้อมูล: TrackedPlayer.kp (มีเฉพาะ P1/P2 — คนอื่นในกล้อง tracker ไม่ส่งมา)
// ใช้โดย  : หน้าจุดยืน (StandbyScreen) และแผงเล็กใน HUD ระหว่างเล่น (PlayerHud)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class SkeletonView
    {
        // เลขจุดของ YOLO pose: 0 จมูก | 5,6 ไหล่ | 7,8 ศอก | 9,10 ข้อมือ | 11,12 สะโพก | 13,14 เข่า | 15,16 ข้อเท้า
        static readonly int[,] Bones =
        {
            { 5, 6 }, { 5, 7 }, { 7, 9 }, { 6, 8 }, { 8, 10 },        // ไหล่และแขน
            { 5, 11 }, { 6, 12 }, { 11, 12 },                          // ลำตัว
            { 11, 13 }, { 13, 15 }, { 12, 14 }, { 14, 16 }             // ขา
        };
        const float MinConfidence = .3f;   // จุดที่กล้องมั่นใจน้อยกว่านี้ไม่วาด

        public readonly RectTransform Root;
        readonly Image[] bones = new Image[Bones.GetLength(0)];
        readonly Image[] joints = new Image[17];
        readonly Image head;
        readonly Vector2 boxSize;
        float alpha;

        public SkeletonView(Transform parent, string name, Vector2 size, Color color, float thickness = 10)
        {
            boxSize = size;
            Root = UIKit.Rect(name, parent);
            Root.sizeDelta = size;
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = UIKit.Panel("Bone", Root, new Vector2(10, thickness), color);
                bones[i].rectTransform.pivot = new Vector2(0, .5f);   // หมุนรอบปลายด้านหนึ่งของกระดูก
            }
            for (int i = 0; i < 17; i++)
            {
                joints[i] = UIKit.Img("Joint", Root, UIKit.Load("fx_circle"), Vector2.one * thickness * 1.5f, false);
                joints[i].color = Color.white;
            }
            head = UIKit.Img("Head", Root, UIKit.Load("fx_ring"), Vector2.one * thickness * 5, false);
            head.color = color;
        }

        // เรียกทุกเฟรม: kp = null แปลว่าไม่มีผู้เล่น -> ค่อย ๆ จางหาย
        public void Set(float[] kp, float dt)
        {
            bool hasData = kp != null && kp.Length >= 51;
            alpha = Mathf.MoveTowards(alpha, hasData ? 1 : 0, dt * 4);
            Root.gameObject.SetActive(alpha > 0);
            if (hasData) Layout(kp);
            ApplyAlpha();
        }

        // วางชิ้นส่วนทั้งหมดตามจุดกระดูก
        void Layout(float[] kp)
        {
            bool Seen(int i) => kp[i * 3 + 2] >= MinConfidence;

            // 1) หากรอบของจุดที่มองเห็น
            float minX = 9, minY = 9, maxX = -9, maxY = -9;
            for (int i = 0; i < 17; i++)
            {
                if (!Seen(i)) continue;
                minX = Mathf.Min(minX, kp[i * 3]); maxX = Mathf.Max(maxX, kp[i * 3]);
                minY = Mathf.Min(minY, kp[i * 3 + 1]); maxY = Mathf.Max(maxY, kp[i * 3 + 1]);
            }
            if (maxX < minX) return;

            // 2) ย่อขยายให้พอดีกรอบ (เว้นขอบ 8%) และกลับแกน y (ภาพกล้อง y ลงล่าง, UI y ขึ้นบน)
            float scale = Mathf.Min(boxSize.x * .84f / Mathf.Max(.05f, maxX - minX), boxSize.y * .84f / Mathf.Max(.05f, maxY - minY));
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
            Vector2 Pos(int i) => new Vector2((kp[i * 3] - center.x) * scale, -(kp[i * 3 + 1] - center.y) * scale);

            // 3) เส้นกระดูก: วางที่จุดแรก ยาวถึงจุดที่สอง หมุนตามมุม
            for (int b = 0; b < bones.Length; b++)
            {
                int from = Bones[b, 0], to = Bones[b, 1];
                bones[b].enabled = Seen(from) && Seen(to);
                if (!bones[b].enabled) continue;
                Vector2 a = Pos(from), d = Pos(to) - a;
                var rt = bones[b].rectTransform;
                rt.anchoredPosition = a;
                rt.sizeDelta = new Vector2(d.magnitude, rt.sizeDelta.y);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }

            // 4) จุดข้อต่อ (ไม่วางบนตา/หู) และวงหัวที่จมูก ขนาดตามความกว้างไหล่
            for (int i = 0; i < 17; i++)
            {
                joints[i].enabled = i >= 5 && Seen(i);
                if (joints[i].enabled) joints[i].rectTransform.anchoredPosition = Pos(i);
            }
            head.enabled = Seen(0);
            if (head.enabled)
            {
                float shoulders = Seen(5) && Seen(6) ? Vector2.Distance(Pos(5), Pos(6)) : 60;
                head.rectTransform.anchoredPosition = Pos(0);
                head.rectTransform.sizeDelta = Vector2.one * Mathf.Clamp(shoulders * .6f, 20, 160);
            }
        }

        void ApplyAlpha()
        {
            foreach (var img in bones) SetA(img);
            foreach (var img in joints) SetA(img);
            SetA(head);
            void SetA(Image img) { var c = img.color; c.a = alpha; img.color = c; }
        }
    }
}
