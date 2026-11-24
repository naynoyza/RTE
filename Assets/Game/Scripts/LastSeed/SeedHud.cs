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
        readonly Text[] hint = new Text[2], hintEn = new Text[2];
        readonly Text waterReady, sunReady;
        readonly RectTransform balanceBar;
        readonly SkeletonView[] skeleton = new SkeletonView[2];
        float popupTime = 99, knobX;

        public SeedHud(Transform canvas)
        {
            var rt = UIKit.Rect("SeedHud", canvas); UIKit.Stretch(rt);
            Root = rt.gameObject;

            waterFill = Bar(rt, "hud_water", new Color(.25f, .7f, 1f), new Color(.5f, .9f, 1f), 10);
            sunFill = Bar(rt, "hud_sun", new Color(1f, .7f, .1f), new Color(1f, .88f, .3f), 10);
            treeFill = Bar(rt, "hud_tree", new Color(.3f, .8f, .25f), new Color(.95f, .92f, .2f), 16);
            waterReady = Ready(waterFill, "พร้อมส่ง!");
            sunReady = Ready(sunFill, "พร้อมส่ง!");

            var tree = SeedKit.Box("hud_tree");
            stage = UIKit.Label("Stage", rt, "", 30, Color.white);
            UIKit.Anchor(stage.rectTransform, new Vector2(.5f, 1), new Vector2(30, -(tree.yMax + 2)));
            percent = UIKit.Label("Percent", treeFill.transform, "", 34, Color.white);
            UIKit.Stretch(percent.rectTransform);

            // ---------------- Nature Balance: น้ำ (ซ้าย) <—•—> แสง (ขวา), ช่องกลางสีเขียว = สมดุล
            balanceBar = UIKit.Panel("Balance", rt, new Vector2(420, 22), new Color(0, 0, 0, .55f)).rectTransform;
            UIKit.Anchor(balanceBar, new Vector2(.5f, 1), new Vector2(30, -(tree.yMax + 82)));
            var left = UIKit.Panel("WaterSide", balanceBar, new Vector2(170, 14), new Color(SeedSettings.Water.r, SeedSettings.Water.g, SeedSettings.Water.b, .8f));
            left.rectTransform.anchoredPosition = new Vector2(-118, 0);
            var right = UIKit.Panel("SunSide", balanceBar, new Vector2(170, 14), new Color(SeedSettings.Sun.r, SeedSettings.Sun.g, SeedSettings.Sun.b, .8f));
            right.rectTransform.anchoredPosition = new Vector2(118, 0);
            UIKit.Panel("Center", balanceBar, new Vector2(70, 18), new Color(.35f, 1f, .4f, .9f));
            balanceKnob = UIKit.Img("Knob", balanceBar, UIKit.Load("fx_circle"), new Vector2(34, 34));
            balanceKnob.color = Color.white;
            UIKit.Label("Title", balanceBar, "NATURE BALANCE", 20, new Color(1, 1, 1, .8f)).rectTransform.anchoredPosition = new Vector2(0, 26);
            balanceText = UIKit.Label("BalanceText", balanceBar, "", 26, Color.white);
            balanceText.rectTransform.anchoredPosition = new Vector2(0, -30);

            timer = UIKit.Label("Timer", rt, "", 40, Color.white);
            UIKit.Anchor(timer.rectTransform, new Vector2(.5f, 1), new Vector2(330, -(tree.yMax + 76)));

            sync = UIKit.Label("Sync", rt, "", 40, Color.white);
            UIKit.Anchor(sync.rectTransform, new Vector2(.5f, 1), new Vector2(0, -(tree.yMax + 140)));
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

        static Text Ready(Image fill, string text)
        {
            var t = UIKit.Label("Ready", fill.transform, text, 26, Color.white);
            UIKit.Stretch(t.rectTransform);
            return t;
        }

        public void Popup(string text) { popup.text = text; popupTime = 0; }

        public void SetGauges(float waterGauge, float sunGauge)
        {
            waterFill.fillAmount = waterGauge; sunFill.fillAmount = sunGauge;
            float blink = .6f + .4f * Mathf.Sin(Time.time * 10);
            waterReady.text = waterGauge >= 1 ? "พร้อมส่ง!" : ""; waterReady.color = new Color(1, 1, 1, blink);
            sunReady.text = sunGauge >= 1 ? "พร้อมส่ง!" : ""; sunReady.color = new Color(1, 1, 1, blink);
        }

        public void SetGrowth(float growth)
        {
            treeFill.fillAmount = growth;
            percent.text = $"{Mathf.FloorToInt(growth * 100)}%";
            stage.text = growth < .3f ? "ระยะ 1 • ต้นอ่อน" : growth < .7f ? "ระยะ 2 • ต้นไม้กำลังโต" : growth < 1 ? "ระยะ 3 • ต้นไม้สมบูรณ์" : "โตเต็มที่!";
        }

        /// lean: -1 = น้ำมากไป, +1 = แสงมากไป, ratio = น้อย/มาก, multiplier = ผลต่อการโต
        public void SetBalance(float lean, float ratio, float multiplier, bool balanced, float dt)
        {
            knobX = SeedKit.Damp(knobX, Mathf.Clamp(lean, -1, 1) * 190, 6, dt);
            balanceKnob.rectTransform.anchoredPosition = new Vector2(knobX, 0);
            balanceKnob.color = balanced ? new Color(.5f, 1f, .5f) : new Color(1f, .6f, .4f);
            balanceText.text = balanced ? $"สมดุล  โบนัส ×{multiplier:0.00}" :
                               lean < 0 ? $"แสงน้อยไป  โตช้าลง ×{multiplier:0.00}" : $"น้ำน้อยไป  โตช้าลง ×{multiplier:0.00}";
            balanceText.color = balanced ? new Color(.6f, 1f, .6f) : new Color(1f, .7f, .5f);
        }

        public void SetTime(float secondsLeft)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0, secondsLeft));
            timer.text = $"{s / 60}:{s % 60:00}";
            timer.color = s <= 10 ? new Color(1, .35f, .3f) : Color.white;
        }

        /// both = ส่งพร้อมกัน, solo = ส่งอยู่คนเดียว
        public void SetSync(bool both, bool solo, float multiplier)
        {
            sync.text = both ? $"ส่งพร้อมกัน!  ต้นไม้โตเต็มที่ ×{multiplier:0.0}" : solo ? $"ส่งพร้อมกันสิ!  ต้นไม้โตช้าลง ×{multiplier:0.00}" : "";
            sync.color = both ? new Color(.6f, 1f, .6f) : new Color(1f, .75f, .45f);
        }

        public void SetHint(int i, string th, string en, Color color)
        {
            hint[i].text = th; hint[i].color = color; hintEn[i].text = en;
        }

        public void SetSkeleton(int i, float[] kp, float dt) => skeleton[i].Set(kp, dt);

        public void Tick(float dt)
        {
            popupTime += dt;
            float k = Mathf.Clamp01(popupTime / .35f);
            popup.rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(k);
            var c = popup.color; c.a = Mathf.Clamp01(1.6f - popupTime); popup.color = c;
        }
    }
}
