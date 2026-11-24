// =====================================================================================================
// TreeView — ต้นไม้กลางจอ โตตาม Growth Bar
// -----------------------------------------------------------------------------------------------------
// ระยะ 1 ต้นอ่อน (0–30%)   : ภาพต้นอ่อนจาก Ref/24 ค่อย ๆ ขยายจากเมล็ดบนกองดิน
// ระยะ 2–3 (30–100%)       : sprite sheet tree_atlas (96 เฟรม เรนเดอร์จาก Blender) เลื่อนเฟรมตาม % การเติบโต
//                            ใช้ 2 SpriteRenderer ผสมเฟรมติดกัน (crossfade) จึงลื่นแม้โตช้า ๆ
// ได้รับพลัง (Receive)     : ต้นอ่อนสลับเป็นภาพเรืองแสง + แสงฟุ้ง + ประกายรอบต้น + ต้นไม้เด้งเบา ๆ
// ดึงข้อมูล: Resources/Sprites/seed/sprout, sprout_glow, tree_atlas, fx_soft, fx_spark, fx_drop
// =====================================================================================================
using UnityEngine;

namespace RTE
{
    public class TreeView
    {
        public static readonly Vector3 Base = SeedKit.Px(975, 905);       // โคนต้นบนกองดินในภาพพื้นหลัง
        public static Vector3 Target => Base + new Vector3(0, 1.6f, 0);  // จุดที่น้ำ/แสงพุ่งเข้าหา

        const int Cols = 12, Frames = 96, FirstTreeFrame = 30;         // เฟรม 30 = ต้นกล้าสูงพอ ๆ กับภาพต้นอ่อน
        const float TreePpu = 54.6f;                                    // ต้นไม้โตเต็มสูง ~680 พิกเซลบนจอ

        readonly Transform root, sway;
        readonly SpriteRenderer sprout, sproutGlow, treeA, treeB, glow;
        readonly Sprite[] frames = new Sprite[Frames];
        readonly ParticleSystem sparks, leaves, burst;
        float shown, receive, pulse;

