// =====================================================================================================
// PlayerLane — จอครึ่งหนึ่งของผู้เล่น 1 คน (P1 = ครึ่งซ้าย, P2 = ครึ่งขวา)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : ทุกอย่างที่เกิดบนจอของผู้เล่นคนนั้น
//           • เดิน: ถนนเลื่อนตามความเร็วจาก WalkController
//           • ขยะ : เกิดตามระยะทาง เลื่อนเข้าหาผู้เล่น เลยตัวไปแล้ว = หลุด (combo หาย)
//           • เล็ง: เป้าเล็งตามมือที่ยก แตะขยะ = ดูดทันที -> ได้คะแนน + นับเป็นรีไซเคิล (GOAL); เล็งพลาดขยะก็เลยตัวไป
//           • ระยะดูด: ขยะต้องเข้ามาใกล้กว่า SuckRange (สัดส่วนของระยะเกิด) จึงจะดูดได้
//           • ขยะที่เล็งยาก (ชิ้นเล็ก/ริมถนน/ขยะพิเศษ) เคลื่อนช้ากว่า จึงอยู่บนจอนานกว่า
//           • ปืน 3D (VacuumGun) ถือมือข้างที่ยก, ลำแสงดูดจากปากปืนถึงเป้า
// โหมดซ้อม (Practice): ขยะซ้อมโผล่ใกล้ ๆ และไม่เคลื่อน -> DidVacuum (ดูดได้ 1 ชิ้น), DidStep (ย่ำเท้าจนเร่ง)
// อีเวนต์ในเกม:
//   OVERDRIVE   : combo ครบ 8 -> 5 วิ วงดูดกว้างขึ้นเป็นวังวน ดูดทุกชิ้นในวงพร้อมกัน
//   DANGER      : แผงวงจรช็อตไฟโผล่เป็นระยะ ดูดเข้าไป = STUNNED 2 วิ (ปืนใช้ไม่ได้, combo หาย, หักคะแนน)
//   DUMP FEVER  : (สั่งจาก WasteEvents) ขยะร่วงจากฟ้าเป็นกอง + คะแนน x2
//   DUST STORM  : (สั่งจาก WasteEvents) ฝุ่นบังหน้ากาก ปัดมือซ้าย-ขวาเพื่อเช็ด -> VISOR CLEAR!
// ดึงข้อมูล: TrackedPlayer ของตัวเอง (present, hand, use, x, y, steps, dist, kp)
// ส่งออก  : Score, Recycled, ImpactCollected/ImpactSpawned (ให้หน้าสรุปผล)
// =====================================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class PlayerLane
    {
        public int Score, Recycled, Combo, ImpactCollected, ImpactSpawned;
        public bool Practice;                // โหมดซ้อมก่อนเริ่มเกม
        public bool DidVacuum, DidStep;      // ภารกิจซ้อม 2 อย่าง
        public bool Dusty => dust > 0;
        /// <summary>WasteEvents ตั้งค่าทุกเฟรม: ป้ายอีเวนต์รวม (FEVER/พายุฝุ่น) กำลังแสดงอยู่ — ป้ายรายฝั่งและคะแนนลอยจะเลื่อนลงไปใต้ป้าย</summary>
        public bool BannerUp;
        /// <summary>WasteHunters ตั้งค่าทุกเฟรม: ช่วง FINAL RUSH (ขยะถี่ขึ้น + คะแนนคูณ)</summary>
        public bool Rush;
        const float StatusH = 190;   // ความสูงป้ายสถานะ OVERDRIVE / STUNNED (ใช้ช่องเดียวกัน)
        const float EventY = 140, ToastY = -45, BannerShift = -150;   // ป้ายสถานะอยู่ใต้แถบ HUD ด้านบน, คะแนนลอยอยู่ใต้ป้าย
        float shiftY;

        // อีเวนต์
        const float OverdriveSeconds = 5f, StunSeconds = 2f, DustAutoClear = 12f;
        float feverTime, overdriveTime, stunTime, dust, dustAge, nextDangerIn, eventTextTime, lastAimX;
        readonly Image vortex, dustLayer, shock;
        readonly Image[] dustSpecks = new Image[14];
        readonly Text eventText, dustHint;
        // เอฟเฟกต์จาก UI/11.jpg
        readonly Image suckFx, eventImg, lightning, handIcon, drone;
        readonly Vector2 suckPivot;
        float eventImgTime, droneT = -1;

        readonly GameSettings settings;
        readonly int slot;
        readonly Color color;
        readonly RectTransform view, trashLayer;
        readonly RoadScroller road;
        readonly WalkController walk;
        readonly VacuumGun gun;
        readonly PlayerHud hud;
        readonly Image beam, crosshair, lockFill, crosshairDot;
        readonly Text hint, toast;
        readonly List<TrashPiece> pieces = new List<TrashPiece>();
        readonly System.Func<Vector2?> botTarget;   // ส่งให้ BotPlayer (สร้างครั้งเดียว ไม่สร้าง delegate ใหม่ทุกเฟรม)

        Vector2 aim;                 // เป้าเล็ง (พิกเซลจากกึ่งกลางจอครึ่ง)
        int hand = -1;               // มือที่ถือปืน 0 ซ้าย / 1 ขวา / -1 ไม่ได้ยก
        float nextSpawnIn, toastTime, toastDur = 1;

        const float PassZ = 1.2f;    // ขยะเข้าใกล้กว่านี้ = เดินผ่านไปแล้ว
        const float RoadHalf = 1.3f; // ครึ่งความกว้างถนน (เมตร)
        const float SuckTime = .35f;
        const float LaneW = 960, LaneH = 1080;

        public PlayerLane(GameSettings settings, Transform canvas, Transform hudParent, int slot)
        {
            this.settings = settings; this.slot = slot;
            color = GameSettings.PlayerColors[slot - 1];

            // กรอบจอครึ่ง (ตัดภาพที่ล้นออกด้วย RectMask2D)
            view = UIKit.Rect($"P{slot}View", canvas);
            view.anchorMin = new Vector2(slot == 1 ? 0 : .5f, 0); view.anchorMax = new Vector2(slot == 1 ? .5f : 1, 1);
            view.offsetMin = view.offsetMax = Vector2.zero;
            view.gameObject.AddComponent<RectMask2D>();

            // ลำดับชั้นจากล่างขึ้นบน: ถนน -> ขยะ -> ลำแสง -> ปืน -> เป้าเล็ง -> ข้อความ
            road = new RoadScroller(view, TrashPiece.HorizonY);
            trashLayer = UIKit.Rect("Trash", view); UIKit.Stretch(trashLayer);
            beam = UIKit.Img("SuctionBeam", view, UIKit.Load("fx_glow"), new Vector2(10, 90), false);
            beam.rectTransform.pivot = new Vector2(0, .5f);
            beam.color = Color.clear;
            gun = new VacuumGun(view, slot, color);

            // วงเป้าเล็ง = ระยะดูดจริง (เส้นวงใน sprite fx_ring อยู่ที่ ~89% ของขนาดรูป)
            float ring = settings.HitRadiusPx * 2f / .89f;
            // ลมดูด (หมุนวน) อยู่ใต้เป้าเล็ง หางชี้ไปที่ปากปืน
            suckPivot = slot == 1 ? new Vector2(.77f, .27f) : new Vector2(.72f, .25f);
            suckFx = UIKit.Img("SuckSwirl", view, UIKit.Load(slot == 1 ? "fx/suck_blue" : "fx/suck_orange"), new Vector2(440, 365), false);
            suckFx.rectTransform.pivot = suckPivot; suckFx.color = Color.clear;
            crosshair = UIKit.Img("Crosshair", view, UIKit.Load(slot == 1 ? "fx/aim_blue" : "fx/aim_orange"), new Vector2(ring * 1.45f, ring * 1.45f), false);
            lockFill = UIKit.Img("LockFill", crosshair.transform, UIKit.Load("fx_ring"), new Vector2(ring * 1.3f, ring * 1.3f), false);
            lockFill.type = Image.Type.Filled; lockFill.fillMethod = Image.FillMethod.Radial360;
            lockFill.fillOrigin = (int)Image.Origin360.Top; lockFill.color = new Color(.4f, 1f, .5f);
            crosshairDot = UIKit.Img("Dot", crosshair.transform, UIKit.Load("fx_circle"), new Vector2(14, 14), false);

            // วังวน OVERDRIVE (อยู่ใต้เป้าเล็ง)
            vortex = UIKit.Img("Vortex", view, UIKit.Load("fx/vortex"), new Vector2(ring * 3.4f, ring * 2.9f), false);
            vortex.color = Color.clear;
            vortex.transform.SetSiblingIndex(crosshair.transform.GetSiblingIndex());
            // ไฟช็อตตอน STUNNED
            shock = UIKit.Img("Shock", view, UIKit.Load("fx_glow"), new Vector2(900, 900), false);
            shock.rectTransform.anchoredPosition = new Vector2(0, -250); shock.color = Color.clear;
            lightning = UIKit.Img("Lightning", view, UIKit.Load("fx/lightning"), new Vector2(520, 450), false);
            lightning.color = Color.clear;
            // ฝุ่นบังหน้ากาก (DUST STORM)
            dustLayer = UIKit.Img("Dust", view, null, Vector2.zero, false); UIKit.Stretch(dustLayer.rectTransform);
            dustLayer.color = Color.clear;
            for (int i = 0; i < dustSpecks.Length; i++)
                dustSpecks[i] = UIKit.Img("Speck", dustLayer.transform, UIKit.Load("fx_circle"), Vector2.one * Random.Range(8, 26), false);
            handIcon = UIKit.Img("SwipeHand", view, UIKit.Load("fx/hand"), new Vector2(230, 200), false);
            handIcon.color = Color.clear;
            dustHint = UIKit.Label("DustHint", view, "ปัดมือไปมาเพื่อเช็ดหน้ากาก!", 46, new Color(.3f, 1f, 1f));
            dustHint.rectTransform.anchoredPosition = new Vector2(0, -120);
            // โดรนทิ้งขยะ (DUMP FEVER)
            drone = UIKit.Img("Drone", view, UIKit.Load("fx/drone"), new Vector2(330, 245), false);
            drone.color = Color.clear;

            hint = UIKit.Label("RaiseHandHint", view, "ยกมือข้างที่ถนัดขึ้นเพื่อเล็งปืนดูด", 40, new Color(1f, .95f, .6f));
            hint.rectTransform.anchoredPosition = new Vector2(0, -250);   // เหนือปืน ไม่ชนคะแนนลอย
            toast = UIKit.Label("Toast", view, "", 46, Color.white);
            toast.rectTransform.anchoredPosition = new Vector2(0, ToastY);
            eventText = UIKit.Label("EventText", view, "", 56, Color.white);
            eventText.rectTransform.anchoredPosition = new Vector2(0, EventY);
            eventImg = UIKit.Img("EventImage", view, null, new Vector2(460, 150), true);
            eventImg.rectTransform.anchoredPosition = new Vector2(0, EventY); eventImg.color = Color.clear;

            botTarget = BotTarget;
            walk = new WalkController(settings, slot);
            hud = new PlayerHud(hudParent, slot);
            Reset();
        }

        // เริ่มรอบใหม่: ลบขยะ ล้างคะแนน
        public void Reset()
        {
            foreach (var p in pieces) p.Destroy();
            pieces.Clear();
            Score = Recycled = Combo = ImpactCollected = ImpactSpawned = 0;
            walk.Reset();
            nextSpawnIn = 2; toastTime = 0; toast.text = "";
            DidVacuum = DidStep = false;
            feverTime = overdriveTime = stunTime = dust = dustAge = eventTextTime = shiftY = 0; BannerUp = Rush = false;
            nextDangerIn = Random.Range(14f, 20f);
            eventText.text = ""; vortex.color = shock.color = dustLayer.color = Color.clear;
            eventImgTime = 0; droneT = -1; eventImg.color = lightning.color = handIcon.color = drone.color = suckFx.color = Color.clear;
            foreach (var sp in dustSpecks) sp.color = Color.clear;
            dustHint.enabled = false;
            RefreshHud();
        }

        // ================================================================== ทุกเฟรม
        // playing = กำลังเล่น (เดิน/เกิดขยะ/เล็งได้), showWorld = แสดงขยะ+ปืน (รวมช่วงนับถอยหลัง)
        public void Tick(TrackedPlayer p, bool cameraOnline, bool playing, bool showWorld)
        {
            float dt = Time.deltaTime;
            bool present = p.present;
            if (playing && BotPlayer.Controls(slot - 1)) { BotPlayer.AimPing[slot - 1] = Time.unscaledTime; BotPlayer.AimTarget[slot - 1] = botTarget; }

            // 1) เดิน: ความเร็วจากการย่ำเท้า -> ถนนเลื่อน
            float metersThisFrame = walk.Tick(p, cameraOnline, playing, dt);
            road.SetDistance(walk.Walked);
            hud.SetSpeed(walk.Speed / settings.MaxSpeed, walk.Boost, playing);
            hud.SetSkeleton(present ? p.kp : null, dt, present ? p.pose : null);
            if (Practice && walk.Boost > .35f) DidStep = true;

            // 2) มือที่เล็ง: ตำแหน่งมือ 0..1 -> พิกเซลบนจอ (ทำให้นุ่มไม่กระตุก)
            hand = present && p.hand ? p.use : -1;
            if (hand >= 0)
            {
                var target = new Vector2((p.x - .5f) * LaneW, (p.y - .5f) * LaneH);
                aim = Vector2.Lerp(aim, target, 1 - Mathf.Exp(-dt * 18));
            }
            hud.SetWarning("");

            // 3) ข้อความเตือน
            UpdateToast(p, cameraOnline, playing, present, dt);

            // 4) ขยะ: เกิด, เลื่อน, เล็ง, ดูด
            TickEvents(playing, dt);
            bool canSuck = playing && hand >= 0 && stunTime <= 0;
            TrashPiece target2 = null;
            if (showWorld)
            {
                if (Practice) SpawnPractice();
                else if (playing) SpawnByDistance(metersThisFrame, dt);
                target2 = canSuck ? FindTargetUnderCrosshair() : null;
                MoveAndCollect(metersThisFrame, target2, canSuck && overdriveTime > 0, dt);
            }

            // 5) เป้าเล็ง ลำแสง และปืน
            DrawAim(showWorld, playing, present, target2);
            bool sucking = showWorld && hand >= 0 && stunTime <= 0 && (target2 != null || pieces.Exists(t => t.SuckProgress >= 0));
            // เสียงวน: ดูดขยะ / เดิน-ย่ำเท้า (ดังตามความเร็ว) / ลมพายุฝุ่น
            Sfx.Loop($"vac{slot}", "loop_vacuum", sucking, .45f);
            Sfx.Loop($"walk{slot}", "loop_walk", showWorld && present && walk.Boost > .15f, .2f + .35f * Mathf.Clamp01(walk.Boost));
            Sfx.Loop($"wind{slot}", "loop_wind", showWorld && dust > 0, .35f);
            gun.Update(aim, showWorld ? hand : -1, sucking, walk.Bob, dt);
            DrawBeam(sucking, target2 != null ? target2.ScreenPos : aim);
        }

        // ------------------------------------------------------------------ ขยะ
        // เกิดขยะทุก 2.2–3.8 เมตรที่เดิน
        void SpawnByDistance(float meters, float dt)
        {
            nextSpawnIn -= meters;
            if (nextSpawnIn <= 0)
            {
                var t = new TrashPiece(trashLayer, Random.Range(-RoadHalf, RoadHalf) * .9f, settings.TrashScale, settings.HardSlowdown);
                ImpactSpawned += t.BaseScore;
                pieces.Add(t);
                nextSpawnIn = Random.Range(2.2f, 3.8f) * (Rush ? settings.RushSpawnScale : 1f);
            }
        }

        // โหมดซ้อม: มีขยะซ้อม 1 ชิ้นอยู่ในระยะดูดเสมอ (ไม่เคลื่อน) จนกว่าจะดูดได้
        void SpawnPractice()
        {
            if (DidVacuum || pieces.Count > 0) return;
            var t = new TrashPiece(trashLayer, slot == 1 ? Random.Range(-.75f, -.15f) : Random.Range(.15f, .75f), settings.TrashScale, 0) { Z = TrashPiece.SpawnZ * settings.SuckRange * .85f, Pace = 0 };
            t.Place();
            pieces.Add(t);
        }

        // DUMP FEVER: ขยะร่วงจากฟ้าเป็นกองตรงหน้า (อยู่ในระยะดูดเกือบทั้งหมด)
        public void DumpBurst(int count)
        {
            droneT = 0;
            for (int i = 0; i < count; i++)
            {
                var t = new TrashPiece(trashLayer, Random.Range(-RoadHalf, RoadHalf) * .8f, settings.TrashScale, settings.HardSlowdown)
                    { Z = Random.Range(3.2f, 6.5f), Drop = 1f + i * .12f, Pace = .35f };
                ImpactSpawned += t.BaseScore; pieces.Add(t);
            }
        }

        public void StartFever(float seconds) { feverTime = seconds; }

        public void StartDust()
        {
            dust = 1; dustAge = 0;
            foreach (var sp in dustSpecks) sp.rectTransform.anchoredPosition = new Vector2(Random.Range(-440, 440), Random.Range(-420, 380));
        }

        public void SpawnDanger()
        {
            pieces.Add(new TrashPiece(trashLayer, Random.Range(-RoadHalf, RoadHalf) * .7f, settings.TrashScale, 0, true) { Pace = .8f });
            Sfx.Play("danger", .6f);
        }

        // ทดสอบ: จำลองว่าดูดขยะช็อตไฟเข้าไป
        public void DebugStun() { var t = new TrashPiece(trashLayer, 0, 1, 0, true); Collect(t); t.Destroy(); }

        public void StartOverdrive()
        {
            overdriveTime = OverdriveSeconds;
            Sfx.Play("overdrive");
            ShowEventImage("ui2/overdrive", new Vector2(StatusH * 3.04f, StatusH), 2.2f);   // ตำแหน่ง/ความสูงเดียวกับ STUNNED
        }

        // ป้ายอีเวนต์แบบรูป (เด้งขยายแล้วค้าง)
        void ShowEventImage(string sprite, Vector2 size, float seconds)
        {
            eventText.text = ""; eventImg.sprite = UIKit.Load(sprite);
            eventImg.rectTransform.sizeDelta = size; eventImgTime = seconds;
        }

        void ShowEvent(string text, Color c, float seconds) { eventImgTime = 0; eventText.text = text; eventText.color = c; eventTextTime = seconds; }

        // ทุกเฟรม: นับเวลาอีเวนต์ของฝั่งนี้ และวาดเอฟเฟกต์
        void TickEvents(bool playing, float dt)
        {
            // ป้ายรายฝั่ง + คะแนนลอย หลบลงใต้ป้ายอีเวนต์รวมขณะที่ป้ายนั้นแสดงอยู่ (ไม่ให้ข้อความซ้อนกัน)
            shiftY = Mathf.MoveTowards(shiftY, BannerUp ? BannerShift : 0, dt * 900);
            eventImg.rectTransform.anchoredPosition = eventText.rectTransform.anchoredPosition = new Vector2(0, EventY + shiftY);
            // คะแนนลอย: ค่อย ๆ ลอยขึ้นและจางหาย
            //   (คำเตือนที่ส่งซ้ำทุกเฟรม toastDur สั้น = อยู่นิ่งและทึบ)
            bool floating = toastDur > .5f;
            float rise = floating ? 1 - Mathf.Clamp01(toastTime / toastDur) : 0;
            toast.rectTransform.anchoredPosition = new Vector2(0, ToastY + shiftY + rise * 55);
            var tc = toast.color; tc.a = floating ? Mathf.Clamp01(toastTime / .35f) : 1; toast.color = tc;

            if (eventTextTime > 0 && (eventTextTime -= dt) <= 0) eventText.text = "";
            eventText.rectTransform.localScale = Vector3.one * (1 + .05f * Mathf.Sin(Time.time * 10));
            if (feverTime > 0) feverTime -= dt;

            // ป้ายรูป: เด้งเข้า ค่อยจางออก
            if (eventImgTime > 0) eventImgTime -= dt;
            float ea = Mathf.Clamp01(eventImgTime / .4f);
            eventImg.color = new Color(1, 1, 1, ea);
            eventImg.rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(Time.time * 10));

            // โดรน: บินเข้าจากด้านข้าง ลอยค้างกลางบนระหว่าง FEVER แล้วบินออก
            if (droneT >= 0)
            {
                droneT += dt;
                float inX = slot == 1 ? -700 : 700;
                float x = droneT < 1f ? Mathf.Lerp(inX, 0, Mathf.SmoothStep(0, 1, droneT)) :
                          feverTime > 0 ? Mathf.Sin(droneT * 1.3f) * 120 : Mathf.Lerp(0, -inX, Mathf.Clamp01((droneT - 1) * .25f));
                drone.rectTransform.anchoredPosition = new Vector2(x, 330 + Mathf.Sin(droneT * 6) * 10);
                drone.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(droneT * 4) * 4);
                drone.color = Color.white;
                if (feverTime <= 0 && Mathf.Abs(x) > 650) { droneT = -1; drone.color = Color.clear; }
            }

            // ไฟช็อตตอน STUNNED (สายฟ้ากระตุกรอบปืน)
            if (stunTime > 0)
            {
                lightning.color = new Color(1, 1, 1, Random.value < .75f ? 1 : .3f);
                lightning.rectTransform.anchoredPosition = gun.NozzleLocal + new Vector2(Random.Range(-30, 30), 120 + Random.Range(-20, 20));
                lightning.rectTransform.localScale = new Vector3(Random.value < .5f ? 1 : -1, 1, 1) * Random.Range(.85f, 1.15f);
            }
            else lightning.color = Color.clear;

            // มือปัดตอนพายุฝุ่น
            handIcon.color = new Color(1, 1, 1, dust > 0 ? 1 : 0);
            handIcon.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.time * 5) * 90, 20);
            handIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(Time.time * 5) * 15);

            // DANGER: แผงวงจรช็อตไฟโผล่ทุก 14–22 วิ
            if (playing && !Practice && (nextDangerIn -= dt) <= 0) { SpawnDanger(); nextDangerIn = Random.Range(14f, 22f); }

            // OVERDRIVE: วังวนหมุนรอบเป้าเล็ง
            if (overdriveTime > 0) overdriveTime -= dt;
            bool od = overdriveTime > 0 && hand >= 0;
            vortex.color = od ? new Color(1f, 1f, 1f, .8f + .2f * Mathf.Sin(Time.time * 14)) : Color.clear;
            vortex.rectTransform.anchoredPosition = aim;
            vortex.rectTransform.localRotation = Quaternion.Euler(0, 0, -Time.time * 220);
            vortex.rectTransform.localScale = Vector3.one * (settings.OverdriveAimScale / 2.4f) * (od ? 1 + .08f * Mathf.Sin(Time.time * 9) : 1);   // วังวนย่อตามวงเล็ง (ภาพออกแบบไว้ที่ 2.4 เท่า)

            // STUNNED: ไฟช็อตกระตุกที่ปืน
            if (stunTime > 0) stunTime -= dt;
            shock.color = stunTime > 0 ? new Color(.85f, .3f, 1f, Random.value * .8f) : Color.clear;
            shock.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.value * 360);

            // DUST STORM: ปัดมือซ้าย-ขวา = เช็ด (ระยะมือที่ปัดรวม ~2600 px = สะอาด) หรือหายเองใน 12 วิ
            if (dust > 0)
            {
                dustAge += dt;
                if (hand >= 0) dust -= Mathf.Abs(aim.x - lastAimX) / 2600f;
                if (dustAge > DustAutoClear) dust -= dt;
                if (dust <= .05f) { dust = 0; ShowEventImage("fx/visor_tx", new Vector2(580, 111), 1.8f); }
            }
            lastAimX = aim.x;
            dustLayer.color = new Color(.78f, .5f, .25f, .82f * dust);
            foreach (var sp in dustSpecks) sp.color = new Color(.3f, .18f, .08f, .55f * dust);
            dustHint.enabled = dust > 0;
            dustHint.rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(Time.time * 8));
            dustHint.rectTransform.anchoredPosition = new Vector2(0, -130);
        }


        // ขยะที่ใกล้เป้าเล็งที่สุด (จุดกลางขยะต้องอยู่ในวงเป้าเล็ง และเข้ามาในระยะดูดแล้ว)
        TrashPiece FindTargetUnderCrosshair()
        {
            TrashPiece best = null; float bestDist = float.MaxValue;
            foreach (var t in pieces)
            {
                if (t.SuckProgress >= 0 || !InRange(t)) continue;
                float d = Vector2.Distance(t.ScreenPos, aim);
                if (d < HitRadius && d < bestDist) { best = t; bestDist = d; }
            }
            return best;
        }

        // บอต: ขยะดี (ไม่ช็อต) ที่ใกล้ผู้เล่นที่สุด -> ตำแหน่ง 0..1 ของจอตัวเอง
        Vector2? BotTarget()
        {
            TrashPiece best = null;
            foreach (var t in pieces)
                if (!t.Danger && t.SuckProgress < 0 && t.Drop <= 0 && (best == null || t.Z < best.Z)) best = t;
            if (best == null) return null;
            return new Vector2(best.ScreenPos.x / LaneW + .5f, best.ScreenPos.y / LaneH + .5f);
        }

        float HitRadius => settings.HitRadiusPx * (overdriveTime > 0 ? settings.OverdriveAimScale : 1f);

        // ขยะเข้ามาใกล้กว่า SuckRange x ระยะเกิด แล้วหรือยัง (เช่น 0.5 = ต้องเดินเข้าไปเกินครึ่งทาง)
        bool InRange(TrashPiece t) => t.Z <= TrashPiece.SpawnZ * settings.SuckRange;

        // เลื่อนขยะเข้าหาผู้เล่น, สะสมเวลาเล็ง, เริ่มดูดเมื่อครบ, ลบชิ้นที่เดินผ่านไปแล้ว
        void MoveAndCollect(float meters, TrashPiece target, bool vortexOn, float dt)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var t = pieces[i];
                if (t.SuckProgress >= 0)                                   // กำลังลอยเข้าปืน
                {
                    if (t.SuckToward(gun.NozzleLocal, dt, SuckTime)) { Collect(t); Remove(i); }
                    continue;
                }
                t.Z -= meters * t.Pace;
                if (t.IsRare && (t.RareLife -= dt) <= 0) { Remove(i); continue; }

                // เป้าเล็งแตะ = ดูดทันที
                if (t == target) { t.SuckProgress = 0; continue; }
                // OVERDRIVE: ดูดทุกชิ้นในวังวนพร้อมกัน (ยกเว้นขยะช็อตไฟ)
                if (vortexOn && !t.Danger && t.Drop <= 0 && InRange(t) && Vector2.Distance(t.ScreenPos, aim) < HitRadius) { t.SuckProgress = 0; continue; }

                if (t.Z < PassZ)                                           // เดินผ่านไปแล้ว
                {
                    if (!t.Danger) Combo = 0;                              // หลบขยะช็อตไฟได้ = ไม่เสีย combo
                    Remove(i); continue;
                }
                t.InRange = InRange(t);
                t.Place();
            }
            pieces.Sort((a, b) => b.Z.CompareTo(a.Z));                     // ชิ้นใกล้วาดทับชิ้นไกล
            for (int i = 0; i < pieces.Count; i++) pieces[i].Rect.SetSiblingIndex(i);
        }

        // ดูดสำเร็จ: คะแนน + โบนัส combo, นับเป็นรีไซเคิลทันที (ไม่มีถุง/ถังแล้ว)
        void Collect(TrashPiece t)
        {
            if (t.Danger)                                                  // ดูดขยะช็อตไฟ = โดนช็อต
            {
                stunTime = StunSeconds; Combo = 0; overdriveTime = 0;
                Sfx.Play("stun");
                Score = Mathf.Max(0, Score - TrashCatalog.DangerPenalty);
                ShowEventImage("ui2/stunned", new Vector2(StatusH * 2.88f, StatusH), StunSeconds);
                ShowToast($"-{TrashCatalog.DangerPenalty}  โดนไฟช็อต!", new Color(1f, .4f, .4f));
                RefreshHud(); return;
            }
            Sfx.Play(t.IsRare ? "rare" : "collect", .7f, Random.Range(.95f, 1.08f));
            if (Practice) { DidVacuum = true; ShowToast("เยี่ยม! ดูดได้แล้ว", new Color(.4f, 1f, .5f)); return; }
            Combo++;
            int bonus = TrashCatalog.ComboBonus(Combo);
            int mult = feverTime > 0 ? 2 : 1;                              // DUMP FEVER = คะแนน x2
            if (Rush) mult = Mathf.Max(mult, settings.RushScoreMultiplier);  // FINAL RUSH (ไม่ซ้อนกับ FEVER)
            Score += (t.Points + bonus) * mult; Recycled++; ImpactCollected += t.BaseScore;
            if (Combo % 8 == 0) StartOverdrive();
            ShowToast($"{(mult > 1 ? "2X " : "")}+{t.Points * mult}{(bonus > 0 ? $"  COMBO x{Combo} +{bonus}" : "")}\n{t.Kind.Name}{(t.IsRare ? "  ✦RARE" : "")}",
                      t.IsRare ? new Color(1f, .9f, .3f) : Color.white);
            RefreshHud();
        }

        void Remove(int i) { pieces[i].Destroy(); pieces.RemoveAt(i); }

        // ------------------------------------------------------------------ การแสดงผล
        void DrawAim(bool showWorld, bool playing, bool present, TrashPiece target)
        {
            bool show = showWorld && hand >= 0;
            float rs = overdriveTime > 0 ? settings.OverdriveAimScale : 1f;
            crosshair.enabled = lockFill.enabled = crosshairDot.enabled = show;
            hint.enabled = playing && present && hand < 0;
            crosshair.rectTransform.anchoredPosition = aim;
            lockFill.fillAmount = target != null ? 1 : 0;
            crosshair.color = stunTime > 0 ? new Color(.85f, .4f, 1f) : target != null ? new Color(.6f, 1f, .6f) : Color.white;
            crosshair.rectTransform.localRotation = Quaternion.Euler(0, 0, target != null ? Time.time * 180 : 0);
            crosshair.rectTransform.localScale = Vector3.one * rs * (target != null ? .85f + .1f * Mathf.Sin(Time.time * 20) : 1f);
        }

        // ลำแสงดูด: จากปากปืนถึงเป้า ค่อย ๆ ปรากฏ/จาง
        void DrawBeam(bool on, Vector2 to)
        {
            var from = gun.NozzleLocal;
            var d = to - from;
            var rt = beam.rectTransform;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(d.magnitude, 80 + 20 * Mathf.Sin(Time.time * 30));
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float a = Mathf.MoveTowards(beam.color.a, on ? .75f : 0, Time.deltaTime * 6);
            beam.color = new Color(color.r * .6f + .4f, color.g * .6f + .4f, color.b * .6f + .4f, a * .5f);
            // ลมดูดหมุนวน: วางที่เป้า หมุนให้หางชี้ไปปากปืน (หางในรูปชี้ไปซ้ายบน ~145°)
            var srt = suckFx.rectTransform;
            srt.anchoredPosition = to;
            srt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-d.y, -d.x) * Mathf.Rad2Deg - 145f);
            float len = Mathf.Clamp(d.magnitude / 380f, .6f, 1.6f);
            srt.localScale = new Vector3(len, 1 + .06f * Mathf.Sin(Time.time * 25), 1);
            suckFx.color = new Color(1, 1, 1, Mathf.Clamp01(a * 1.3f));
        }

        // เตือนเมื่อผู้เล่นออกจากกล้อง หรือเข้าใกล้กล้องเกิน 1.3 ม. (ยังเล่นต่อได้แต่ตัวจะล้นภาพ)
        void UpdateToast(TrackedPlayer p, bool cameraOnline, bool playing, bool present, float dt)
        {
            if (toastTime > 0 && (toastTime -= dt) <= 0) toast.text = "";
            if (!playing || !cameraOnline) return;
            if (!present) ShowToast($"P{slot} ออกนอกกล้อง!\nกลับมายืนที่จุดเดิม (2 – 3 ม.)", new Color(1f, .45f, .35f), .1f);
            else if (p.dist > 0 && p.dist < 1.3f) ShowToast("ใกล้กล้องเกินไป\nถอยหลังหน่อย", new Color(1f, .8f, .3f), .1f);
        }

        void ShowToast(string text, Color c, float seconds = 1.4f) { toast.text = text; toast.color = c; toastTime = toastDur = seconds; }

        void RefreshHud() => hud.SetNumbers(Score, Recycled);
    }
}
