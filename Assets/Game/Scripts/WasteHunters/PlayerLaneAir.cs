// =====================================================================================================
// PlayerLaneAir — ระบบอากาศของผู้เล่น 1 คน (ส่วนหนึ่งของ PlayerLane)
// -----------------------------------------------------------------------------------------------------
//   • หลอด AIR 0..100 ลดเองตามเวลา (พายุฝุ่น = ลดเร็วขึ้น), ไม่ลดตอนซ้อม/นับถอยหลัง
//   • ลูกอากาศตกจากฟ้าลงกลางถนนในเลนตัวเองเป็นระยะ (อากาศต่ำ = ออกถี่ขึ้น) แล้วไหลเข้าหาผู้เล่นตามทางเดินเหมือนขยะ
//     เก็บได้เมื่อเข้าระยะดูด (SuckRange) — เดินเลยไปแล้ว = พลาด
//   • เล็งค้างบนลูก AirGrabSeconds = เก็บเข้าหลอดตัวเอง (+AIR)
//   • ปัดมือแรง ๆ ผ่านลูกไปทางฝั่งเพื่อน = ส่งลูกข้ามจอไปโผล่ในเลนเพื่อน (เพื่อนต้องเล็งรับเองภายใน GiftLife วิ)
//     เพื่อนรับได้ -> ผู้ส่งได้ TEAM +TeamBonus คะแนน และนับ AirSent; ลูกที่เพื่อนส่งมาส่งกลับไม่ได้
//   • อากาศหมด = อ่อนแรงชั่วคราว: วงดูดเล็กลง + เดินช้าลง จนกว่าจะได้อากาศเพิ่ม (ไม่มีเกมโอเวอร์)
// ค่าทั้งหมดอยู่ใน GameSettings (หัวข้อ "อากาศ")
// =====================================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public partial class PlayerLane
    {
        /// <summary>อากาศคงเหลือ 0..100</summary>
        public float Air = 100f;
        /// <summary>จำนวนลูกอากาศที่ส่งแล้วเพื่อนรับได้ (หน้าสรุปผล)</summary>
        public int AirSent;
        /// <summary>เลนของอีกฝั่ง (WasteHunters ตั้งให้) — ปลายทางของลูกอากาศที่ปัดส่ง</summary>
        public PlayerLane Partner;
        public bool OutOfAir => Air <= 0 && !Practice;

        class AirBubble
        {
            public Image Img, Ring;
            public Vector2 Pos, From, Rest;
            public float Age, Life, Fall, Grab, Z, Lat, Drop, Size = BubbleSize;
            public bool Leaf, Gift, Flying, OnRoad;
            public PlayerLane Sender;
        }

        RectTransform airLayer;
        readonly List<AirBubble> bubbles = new List<AirBubble>();
        Image airTint, airPlus, swoosh;
        float nextAirIn, aimVelX, prevAimX, plusTime, swooshTime;
        bool warnedLow;
        const float BubbleSize = 120f, EdgeX = 560f;

        void InitAir()
        {
            airTint = UIKit.Img("LowAirTint", view, UIKit.Load("fx_glow"), new Vector2(1500, 1500), false);
            airTint.color = Color.clear;
            swoosh = UIKit.Img("AirSwoosh", view, UIKit.Load("ui2/air_swoosh"), new Vector2(330, 146), false);
            swoosh.color = Color.clear;
            airPlus = UIKit.Img("AirPlus", view, UIKit.Load("ui2/air_plus"), new Vector2(190, 154), false);
            airPlus.color = Color.clear;
        }

        void ResetAir()
        {
            foreach (var b in bubbles) Object.Destroy(b.Img.gameObject);
            bubbles.Clear();
            Air = 100; AirSent = 0; nextAirIn = Random.Range(settings.AirSpawnMin, settings.AirSpawnMax);
            plusTime = swooshTime = 0; warnedLow = false; aimVelX = 0;
            if (airTint != null) airTint.color = airPlus.color = swoosh.color = Color.clear;
        }

        AirBubble NewBubble(bool leaf, bool gift)
        {
            var b = new AirBubble { Leaf = leaf, Gift = gift, Life = gift ? settings.GiftLife : 999f };
            b.Img = UIKit.Img(gift ? "GiftAir" : "AirBubble", airLayer, UIKit.Load(leaf ? "ui2/air_bubble_leaf" : "ui2/air_bubble"), new Vector2(BubbleSize, BubbleSize), false);
            b.Ring = UIKit.Img("Grab", b.Img.transform, UIKit.Load("fx_ring"), new Vector2(BubbleSize * 1.35f, BubbleSize * 1.35f), false);
            b.Ring.type = Image.Type.Filled; b.Ring.fillMethod = Image.FillMethod.Radial360; b.Ring.fillOrigin = (int)Image.Origin360.Top;
            b.Ring.color = new Color(.5f, 1f, 1f, .95f); b.Ring.fillAmount = 0;
            bubbles.Add(b);
            return b;
        }

        // ลูกปกติ: ตกจากฟ้าลงกลางถนน (ระยะ 8–11 ม.) แล้วไหลเข้าหาผู้เล่นตามระยะที่เดิน (มุมมองเดียวกับขยะ)
        void SpawnAirBubble()
        {
            var b = NewBubble(Random.value < settings.LeafChance, false);
            b.OnRoad = true; b.Z = Random.Range(8f, 11f); b.Lat = Random.Range(-RoadHalf, RoadHalf) * .8f; b.Drop = 1f;
            PlaceOnRoad(b);
        }

        // ลอยเหนือพื้นถนนเล็กน้อย ขนาดตามระยะ (ไกล = เล็ก)
        void PlaceOnRoad(AirBubble b)
        {
            b.Size = Mathf.Max(55f, 420f * settings.TrashScale / Mathf.Max(.5f, b.Z));
            b.Pos = TrashPiece.ToScreen(b.Lat, b.Z, b.Size * .9f + Mathf.Sin(b.Age * 3) * b.Size * .08f + b.Drop * b.Drop * 750f);
        }

        /// <summary>รับลูกอากาศที่เพื่อนปัดมา: พุ่งเข้าจากขอบฝั่งเส้นกลางจอ แล้วค้างให้เล็งรับ</summary>
        public void ReceiveGift(PlayerLane sender, bool leaf)
        {
            var b = NewBubble(leaf, true);
            b.Sender = sender;
            b.Rest = new Vector2(slot == 1 ? Random.Range(60f, 300f) : Random.Range(-300f, -60f), Random.Range(-60f, 180f));
            b.From = new Vector2(slot == 1 ? EdgeX : -EdgeX, b.Rest.y + 60);
            b.Pos = b.From; b.Fall = .45f;
            ShowToast("เพื่อนส่งอากาศมา!\nเล็งค้างเพื่อรับ", new Color(.5f, 1f, 1f), 1.6f);
            Sfx.Play("join", .8f, 1.2f);
        }

        // ทุกเฟรม (เรียกจาก Tick): ลดอากาศ, เกิดลูก, เก็บ/ปัดส่ง, วาด
        void TickAir(bool playing, bool showWorld, bool canSuck, float meters, float dt)
        {
            // ความเร็วมือแนวนอน (ใช้ตรวจการปัด)
            float vx = dt > 0 ? (aim.x - prevAimX) / dt : 0;
            aimVelX = Mathf.Lerp(aimVelX, vx, 1 - Mathf.Exp(-dt * 20));
            prevAimX = aim.x;

            bool live = playing && !Practice;
            if (live)
            {
                Air = Mathf.Max(0, Air - settings.AirDrainPerSecond * (Dusty ? settings.DustDrainScale : 1f) * dt);
                if ((nextAirIn -= dt) <= 0)
                {
                    if (CountFree() < 2) SpawnAirBubble();
                    bool low = Air < settings.AirLowSpawnBelow;
                    nextAirIn = low ? Random.Range(settings.AirLowSpawnMin, settings.AirLowSpawnMax)
                                    : Random.Range(settings.AirSpawnMin, settings.AirSpawnMax);
                }
                if (Air < settings.AirWarnBelow && !warnedLow) { warnedLow = true; Sfx.Play("alert", .6f); }
                if (Air >= settings.AirWarnBelow + 5) warnedLow = false;
            }

            float grabR = HitRadius;
            bool bot = BotPlayer.Controls(slot - 1);
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.Age += dt;
                if (b.Flying)                                                    // ปัดส่ง: พุ่งออกขอบจอไปหาเพื่อน
                {
                    b.Pos.x += (slot == 1 ? 1 : -1) * 2400 * dt;
                    if (Mathf.Abs(b.Pos.x) > EdgeX + 80) { Partner?.ReceiveGift(this, b.Leaf); KillBubble(i); continue; }
                }
                else
                {
                    bool ready;
                    if (b.OnRoad)                                                // ไหลมาตามถนน เดินเลยไป = พลาด
                    {
                        b.Z -= meters;
                        if (b.Drop > 0) { b.Drop = Mathf.Max(0, b.Drop - dt * 1.5f); if (b.Drop == 0) Sfx.Play("drop", .4f); }   // ตกจากฟ้าราว 0.7 วิ
                        if (b.Z < PassZ || !showWorld) { KillBubble(i); continue; }
                        PlaceOnRoad(b);
                        ready = b.Drop <= 0 && b.Z <= TrashPiece.SpawnZ * settings.SuckRange;   // ถึงพื้นและเข้าระยะดูดแล้ว
                    }
                    else                                                         // ลูกที่เพื่อนส่งมา: ค้างให้เล็งรับ
                    {
                        float f = Mathf.Clamp01(b.Age / b.Fall);
                        b.Pos = Vector2.Lerp(b.From, b.Rest, Mathf.SmoothStep(0, 1, f)) + new Vector2(0, Mathf.Sin(b.Age * 3 + i) * 10 * f);
                        if (b.Age > b.Life || !showWorld) { KillBubble(i); continue; }
                        ready = f >= 1;
                    }

                    bool over = canSuck && ready && Vector2.Distance(aim, b.Pos) < Mathf.Max(grabR, b.Size * .55f);
                    bool toPartner = slot == 1 ? aimVelX > settings.SwipeSpeedPx : aimVelX < -settings.SwipeSpeedPx;
                    // บอตทดสอบ: บางครั้งส่งลูกให้เพื่อนแทนการเก็บเอง (ตอนอากาศตัวเองยังเยอะ)
                    if (bot && over && !b.Gift && b.Grab == 0 && Air > 70 && Random.value < .35f) toPartner = true;
                    if (over && toPartner && !b.Gift && live && Partner != null)
                    {
                        b.Flying = true; b.Grab = 0; b.Ring.fillAmount = 0;
                        swoosh.rectTransform.anchoredPosition = b.Pos; swoosh.rectTransform.localScale = new Vector3(slot == 1 ? 1 : -1, 1, 1); swooshTime = .45f;
                        Sfx.Play("warp", .7f, 1.2f);
                        ShowToast("ส่งอากาศให้เพื่อน!", new Color(.5f, 1f, 1f), 1.2f);
                        continue;
                    }
                    b.Grab = over ? b.Grab + dt : Mathf.Max(0, b.Grab - dt * 2);
                    b.Ring.fillAmount = b.Grab / settings.AirGrabSeconds;
                    if (b.Grab >= settings.AirGrabSeconds) { CollectAir(b); KillBubble(i); continue; }
                }
                var rt = b.Img.rectTransform;
                rt.anchoredPosition = b.Pos;
                float fade = b.OnRoad ? 1f : Mathf.Clamp01((b.Life - b.Age) / .6f);
                rt.sizeDelta = Vector2.one * b.Size; b.Ring.rectTransform.sizeDelta = Vector2.one * b.Size * 1.35f;
                float blink = b.Gift ? .65f + .35f * Mathf.Abs(Mathf.Sin(b.Age * 7)) : 1;
                b.Img.color = new Color(1, 1, 1, fade * blink);
                rt.localScale = Vector3.one * (1 + .06f * Mathf.Sin(b.Age * 5)) * (b.Gift ? 1.1f : 1f);
            }

            // เอฟเฟกต์ +AIR / swoosh / ขอบจอแดงตอนอากาศต่ำ
            if (plusTime > 0) plusTime -= dt;
            airPlus.color = new Color(1, 1, 1, Mathf.Clamp01(plusTime / .3f));
            airPlus.rectTransform.anchoredPosition += new Vector2(0, 90 * dt);
            if (swooshTime > 0) { swooshTime -= dt; swoosh.rectTransform.anchoredPosition += new Vector2((slot == 1 ? 1 : -1) * 1800 * dt, 0); }
            swoosh.color = new Color(1, 1, 1, Mathf.Clamp01(swooshTime / .2f));
            bool lowAir = live && Air < settings.AirWarnBelow;
            airTint.color = lowAir ? new Color(1f, .15f, .1f, (Air <= 0 ? .22f : .12f) + .08f * Mathf.Sin(Time.time * 6)) : Color.clear;

            hud.SetAir(Air / 100f, lowAir, playing);
            hud.SetWarning(live && Air <= 0 ? "อากาศหมด! อ่อนแรง\nเล็งเก็บลูกอากาศ" : "");
            walk.SpeedScale = live && Air <= 0 ? settings.NoAirSpeedScale : 1f;
        }

        int CountFree() { int n = 0; foreach (var b in bubbles) if (!b.Gift && !b.Flying) n++; return n; }

        void CollectAir(AirBubble b)
        {
            Air = Mathf.Min(100, Air + (b.Leaf ? settings.LeafBubbleGain : settings.AirBubbleGain));
            airPlus.rectTransform.anchoredPosition = b.Pos + new Vector2(0, 40); plusTime = .9f;
            Sfx.Play("sync", .7f, b.Leaf ? 1.15f : 1f);
            if (b.Gift && b.Sender != null) b.Sender.GiftReceived();
        }

        /// <summary>เพื่อนรับลูกอากาศที่เราส่งได้แล้ว -> TEAM +คะแนน</summary>
        void GiftReceived()
        {
            AirSent++;
            Score += settings.TeamBonus;
            ShowToast($"TEAM +{settings.TeamBonus}\nเพื่อนรับอากาศแล้ว!", new Color(.5f, 1f, .6f));
            Sfx.Play("ready", .7f);
            RefreshHud();
        }

        void KillBubble(int i) { Object.Destroy(bubbles[i].Img.gameObject); bubbles.RemoveAt(i); }

        // บอต: เล็งลูกอากาศก่อนขยะเสมอ (อากาศเยอะจะสุ่มปัดส่งให้เพื่อนใน TickAir)
        Vector2? BotAirTarget()
        {
            foreach (var b in bubbles)
                if (!b.Flying && (b.OnRoad ? b.Drop <= 0 && b.Z <= TrashPiece.SpawnZ * settings.SuckRange : b.Age >= b.Fall))
                    return new Vector2(b.Pos.x / LaneW + .5f, b.Pos.y / LaneH + .5f);
            return null;
        }
    }
}
