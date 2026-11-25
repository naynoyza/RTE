// =====================================================================================================
// TreeView — ต้นไม้กลางจอ โตตาม Growth Bar (4 ระดับ จาก Ref/Tree.png)
// -----------------------------------------------------------------------------------------------------
// ระดับ 1 ต้นอ่อน        (0–25%)   : ใบเลี้ยงคู่บนกองดิน
// ระดับ 2 ต้นกล้า        (25–50%)  : ลำต้นเล็ก ใบหลายคู่
// ระดับ 3 ต้นไม้วัยรุ่น   (50–75%)  : ทรงพุ่มเต็ม ลำต้นบิด
// ระดับ 4 โตเต็มวัย      (75–100%) : ต้นใหญ่ รากแผ่ แสงเรืองในพุ่ม
// ในแต่ละระดับต้นไม้ค่อย ๆ ขยาย 80% -> 100%, เปลี่ยนระดับ = ภาพใหม่จางเข้า + เด้ง + พลุแสง
// ได้รับพลัง (Receive): ชั้นเรืองแสง (ภาพเดียวกันแบบ additive) + แสงฟุ้ง + ประกายรอบต้น
// ดึงข้อมูล: Resources/Sprites/seed/tree_s1..s4 (Tools/make_seed_v2.py), fx_soft, fx_spark, fx_drop
// =====================================================================================================
using UnityEngine;

namespace RTE
{
    public class TreeView
    {
        public static readonly Vector3 Base = SeedKit.Px(975, 925);       // โคนต้นบนกองดินในภาพพื้นหลัง
        public static Vector3 Target { get; private set; } = Base + new Vector3(0, .8f, 0);  // จุดที่น้ำ/แสงพุ่งเข้าหา (สูงตามต้น)

        public const int Stages = 4;
        public static readonly float[] StageStart = { 0f, .25f, .5f, .75f };
        public static readonly string[] StageName = { "ต้นอ่อน", "ต้นกล้า", "ต้นไม้วัยรุ่น", "ต้นไม้โตเต็มวัย" };
        static readonly float[] ScreenScale = { 1.55f, 1.45f, 1.35f, 1.25f };   // พิกเซลจอ ต่อ พิกเซลภาพ ของแต่ละระดับ

        readonly Transform root, sway;
        readonly SpriteRenderer[] trees = new SpriteRenderer[Stages], glowTrees = new SpriteRenderer[Stages];
        readonly float[] stageH = new float[Stages];                     // ความสูงบนจอ (หน่วยโลก)
        readonly SpriteRenderer glow;
        readonly ParticleSystem sparks, leaves, burst;
        readonly float[] weight = new float[Stages];
        float shown, receive, pulse;
        int stageNow;

        public static int StageOf(float growth) { int k = 0; for (int i = 1; i < Stages; i++) if (growth >= StageStart[i]) k = i; return k; }

        public TreeView(Transform parent)
        {
            root = new GameObject("Tree").transform; root.SetParent(parent, false); root.position = Base;
            sway = new GameObject("Sway").transform; sway.SetParent(root, false);

            glow = SeedKit.MakeSprite("Glow", root, SeedKit.Spr("fx_soft", new Vector2(.5f, .5f), 64), SeedKit.OrderTreeGlow, true);
            glow.color = new Color(.8f, 1f, .6f, 0);

            for (int i = 0; i < Stages; i++)
            {
                var tex = SeedKit.Tex($"tree_s{i + 1}");
                float ppu = 100f / ScreenScale[i];
                var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .07f), ppu, 0, SpriteMeshType.FullRect);
                trees[i] = SeedKit.MakeSprite($"Stage{i + 1}", sway, spr, SeedKit.OrderTree + i * 2);
                glowTrees[i] = SeedKit.MakeSprite($"Stage{i + 1}Glow", sway, spr, SeedKit.OrderTree + i * 2 + 1, true);
                stageH[i] = tex.height * .93f / ppu;
            }

