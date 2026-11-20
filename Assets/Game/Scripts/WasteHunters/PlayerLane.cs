// =====================================================================================================
// PlayerLane — จอครึ่งหนึ่งของผู้เล่น 1 คน (P1 = ครึ่งซ้าย, P2 = ครึ่งขวา)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : ทุกอย่างที่เกิดบนจอของผู้เล่นคนนั้น
//           • เดิน: ถนนเลื่อนตามความเร็วจาก WalkController
//           • ขยะ : เกิดตามระยะทาง เลื่อนเข้าหาผู้เล่น เลยตัวไปแล้ว = หลุด (combo หาย)
//           • เล็ง: เป้าเล็งตามมือที่ยก แตะขยะ = ดูดทันที -> ได้คะแนน + นับเป็นรีไซเคิล (GOAL); เล็งพลาดขยะก็เลยตัวไป
//           • ระยะดูด: ขยะต้องเข้ามาใกล้กว่า SuckRange (สัดส่วนของระยะเกิด) จึงจะดูดได้
//           • กัน AFK: เล็งค้างจุดเดิมนานเกิน AfkSeconds = ปืนหยุดดูดจนกว่าจะขยับมือ
//           • ขยะที่เล็งยาก (ชิ้นเล็ก/ริมถนน/ขยะพิเศษ) เคลื่อนช้ากว่า จึงอยู่บนจอนานกว่า
//           • ปืน 3D (VacuumGun) ถือมือข้างที่ยก, ลำแสงดูดจากปากปืนถึงเป้า
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

        Vector2 aim;                 // เป้าเล็ง (พิกเซลจากกึ่งกลางจอครึ่ง)
        int hand = -1;               // มือที่ถือปืน 0 ซ้าย / 1 ขวา / -1 ไม่ได้ยก
        float nextSpawnIn, toastTime;
        Vector2 afkAnchor;           // จุดที่เป้าเล็งค้างอยู่ (ใช้ตรวจ AFK)
        float stillTime;             // เป้าเล็งค้างอยู่ใกล้ afkAnchor มากี่วินาทีแล้ว
        bool afk;                    // ค้างนานเกิน -> ดูดไม่ได้จนกว่าจะขยับ

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
            crosshair = UIKit.Img("Crosshair", view, UIKit.Load("fx_ring"), new Vector2(ring, ring), false);
            lockFill = UIKit.Img("LockFill", crosshair.transform, UIKit.Load("fx_ring"), new Vector2(ring * 1.3f, ring * 1.3f), false);
            lockFill.type = Image.Type.Filled; lockFill.fillMethod = Image.FillMethod.Radial360;
            lockFill.fillOrigin = (int)Image.Origin360.Top; lockFill.color = new Color(.4f, 1f, .5f);
            crosshairDot = UIKit.Img("Dot", crosshair.transform, UIKit.Load("fx_circle"), new Vector2(14, 14), false);

            hint = UIKit.Label("RaiseHandHint", view, "ยกมือข้างที่ถนัดขึ้นเพื่อเล็งปืนดูด", 40, new Color(1f, .95f, .6f));
            hint.rectTransform.anchoredPosition = new Vector2(0, -60);
            toast = UIKit.Label("Toast", view, "", 54, Color.white);
            toast.rectTransform.anchoredPosition = new Vector2(0, 160);

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
            nextSpawnIn = 2; toastTime = 0; stillTime = 0; afk = false; toast.text = "";
            RefreshHud();
        }

        // ================================================================== ทุกเฟรม
        // playing = กำลังเล่น (เดิน/เกิดขยะ/เล็งได้), showWorld = แสดงขยะ+ปืน (รวมช่วงนับถอยหลัง)
        public void Tick(TrackedPlayer p, bool cameraOnline, bool playing, bool showWorld)
        {
            float dt = Time.deltaTime;
            bool present = p.present;

            // 1) เดิน: ความเร็วจากการย่ำเท้า -> ถนนเลื่อน
            float metersThisFrame = walk.Tick(p, cameraOnline, playing, dt);
            road.SetDistance(walk.Walked);
            hud.SetSpeed(walk.Speed / settings.MaxSpeed, walk.Boost, playing);
            hud.SetSkeleton(present ? p.kp : null, dt);

            // 2) มือที่เล็ง: ตำแหน่งมือ 0..1 -> พิกเซลบนจอ (ทำให้นุ่มไม่กระตุก)
            hand = present && p.hand ? p.use : -1;
            if (hand >= 0)
            {
                var target = new Vector2((p.x - .5f) * LaneW, (p.y - .5f) * LaneH);
                aim = Vector2.Lerp(aim, target, 1 - Mathf.Exp(-dt * 18));
            }
            UpdateAfk(playing, dt);
            hud.SetWarning(afk ? "ห้ามเล็งค้างที่เดิม!\nขยับมือเล็งขยะ" : "");

            // 3) ข้อความเตือน
            UpdateToast(p, cameraOnline, playing, present, dt);

            // 4) ขยะ: เกิด, เลื่อน, เล็ง, ดูด
            TrashPiece target2 = null;
            if (showWorld)
            {
                if (playing) SpawnByDistance(metersThisFrame, dt);
                target2 = playing && hand >= 0 && !afk ? FindTargetUnderCrosshair() : null;
                MoveAndCollect(metersThisFrame, target2, dt);
            }

            // 5) เป้าเล็ง ลำแสง และปืน
            DrawAim(showWorld, playing, present, target2);
            bool sucking = showWorld && hand >= 0 && (target2 != null || pieces.Exists(t => t.SuckProgress >= 0));
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
                nextSpawnIn = Random.Range(2.2f, 3.8f);
            }
        }

        // กัน AFK: เป้าเล็งต้องขยับออกจากจุดเดิมเกิน AfkMovePx ภายใน AfkSeconds ไม่งั้นดูดไม่ได้
        void UpdateAfk(bool playing, float dt)
        {
            if (!playing || hand < 0) { stillTime = 0; afk = false; afkAnchor = aim; return; }
            if (Vector2.Distance(aim, afkAnchor) > settings.AfkMovePx)
            {
                afkAnchor = aim; stillTime = 0; afk = false;
                return;
            }
            stillTime += dt;
            if (stillTime >= settings.AfkSeconds)
            {
                afk = true;
            }
        }

        // ขยะที่ใกล้เป้าเล็งที่สุด (จุดกลางขยะต้องอยู่ในวงเป้าเล็ง และเข้ามาในระยะดูดแล้ว)
        TrashPiece FindTargetUnderCrosshair()
        {
            TrashPiece best = null; float bestDist = float.MaxValue;
            foreach (var t in pieces)
            {
                if (t.SuckProgress >= 0 || !InRange(t)) continue;
                float d = Vector2.Distance(t.ScreenPos, aim);
                if (d < settings.HitRadiusPx && d < bestDist) { best = t; bestDist = d; }
            }
            return best;
        }

        // ขยะเข้ามาใกล้กว่า SuckRange x ระยะเกิด แล้วหรือยัง (เช่น 0.5 = ต้องเดินเข้าไปเกินครึ่งทาง)
        bool InRange(TrashPiece t) => t.Z <= TrashPiece.SpawnZ * settings.SuckRange;

        // เลื่อนขยะเข้าหาผู้เล่น, สะสมเวลาเล็ง, เริ่มดูดเมื่อครบ, ลบชิ้นที่เดินผ่านไปแล้ว
        void MoveAndCollect(float meters, TrashPiece target, float dt)
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

                if (t.Z < PassZ)                                           // เดินผ่านไปแล้ว
                {
                    Combo = 0;
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
            Combo++;
            int bonus = TrashCatalog.ComboBonus(Combo);
            Score += t.Points + bonus; Recycled++; ImpactCollected += t.BaseScore;
            ShowToast($"+{t.Points}{(bonus > 0 ? $"  COMBO x{Combo} +{bonus}" : "")}\n{t.Kind.Name}{(t.IsRare ? "  ✦RARE" : "")}",
                      t.IsRare ? new Color(1f, .9f, .3f) : Color.white);
            RefreshHud();
        }

        void Remove(int i) { pieces[i].Destroy(); pieces.RemoveAt(i); }

        // ------------------------------------------------------------------ การแสดงผล
        void DrawAim(bool showWorld, bool playing, bool present, TrashPiece target)
        {
            bool show = showWorld && hand >= 0;
            crosshair.enabled = lockFill.enabled = crosshairDot.enabled = show;
            hint.enabled = playing && present && hand < 0;
            crosshair.rectTransform.anchoredPosition = aim;
            lockFill.fillAmount = target != null ? 1 : 0;
            crosshair.color = afk ? new Color(1f, .3f, .25f) : target != null ? new Color(.4f, 1f, .5f) : color;
            crosshair.rectTransform.localScale = Vector3.one * (target != null ? .85f + .1f * Mathf.Sin(Time.time * 20) : 1f);
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
            beam.color = new Color(color.r * .6f + .4f, color.g * .6f + .4f, color.b * .6f + .4f, a);
        }

        // เตือนเมื่อผู้เล่นออกจากกล้อง หรือเข้าใกล้กล้องเกิน 1.3 ม. (ยังเล่นต่อได้แต่ตัวจะล้นภาพ)
        void UpdateToast(TrackedPlayer p, bool cameraOnline, bool playing, bool present, float dt)
        {
            if (toastTime > 0 && (toastTime -= dt) <= 0) toast.text = "";
            if (!playing || !cameraOnline) return;
            if (!present) ShowToast($"P{slot} ออกนอกกล้อง!\nกลับมายืนที่จุดเดิม (2 – 3 ม.)", new Color(1f, .45f, .35f), .1f);
            else if (p.dist > 0 && p.dist < 1.3f) ShowToast("ใกล้กล้องเกินไป\nถอยหลังหน่อย", new Color(1f, .8f, .3f), .1f);
        }

        void ShowToast(string text, Color c, float seconds = 1.4f) { toast.text = text; toast.color = c; toastTime = seconds; }

        void RefreshHud() => hud.SetNumbers(Score, Recycled, settings.Goal);
    }
}