        public TreeView(Transform parent)
        {
            root = new GameObject("Tree").transform; root.SetParent(parent, false); root.position = Base;
            sway = new GameObject("Sway").transform; sway.SetParent(root, false);

            glow = SeedKit.MakeSprite("Glow", root, SeedKit.Spr("fx_soft", new Vector2(.5f, .5f), 64), SeedKit.OrderTreeGlow, true);
            glow.transform.localPosition = new Vector3(0, 1.6f, 0);
            glow.color = new Color(.8f, 1f, .6f, 0);

            sprout = SeedKit.MakeSprite("Sprout", sway, SeedKit.Spr("sprout", new Vector2(.5f, .03f)), SeedKit.OrderTree);
            sproutGlow = SeedKit.MakeSprite("SproutGlow", sway, SeedKit.Spr("sprout_glow", new Vector2(.5f, .03f)), SeedKit.OrderTree + 1);

            var atlas = SeedKit.Tex("tree_atlas");
            int size = atlas.width / Cols, rows = Frames / Cols;
            for (int i = 0; i < Frames; i++)
            {
                var r = new Rect((i % Cols) * size, (rows - 1 - i / Cols) * size, size, size);   // แถวบนสุดของภาพ = เฟรมแรก
                frames[i] = Sprite.Create(atlas, r, new Vector2(.5f, .041f), TreePpu, 0, SpriteMeshType.FullRect);
            }
            treeA = SeedKit.MakeSprite("TreeA", sway, frames[FirstTreeFrame], SeedKit.OrderTree + 2);
            treeB = SeedKit.MakeSprite("TreeB", sway, frames[FirstTreeFrame], SeedKit.OrderTree + 3);

            // ประกายรอบต้นขณะได้รับพลัง
            sparks = SeedKit.Particles("Sparks", root, "fx_spark", true, SeedKit.OrderTreeFx, 1.1f, .35f, .8f, new Color(.85f, 1f, .6f));
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.2f;
            sparks.transform.localPosition = new Vector3(0, 1.4f, 0);
            var vel = sparks.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(-.2f, .2f); vel.y = new ParticleSystem.MinMaxCurve(.4f, 1.2f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // ใบไม้ปลิว (ตอนจบ) และพลุตอนเปลี่ยนระยะ
            leaves = SeedKit.Particles("Leaves", root, "fx_drop", false, SeedKit.OrderTreeFx + 1, 4f, .3f, 2.5f, new Color(.45f, .85f, .25f));
            var lsh = leaves.shape; lsh.enabled = true; lsh.shapeType = ParticleSystemShapeType.Circle; lsh.radius = 2f;
            leaves.transform.localPosition = new Vector3(0, 4.5f, 0);
            var lm = leaves.main; lm.gravityModifier = .15f; lm.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var lrot = leaves.rotationOverLifetime; lrot.enabled = true; lrot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            burst = SeedKit.Particles("Burst", root, "fx_soft", true, SeedKit.OrderTreeFx + 2, .9f, .5f, 6f, new Color(1f, 1f, .7f));
            var bsh = burst.shape; bsh.enabled = true; bsh.shapeType = ParticleSystemShapeType.Circle; bsh.radius = .4f;
            burst.transform.localPosition = new Vector3(0, 1.5f, 0);
            Set(0, 0, 0, true);
        }

        public void Reset() { shown = 0; receive = 0; pulse = 0; SeedKit.Rate(leaves, 0); Set(0, 0, 0, true); }

        /// เปลี่ยนระยะ / PERFECT SYNC: พลุแสง + เด้ง
        public void Burst(int count = 60) { burst.Emit(count); pulse = 1; }

        public void Celebrate(bool on) => SeedKit.Rate(leaves, on ? 25 : 0);

        /// growth 0..1, receiving 0..1 (กำลังได้รับน้ำ/แสงมากแค่ไหน)
        public void Set(float growth, float receiving, float dt, bool snap = false)
        {
            shown = snap ? growth : SeedKit.Damp(shown, growth, 4f, dt);      // โตแบบนุ่ม ไม่กระโดด
            receive = SeedKit.Damp(receive, receiving, 6f, dt);
            pulse = Mathf.Max(0, pulse - dt * 2.2f);

            // ระยะ 1: ต้นอ่อนจาก Ref ขยายขึ้น, ช่วง 25–35% ผสมเข้าหาต้นไม้จาก atlas
            float sproutScale = Mathf.Lerp(.15f, 1f, Mathf.Clamp01(shown / .3f));
            float toTree = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .35f, shown));
            sprout.transform.localScale = sproutGlow.transform.localScale = Vector3.one * sproutScale;
            sprout.color = new Color(1, 1, 1, (1 - toTree) * (1 - receive));
            sproutGlow.color = new Color(1, 1, 1, (1 - toTree) * receive);

            // ยกกำลัง 0.6 = ช่วงกิ่งเปล่าผ่านไปเร็ว ใบเริ่มออกราว 45% (ระยะ 2 ดูมีชีวิต)
            float f = Mathf.Lerp(FirstTreeFrame, Frames - 1, Mathf.Pow(Mathf.InverseLerp(.25f, 1f, shown), .6f));
            int i0 = Mathf.Clamp(Mathf.FloorToInt(f), 0, Frames - 1), i1 = Mathf.Min(i0 + 1, Frames - 1);
            treeA.sprite = frames[i0]; treeB.sprite = frames[i1];
            treeA.color = new Color(1, 1, 1, toTree);
            treeB.color = new Color(1, 1, 1, toTree * (f - i0));
            var tint = Color.Lerp(Color.white, new Color(1.25f, 1.3f, 1.05f), receive * .6f);  // สว่างขึ้นเล็กน้อยตอนได้พลัง
            treeA.color *= tint; treeB.color *= tint;

            // เด้งเบา ๆ ตอนได้พลัง + ไหวตามลมตลอดเวลา
            float s = 1 + .04f * receive * Mathf.Sin(Time.time * 14) + .08f * UIKit.EaseOutBack(1 - pulse) * pulse;
            sway.localScale = new Vector3(s, s, 1);
            sway.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.3f) * 1.5f * shown);

            glow.color = new Color(.8f, 1f, .6f, .15f * shown + .55f * receive + .5f * pulse);
            glow.transform.localScale = Vector3.one * (1.2f + 2.2f * shown);
            SeedKit.Rate(sparks, 40 * receive);
        }
    }
}
