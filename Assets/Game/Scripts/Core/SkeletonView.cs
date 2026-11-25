// =====================================================================================================
// SkeletonView — วาดโครงกระดูกของผู้เล่นในกรอบ UI
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : วาดโครงกระดูกจาก MediaPipe (tracker ส่งมาเฉพาะ 13 จุดที่เกมใช้: จมูก ไหล่ ศอก ข้อมือ สะโพก เข่า ข้อเท้า) ถ้ามี
//           ไม่มี (เช่น บอต) ใช้ 17 จุดของ YOLO แทน — วงหัว + เส้นแขน/ลำตัว/ขา + จุดข้อต่อ
//           ย่อขยายให้พอดีกรอบเสมอ (ยืนใกล้หรือไกลก็เห็นเต็มกรอบ)
// ดึงข้อมูล: TrackedPlayer.pose (33 จุด) / TrackedPlayer.kp (17 จุด) — มีเฉพาะ P1/P2, คนอื่นในกล้อง tracker ไม่ส่งมา
// ใช้โดย  : แผงเล็กใน HUD ระหว่างเล่น (PlayerHud, SeedHud)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class SkeletonView
    {
        // เลขจุดของ YOLO pose: 0 จมูก | 5,6 ไหล่ | 7,8 ศอก | 9,10 ข้อมือ | 11,12 สะโพก | 13,14 เข่า | 15,16 ข้อเท้า
        static readonly int[,] KpBones =
        {
            { 5, 6 }, { 5, 7 }, { 7, 9 }, { 6, 8 }, { 8, 10 },        // ไหล่และแขน
            { 5, 11 }, { 6, 12 }, { 11, 12 },                          // ลำตัว
            { 11, 13 }, { 13, 15 }, { 12, 14 }, { 14, 16 }             // ขา
        };
        // เลขจุดของ MediaPipe Pose (33): 0 จมูก | 1-6 ตา | 7,8 หู | 9,10 ปาก | 11,12 ไหล่ | 13,14 ศอก | 15,16 ข้อมือ |
        // 17-22 นิ้วก้อย/ชี้/โป้ง | 23,24 สะโพก | 25,26 เข่า | 27,28 ข้อเท้า | 29,30 ส้นเท้า | 31,32 ปลายเท้า
        static readonly int[,] PoseBones =
        {
            { 11, 12 }, { 11, 13 }, { 13, 15 }, { 12, 14 }, { 14, 16 },                                       // ไหล่และแขน
            { 15, 17 }, { 15, 19 }, { 15, 21 }, { 17, 19 }, { 16, 18 }, { 16, 20 }, { 16, 22 }, { 18, 20 },   // มือ
            { 11, 23 }, { 12, 24 }, { 23, 24 },                                                                // ลำตัว
            { 23, 25 }, { 25, 27 }, { 24, 26 }, { 26, 28 },                                                    // ขา
            { 27, 29 }, { 29, 31 }, { 27, 31 }, { 28, 30 }, { 30, 32 }, { 28, 32 },                            // เท้า
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 7 }, { 0, 4 }, { 4, 5 }, { 5, 6 }, { 6, 8 }, { 9, 10 }         // หน้า
        };
        const float MinConfidence = .3f;   // จุดที่กล้องมั่นใจน้อยกว่านี้ไม่วาด (YOLO)
        const float MinVisibility = .5f;   // จุดที่มองไม่เห็นไม่วาด (MediaPipe)

        public readonly RectTransform Root;
        readonly Image[] bones = new Image[PoseBones.GetLength(0)];
        readonly Image[] joints = new Image[33];
        readonly Image head;
        readonly Vector2 boxSize;
        readonly float thickness;
        float alpha;

        public SkeletonView(Transform parent, string name, Vector2 size, Color color, float thickness = 10)
        {
            boxSize = size;
            this.thickness = thickness;
            Root = UIKit.Rect(name, parent);
            Root.sizeDelta = size;
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = UIKit.Panel("Bone", Root, new Vector2(10, thickness), color);
                bones[i].rectTransform.pivot = new Vector2(0, .5f);   // หมุนรอบปลายด้านหนึ่งของกระดูก
            }
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i] = UIKit.Img("Joint", Root, UIKit.Load("fx_circle"), Vector2.one * thickness * 1.5f, false);
                joints[i].color = Color.white;
            }
            head = UIKit.Img("Head", Root, UIKit.Load("fx_ring"), Vector2.one * thickness * 5, false);
            head.color = color;
        }

        // เรียกทุกเฟรม: kp/pose = null แปลว่าไม่มีผู้เล่น (หรืออยู่นอกระยะ 2-3 ม.) -> ค่อย ๆ จางหาย
        // pose (33 จุด x [x, y, z, มองเห็น]) ใช้ก่อนถ้ามี ไม่งั้นใช้ kp (17 จุด x [x, y, มั่นใจ])
        public void Set(float[] kp, float dt, float[] pose = null)
        {
            bool full = pose != null && pose.Length >= 33 * 4;
            bool hasData = full || (kp != null && kp.Length >= 51);
            alpha = Mathf.MoveTowards(alpha, hasData ? 1 : 0, dt * 4);
            Root.gameObject.SetActive(alpha > 0);
            if (full) Layout(pose, 4, 33, PoseBones, MinVisibility, 11);
            else if (hasData) Layout(kp, 3, 17, KpBones, MinConfidence, 5);
            ApplyAlpha();
        }

        // วางชิ้นส่วนทั้งหมดตามจุดกระดูก. stride = ค่าต่อจุด, n = จำนวนจุด, firstJoint = จุดแรกที่วางวงข้อต่อ (ข้ามหน้า)
        void Layout(float[] pts, int stride, int n, int[,] boneList, float minSeen, int firstJoint)
        {
            bool Seen(int i) => pts[i * stride + stride - 1] >= minSeen;   // ค่าสุดท้ายของแต่ละจุด = มั่นใจ/มองเห็น

            // 1) หากรอบของจุดที่มองเห็น
            float minX = 9, minY = 9, maxX = -9, maxY = -9;
            for (int i = 0; i < n; i++)
            {
                if (!Seen(i)) continue;
                minX = Mathf.Min(minX, pts[i * stride]); maxX = Mathf.Max(maxX, pts[i * stride]);
                minY = Mathf.Min(minY, pts[i * stride + 1]); maxY = Mathf.Max(maxY, pts[i * stride + 1]);
            }
            if (maxX < minX) return;

            // 2) ย่อขยายให้พอดีกรอบ (เว้นขอบ 8%) และกลับแกน y (ภาพกล้อง y ลงล่าง, UI y ขึ้นบน)
            float scale = Mathf.Min(boxSize.x * .84f / Mathf.Max(.05f, maxX - minX), boxSize.y * .84f / Mathf.Max(.05f, maxY - minY));
            Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
            Vector2 Pos(int i) => new Vector2((pts[i * stride] - center.x) * scale, -(pts[i * stride + 1] - center.y) * scale);

            // 3) เส้นกระดูก: วางที่จุดแรก ยาวถึงจุดที่สอง หมุนตามมุม (หน้า/มือ/เท้า เส้นบางกว่า)
            int nb = boneList.GetLength(0);
            for (int b = 0; b < bones.Length; b++)
            {
                bones[b].enabled = b < nb && Seen(boneList[b, 0]) && Seen(boneList[b, 1]);
                if (!bones[b].enabled) continue;
                int from = boneList[b, 0], to = boneList[b, 1];
                Vector2 a = Pos(from), d = Pos(to) - a;
                bool thin = stride == 4 && (to <= 10 || to >= 17 && to <= 22 || to >= 29);
                var rt = bones[b].rectTransform;
                rt.anchoredPosition = a;
                rt.sizeDelta = new Vector2(d.magnitude, thickness * (thin ? .5f : 1f));
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }

            // 4) จุดข้อต่อ (ไม่วางบนหน้า) และวงหัวที่จมูก ขนาดตามความกว้างไหล่
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i].enabled = i < n && i >= firstJoint && Seen(i);
                if (!joints[i].enabled) continue;
                joints[i].rectTransform.anchoredPosition = Pos(i);
                bool small = stride == 4 && (i >= 17 && i <= 22 || i >= 29);   // นิ้วมือ ส้นเท้า ปลายเท้า
                joints[i].rectTransform.sizeDelta = Vector2.one * thickness * (small ? .8f : 1.5f);
            }
            int ls = stride == 4 ? 11 : 5, rs = ls + 1;
            head.enabled = Seen(0);
            if (head.enabled)
            {
                float shoulders = Seen(ls) && Seen(rs) ? Vector2.Distance(Pos(ls), Pos(rs)) : 60;
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
