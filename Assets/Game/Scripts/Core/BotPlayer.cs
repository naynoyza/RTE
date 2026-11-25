// =====================================================================================================
// BotPlayer — บอตเล่นแทนผู้เล่น (ไว้ทดสอบเกมโดยไม่ต้องมีคนยืนหน้ากล้อง)
// -----------------------------------------------------------------------------------------------------
// เปิด/ปิด : F3        = บอตเล่นทั้ง P1 และ P2 (ไม่ต้องรัน tracker — เกมถือว่ากล้องต่ออยู่)
//           Shift+F3  = บอตเฉพาะ P1 เล่นกับคนจริง (คนจริงยืนฝั่งขวาของกล้อง = P2, ต้องรัน tracker)
//           มีป้าย "BOT ..." ล่างกลางจอตอนเปิด
// ทำอะไร  : สร้างข้อมูลผู้เล่นปลอมทับข้อมูลจากกล้องทุกเฟรม (เหมือน tracker ส่งมาจริง)
//           บอตแต่ละคนเล่นแบบคนจริง ไม่ใช่เครื่องจักร (ดู Profile):
//           - เห็นเป้าใหม่แล้วต้องใช้เวลาตอบสนองก่อนขยับมือ, มือเคลื่อนเร็วจำกัด เลยเป้านิดหน่อย และสั่น
//           - บางครั้งเล็งเพี้ยน, เหนื่อยแล้วลดแขนพัก, ย่ำเท้าไม่สม่ำเสมอและหยุดพักบ้าง
//           หน้าเลือกตัวละคร : ลังเล 1–3 วิ แล้วยกแขนเลือกตัวที่อีกคนยังไม่เอา
//           เกม 1 (Waste Hunters) : เล็งขยะตาม PlayerLane.BotTarget (ไม่เล็งขยะช็อตไฟ) ไม่มีขยะก็กวาดมือหา
//           เกม 2 (Last Seed)     : ทำท่าที่ LastSeed บอก (WantPose) แต่ช้ากว่าคำสั่ง และท่าหลุดบ้าง
// ใช้ยังไง : ฉากต่าง ๆ ตั้ง Ping/เป้า ให้ทุกเฟรม เช่น
//           if (BotPlayer.Controls(i)) { BotPlayer.AimPing[i] = Time.unscaledTime; BotPlayer.AimTarget[i] = botTarget; }
//           แล้ว TrackingReceiver เรียก BotPlayer.Fill(Players[i], i, dt) ให้เอง
// หมายเหตุ: เป็นเครื่องมือทดสอบเท่านั้น (สร้าง array ใหม่ทุกเฟรมได้ ไม่ใช่โค้ดเกมจริง)
// =====================================================================================================
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RTE
{
    public static class BotPlayer
    {
        /// <summary>เปิดบอตอยู่ไหม</summary>
        public static bool On;
        /// <summary>true = บอตเล่นทั้ง 2 คน, false = บอตเฉพาะ P1</summary>
        public static bool Both = true;
        /// <summary>แสดงป้าย "BOT ..." ล่างจอไหม (ปิดตอนแคปภาพทำรายงาน)</summary>
        public static bool ShowLabel = true;
        /// <summary>บอตคุมผู้เล่นช่องนี้ (0 = P1, 1 = P2) อยู่ไหม — ตัวอย่าง: if (BotPlayer.Controls(1)) ...</summary>
        public static bool Controls(int index) => On && (Both || index == 0);

        // ฉากต่าง ๆ บอกบอตว่าตอนนี้ควรทำอะไร (ตั้งค่าทุกเฟรมขณะที่หน้านั้นทำงาน) — index 0 = P1, 1 = P2
        public static readonly float[] SelectPing = { -99, -99 };                 // CharacterSelect กำลังรอเลือกตัว
        public static readonly Func<Vector2?>[] AimTarget = new Func<Vector2?>[2]; // เกม 1: ขยะเป้าหมาย (0..1 ของจอตัวเอง) หรือ null
        public static readonly float[] AimPing = { -99, -99 };                    // เกม 1 กำลังเล่น/ซ้อม
        public static readonly SeedPose[] WantPose = new SeedPose[2];             // เกม 2: ท่าที่ต้องการ
        public static readonly float[] PosePing = { -99, -99 };

        /// <summary>เกม 2: เกจเต็มแล้วรอเพื่อนผลักได้นานสุดกี่วินาทีก่อนผลักเอง (ตามนิสัยของบอตคนนั้น)</summary>
        public static float Patience(int index) => Brains[index].Prof.Patience;

        // นิสัยการเล่นของบอตแต่ละคน
        public class Profile
        {
            public string Name;
            public Vector2 React;          // เวลาตอบสนองเมื่อเปลี่ยนเป้า (วินาที, สุ่มในช่วง)
            public float AimSpeed;         // มือเคลื่อนเร็วสุด (สัดส่วนจอ/วินาที)
            public float Overshoot;        // เลยเป้าไปกี่ % ของระยะที่ขยับ
            public float Tremor;           // มือสั่น (สัดส่วนจอ)
            public float MissChance;       // โอกาสเล็งเพี้ยนเมื่อเปลี่ยนเป้า
            public Vector2 RestEvery, RestFor;   // ลดแขนพัก: ทุก ๆ / นาน (วินาที)
            public float StepRate;         // ย่ำเท้ากี่ก้าว/วินาที (เฉลี่ย)
            public Vector2 PauseEvery, PauseFor; // หยุดย่ำ: ทุก ๆ / นาน (วินาที)
            public Vector2 SelectDelay;    // ลังเลก่อนยกแขนเลือกตัวละคร
            public Vector2 PoseDelay;      // เปลี่ยนท่าช้ากว่าคำสั่ง (เกม 2)
            public float PoseSlip;         // โอกาสท่า V หลุดต่อวินาที (เกม 2)
            public float Patience;         // รอเพื่อนผลักนานสุด (เกม 2)
        }

        public static readonly Profile Skilled = new Profile
        {
            Name = "เก่ง", React = new Vector2(.25f, .45f), AimSpeed = 1.3f, Overshoot = .08f, Tremor = .006f, MissChance = .12f,
            RestEvery = new Vector2(25, 40), RestFor = new Vector2(.8f, 1.6f), StepRate = 2.6f,
            PauseEvery = new Vector2(15, 25), PauseFor = new Vector2(1, 2.5f), SelectDelay = new Vector2(1, 2),
            PoseDelay = new Vector2(.25f, .5f), PoseSlip = .04f, Patience = 2.5f,
        };
        public static readonly Profile Casual = new Profile
        {
            Name = "มือใหม่", React = new Vector2(.4f, .8f), AimSpeed = .85f, Overshoot = .18f, Tremor = .012f, MissChance = .25f,
            RestEvery = new Vector2(12, 22), RestFor = new Vector2(1.2f, 2.8f), StepRate = 1.9f,
            PauseEvery = new Vector2(8, 16), PauseFor = new Vector2(1.5f, 3.5f), SelectDelay = new Vector2(1.5f, 3),
            PoseDelay = new Vector2(.4f, .9f), PoseSlip = .12f, Patience = 1.6f,
        };

        static readonly Brain[] Brains = { new Brain(0, Skilled), new Brain(1, Casual) };

        /// <summary>เขียนข้อมูลผู้เล่นปลอมของช่อง index ลงใน p (เรียกจาก TrackingReceiver ทุกเฟรม)</summary>
        public static void Fill(TrackedPlayer p, int index, float dt) => Brains[index].Fill(p, dt);

        /// <summary>ชื่อนิสัยบอตช่องนี้ (แสดงบนป้าย BOT)</summary>
        public static string ProfileName(int index) => Brains[index].Prof.Name;

        // ================================================================== บอต 1 คน
        class Brain
        {
            public readonly Profile Prof;
            readonly int index;
            readonly float seed;
            readonly float[] kp = new float[51];
            readonly TrackedHand noHand = new TrackedHand();

            // ย่ำเท้า
            int steps; float stepClock, walkPhase, nextPause, pauseLeft;
            // เล็ง
            Vector2 aim = new Vector2(.5f, .5f), vel, committed = new Vector2(.5f, .5f), seen = new Vector2(-1, -1), missOffset, overshoot;
            float reactLeft, nextRest, restLeft;
            bool hadTarget;
            // เลือกตัวละคร
            float selectSince = -99, lastSelectPing = -99;
            // เกม 2
            SeedPose shownPose = SeedPose.None, pendingPose = SeedPose.None;
            float poseWait, slipLeft;

            public Brain(int index, Profile prof)
            {
                this.index = index; Prof = prof; seed = 17.3f + index * 41.7f;
                nextPause = Random.Range(prof.PauseEvery.x, prof.PauseEvery.y);
                nextRest = Random.Range(prof.RestEvery.x, prof.RestEvery.y);
            }

            float Cx => index == 0 ? .28f : .72f;   // ยืนฝั่งของตัวเองในภาพกล้อง
            const float Sw = .10f;                 // ความกว้างไหล่

            public void Fill(TrackedPlayer p, float dt)
            {
                float now = Time.unscaledTime;
                bool selecting = now - SelectPing[index] < .3f;
                bool aiming = now - AimPing[index] < .3f;
                bool posing = now - PosePing[index] < .3f;

                bool stepping = aiming && Walk(dt);
                bool handUp = aiming && Arm(now, dt);
                int raise = Select(selecting, now);
                SeedPose pose = posing ? Pose(dt) : SeedPose.None;

                BuildSkeleton(raise, pose, handUp, stepping);
                p.slot = index + 1;
                p.present = true;
                p.dist = 2.5f + .08f * Mathf.Sin(now * .4f + seed);     // คนจริงโยกตัวเล็กน้อย
                p.approach = -1;
                p.steps = steps;
                p.hand = handUp;
                p.use = handUp ? 1 : -1;
                p.x = aim.x; p.y = aim.y;
                p.kp = (float[])kp.Clone();
                p.handL = p.handR = noHand;
            }

            // ย่ำเท้า: จังหวะไม่สม่ำเสมอ และหยุดพักเป็นระยะ — คืนค่า true ถ้ากำลังย่ำอยู่
            bool Walk(float dt)
            {
                if (pauseLeft > 0) { pauseLeft -= dt; return false; }
                if ((nextPause -= dt) <= 0)
                {
                    pauseLeft = Random.Range(Prof.PauseFor.x, Prof.PauseFor.y);
                    nextPause = Random.Range(Prof.PauseEvery.x, Prof.PauseEvery.y);
                    return false;
                }
                float rate = Prof.StepRate * (.75f + .5f * Mathf.PerlinNoise(seed, Time.unscaledTime * .3f));
                stepClock += dt;
                if (stepClock > 1f / rate) { stepClock = 0; steps++; }
                walkPhase += dt * Mathf.PI * rate;
                return true;
            }

            // มือเล็ง: ตอบสนองช้า, เลยเป้า, สั่น, เล็งเพี้ยนบ้าง, ลดแขนพักบ้าง — คืนค่า true ถ้ายกมืออยู่
            bool Arm(float now, float dt)
            {
                if (restLeft > 0) { restLeft -= dt; hadTarget = false; return false; }
                if ((nextRest -= dt) <= 0)
                {
                    restLeft = Random.Range(Prof.RestFor.x, Prof.RestFor.y);
                    nextRest = Random.Range(Prof.RestEvery.x, Prof.RestEvery.y);
                    return false;
                }

                var t = AimTarget[index]?.Invoke();
                if (t.HasValue)
                {
                    // เป้าใหม่ (กระโดดไกล) = ต้องตอบสนองก่อน, เป้าเดิมที่ค่อย ๆ เลื่อน = ตามไปเรื่อย ๆ
                    if (!hadTarget || Vector2.Distance(t.Value, seen) > .1f)
                    {
                        reactLeft = Random.Range(Prof.React.x, Prof.React.y);
                        missOffset = Random.value < Prof.MissChance ? Random.insideUnitCircle.normalized * Random.Range(.06f, .11f) : Vector2.zero;
                    }
                    seen = t.Value; hadTarget = true;
                    if (reactLeft > 0 && (reactLeft -= dt) <= 0)
                        overshoot = (seen + missOffset - aim) * Prof.Overshoot;   // เพิ่งตัดสินใจ: เหวี่ยงมือเลยเป้านิดหน่อย
                    if (reactLeft <= 0) committed = seen + missOffset + overshoot;
                    missOffset = Vector2.MoveTowards(missOffset, Vector2.zero, dt * .05f);  // ค่อย ๆ แก้เล็งเพี้ยน
                }
                else
                {
                    hadTarget = false;
                    committed = new Vector2(.5f + .3f * Mathf.Sin(now * .6f + seed), .35f + .1f * Mathf.Sin(now * 1.1f + seed)); // กวาดมือหาขยะ
                }
                overshoot = Vector2.MoveTowards(overshoot, Vector2.zero, dt * .25f);       // แล้วค่อยดึงกลับเข้าเป้า

                // สปริงหน่วง + จำกัดความเร็ว (มือคนไม่วาร์ป)
                vel += ((committed - aim) * 40f - vel * 11f) * dt;
                if (vel.magnitude > Prof.AimSpeed) vel = vel.normalized * Prof.AimSpeed;
                aim += vel * dt;

                var tremor = new Vector2(Mathf.PerlinNoise(seed, now * 6) - .5f, Mathf.PerlinNoise(seed + 9, now * 6) - .5f) * (2 * Prof.Tremor);
                aim = new Vector2(Mathf.Clamp01(aim.x + tremor.x), Mathf.Clamp01(aim.y + tremor.y));
                return true;
            }

            // เลือกตัวละคร: ลังเลก่อน แล้วยกแขนข้างที่เอาตัวที่อีกคนยังไม่เลือก — คืนค่า 0 ซ้าย / 1 ขวา / -1 ไม่ยก
            int Select(bool selecting, float now)
            {
                if (!selecting || GameSession.Heroes[index] != Hero.None) return -1;
                if (now - lastSelectPing > 1f) selectSince = now + Random.Range(Prof.SelectDelay.x, Prof.SelectDelay.y);
                lastSelectPing = now;
                if (now < selectSince) return -1;
                return GameSession.Heroes[1 - index] == Hero.Female ? 0 : 1;
            }

            // เกม 2: ทำท่าตามคำสั่งแต่ช้ากว่า และท่า V หลุดบ้าง
            SeedPose Pose(float dt)
            {
                var want = WantPose[index];
                if (want != pendingPose) { pendingPose = want; poseWait = Random.Range(Prof.PoseDelay.x, Prof.PoseDelay.y); }
                if ((poseWait -= dt) <= 0) shownPose = pendingPose;

                if (slipLeft > 0) { slipLeft -= dt; return SeedPose.None; }
                if (shownPose == SeedPose.Charge && Random.value < Prof.PoseSlip * dt) { slipLeft = Random.Range(.3f, .8f); return SeedPose.None; }
                return shownPose;
            }

            void Set(int i, float x, float y) { kp[i * 3] = x; kp[i * 3 + 1] = y; kp[i * 3 + 2] = .95f; }

            // จุด YOLO: 0 จมูก 1-4 ตา/หู 5/6 ไหล่ 7/8 ศอก 9/10 ข้อมือ 11/12 สะโพก 13/14 เข่า 15/16 ข้อเท้า (y ลงล่าง)
            void BuildSkeleton(int raiseSide, SeedPose pose, bool aiming, bool stepping)
            {
                float sway = .006f * Mathf.Sin(Time.unscaledTime * 1.3f + seed);   // ตัวโยกเล็กน้อย
                float cx = Cx + sway;
                float l = cx - Sw * .5f, r = cx + Sw * .5f, sy = .40f;
                Set(0, cx, .30f); Set(1, cx - .012f, .29f); Set(2, cx + .012f, .29f); Set(3, cx - .025f, .30f); Set(4, cx + .025f, .30f);
                Set(5, l, sy); Set(6, r, sy);

                // แขนปกติ: ห้อยลง
                Vector2 le = new Vector2(l - .01f, .52f), lw = new Vector2(l - .005f, .63f);
                Vector2 re = new Vector2(r + .01f, .52f), rw = new Vector2(r + .005f, .63f);

                if (pose == SeedPose.Charge)          // ท่า V เหนือศีรษะ
                {
                    le = new Vector2(l - .05f, .29f); lw = new Vector2(l - .09f, .18f);
                    re = new Vector2(r + .05f, .29f); rw = new Vector2(r + .09f, .18f);
                }
                else if (pose == SeedPose.Push)       // แขนเหยียดตรงระดับไหล่
                {
                    le = new Vector2(l - .07f, sy + .01f); lw = new Vector2(l - .14f, sy + .02f);
                    re = new Vector2(r + .07f, sy + .01f); rw = new Vector2(r + .14f, sy + .02f);
                }
                else if (raiseSide >= 0)              // ยกแขนข้างเดียว (เลือกตัวละคร)
                {
                    if (raiseSide == 0) { le = new Vector2(l - .01f, .29f); lw = new Vector2(l - .02f, .19f); }
                    else { re = new Vector2(r + .01f, .29f); rw = new Vector2(r + .02f, .19f); }
                }
                else if (aiming)                      // มือขวายกไปข้างหน้าตามเป้า
                {
                    re = new Vector2(r + .03f, .47f);
                    rw = new Vector2(r + .02f + (aim.x - .5f) * .08f, .50f - (aim.y - .5f) * .12f);
                }
                Set(7, le.x, le.y); Set(8, re.x, re.y); Set(9, lw.x, lw.y); Set(10, rw.x, rw.y);

                // ขา: ย่ำเท้าสลับกัน
                float lift = stepping ? .03f : 0, a = Mathf.Max(0, Mathf.Sin(walkPhase)) * lift, b = Mathf.Max(0, -Mathf.Sin(walkPhase)) * lift;
                Set(11, cx - .035f, .62f); Set(12, cx + .035f, .62f);
                Set(13, cx - .04f, .79f - a); Set(14, cx + .04f, .79f - b);
                Set(15, cx - .045f, .95f - a * 1.5f); Set(16, cx + .045f, .95f - b * 1.5f);
            }
        }
    }
}
