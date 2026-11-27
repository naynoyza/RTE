// =====================================================================================================
// GuardianView — มือชุดอวกาศ + เอฟเฟกต์พลังของผู้เล่น 1 คน (P1 = Water Guardian ซ้าย, P2 = Sun Guardian ขวา)
// -----------------------------------------------------------------------------------------------------
// แขน   : ภาพจาก Ref/25 (ท่าชูแขน, มือเรืองแสงสีของตัวเอง) และ Ref/27 (ท่าผลัก/ยกฝ่ามือ แทน Ref/26)
//          สลับภาพแบบจางเข้า-ออกตามท่า, ไม่ทำท่า = แขนลดลงด้านล่าง, ขยับตามตำแหน่งมือจริงเล็กน้อย
// สะสมพลัง (ท่า V) — แทนภาพนิ่ง Ref/20, 22 ด้วย Particle ที่เคลื่อนไหวจริง:
//    น้ำ : ละอองน้ำลอยขึ้นจากพื้นวนขึ้นไปรวม + ลูกบอลน้ำวน (เกลียวหมุนสวนกัน 2 ชั้น) + หยดน้ำโคจรรอบลูกบอล
//    แสง : ลำแสงพุ่งลงจากฟ้าเข้าหามือ + ดวงอาทิตย์ (รัศมีหมุน + แกนสว่างเต้นเป็นจังหวะ) + ประกายโคจร
// ส่งพลัง (ท่าผลัก) — แทนภาพนิ่ง Ref/21, 23:
//    สายน้ำ/ลำแสงเป็น LineRenderer โค้งจากฝ่ามือไปที่ต้นไม้ (ลาย Ref/28 น้ำสาด, Ref/29 ลำแสงดาวหาง) เลื่อนไหลตลอด
//    + หัวสาย = ภาพ Ref/28/29 ทั้งภาพ หมุนตามทิศทาง + แสงฟุ้งที่ฝ่ามือ + แสงกระทบที่ต้นไม้เมื่อถึง
//    + หยดน้ำ/ประกายหลุดออกตามเส้น ; ปลายสายวิ่งไปถึงต้นไม้ใน StreamTravelSeconds (Arrived = ถึงแล้ว)
//    หยุดผลัก -> หางสายไหลตามเข้าไปในต้นไม้จนหมด (ไม่หายวูบ)
// =====================================================================================================
using UnityEngine;

namespace RTE
{
    public class GuardianView
    {
        public bool Arrived => sending && head >= 1f;

        readonly bool water;
        readonly SeedSettings s;
        readonly Transform root;
        readonly SpriteRenderer armsCharge, armsPush;
        readonly Vector3 chargePos, pushPos, orbChargePos, palmPos;
        readonly Transform orb;
        readonly SpriteRenderer orbA, orbB, orbGlow;
        readonly ParticleSystem rise, orbit, along;
        readonly LineRenderer body, core;
        readonly SpriteRenderer headFx, palmGlow, impact;
        readonly float headAngle;
        float impactA;
        readonly Vector3[] pts;
        float wCharge, wPush, alpha = 1, orbSize, head, tail, spin;
        bool sending;
        Vector3 offset, orbPos;

