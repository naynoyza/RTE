// =====================================================================================================
// SeedHud — UI ระหว่างเล่นภารกิจที่ 2 (วางตามภาพอ้างอิง Ref/17-18)
// -----------------------------------------------------------------------------------------------------
// บนซ้าย  : Water Gauge (กรอบ hud_water)      บนกลาง : Shared Growth Bar (กรอบ hud_tree) + ระยะการเติบโต
// บนขวา   : Sunlight Gauge (กรอบ hud_sun)      ใต้แถบต้นไม้: Nature Balance (น้ำ <—•—> แสง) + เวลา
// กลางจอ  : สถานะความพร้อมกัน (ส่งพร้อมกัน ×1.0 / ส่งคนเดียว โตช้าลง) และป้ายเด้ง PERFECT SYNC
// ล่างซ้าย/ขวา: คำแนะนำท่าของผู้เล่นแต่ละคน (ชูแขนตัว V / ผลักฝ่ามือ / ส่งพร้อมเพื่อน / หลุดกล้อง)
// ใต้เกจน้ำ/แสง: แผงโครงกระดูกเล็กของ P1/P2 (ให้รู้ว่ากล้องกำลังตามตัวเราอยู่ เหมือนใน WasteHunters)
// ดึงข้อมูล: กรอบจาก Resources/Sprites/seed/hud_*.png, ตำแหน่งช่องเกจจาก layout.json
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class SeedHud
    {
        public readonly GameObject Root;
        readonly Image waterFill, sunFill, treeFill, balanceKnob;
        readonly Text stage, percent, timer, balanceText, sync, popup;
        /// <summary>แสดงกรอบนาฬิกาไหม (มาจาก SeedSettings.ShowTimer)</summary>
        public bool ShowTimer = true;
        readonly CanvasGroup stallHint;   // การ์ดเตือนท่าเมื่อต้นไม้ไม่โต
        bool stallOn;
        readonly Text[] hint = new Text[2], hintEn = new Text[2];
        readonly Image waterReady, sunReady, syncImg, waterLow, sunLow;
        readonly RectTransform balanceBar;
        const float BalanceW = 480f;        // ความกว้างรูปแถบสมดุลบนจอ (สูง = กว้าง x .33 ตามสัดส่วนรูป)
        const float KnobTravel = .36f;      // ปุ่มเลื่อนได้ ± ความกว้าง x ค่านี้ (รางในรูปอยู่ราว 11.5%–88.5% ของความกว้าง)
        const float TrackDropY = .02f;      // กลางรางอยู่ต่ำกว่ากลางรูปเล็กน้อย (สัดส่วนของความสูง)
        readonly SkeletonView[] skeleton = new SkeletonView[2];
        float popupTime = 99, knobX;

        public SeedHud(Transform canvas)
        {
            var rt = UIKit.Rect("SeedHud", canvas); UIKit.Stretch(rt);
            Root = rt.gameObject;

            waterFill = Bar(rt, "hud_water", new Color(.25f, .7f, 1f), new Color(.5f, .9f, 1f), 10);
            sunFill = Bar(rt, "hud_sun", new Color(1f, .7f, .1f), new Color(1f, .88f, .3f), 10);
            treeFill = Bar(rt, "hud_tree", new Color(.3f, .8f, .25f), new Color(.95f, .92f, .2f), 16);
            waterReady = Ready(waterFill);
            sunReady = Ready(sunFill);
            // ป้ายกำกับใต้เกจมุมบน: บอกว่าเป็นพลังของใคร (ไม่ใช่เวลา)
            BarLabel(waterFill, "P1 • พลังน้ำ", GameSettings.PlayerColors[0], 40);
            BarLabel(sunFill, "P2 • พลังแสง", GameSettings.PlayerColors[1], -70);

            var tree = SeedKit.Box("hud_tree");
            stage = UIKit.Label("Stage", rt, "", 30, Color.white);
            UIKit.Anchor(stage.rectTransform, new Vector2(.5f, 1), new Vector2(30, -(tree.yMax - 20)));
            percent = UIKit.Label("Percent", treeFill.transform, "", 34, Color.white);
            UIKit.Stretch(percent.rectTransform);

            // ---------------- Nature Balance: น้ำ (ซ้าย) <—•—> แสง (ขวา), ช่องกลางสีเขียว = สมดุล
            // รูปแถบ ui2/balance_bar (กรอบไม้พันเถาวัลย์: น้ำ | ต้นอ่อนตรงกลาง | แสงแดด) + ปุ่มคริสตัลต้นอ่อน ui2/balance_knob
            balanceBar = UIKit.Img("Balance", rt, UIKit.Load("ui2/balance_bar"), new Vector2(BalanceW, BalanceW * .33f), false).rectTransform;
            UIKit.Anchor(balanceBar, new Vector2(.5f, 1), new Vector2(30, -(tree.yMax + 8)));
            balanceKnob = UIKit.Img("Knob", balanceBar, UIKit.Load("ui2/balance_knob"), new Vector2(54, 54), false);
            balanceKnob.color = Color.white;
            UIKit.Label("Title", balanceBar, "NATURE BALANCE", 20, new Color(1, 1, 1, .85f)).rectTransform.anchoredPosition = new Vector2(0, 44);
            balanceText = UIKit.Label("BalanceText", balanceBar, "", 26, Color.white);
            balanceText.rectTransform.anchoredPosition = new Vector2(0, -50);
            // ไอคอนบอกฝั่งที่ขาด: น้ำน้อย (ซ้าย) / แสงน้อย (ขวา)
            waterLow = UIKit.Img("WaterLow", balanceBar, UIKit.Load("ui2/water_low"), new Vector2(54, 58));
            waterLow.rectTransform.anchoredPosition = new Vector2(-BalanceW * .5f - 34, 0);
            sunLow = UIKit.Img("SunLow", balanceBar, UIKit.Load("ui2/sun_low"), new Vector2(60, 55));
            sunLow.rectTransform.anchoredPosition = new Vector2(BalanceW * .5f + 34, 0);

            var timerBox = UIKit.Img("TimerBox", rt, UIKit.Load("ui2/seed_timer"), new Vector2(230, 84));
            UIKit.Anchor(timerBox.rectTransform, new Vector2(.5f, 1), new Vector2(360, -(tree.yMax + 76)));
            timer = UIKit.Label("Timer", timerBox.transform, "", 42, Color.white);
            timer.rectTransform.anchoredPosition = new Vector2(30, 0);

            sync = UIKit.Label("Sync", rt, "", 40, Color.white);
            UIKit.Anchor(sync.rectTransform, new Vector2(.5f, 1), new Vector2(30, -(tree.yMax + 143)));   // ใต้ข้อความสมดุล ไม่ทับกัน
            // ป้ายรูป TOGETHER! / WAIT FOR YOUR PARTNER แทนข้อความส่งพร้อมกัน
            syncImg = UIKit.Img("SyncBanner", rt, UIKit.Load("ui2/sync_together"), new Vector2(380, 116));
            UIKit.Anchor(syncImg.rectTransform, new Vector2(.5f, 1), new Vector2(30, -(tree.yMax + 153)));
            stallHint = BuildStallHint(rt);
            popup = UIKit.Label("Popup", rt, "", 96, new Color(1f, .95f, .4f));
            UIKit.Anchor(popup.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 40));
            popup.GetComponent<Shadow>().effectDistance = new Vector2(4, -4);

            // ---------------- คำแนะนำท่า (ตำแหน่งเดียวกับข้อความในภาพอ้างอิง)
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -540 : 520;
                hint[i] = UIKit.Label($"P{i + 1}Hint", rt, "", 44, Color.white);
                UIKit.Anchor(hint[i].rectTransform, new Vector2(.5f, 0), new Vector2(x, 190));
                hint[i].GetComponent<Shadow>().effectDistance = new Vector2(3, -3);
                hintEn[i] = UIKit.Label($"P{i + 1}HintEn", rt, "", 30, new Color(1, 1, 1, .85f));
                UIKit.Anchor(hintEn[i].rectTransform, new Vector2(.5f, 0), new Vector2(x, 140));
            }

            // ---------------- แผงโครงกระดูก: P1 ใต้เกจน้ำ (ซ้าย), P2 ใต้เกจแสง (ขวา)
            for (int i = 0; i < 2; i++)
            {
                bool p1 = i == 0;
                var gauge = SeedKit.Box(p1 ? "hud_water" : "hud_sun");
                var bg = UIKit.Panel($"P{i + 1}SkeletonPanel", rt, new Vector2(150, 200), new Color(0, 0, 0, .45f));
                UIKit.Anchor(bg.rectTransform, new Vector2(p1 ? 0 : 1, 1), new Vector2(40 * (p1 ? 1 : -1), -(gauge.yMax + 15)));
                skeleton[i] = new SkeletonView(bg.transform, "Skeleton", new Vector2(140, 190), p1 ? SeedSettings.Water : SeedSettings.Sun, 5);
            }
        }

        // กรอบแถบ + สีเกจแบบ Filled + เส้นแบ่งช่องให้ดูเป็นปล้อง ๆ แบบในภาพอ้างอิง
        // ป้ายชื่อเกจ วางชิดใต้กรอบเกจ
        static void BarLabel(Image fill, string text, Color color, float x)
        {
            var frame = (RectTransform)fill.transform.parent;
            var t = UIKit.Label("BarLabel", frame, text, 28, color);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(.5f, 0);
            t.rectTransform.pivot = new Vector2(.5f, 1);
            t.rectTransform.anchoredPosition = new Vector2(x, 2);   // เลื่อนเข้าด้านในจอ ไม่ชนแผงโครงกระดูก
        }

        static Image Bar(RectTransform parent, string name, Color from, Color to, int segments)
        {
            var box = SeedKit.Box(name); var slot = SeedKit.Box(name + "_slot");
            var frame = UIKit.Img(name, parent, UIKit.Load("seed/" + name), box.size, false);
            UIKit.Anchor(frame.rectTransform, new Vector2(0, 1), new Vector2(box.x, -box.y));
            var slotRt = UIKit.Rect("Slot", frame.transform);
            slotRt.anchorMin = slotRt.anchorMax = slotRt.pivot = new Vector2(0, 1);
            slotRt.anchoredPosition = new Vector2(slot.x + 3, -slot.y - 3);
            slotRt.sizeDelta = slot.size - new Vector2(6, 6);

            var tex = new Texture2D(64, 8) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
                for (int y = 0; y < 8; y++)
                    tex.SetPixel(x, y, Color.Lerp(from, to, x / 63f) * (1.1f - .25f * y / 7f));
            tex.Apply();
            var fill = slotRt.gameObject.AddComponent<Image>();
            fill.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 8), new Vector2(.5f, .5f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0;
            fill.raycastTarget = false;
            for (int i = 1; i < segments; i++)
            {
                var line = UIKit.Img("Seg", slotRt, null, new Vector2(2, slot.height - 6), false);
                line.color = new Color(0, 0, 0, .25f);
                UIKit.Anchor(line.rectTransform, new Vector2(i / (float)segments, .5f), Vector2.zero);
            }
            return fill;
        }

        // ป้าย READY! วางกลางเกจ (แสดงเมื่อเกจเต็ม)
        static Image Ready(Image fill)
        {
            var img = UIKit.Img("Ready", fill.transform.parent, UIKit.Load("ui2/ready_badge"), new Vector2(150, 48));
            img.color = Color.clear;
            return img;
        }

        public void Popup(string text) { popup.text = text; popupTime = 0; }

        // โหมดสอนเล่น: กรอบต้นไม้กลายเป็นหัวข้อ HOW TO PLAY • 15s, ซ่อนระยะ / NATURE BALANCE / เวลา / ข้อความกลางจอ / คำแนะนำล่างจอ
        public void SetPractice(bool on, string header = "")
        {
            balanceBar.gameObject.SetActive(!on);
            stage.enabled = timer.enabled = sync.enabled = !on;
            timer.transform.parent.gameObject.SetActive(!on && ShowTimer);
            if (on) syncImg.enabled = false;
            for (int i = 0; i < 2; i++) hint[i].enabled = hintEn[i].enabled = !on;
            if (on) { treeFill.fillAmount = 0; percent.text = header; }
        }

        public void SetGauges(float waterGauge, float sunGauge)
        {
            waterFill.fillAmount = waterGauge; sunFill.fillAmount = sunGauge;
            float blink = .6f + .4f * Mathf.Sin(Time.time * 10);
            waterReady.color = new Color(1, 1, 1, waterGauge >= 1 ? blink : 0);
            sunReady.color = new Color(1, 1, 1, sunGauge >= 1 ? blink : 0);
        }

        public void SetGrowth(float growth)
        {
            treeFill.fillAmount = growth;
            percent.text = $"ต้นไม้โต {Mathf.FloorToInt(growth * 100)}%";   // บอกชัดว่าแถบนี้คือการเติบโต ไม่ใช่เวลา
            int k = TreeView.StageOf(growth);
            stage.text = growth >= 1 ? "โตเต็มที่!" : $"ระดับ {k + 1} • {TreeView.StageName[k]}";
        }

        /// lean: -1 = น้ำมากไป, +1 = แสงมากไป, ratio = น้อย/มาก, multiplier = ผลต่อการโต
        public void SetBalance(float lean, float ratio, float multiplier, bool balanced, float dt)
        {
            knobX = SeedKit.Damp(knobX, Mathf.Clamp(lean, -1, 1) * BalanceW * KnobTravel, 6, dt);
            balanceKnob.rectTransform.anchoredPosition = new Vector2(knobX, -BalanceW * .33f * TrackDropY);
            balanceKnob.color = balanced ? Color.white : new Color(1f, .78f, .6f);   // ไม่สมดุล = อมส้มเล็กน้อย
            balanceKnob.rectTransform.localScale = Vector3.one * (balanced ? 1 + .06f * Mathf.Sin(Time.time * 6) : 1);
            balanceText.text = balanced ? "สมดุล  ต้นไม้โตเร็วขึ้น!" : lean < 0 ? "แสงน้อยไป  ต้นไม้โตช้าลง" : "น้ำน้อยไป  ต้นไม้โตช้าลง";
            balanceText.color = balanced ? new Color(.6f, 1f, .6f) : new Color(1f, .7f, .5f);
            float pulse = 1 + .1f * Mathf.Sin(Time.time * 8);
            waterLow.enabled = !balanced && lean > 0; sunLow.enabled = !balanced && lean < 0;
            waterLow.rectTransform.localScale = sunLow.rectTransform.localScale = Vector3.one * pulse;
        }

        public void SetTime(float secondsLeft)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0, secondsLeft));
            timer.text = $"{s / 60}:{s % 60:00}";
            timer.color = s <= 10 ? new Color(1, .35f, .3f) : Color.white;
            timer.transform.parent.gameObject.SetActive(ShowTimer && timer.enabled);
        }

        /// both = ส่งพร้อมกัน, solo = ส่งอยู่คนเดียว
        public void SetSync(bool both, bool solo, float multiplier)
        {
            sync.text = "";
            syncImg.enabled = (both || solo) && popupTime > 1.6f;   // ป้ายเด้ง (PERFECT SYNC / ระดับต้นไม้) ขึ้นอยู่ = ซ่อนป้ายนี้ไม่ให้ทับกัน
            syncImg.sprite = UIKit.Load(both ? "ui2/sync_together" : "ui2/sync_wait");
            syncImg.rectTransform.localScale = Vector3.one * (both ? 1 + .05f * Mathf.Sin(Time.time * 10) : 1);
        }

        public void SetHint(int i, string th, string en, Color color)
        {
            hint[i].text = th; hint[i].color = color; hintEn[i].text = en;
        }

        public void SetSkeleton(int i, float[] kp, float dt, float[] pose = null) => skeleton[i].Set(kp, dt, pose);

        /// <summary>แสดง/ซ่อนการ์ดเตือนท่า (ค่อย ๆ ปรากฏ/จาง) — LastSeed เรียกทุกเฟรมขณะเล่น</summary>
        public void SetStallHint(bool on) => stallOn = on;

        // การ์ดเตือนท่า: รูปท่า V -> ลูกศร -> รูปผลักฝ่ามือ พร้อมข้อความสั้น (ใช้รูปเดียวกับหน้าสอน)
        static CanvasGroup BuildStallHint(RectTransform parent)
        {
            var card = UIKit.Panel("StallHint", parent, new Vector2(620, 250), new Color(.04f, .07f, .1f, .88f));
            card.rectTransform.anchoredPosition = new Vector2(0, -20);   // ใต้ข้อความสมดุล เหนือต้นไม้
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.blocksRaycasts = false;
            UIKit.Label("Title", card.transform, "ต้นไม้ยังไม่โต ลองทำแบบนี้!", 34, new Color(1f, .85f, .3f))
                .rectTransform.anchoredPosition = new Vector2(0, 92);
            string[] pics = { "seed/tut_charge", "seed/tut_push" };
            for (int b = 0; b < 2; b++)
            {
                var pic = UIKit.Img($"Pic{b + 1}", card.transform, UIKit.Load(pics[b]), new Vector2(230, 136), false);
                pic.rectTransform.anchoredPosition = new Vector2(b == 0 ? -150 : 150, -10);
                UIKit.Label($"Cap{b + 1}", card.transform, b == 0 ? "1. ชูแขนตัว V" : "2. ผลักพร้อมกัน", 26, Color.white)
                    .rectTransform.anchoredPosition = new Vector2(b == 0 ? -150 : 150, -100);
            }
            UIKit.Label("Arrow", card.transform, "➜", 54, new Color(.4f, 1f, .5f)).rectTransform.anchoredPosition = new Vector2(0, -10);
            return group;
        }

        public void Tick(float dt)
        {
            stallHint.alpha = Mathf.MoveTowards(stallHint.alpha, stallOn && popupTime > 1.6f ? 1 : 0, dt * 3);   // ป้ายเด้งขึ้นอยู่ = หลบ
            stallHint.transform.localScale = Vector3.one * (stallOn ? 1 + .02f * Mathf.Sin(Time.time * 4) : 1);
            popupTime += dt;
            float k = Mathf.Clamp01(popupTime / .35f);
            popup.rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(k);
            var c = popup.color; c.a = Mathf.Clamp01(1.6f - popupTime); popup.color = c;
        }
    }
}