            // ประกายรอบต้นขณะได้รับพลัง
            sparks = SeedKit.Particles("Sparks", root, "fx_spark", true, SeedKit.OrderTreeFx, 1.1f, .35f, .8f, new Color(.85f, 1f, .6f));
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.2f;
            var vel = sparks.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(-.2f, .2f); vel.y = new ParticleSystem.MinMaxCurve(.4f, 1.2f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // ใบไม้ปลิว (ตอนจบ) และพลุตอนเปลี่ยนระดับ
            leaves = SeedKit.Particles("Leaves", root, "fx_drop", false, SeedKit.OrderTreeFx + 1, 4f, .3f, 2.5f, new Color(.45f, .85f, .25f));
            var lsh = leaves.shape; lsh.enabled = true; lsh.shapeType = ParticleSystemShapeType.Circle; lsh.radius = 2f;
            leaves.transform.localPosition = new Vector3(0, 4.5f, 0);
            var lm = leaves.main; lm.gravityModifier = .15f; lm.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var lrot = leaves.rotationOverLifetime; lrot.enabled = true; lrot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            burst = SeedKit.Particles("Burst", root, "fx_soft", true, SeedKit.OrderTreeFx + 2, .9f, .5f, 6f, new Color(1f, 1f, .7f));
            var bsh = burst.shape; bsh.enabled = true; bsh.shapeType = ParticleSystemShapeType.Circle; bsh.radius = .4f;
            Set(0, 0, 0, true);
        }

        /// <summary>ความทึบของต้นไม้ทั้งต้น (1 = เห็นเต็ม, 0 = หายไป) — ฉากจบใช้จางต้นไม้ออกให้เหลือแต่เมืองที่ฟื้นแล้ว</summary>
        public float Fade = 1f;

        public void Reset() { shown = 0; receive = 0; pulse = 0; stageNow = 0; Fade = 1f; SeedKit.Rate(leaves, 0); Set(0, 0, 0, true); }

        /// เปลี่ยนระดับ / PERFECT SYNC: พลุแสง + เด้ง
        public void Burst(int count = 60) { burst.Emit(count); pulse = 1; }

        public void Celebrate(bool on) => SeedKit.Rate(leaves, on ? 25 : 0);

        /// growth 0..1, receiving 0..1 (กำลังได้รับน้ำ/แสงมากแค่ไหน)
        public void Set(float growth, float receiving, float dt, bool snap = false)
        {
            shown = snap ? growth : SeedKit.Damp(shown, growth, 4f, dt);      // โตแบบนุ่ม ไม่กระโดด
            receive = SeedKit.Damp(receive, receiving, 6f, dt);
            pulse = Mathf.Max(0, pulse - dt * 2.2f);

            int st = StageOf(shown);
            if (st != stageNow) { if (st > stageNow && !snap) Burst(70); stageNow = st; }

            // ระดับปัจจุบันจางเข้า ระดับเก่าจางออก (ไม่ตัดวูบ)
            float height = 0;
            for (int i = 0; i < Stages; i++)
            {
                weight[i] = snap ? (i == st ? 1 : 0) : SeedKit.Damp(weight[i], i == st ? 1 : 0, 5f, dt);
                float end = i + 1 < Stages ? StageStart[i + 1] : 1.0001f;
                float inStage = Mathf.Clamp01(Mathf.InverseLerp(StageStart[i], end, shown));
                float scale = Mathf.Lerp(.8f, 1f, inStage) * (i == st ? 1 : Mathf.Lerp(1.08f, 1f, weight[i]));
                trees[i].transform.localScale = glowTrees[i].transform.localScale = Vector3.one * scale;
                trees[i].color = new Color(1, 1, 1, weight[i] * Fade);
                glowTrees[i].color = new Color(.55f, .75f, .35f, weight[i] * (.45f * receive + .6f * pulse) * Fade);
                height += stageH[i] * scale * weight[i];
            }
            Target = Base + new Vector3(0, Mathf.Max(.6f, height * .55f), 0);

            // เด้งเบา ๆ ตอนได้พลัง + ไหวตามลมตลอดเวลา
            float s = 1 + .04f * receive * Mathf.Sin(Time.time * 14) + .1f * UIKit.EaseOutBack(1 - pulse) * pulse;
            sway.localScale = new Vector3(s, s, 1);
            sway.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.3f) * (.6f + 1.2f * shown));

            glow.transform.position = Target;
            glow.color = new Color(.8f, 1f, .6f, (.15f + .15f * shown + .55f * receive + .5f * pulse) * Fade);
            glow.transform.localScale = Vector3.one * (1.3f + height * .9f);
            sparks.transform.position = Target; burst.transform.position = Target;
            var sh = sparks.shape; sh.radius = .5f + height * .35f;
            SeedKit.Rate(sparks, 40 * receive);
        }
    }
}
