// =====================================================================================================
// LaunchSequence — ฉากออกภารกิจหลังเลือกตัวละครครบ (แทนการนับ 3-2-1 + ซูมจอดำแบบเดิม)
// -----------------------------------------------------------------------------------------------------
//   1) READY CHECK (ผู้เล่นทำเอง) : ชูแขนทั้งสองข้างค้าง 1 วิ = "พร้อม!" วงจุดยืนเต็ม + แสงวาบ
//                                    พร้อมครบ 2 คน -> ไปต่อทันที  (ไม่มีใครทำ = ออกเองใน AutoSeconds)
//   2) TELEPORT                      : เสาแสงพุ่งแรง ตัวละครยืดเป็นลำแสงแล้วถูกดูดขึ้นไป
//   3) WARP + START                  : เส้นความเร็วพุ่งเข้าหากล้อง, การ์ด START + การ์ดตัวละครของ P1/P2 สไลด์เข้า
//   4) FLASH                          : แสงขาววาบ -> Done (เข้าหน้าสอนเล่นเกม 1)
// คลาสนี้ดูแลเฉพาะชั้น UI ด้านบน (เส้นความเร็ว, การ์ดภารกิจ, แฟลช) — ตัวละคร/เสาแสงอยู่ใน CharacterSelect
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class LaunchSequence
    {
        public const float ReadyHold = 1f, AutoSeconds = 8f, TeleportSeconds = 1.3f, WarpSeconds = 2.6f, FlashSeconds = .45f;
        const int Lines = 70;

        readonly RectTransform root, card;
        readonly Image dark, flash, cardGlow;
        readonly Image[] lines = new Image[Lines];
        readonly float[] lineAng = new float[Lines], lineR = new float[Lines], lineSpd = new float[Lines];
        readonly RectTransform[] heroCard = new RectTransform[2];
        readonly Image[] heroImg = new Image[2];
        readonly Text[] heroTag = new Text[2];
        readonly Text banner, bannerSub;

        static readonly Color Pink = new Color(1f, .35f, .8f), Cyan = new Color(.25f, .95f, 1f);

        public LaunchSequence(Transform parent)
        {
            root = UIKit.Rect("LaunchSequence", parent); UIKit.Stretch(root);

            // ป้าย READY CHECK กลางล่าง (เห็นเฉพาะช่วงที่ 1)
            banner = UIKit.Label("ReadyBanner", root, "", 54, Color.white);
            banner.rectTransform.anchoredPosition = new Vector2(0, -210);
            banner.GetComponent<Shadow>().effectDistance = new Vector2(4, -4);
            bannerSub = UIKit.Label("ReadySub", root, "", 32, new Color(.8f, .95f, 1f));
            bannerSub.rectTransform.anchoredPosition = new Vector2(0, -275);

            dark = UIKit.Img("Dark", root, null, Vector2.zero, false); UIKit.Stretch(dark.rectTransform);
            // เส้นความเร็ว: พุ่งออกจากกลางจอ
            for (int i = 0; i < Lines; i++)
            {
                lines[i] = UIKit.Img("Line", root, UIKit.Load("fx/light_beam"), new Vector2(8, 200), false);
                lines[i].rectTransform.pivot = new Vector2(.5f, 0);
                lineAng[i] = Random.value * 360; lineR[i] = Random.Range(40f, 1100f); lineSpd[i] = Random.Range(1400f, 2600f);
            }

            // การ์ดภารกิจ
            card = UIKit.Rect("MissionCard", root);
            card.sizeDelta = new Vector2(1000, 330);
            cardGlow = UIKit.Img("Glow", card, UIKit.Load("fx_glow"), new Vector2(1500, 650), false);
            var frame = UIKit.Panel("Frame", card, new Vector2(1010, 300), new Color(.25f, .95f, 1f, .95f));
            UIKit.Panel("Plate", card, new Vector2(994, 284), new Color(.03f, .07f, .12f, .96f));
            for (int k = 0; k < 2; k++)                                        // แถบเหลือง-ดำบน/ล่าง
                UIKit.Panel("Stripe", card, new Vector2(900, 10), new Color(1f, .8f, .1f)).rectTransform.anchoredPosition = new Vector2(0, k == 0 ? 128 : -128);
            var start = UIKit.Label("Start", card, "START", 190, Color.white);     // คำว่า START ตัวใหญ่สีขาวเต็มกรอบ
            start.rectTransform.sizeDelta = new Vector2(980, 260);
            start.rectTransform.anchoredPosition = new Vector2(0, -4);

            // การ์ดตัวละครของผู้เล่น (สไลด์เข้าจากข้าง)
            for (int i = 0; i < 2; i++)
            {
                heroCard[i] = UIKit.Rect($"P{i + 1}HeroCard", root);
                heroCard[i].sizeDelta = new Vector2(420, 420);                     // การ์ดตัวละครใหญ่ (เดิม 300)
                var rim = UIKit.Panel("Rim", heroCard[i], new Vector2(420, 448), i == 0 ? Pink : Cyan);
                heroImg[i] = UIKit.Img("Hero", heroCard[i], null, new Vector2(395, 395), true);
                heroTag[i] = UIKit.Label("Tag", heroCard[i], "", 56, i == 0 ? Pink : Cyan);
                heroTag[i].rectTransform.anchoredPosition = new Vector2(0, -270);
            }

            flash = UIKit.Img("Flash", root, null, Vector2.zero, false); UIKit.Stretch(flash.rectTransform);
            Reset();
        }

        public void Reset()
        {
            banner.text = bannerSub.text = "";
            dark.color = flash.color = cardGlow.color = Color.clear;
            foreach (var l in lines) l.color = Color.clear;
            card.gameObject.SetActive(false);
            foreach (var h in heroCard) h.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 1) READY CHECK
        public void ReadyCheck(float t, bool[] ready)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(AutoSeconds - t));
            banner.text = ready[0] && ready[1] ? "พร้อมออกภารกิจ!" : "ชูแขนทั้ง 2 ข้าง  =  พร้อมออกภารกิจ!";
            banner.color = ready[0] && ready[1] ? new Color(.45f, 1f, .5f) : Color.Lerp(Color.white, new Color(1f, .85f, .3f), Mathf.PingPong(t * 2, 1));
            banner.rectTransform.localScale = Vector3.one * (1 + .04f * Mathf.Sin(t * 8));
            bannerSub.text = $"READY CHECK  •  P1 {(ready[0] ? "✓" : "...")}   P2 {(ready[1] ? "✓" : "...")}   •  ออกเองใน {left} วิ";
        }

        // ---------------------------------------------------------------- 2-4) TELEPORT / WARP / FLASH
        // t = เวลาตั้งแต่จบ READY CHECK ; คืน true เมื่อจบทั้งหมด
        public bool Launch(float t, Hero[] heroes)
        {
            banner.text = bannerSub.text = "";
            float warpT = t - TeleportSeconds;
            float flashT = warpT - WarpSeconds;

            // ฉากหลังมืดลงตอนวาร์ป
            dark.color = new Color(0, .02f, .06f, Mathf.Clamp01(warpT / .5f) * .88f);

            // เส้นความเร็ว: ยาวและเร็วขึ้นเรื่อย ๆ
            float warp = Mathf.Clamp01(warpT / .6f);
            float speedUp = 1 + 2.5f * Mathf.Clamp01(warpT / WarpSeconds);
            for (int i = 0; i < Lines; i++)
            {
                if (warpT <= 0) { lines[i].color = Color.clear; continue; }
                lineR[i] += lineSpd[i] * speedUp * Time.deltaTime;
                if (lineR[i] > 1300) { lineR[i] = Random.Range(30f, 200f); lineAng[i] = Random.value * 360; }
                var rt = lines[i].rectTransform;
                float a = lineAng[i] * Mathf.Deg2Rad;
                rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * lineR[i];
                rt.localRotation = Quaternion.Euler(0, 0, lineAng[i] - 90);
                rt.sizeDelta = new Vector2(6 + lineR[i] * .006f, 80 + lineR[i] * .35f * speedUp);
                var c = i % 3 == 0 ? Pink : i % 3 == 1 ? Cyan : Color.white;
                lines[i].color = new Color(c.r, c.g, c.b, warp * Mathf.Clamp01(lineR[i] / 300f) * .9f);
            }

            // การ์ดภารกิจ: เด้งเข้ากลางจอ
            bool showCard = warpT > .35f;
            card.gameObject.SetActive(showCard);
            if (showCard)
            {
                float k = Mathf.Clamp01((warpT - .35f) / .45f);
                card.localScale = new Vector3(UIKit.EaseOutBack(k), Mathf.Lerp(.05f, 1, Mathf.Sqrt(k)), 1);
                card.anchoredPosition = new Vector2(0, 230);   // ค่อนไปด้านบน ห่างการ์ดตัวละคร P1/P2 ด้านล่าง
                cardGlow.color = new Color(.3f, .9f, 1f, .45f + .2f * Mathf.Sin(t * 9));
            }

            // การ์ดตัวละคร P1 / P2 สไลด์เข้าจากซ้าย/ขวา
            for (int i = 0; i < 2; i++)
            {
                bool on = warpT > .7f + i * .15f;
                heroCard[i].gameObject.SetActive(on);
                if (!on) continue;
                float k = UIKit.EaseOutBack(Mathf.Clamp01((warpT - .7f - i * .15f) / .45f));
                float side = i == 0 ? -1 : 1;
                heroCard[i].anchoredPosition = new Vector2(side * Mathf.Lerp(1300, 380, k), -190);
                heroCard[i].localRotation = Quaternion.Euler(0, 0, side * -4);
                var h = heroes[i] == Hero.None ? (Hero)i : heroes[i];
                heroImg[i].sprite = UIKit.Load(h == Hero.Male ? "intro/card_male" : "intro/card_female");
                heroTag[i].text = $"P{i + 1}  {(h == Hero.Male ? "MALE" : "FEMALE")}";
            }

            // แฟลชขาว
            flash.color = new Color(1, 1, 1, flashT > 0 ? Mathf.Clamp01(flashT / FlashSeconds) : 0);
            return flashT >= FlashSeconds;
        }
    }
}
