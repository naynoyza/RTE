// =====================================================================================================
// SeedPractice — สอนเล่นเกม 2 แบบ Synchronized 2-Step Practice (ซ้อนบนฉากจริง ตามแบบ Ref/สอนเกม2.jpg)
// -----------------------------------------------------------------------------------------------------
//   บนกลาง  : กรอบต้นไม้เดิมเปลี่ยนเป็น "HOW TO PLAY • 15s" (SeedHud.SetPractice)
//   ท้องฟ้า : หน้าต่างกะทัดรัด 2 กล่อง  [1. ชูแขนตัว V ชาร์จพลัง] ➜ [2. ผลักฝ่ามือพร้อมกัน] + แถบ "รักษาน้ำและแสงให้สมดุล"
//   ใกล้มือ : ป้ายสถานะสด P1 น้ำ (ฟ้า) / P2 แสงแดด (ทอง)
// ลำดับ   : สเต็ป 1 ชูแขน V จนเกจเต็มทั้งคู่ (ชาร์จเร็ว ~1.5 วิ) -> สเต็ป 2 ผลักฝ่ามือพร้อมกัน จนสายน้ำ+ลำแสงถึงต้นอ่อน
//           -> PERFECT SYNC! พร้อมลุย! -> พับหน้าต่าง -> 3-2-1-GO!   (ครบ 15 วิแล้วยังไม่ครบ = เข้าเกมเอง)
// ตรรกะการชาร์จ/ส่งจริงอยู่ใน LastSeed (สถานะ Practice) คลาสนี้ดูแลเฉพาะหน้าตา
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class SeedPractice
    {
        public const float MaxSeconds = 15f, ChargeSeconds = 1.5f;

        readonly RectTransform window;
        readonly CanvasGroup group;
        readonly Image[] boxFrame = new Image[2], boxGlow = new Image[2];
        readonly Image[] statusBg = new Image[2];
        readonly Text[] statusText = new Text[2];
        readonly Text arrow, flash;
        readonly GameObject root;
        float fold = 1, flashT = 99;

        static readonly Color Metal = new Color(.12f, .14f, .16f, .94f);

        public SeedPractice(Transform canvas)
        {
            var rt = UIKit.Rect("SeedPractice", canvas); UIKit.Stretch(rt);
            root = rt.gameObject;

            // ---------------- หน้าต่างกลางท้องฟ้า (เหนือต้นอ่อน)
            window = UIKit.Rect("Window", rt);
            window.sizeDelta = new Vector2(800, 330);
            window.anchoredPosition = new Vector2(0, 75);
            group = window.gameObject.AddComponent<CanvasGroup>();
            var rim = UIKit.Panel("Rim", window, new Vector2(812, 342), new Color(.55f, .6f, .62f, .95f));
            var panel = UIKit.Panel("Panel", window, new Vector2(796, 326), Metal);
            var glow = UIKit.Img("Glow", window, UIKit.Load("fx_glow"), new Vector2(1100, 560), false);
            glow.color = new Color(.5f, 1f, .6f, .18f); glow.transform.SetAsFirstSibling();

            string[] titles = { "1. ชูแขนตัว V ชาร์จพลัง", "2. ผลักฝ่ามือพร้อมกัน" };
            string[] pics = { "seed/tut_charge", "seed/tut_push" };
            Color[] cols = { SeedSettings.Water, SeedSettings.Sun };
            for (int b = 0; b < 2; b++)
            {
                float x = b == 0 ? -205 : 205;
                UIKit.Label($"Title{b + 1}", window, titles[b], 28, Color.white).rectTransform.anchoredPosition = new Vector2(x, 125);
                boxGlow[b] = UIKit.Img($"BoxGlow{b + 1}", window, UIKit.Load("fx_glow"), new Vector2(470, 300), false);
                boxGlow[b].rectTransform.anchoredPosition = new Vector2(x, 0);
                boxFrame[b] = UIKit.Panel($"Box{b + 1}", window, new Vector2(330, 200), cols[b]);
                boxFrame[b].rectTransform.anchoredPosition = new Vector2(x, 2);
                var pic = UIKit.Img("Pic", boxFrame[b].transform, UIKit.Load(pics[b]), new Vector2(318, 188), false);
                pic.color = Color.white;
            }
            arrow = UIKit.Label("Arrow", window, "➜", 64, new Color(.4f, 1f, .5f));
            arrow.rectTransform.anchoredPosition = new Vector2(0, 2);

            var foot = UIKit.Panel("Footer", window, new Vector2(760, 50), new Color(.05f, .07f, .08f, .95f));
            foot.rectTransform.anchoredPosition = new Vector2(0, -132);
            var bal = UIKit.Panel("BalanceMini", foot.transform, new Vector2(120, 14), Color.white);
            bal.rectTransform.anchoredPosition = new Vector2(-235, 0);
            var balL = UIKit.Panel("W", bal.transform, new Vector2(56, 10), SeedSettings.Water); balL.rectTransform.anchoredPosition = new Vector2(-30, 0);
            var balR = UIKit.Panel("S", bal.transform, new Vector2(56, 10), SeedSettings.Sun); balR.rectTransform.anchoredPosition = new Vector2(30, 0);
            UIKit.Img("Knob", bal.transform, UIKit.Load("fx_circle"), new Vector2(22, 22)).color = new Color(.4f, 1f, .45f);
            UIKit.Label("Text", foot.transform, "รักษา <color=#5CC8FF>น้ำ</color> และ <color=#FFC83F>แสง</color> ให้สมดุล", 28, Color.white)
                .rectTransform.anchoredPosition = new Vector2(60, 0);

            flash = UIKit.Label("Flash", rt, "", 64, new Color(.5f, 1f, .5f));
            flash.rectTransform.anchoredPosition = new Vector2(0, -170);
            flash.GetComponent<Shadow>().effectDistance = new Vector2(4, -4);

            // ---------------- ป้ายสถานะสดใกล้มือแต่ละฝั่ง
            for (int i = 0; i < 2; i++)
            {
                var c = i == 0 ? SeedSettings.Water : SeedSettings.Sun;
                var border = UIKit.Panel($"P{i + 1}StatusRim", rt, new Vector2(430, 74), new Color(c.r, c.g, c.b, .7f));
                border.rectTransform.anchoredPosition = new Vector2(i == 0 ? -500 : 500, -320);
                statusBg[i] = border;
                var inner = UIKit.Panel("Inner", border.transform, new Vector2(420, 64), new Color(.04f, .08f, .1f, .6f));
                statusText[i] = UIKit.Label("Text", border.transform, "", 32, c);
            }
            root.SetActive(false);
        }

        public void Show() { root.SetActive(true); fold = 1; flashT = 99; flash.text = ""; window.localScale = Vector3.one; group.alpha = 1; }
        public void Hide() => root.SetActive(false);

        public void Flash(string text, Color c) { flash.text = text; flash.color = c; flashT = 0; }

        // step = 1 หรือ 2, ready = เกจเต็มแล้ว (สเต็ป 1) / สายถึงต้นไม้แล้ว (สเต็ป 2), pose = ท่าปัจจุบัน
        public void Tick(int step, bool[] ready, bool[] arrived, SeedPose[] pose, bool[] lost, bool folding)
        {
            float dt = Time.deltaTime, t = Time.time;
            // กล่องที่กำลังทำเรืองแสงกะพริบ อีกกล่องหรี่ลง
            for (int b = 0; b < 2; b++)
            {
                bool active = step == b + 1;
                var c = b == 0 ? SeedSettings.Water : SeedSettings.Sun;
                boxGlow[b].color = new Color(c.r, c.g, c.b, active ? .55f + .3f * Mathf.Sin(t * 6) : 0);
                boxFrame[b].color = active ? Color.Lerp(c, Color.white, .3f + .3f * Mathf.Sin(t * 6)) : new Color(.35f, .38f, .4f);
                boxFrame[b].rectTransform.localScale = Vector3.one * (active ? 1.04f + .02f * Mathf.Sin(t * 6) : .96f);
                boxFrame[b].transform.GetChild(0).GetComponent<Image>().color = active ? Color.white : new Color(.55f, .55f, .55f);
            }
            arrow.color = step == 2 ? new Color(.4f, 1f, .5f) : new Color(.4f, 1f, .5f, .35f + .35f * Mathf.Sin(t * 5));
            arrow.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 5) * 8, 2);

            for (int i = 0; i < 2; i++)
            {
                var c = i == 0 ? SeedSettings.Water : SeedSettings.Sun;
                string who = i == 0 ? "P1 น้ำ" : "P2 แสงแดด";
                bool done = step == 1 ? ready[i] : arrived[i];
                string what = lost[i] ? "กลับมายืนหน้ากล้อง!" :
                              step == 1 ? (ready[i] ? "พร้อมแล้ว! ✓" : pose[i] == SeedPose.Charge ? "ชาร์จอยู่... ค้างไว้" : "ชูแขนตัว V") :
                              arrived[i] ? "ส่งถึงแล้ว! ✓" : pose[i] == SeedPose.Push ? "ผลักค้างไว้..." : "ผลักฝ่ามือ!";
                statusText[i].text = $"{who} • {what}";
                statusText[i].color = done ? new Color(.45f, 1f, .5f) : lost[i] ? new Color(1f, .5f, .4f) : Color.white;
                statusBg[i].color = done ? new Color(.35f, 1f, .45f, .8f) : new Color(c.r, c.g, c.b, .7f);
                statusBg[i].rectTransform.localScale = Vector3.one * (done ? 1 + .04f * Mathf.Sin(t * 8) : 1);
            }

            // ข้อความกะพริบกลางจอ
            flashT += dt;
            float k = Mathf.Clamp01(flashT / .3f);
            flash.rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(k) * (1 + .04f * Mathf.Sin(t * 10));

            // พับหน้าต่าง (ตอนจบ)
            fold = Mathf.MoveTowards(fold, folding ? 0 : 1, dt * 3f);
            window.localScale = new Vector3(1, Mathf.SmoothStep(0, 1, fold), 1);
            group.alpha = fold;
        }
    }
}
