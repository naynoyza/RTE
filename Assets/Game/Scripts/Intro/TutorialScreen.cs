// =====================================================================================================
// TutorialScreen — หน้าต่างสอนก่อนเล่น (ใช้ทั้งเกม 1 และเกม 2) วางตามแบบ Ref/สอน.png แต่โทนไซไฟนีออน
// -----------------------------------------------------------------------------------------------------
//   [ป้าย GAME 0X + ชื่อเกม]           บนกลาง
//   [แถบ หมวด | ท่าที่ใช้]              ใต้ป้าย
//   [คำอธิบายเกม 1–4]  ซ้าย      [จอตัวอย่าง: ภาพที่อัดจากในเกมวนเป็นสไลด์ + กล่องท่าเคลื่อนไหว]  ขวา
//   [START ▶ วงโหลด]  ล่างกลาง: ยกมือค้าง 3 วิ (คนไหนก็ได้) / รอครบ AutoSeconds / Space
// ดึงข้อมูล: ภาพตัวอย่างจาก Resources/Sprites/<previewFolder>/*.png (เรียงตามชื่อ)
// =====================================================================================================
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class TutorialScreen
    {
        const float HoldSeconds = 3f, AutoSeconds = 20f, SlideSeconds = 1.4f;

        readonly GameObject root;
        readonly CanvasGroup group;
        readonly Image preview, startFill;
        readonly Text startText, startHint;
        readonly Sprite[] slides;
        float hold, shownTime;

        static readonly Color Neon = new Color(.25f, .9f, 1f), Panel = new Color(.02f, .08f, .14f, .88f);

        public TutorialScreen(Transform canvas, string gameLabel, string gameName, string category, string moves,
                              string[] steps, string moveIcon, string previewFolder, string background)
        {
            var rt = UIKit.Rect("Tutorial_" + gameLabel.Replace(" ", ""), canvas); UIKit.Stretch(rt);
            root = rt.gameObject;
            group = root.AddComponent<CanvasGroup>();

            var bg = UIKit.Img("Background", rt, UIKit.Load(background), Vector2.zero, false); UIKit.Stretch(bg.rectTransform);
            bg.color = new Color(.35f, .4f, .48f);
            var tint = UIKit.Img("Tint", rt, null, Vector2.zero, false); UIKit.Stretch(tint.rectTransform);
            tint.color = new Color(0, .05f, .12f, .45f);

            // ป้ายชื่อเกม
            var badge = UIKit.Panel("Badge", rt, new Vector2(360, 92), new Color(.05f, .45f, 1f, .95f));
            badge.rectTransform.anchoredPosition = new Vector2(0, 470);
            UIKit.Label("GameNo", badge.transform, gameLabel, 56, Color.white);
            var banner = UIKit.Panel("Banner", rt, new Vector2(1000, 120), Panel);
            banner.rectTransform.anchoredPosition = new Vector2(0, 360);
            UIKit.Img("BannerGlow", banner.transform, UIKit.Load("fx_glow"), new Vector2(1100, 160), false).color = new Color(Neon.r, Neon.g, Neon.b, .25f);
            UIKit.Label("Name", banner.transform, gameName, 78, Color.white);
            var cat = UIKit.Panel("Category", rt, new Vector2(1000, 64), new Color(1, 1, 1, .9f));
            cat.rectTransform.anchoredPosition = new Vector2(0, 260);
            UIKit.Label("Cat", cat.transform, $"หมวด {category}   |   ท่า {moves}", 32, new Color(.05f, .25f, .6f)).GetComponent<Shadow>().enabled = false;

            // คำอธิบายเกม 1–4 (ซ้าย)
            var left = UIKit.Panel("Steps", rt, new Vector2(820, 520), Panel);
            left.rectTransform.anchoredPosition = new Vector2(-480, -60);
            var head = UIKit.Panel("StepsHead", left.transform, new Vector2(300, 64), new Color(.05f, .45f, 1f, .95f));
            head.rectTransform.anchoredPosition = new Vector2(0, 250);
            UIKit.Label("H", head.transform, "คำอธิบายเกม", 36, Color.white);
            for (int i = 0; i < steps.Length && i < 4; i++)
            {
                float y = 150 - i * 110;
                var row = UIKit.Panel($"Row{i + 1}", left.transform, new Vector2(760, 92), new Color(.1f, .3f, .5f, .55f));
                row.rectTransform.anchoredPosition = new Vector2(0, y);
                var num = UIKit.Img("Num", row.transform, UIKit.Load("fx_circle"), new Vector2(72, 72), false);
                num.color = new Color(.05f, .45f, 1f); num.rectTransform.anchoredPosition = new Vector2(-330, 0);
                UIKit.Label("N", num.transform, (i + 1).ToString(), 46, Color.white);
                var t = UIKit.Label("Text", row.transform, steps[i], 32, Color.white, TextAnchor.MiddleLeft);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0, .5f);
                t.rectTransform.anchoredPosition = new Vector2(100, 0);
            }

            // จอตัวอย่าง (ขวา)
            var monitor = UIKit.Panel("Monitor", rt, new Vector2(860, 520), new Color(.6f, .85f, 1f, .9f));
            monitor.rectTransform.anchoredPosition = new Vector2(470, -60);
            var bezel = UIKit.Panel("Bezel", monitor.transform, new Vector2(830, 490), new Color(.03f, .05f, .08f, 1f));
            preview = UIKit.Img("Preview", bezel.transform, null, new Vector2(784, 441), true);
            preview.color = Color.white;
            var mask = bezel.gameObject.AddComponent<RectMask2D>();
            slides = Resources.LoadAll<Texture2D>("Sprites/" + previewFolder).OrderBy(x => x.name)
                .Select(tex => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f))).ToArray();
            if (slides.Length == 0)
            {
                preview.color = new Color(.1f, .2f, .3f);
                UIKit.Label("NoPreview", bezel.transform, "(ภาพตัวอย่างจากเกม)", 34, new Color(1, 1, 1, .6f));
            }
            var tag = UIKit.Panel("Tag", bezel.transform, new Vector2(440, 52), new Color(1, 1, 1, .92f));
            tag.rectTransform.anchoredPosition = new Vector2(-150, 200);
            UIKit.Label("TagText", tag.transform, $"MISSION PREVIEW  •  {gameLabel}", 26, new Color(.05f, .25f, .6f)).GetComponent<Shadow>().enabled = false;
            var move = UIKit.Panel("MoveBox", bezel.transform, new Vector2(270, 200), new Color(1, 1, 1, .92f));
            move.rectTransform.anchoredPosition = new Vector2(-250, -80);
            UIKit.Label("MoveHead", move.transform, "ท่าเคลื่อนไหว", 26, new Color(.05f, .35f, .9f)).rectTransform.anchoredPosition = new Vector2(0, 68);
            UIKit.Label("MoveIcon", move.transform, moveIcon, 26, new Color(.1f, .15f, .25f)).rectTransform.anchoredPosition = new Vector2(0, -10);

            // ปุ่ม START + วงโหลด
            var start = UIKit.Panel("Start", rt, new Vector2(440, 110), new Color(.05f, .45f, 1f, .97f));
            start.rectTransform.anchoredPosition = new Vector2(0, -410);
            startText = UIKit.Label("StartText", start.transform, "▶  START", 60, Color.white);
            startFill = UIKit.Img("Fill", start.transform, UIKit.Load("fx_ring"), new Vector2(96, 96), false);
            startFill.type = Image.Type.Filled; startFill.fillMethod = Image.FillMethod.Radial360;
            startFill.rectTransform.anchoredPosition = new Vector2(270, 0); startFill.color = Neon;
            startHint = UIKit.Label("StartHint", rt, "", 30, Color.white);
            startHint.rectTransform.anchoredPosition = new Vector2(0, -500);
            root.SetActive(false);
        }

        public void Show() { root.SetActive(true); hold = 0; shownTime = 0; group.alpha = 0; }
        public void Hide() => root.SetActive(false);

        // คืน true เมื่อพร้อมเริ่มเกม
        public bool Tick(TrackedPlayer[] players, bool online, bool skip)
        {
            float dt = Time.deltaTime;
            shownTime += dt;
            group.alpha = Mathf.Clamp01(shownTime / .6f);

            if (slides.Length > 0)
            {
                int idx = (int)(shownTime / SlideSeconds) % slides.Length;
                preview.sprite = slides[idx];
                float k = (shownTime % SlideSeconds) / SlideSeconds;
                preview.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, k);   // ซูมช้า ๆ ให้ดูเหมือนภาพเคลื่อนไหว
            }

            bool raised = online && (ArmRaise.Side(players[0], 0, true) >= 0 || ArmRaise.Side(players[1], 1, true) >= 0);
            hold = raised ? hold + dt : Mathf.Max(0, hold - dt * 2);
            float auto = Mathf.Clamp01(shownTime / AutoSeconds);
            startFill.fillAmount = Mathf.Max(hold / HoldSeconds, auto);
            startText.rectTransform.localScale = Vector3.one * (1 + .04f * Mathf.Sin(shownTime * 5));
            startHint.text = online
                ? $"ยกมือค้างไว้จนวงเต็ม เพื่อเริ่ม  (เริ่มเองใน {Mathf.CeilToInt(AutoSeconds - shownTime)} วิ)"
                : $"กด Space เพื่อเริ่ม  (เริ่มเองใน {Mathf.CeilToInt(AutoSeconds - shownTime)} วิ)";
            return hold >= HoldSeconds || shownTime >= AutoSeconds || (skip && shownTime > .3f);
        }
    }
}
