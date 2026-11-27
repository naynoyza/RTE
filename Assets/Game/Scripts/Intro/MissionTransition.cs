// =====================================================================================================
// MissionTransition — แอนิเมชันเปิดภารกิจก่อนเข้าเกม (ใช้ทั้งเกม 1 และเกม 2)
// -----------------------------------------------------------------------------------------------------
//   จอดำ -> เส้นแสงพุ่งออกจากกลางจอ เร็วขึ้นเรื่อย ๆ -> การ์ด MISSION 0X · ชื่อเกม · เป้าหมาย เด้งเข้า
//   -> แสงขาวเต็มจอ -> Tick() คืน true
// เกม 1: หลัง cutscene Start Game -> หน้าสอนเล่นจางจากขาว (WarpFlash)
// เกม 2: หลัง cutscene ท้ายเกม 1 -> โหลดฉาก The Last Seed (จางจากขาวเองเมื่อ GameSession.FromWarp)
// ใช้: var m = new MissionTransition(canvas, "MISSION 01", "WASTE HUNTERS", "...", ฟ้า, เหลือง); m.Play(); ... if (m.Tick()) ไปต่อ
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class MissionTransition
    {
        public const float WarpSeconds = 2.8f, FlashSeconds = .5f;
        const int Lines = 70;

        public static readonly Color Cyan = new Color(.25f, .95f, 1f), Yellow = new Color(1f, .8f, .1f),
                                     Green = new Color(.45f, 1f, .55f), Pink = new Color(1f, .35f, .8f);

        readonly GameObject root;
        readonly RectTransform card;
        readonly Image dark, flash, glow;
        readonly Image[] lines = new Image[Lines];
        readonly float[] ang = new float[Lines], r = new float[Lines], spd = new float[Lines];
        readonly Color accent, lineB;
        float t;
        bool whooshed;

        /// <param name="accent">สีกรอบ/แสงเรือง</param>
        /// <param name="stripe">สีแถบบน-ล่างในการ์ด</param>
        /// <param name="lineColor">สีเส้นแสงชุดที่ 2 (ชุดแรกใช้ accent)</param>
        public MissionTransition(Transform canvas, string mission, string title, string goal, Color accent, Color stripe, Color lineColor)
        {
            this.accent = accent; lineB = lineColor;
            var rt = UIKit.Rect("MissionTransition_" + mission, canvas); UIKit.Stretch(rt);
            root = rt.gameObject;
            dark = UIKit.Img("Dark", rt, null, Vector2.zero, false); UIKit.Stretch(dark.rectTransform);
            for (int i = 0; i < Lines; i++)
            {
                lines[i] = UIKit.Img("Line", rt, UIKit.Load("fx/light_beam"), new Vector2(8, 200), false);
                lines[i].rectTransform.pivot = new Vector2(.5f, 0);
            }

            card = UIKit.Rect("MissionCard", rt);
            card.sizeDelta = new Vector2(1000, 330);
            glow = UIKit.Img("Glow", card, UIKit.Load("fx_glow"), new Vector2(1500, 650), false);
            UIKit.Panel("Frame", card, new Vector2(1010, 300), new Color(accent.r, accent.g, accent.b, .95f));
            UIKit.Panel("Plate", card, new Vector2(994, 284), new Color(.03f, .07f, .12f, .96f));
            for (int k = 0; k < 2; k++)
                UIKit.Panel("Stripe", card, new Vector2(900, 10), stripe).rectTransform.anchoredPosition = new Vector2(0, k == 0 ? 128 : -128);
            UIKit.Label("Mission", card, mission, 40, new Color(1f, .82f, .2f)).rectTransform.anchoredPosition = new Vector2(0, 82);
            UIKit.Label("Name", card, title, 96, Color.white).rectTransform.anchoredPosition = new Vector2(0, 8);
            UIKit.Label("Goal", card, goal, 34, new Color(.7f, 1f, .75f)).rectTransform.anchoredPosition = new Vector2(0, -72);

            flash = UIKit.Img("Flash", rt, null, Vector2.zero, false); UIKit.Stretch(flash.rectTransform);
            root.SetActive(false);
        }

        public void Play()
        {
            root.SetActive(true); root.transform.SetAsLastSibling();
            t = 0; whooshed = false;
            for (int i = 0; i < Lines; i++) { ang[i] = Random.value * 360; r[i] = Random.Range(40f, 1100f); spd[i] = Random.Range(1400f, 2600f); }
            Sfx.Play("warp");
        }

        public void Hide() => root.SetActive(false);

        /// <summary>เรียกทุกเฟรม คืน true เมื่อขาวเต็มจอแล้ว (พร้อมเข้าเกม)</summary>
        public bool Tick()
        {
            t += Time.deltaTime;
            dark.color = new Color(0, .02f, .05f, Mathf.Clamp01(t / .4f));

            float speedUp = 1 + 2.5f * Mathf.Clamp01(t / WarpSeconds);
            for (int i = 0; i < Lines; i++)
            {
                r[i] += spd[i] * speedUp * Time.deltaTime;
                if (r[i] > 1300) { r[i] = Random.Range(30f, 200f); ang[i] = Random.value * 360; }
                var lt = lines[i].rectTransform;
                float a = ang[i] * Mathf.Deg2Rad;
                lt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r[i];
                lt.localRotation = Quaternion.Euler(0, 0, ang[i] - 90);
                lt.sizeDelta = new Vector2(6 + r[i] * .006f, 80 + r[i] * .35f * speedUp);
                var c = i % 3 == 0 ? accent : i % 3 == 1 ? lineB : Color.white;
                lines[i].color = new Color(c.r, c.g, c.b, Mathf.Clamp01(t / .6f) * Mathf.Clamp01(r[i] / 300f) * .9f);
            }

            bool showCard = t > .5f;
            card.gameObject.SetActive(showCard);
            if (showCard)
            {
                float k = Mathf.Clamp01((t - .5f) / .45f);
                card.localScale = new Vector3(UIKit.EaseOutBack(k), Mathf.Lerp(.05f, 1, Mathf.Sqrt(k)), 1);
                glow.color = new Color(accent.r, accent.g, accent.b, .45f + .2f * Mathf.Sin(t * 9));
            }

            float flashT = t - WarpSeconds;
            if (flashT > -.3f && !whooshed) { whooshed = true; Sfx.Play("flash"); }
            flash.color = new Color(1, 1, 1, Mathf.Clamp01(flashT / FlashSeconds));
            return flashT >= FlashSeconds;
        }
    }
}