        public GuardianView(Transform parent, int slot, SeedSettings settings)
        {
            s = settings;
            water = slot == 1;
            var tint = water ? SeedSettings.Water : SeedSettings.Sun;
            root = new GameObject(water ? "WaterGuardian" : "SunGuardian").transform;
            root.SetParent(parent, false);

            chargePos = SeedKit.BoxCenter($"arms_charge_p{slot}");
            pushPos = SeedKit.BoxCenter($"arms_push_p{slot}");
            orbChargePos = water ? SeedKit.Px(420, 300) : SeedKit.Px(1520, 300);   // ระหว่างฝ่ามือทั้งสองตอนชูแขน (ภาพมือชุดใหม่)
            palmPos = water ? SeedKit.Px(560, 490) : SeedKit.Px(1385, 490);       // ฝ่ามือด้านในตอนผลัก
            orbPos = orbChargePos;

            armsCharge = SeedKit.MakeSprite("ArmsCharge", root, SeedKit.Spr($"arms_charge_p{slot}", new Vector2(.5f, .5f)), SeedKit.OrderArms);
            armsPush = SeedKit.MakeSprite("ArmsPush", root, SeedKit.Spr($"arms_push_p{slot}", new Vector2(.5f, .5f)), SeedKit.OrderArms + 1);

            // ---------------- ลูกบอลพลัง
            orb = new GameObject("Orb").transform; orb.SetParent(root, false);
            orbGlow = SeedKit.MakeSprite("Glow", orb, SeedKit.Spr("fx_soft", new Vector2(.5f, .5f), 40), SeedKit.OrderHandFx, true);
            orbA = SeedKit.MakeSprite("LayerA", orb, SeedKit.Spr(water ? "fx_swirl" : "fx_rays", new Vector2(.5f, .5f), water ? 140 : 120), SeedKit.OrderHandFx + 1, true);
            orbB = SeedKit.MakeSprite("LayerB", orb, SeedKit.Spr(water ? "fx_swirl" : "fx_soft", new Vector2(.5f, .5f), water ? 180 : 42), SeedKit.OrderHandFx + 2, true);
            orbGlow.color = new Color(tint.r, tint.g, tint.b, water ? .7f : 1f);
            orbA.color = water ? new Color(.6f, .9f, 1f) : new Color(1f, .85f, .4f);
            orbB.color = water ? new Color(.85f, .97f, 1f, .8f) : new Color(1f, .98f, .85f);

            orbit = SeedKit.Particles("Orbit", orb, water ? "fx_drop" : "fx_spark", true, SeedKit.OrderHandFx + 3, 1.2f, water ? .22f : .3f, 0, water ? new Color(.75f, .95f, 1f) : new Color(1f, .9f, .5f));
            var osh = orbit.shape; osh.enabled = true; osh.shapeType = ParticleSystemShapeType.Circle; osh.radius = 1.1f;
            var ov = orbit.velocityOverLifetime; ov.enabled = true; ov.space = ParticleSystemSimulationSpace.Local;
            ov.orbitalX = ov.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            ov.orbitalZ = new ParticleSystem.MinMaxCurve(water ? 3.5f : -2.5f); ov.radial = new ParticleSystem.MinMaxCurve(-.6f);
            var om = orbit.main; om.simulationSpace = ParticleSystemSimulationSpace.Local;

            // ---------------- ระหว่างชาร์จ: น้ำลอยขึ้นจากพื้น / แสงพุ่งลงจากฟ้า
            if (water)
            {
                rise = SeedKit.Particles("WaterRise", root, "fx_drop", true, SeedKit.OrderArms - 1, 1.6f, .28f, 0, new Color(.6f, .88f, 1f), 600);
                rise.transform.position = SeedKit.Px(430, 980);
                var sh = rise.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(6f, .3f, .1f);
                var v = rise.velocityOverLifetime; v.enabled = true;
                v.x = new ParticleSystem.MinMaxCurve(-.3f, .3f); v.y = new ParticleSystem.MinMaxCurve(3.5f, 6f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                v.orbitalX = v.orbitalZ = new ParticleSystem.MinMaxCurve(0f); v.orbitalY = new ParticleSystem.MinMaxCurve(2f);                                // ส่ายวนเป็นเกลียว
                var n = rise.noise; n.enabled = true; n.strength = .6f; n.frequency = .6f; n.scrollSpeed = .5f;
            }
            else
            {
                rise = SeedKit.Particles("SunRays", root, "fx_streak", true, SeedKit.OrderHandFx - 1, .9f, .9f, 0, new Color(1f, .85f, .45f), 300);
                rise.transform.position = SeedKit.Px(1480, -60);
                var sh = rise.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(5f, .2f, .1f);
                var v = rise.velocityOverLifetime; v.enabled = true;
                v.y = new ParticleSystem.MinMaxCurve(-10f, -7f); v.x = new ParticleSystem.MinMaxCurve(-.6f, .6f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var r = rise.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .25f; r.lengthScale = 1f;
            }

            // ---------------- สายน้ำ / ลำแสง ไปที่ต้นไม้
            if (water)
            {
                body = SeedKit.Line("WaterStream", root, "fx_water_strip2", true, SeedKit.OrderStream, new Color(1f, 1f, 1f, 1f), 1.8f);
                core = SeedKit.Line("WaterShine", root, "fx_water_strip", true, SeedKit.OrderStream + 1, new Color(.5f, .85f, 1f, .45f), 2.6f);
                body.widthCurve = new AnimationCurve(new Keyframe(0, 1.15f), new Keyframe(.5f, .8f), new Keyframe(1, .55f));
                core.widthCurve = new AnimationCurve(new Keyframe(0, .5f), new Keyframe(1, .25f));
            }
            else
            {
                body = SeedKit.Line("SunBeam", root, "fx_beam_strip2", true, SeedKit.OrderStream, new Color(1f, .95f, .8f, 1f), 2.4f);
                core = SeedKit.Line("SunCore", root, "fx_beam_strip", true, SeedKit.OrderStream + 1, new Color(1f, 1f, .85f, .8f), 3.4f);
                body.widthCurve = new AnimationCurve(new Keyframe(0, 2.2f), new Keyframe(1, .9f));
                core.widthCurve = new AnimationCurve(new Keyframe(0, .7f), new Keyframe(1, .3f));
            }
            pts = new Vector3[body.positionCount];

            // หัวสาย (ภาพเต็มจาก Ref/28, 29) — pivot = จุดหัวในภาพ, headAngle = ทิศทางของภาพ (องศา)
            headFx = SeedKit.MakeSprite("StreamHead", root,
                SeedKit.Spr(water ? "fx_water_head" : "fx_sun_head", water ? new Vector2(.91f, .12f) : new Vector2(.13f, .15f), water ? 260 : 240),
                SeedKit.OrderStream + 3, true);
            headAngle = water ? -32.6f : -147.8f;
            headFx.color = Color.clear;
            palmGlow = SeedKit.MakeSprite("PalmGlow", root, SeedKit.Spr("fx_soft", new Vector2(.5f, .5f), 40), SeedKit.OrderHandFx - 2, true);
            palmGlow.color = Color.clear;
            impact = SeedKit.MakeSprite("Impact", root, SeedKit.Spr("fx_soft", new Vector2(.5f, .5f), 40), SeedKit.OrderTreeFx + 3, true);
            impact.color = Color.clear;
            along = SeedKit.Particles("Along", root, water ? "fx_drop" : "fx_spark", true, SeedKit.OrderStream + 2, .7f, water ? .2f : .28f, 0,
                                     water ? new Color(.8f, .96f, 1f) : new Color(1f, .92f, .55f), 500);
        }

        /// <summary>ซ่อน/แสดงแขนและพลังทั้งหมด (ฉากจบและหน้าสรุปผลซ่อน เพื่อให้เห็นต้นไม้เต็ม ๆ)</summary>
        public void SetVisible(bool on) => root.gameObject.SetActive(on);

        public void Reset()
        {
            sending = false; head = tail = 0; orbSize = 0; wCharge = 0; wPush = 0; offset = Vector3.zero;
            body.enabled = core.enabled = false;
            headFx.color = palmGlow.color = impact.color = Color.clear; impactA = 0;
        }

        public void Tick(SeedPose pose, float gauge, bool send, Vector2 reach, bool lost, float dt)
        {
            // ---------------- แขน: สลับภาพตามท่า + ขยับตามมือจริงเล็กน้อย
            wCharge = SeedKit.Damp(wCharge, pose == SeedPose.Charge ? 1 : 0, 12, dt);
            wPush = 1 - wCharge;
            float lower = pose == SeedPose.None ? 2.6f : 0;                   // ไม่ทำท่า = แขนลดลง
            Vector2 follow = lost ? Vector2.zero : new Vector2(Mathf.Clamp(reach.x * .35f, -.6f, .6f),
                                                                Mathf.Clamp((reach.y - (pose == SeedPose.Charge ? 1.6f : .3f)) * .35f, -.5f, .5f));
            offset = SeedKit.Damp(offset, new Vector3(follow.x, follow.y - lower, 0), 8, dt);
            alpha = SeedKit.Damp(alpha, lost ? .35f : 1, 5, dt);
            float bob = Mathf.Sin(Time.time * 2.1f + (water ? 0 : 1.7f)) * .05f;
            armsCharge.transform.position = chargePos + offset + new Vector3(0, bob + (1 - wCharge) * -1.2f, 0);
            armsPush.transform.position = pushPos + offset + new Vector3(0, bob + (1 - wPush) * -1.2f, 0);
            armsCharge.color = new Color(1, 1, 1, wCharge * alpha);
            armsPush.color = new Color(1, 1, 1, wPush * alpha);

            // ---------------- ลูกบอลพลัง: โตตามเกจ, ย้ายไปที่ฝ่ามือตอนผลัก
            bool charging = pose == SeedPose.Charge && !lost;
            float targetSize = gauge > .001f ? .35f + .85f * gauge : 0;
            if (gauge >= 1) targetSize *= 1 + .06f * Mathf.Sin(Time.time * 9);    // เต็ม = เต้นตุบ ๆ (พร้อมส่ง)
            orbSize = SeedKit.Damp(orbSize, targetSize, 8, dt);
            orbPos = SeedKit.Damp(orbPos, (pose == SeedPose.Push ? palmPos : orbChargePos) + offset * .8f, 9, dt);
            orb.position = orbPos;
            orb.localScale = Vector3.one * orbSize;
            spin += dt * (charging ? 260 : 110);
            orbA.transform.localRotation = Quaternion.Euler(0, 0, water ? -spin : spin * .35f);
            orbB.transform.localRotation = Quaternion.Euler(0, 0, water ? spin * 1.4f : 0);
            if (!water) orbB.transform.localScale = Vector3.one * (1 + .12f * Mathf.Sin(Time.time * 6));
            orbGlow.transform.localScale = Vector3.one * (1.3f + .15f * Mathf.Sin(Time.time * 4));
            SeedKit.Rate(orbit, orbSize > .05f ? (charging ? 45 : 14) : 0);
            SeedKit.Rate(rise, charging && gauge < 1 ? (water ? 90 : 26) : 0);

            // ---------------- สายพลังไปที่ต้นไม้
            if (send && !sending) { sending = true; head = tail = 0; }
            if (!send && sending) sending = false;
            float speed = 1f / Mathf.Max(.05f, s.StreamTravelSeconds);
            if (sending) head = Mathf.Min(1, head + speed * dt);
            else if (head > 0) { tail = Mathf.Min(head, tail + speed * dt); if (tail >= head) head = tail = 0; }
            DrawStream(dt);
        }

        Vector3 Curve(float t, Vector3 from)
        {
            Vector3 to = TreeView.Target + new Vector3(water ? -.25f : .25f, 0, 0);
            Vector3 mid = (from + to) * .5f + new Vector3(0, 1.4f, 0);
            Vector3 p = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * mid + t * t * to;
            Vector3 dir = (2 * (1 - t) * (mid - from) + 2 * t * (to - mid)).normalized;
            Vector3 normal = new Vector3(-dir.y, dir.x, 0);
            float wave = Mathf.Sin(t * 11 - Time.time * (water ? 9 : 5)) * Mathf.Sin(t * Mathf.PI) * (water ? .18f : .07f);
            return p + normal * wave;
        }

        void DrawStream(float dt)
        {
            bool visible = head > tail + .01f;
            body.enabled = core.enabled = visible;
            var tint = water ? SeedSettings.Water : SeedSettings.Sun;

            // แสงกระทบต้นไม้: สว่างขึ้นเมื่อสายถึงต้น ค่อย ๆ จางเมื่อหยุด
            impactA = SeedKit.Damp(impactA, Arrived ? 1 : 0, Arrived ? 10 : 3, dt);
            impact.transform.position = TreeView.Target + new Vector3(water ? -.25f : .25f, 0, 0);
            impact.transform.localScale = Vector3.one * (2.2f + .5f * Mathf.Sin(Time.time * 18));
            impact.color = new Color(tint.r, tint.g, tint.b, .9f * impactA);
            palmGlow.transform.position = orbPos;
            palmGlow.transform.localScale = Vector3.one * (2.4f + .3f * Mathf.Sin(Time.time * 12));
            palmGlow.color = new Color(tint.r, tint.g, tint.b, visible && sending ? .85f : 0);

            if (!visible) { headFx.color = Color.clear; return; }
            Vector3 from = orbPos;
            for (int i = 0; i < pts.Length; i++)
                pts[i] = Curve(Mathf.Lerp(tail, head, i / (pts.Length - 1f)), from);
            body.SetPositions(pts); core.SetPositions(pts);

            // หัวสาย: อยู่ที่ปลาย หมุนตามทิศสาย ขนาด/ความสว่างกระเพื่อม
            var hp = pts[pts.Length - 1]; var hd = hp - pts[pts.Length - 3];
            headFx.transform.position = hp;
            headFx.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(hd.y, hd.x) * Mathf.Rad2Deg - headAngle);
            float hs = (water ? .8f : .9f) + .08f * Mathf.Sin(Time.time * (water ? 13 : 9));
            headFx.transform.localScale = new Vector3(hs, hs * (1 + .1f * Mathf.Sin(Time.time * 7)), 1);
            headFx.color = new Color(1, 1, 1, Mathf.Clamp01((head - tail) * 3) * (water ? 1f : .95f));

            // หยดน้ำ/ประกาย หลุดออกตามเส้น
            int n = Mathf.RoundToInt((water ? 70 : 50) * dt * 1.0f + Random.value);
            for (int k = 0; k < n; k++)
            {
                float t = Random.Range(tail, head);
                var p = Curve(t, from);
                var tangent = (Curve(Mathf.Min(1, t + .02f), from) - p).normalized;
                along.Emit(new ParticleSystem.EmitParams
                {
                    position = p + (Vector3)Random.insideUnitCircle * .2f,
                    velocity = tangent * Random.Range(2f, 4f) + (Vector3)Random.insideUnitCircle * 1.2f,
                    startLifetime = Random.Range(.35f, .7f),
                    startSize = Random.Range(.08f, water ? .22f : .3f),
                    startColor = water ? new Color(.8f, .96f, 1f) : new Color(1f, .92f, .55f)
                }, 1);
            }
        }
    }
}
