// =====================================================================================================
// CenterOverlay — ข้อความใหญ่กลางจอ (นับถอยหลัง) และหน้าสรุปผลแบบรูป
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : Countdown(): จอมืดลง + 3-2-1-GO! พร้อมวิธีเล่น
//           ShowResult(): หน้าสรุปผลภารกิจ 1 — ป้าย WINNER, การ์ด P1 (ฟ้า) / P2 (ส้ม), เกจ Air Recovery, ดาว 1–3
//           ShowCards():  หน้าสรุปผลแบบเดียวกันที่ภารกิจอื่นกำหนดเนื้อหาเอง (ภารกิจ 2 ใช้)
//           ShowMessage(): ข้อความใด ๆ บนแผงมืด (สำรอง)
// ดึงข้อมูล: คะแนน/รีไซเคิล/ค่าผลกระทบ จาก PlayerLane ทั้งสองฝั่ง
// รูป     : Resources/Sprites/ui2/ result_frame, winner_banner, air_gauge, stars_empty, star_gold
// ใช้โดย  : WasteHunters (Countdown, Result), LastSeed (Countdown, Result)
// =====================================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class CenterOverlay
    {
        /// <summary>เนื้อหาการ์ด 1 ใบในหน้าสรุปผล (หัวข้อสีผู้เล่น + รายละเอียดหลายบรรทัด)</summary>
        public struct Card { public string Title, Body; }

        readonly Image dim, panel;
        readonly Text big, sub;

        // หน้าสรุปผลแบบรูป
        readonly RectTransform result;
        readonly Image banner, gauge, starsBg;
        readonly Image[] stars = new Image[3];
        readonly Text bannerLabel, gaugeText, centerTitle;
        readonly Text[] cardTitle = new Text[2], cardBody = new Text[2];

        static readonly Vector2 FrameSize = new Vector2(1400, 692);
        const float CardX = 405;                       // กลางการ์ดซ้าย/ขวาในรูป result_frame
        static readonly float[] StarX = { -84, 0, 84 }; // ช่องดาวในรูป stars_empty

        public CenterOverlay(Transform canvas)
        {
            dim = UIKit.Img("Dim", canvas, null, Vector2.zero, false); UIKit.Stretch(dim.rectTransform);
            dim.color = new Color(0, 0, 0, .55f);
            panel = UIKit.Panel("ResultPanel", canvas, new Vector2(1320, 460), new Color(.05f, .07f, .1f, .85f));
            panel.rectTransform.anchoredPosition = new Vector2(0, 20);
            big = UIKit.Label("Big", canvas, "", 150, Color.white);
            big.rectTransform.anchoredPosition = new Vector2(0, 80);
            sub = UIKit.Label("Sub", canvas, "", 44, new Color(.85f, 1f, .75f));
            sub.rectTransform.anchoredPosition = new Vector2(0, -60);

            // ---------------- หน้าสรุปผล
            result = UIKit.Rect("ResultScreen", canvas);
            result.sizeDelta = FrameSize;
            UIKit.Img("Frame", result, UIKit.Load("ui2/result_frame"), FrameSize);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -CardX : CardX;
                cardTitle[i] = UIKit.Label($"CardTitle{i + 1}", result, "", 66, GameSettings.PlayerColors[i]);
                cardTitle[i].rectTransform.anchoredPosition = new Vector2(x + (i == 0 ? 30 : -30), 62);   // ใต้ช่องไอคอนของการ์ด
                cardBody[i] = UIKit.Label($"CardBody{i + 1}", result, "", 40, Color.white);
                cardBody[i].rectTransform.anchoredPosition = new Vector2(x, -60);
                cardBody[i].rectTransform.sizeDelta = new Vector2(420, 260);
            }
            gauge = UIKit.Img("Gauge", result, UIKit.Load("ui2/air_gauge"), new Vector2(300, 300));
            gauge.rectTransform.anchoredPosition = new Vector2(0, -5);
            gaugeText = UIKit.Label("GaugeText", gauge.transform, "", 40, Color.white);
            gaugeText.rectTransform.anchoredPosition = new Vector2(0, -78);
            centerTitle = UIKit.Label("CenterTitle", result, "", 34, Color.white);
            centerTitle.rectTransform.anchoredPosition = new Vector2(0, -182);
            centerTitle.rectTransform.sizeDelta = new Vector2(900, 50);

            banner = UIKit.Img("Banner", result, UIKit.Load("ui2/winner_banner"), new Vector2(660, 222));
            banner.rectTransform.anchoredPosition = new Vector2(0, 330);   // ทับป้าย RESULT ของกรอบ
            bannerLabel = UIKit.Label("BannerLabel", result, "", 90, Color.white);
            bannerLabel.rectTransform.anchoredPosition = new Vector2(0, 360);

            starsBg = UIKit.Img("Stars", result, UIKit.Load("ui2/stars_empty"), new Vector2(330, 128));
            starsBg.rectTransform.anchoredPosition = new Vector2(0, -330);
            for (int i = 0; i < 3; i++)
            {
                stars[i] = UIKit.Img($"Star{i + 1}", starsBg.transform, UIKit.Load("ui2/star_gold"), new Vector2(78, 74));
                stars[i].rectTransform.anchoredPosition = new Vector2(StarX[i], 2);
            }
            Hide();
        }

        public void Hide()
        {
            dim.enabled = panel.enabled = false; big.text = sub.text = "";
            result.gameObject.SetActive(false);
        }

        // 3-2-1-GO! แต่ละตัวเด้งขยายแล้วค้าง 0.9 วินาที
        // hint = คำอธิบายวิธีเล่นใต้ตัวเลข (ไม่ใส่ = ของ Waste Hunters)
        public IEnumerator Countdown(string hint = null)
        {
            dim.enabled = true;
            sub.text = hint ?? "ยกมือเล็งปืนให้แตะขยะ = ดูดทันที  •  เดินอัตโนมัติ ย่ำเท้า = เร่งสปีด";
            foreach (var s in new[] { "3", "2", "1", "GO!" })
            {
                big.text = s;
                if (s == "GO!") Sfx.Play("count_go"); else Sfx.Play("count_beep");   // เสียงตรงกับตัวเลขที่ขึ้นทุกตัว
                for (float t = 0; t < .9f; t += Time.deltaTime)
                {
                    big.rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(Mathf.Min(1, t / .3f));
                    yield return null;
                }
            }
            Hide();
        }

        // ข้อความใหญ่ + คำอธิบาย บนแผงกลางจอ (สำรอง)
        public void ShowMessage(string title, string detail)
        {
            dim.enabled = panel.enabled = true;
            big.rectTransform.localScale = Vector3.one;
            big.text = title; sub.text = detail;
        }

        /// <summary>
        /// หน้าสรุปผลแบบรูป. bannerSprite = ป้ายด้านบน (null = ไม่มีป้าย ใช้ bannerText เป็นหัวข้อแทน),
        /// gaugeSprite = รูปวงกลางการ์ด, starCount = 0–3, scale/y = ย่อและเลื่อนทั้งหน้า (ไม่บังฉากสำคัญ).
        /// ตัวอย่าง: overlay.ShowCards("ui2/tree_banner", "", left, right, "ui2/air_gauge", "96%", "ความสมดุล", 3, .8f, -150);
        /// </summary>
        public void ShowCards(string bannerSprite, string bannerText, Card left, Card right,
                              string gaugeSprite, string gaugeValue, string gaugeTitle, int starCount, float scale = 1, float y = 0, bool dimBack = true)
        {
            dim.enabled = dimBack; panel.enabled = false; big.text = sub.text = "";
            result.gameObject.SetActive(true);
            result.localScale = Vector3.one * scale;
            result.anchoredPosition = new Vector2(0, y);

            banner.enabled = bannerSprite != null;
            if (bannerSprite != null)
            {
                banner.sprite = UIKit.Load(bannerSprite);
                var s = banner.sprite.rect.size;
                banner.rectTransform.sizeDelta = new Vector2(660, 660 * s.y / s.x);
            }
            bannerLabel.text = bannerText;
            bannerLabel.rectTransform.anchoredPosition = new Vector2(0, bannerSprite != null ? 465 : 420);   // ไม่มีป้าย: หัวข้ออยู่เหนือป้าย RESULT ของกรอบ

            cardTitle[0].text = left.Title; cardBody[0].text = left.Body;
            cardTitle[1].text = right.Title; cardBody[1].text = right.Body;
            gauge.sprite = UIKit.Load(gaugeSprite);
            gaugeText.text = gaugeValue; centerTitle.text = gaugeTitle;
            for (int i = 0; i < 3; i++) stars[i].enabled = i < starCount;
        }

        /// <summary>ดาว 1–3 จากค่าร้อยละ 0..1 (ใช้ทั้งสองภารกิจ)</summary>
        public static int Stars(float ratio) => ratio >= .7f ? 3 : ratio >= .4f ? 2 : 1;

        // ภารกิจ 1: Air Recovery = คะแนนผลกระทบของขยะที่เก็บได้ ÷ ของขยะที่เกิดทั้งหมด (ทั้งทีม)
        public void ShowResult(PlayerLane p1, PlayerLane p2)
        {
            int collected = p1.ImpactCollected + p2.ImpactCollected;
            int spawned = Mathf.Max(1, p1.ImpactSpawned + p2.ImpactSpawned);
            float air = (float)collected / spawned;
            bool tie = p1.Score == p2.Score;
            string winner = tie ? "เสมอ!" : p1.Score > p2.Score ? "P1" : "P2";
            ShowCards(tie ? null : "ui2/winner_banner", winner,
                      new Card { Title = "P1", Body = $"{p1.Score} คะแนน\nรีไซเคิล {p1.Recycled} ชิ้น" },
                      new Card { Title = "P2", Body = $"{p2.Score} คะแนน\nรีไซเคิล {p2.Recycled} ชิ้น" },
                      "ui2/air_gauge", $"{air * 100:0}%", "อากาศสะอาดขึ้น (ทั้งทีม)", Stars(air));
            if (!tie) bannerLabel.color = GameSettings.PlayerColors[p1.Score > p2.Score ? 0 : 1];
            else bannerLabel.color = Color.white;
        }
    }
}
