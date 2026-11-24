// =====================================================================================================
// PracticeOverlay — สอนเล่นเกม 1 แบบซ้อนบนฉากเกมจริง (In-Scene Overlay) ตามแบบ Ref/สอนเกม1.jpg
// -----------------------------------------------------------------------------------------------------
//   บนกลาง : "HOW TO PLAY • START IN 12s" (แทนที่นาฬิกาชั่วคราว)
//   กลางจอ : แผง 2 การ์ด  1. ยกมือเล็ง = ดูดขยะ   2. ย่ำเท้าอยู่กับที่ = เร่งความเร็ว  + แถบเตือนวงล็อคแดง
//   ล่างแต่ละฝั่ง : กล่อง PRACTICE เช็กลิสต์ 2 ข้อ (ทำสลับลำดับได้) -> ครบ = กล่องเขียว + ป้าย READY!
// จบเมื่อ: READY ทั้งคู่ (ค้างให้เห็น 0.8 วิ) หรือครบ MaxSeconds (Auto Start)
// ข้อมูล : PlayerLane.DidVacuum / DidStep (ผู้เล่นเล่นในฉากจริง ขยะซ้อมไม่เคลื่อน)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class PracticeOverlay
    {
        public const float MaxSeconds = 15f;

        readonly GameObject root;
        readonly Text header;
        readonly Image[] box = new Image[2], readyBadge = new Image[2];
        readonly Text[] boxText = new Text[2];
        readonly Image cardIcon;
        readonly Text[] chevrons = new Text[3];
        float time, bothReadyTime;

        static readonly Color Metal = new Color(.08f, .09f, .1f, 1f), Green = new Color(.3f, 1f, .35f);

        public PracticeOverlay(Transform canvas)
        {
            var rt = UIKit.Rect("PracticeOverlay", canvas); UIKit.Stretch(rt);
            root = rt.gameObject;

            // หัว HOW TO PLAY (ใช้กรอบนาฬิกาเดิม)
            var frame = UIKit.Img("HowToPlay", rt, UIKit.Load("wh/timer"), new Vector2(430, 178));
            UIKit.Anchor(frame.rectTransform, new Vector2(.5f, 1), new Vector2(0, -6));
            var cover = UIKit.Panel("Cover", frame.transform, new Vector2(330, 100), new Color(.13f, .15f, .18f, 1));
            cover.rectTransform.anchoredPosition = new Vector2(0, 4);
            header = UIKit.Label("Text", frame.transform, "", 38, Color.white);
            header.rectTransform.anchoredPosition = new Vector2(0, 4);

            // แผงกลาง 2 การ์ด
            var panel = UIKit.Panel("HowPanel", rt, new Vector2(720, 500), Metal);
            panel.rectTransform.anchoredPosition = new Vector2(0, -30);
            var rim = UIKit.Panel("Rim", panel.transform, new Vector2(740, 520), new Color(.55f, .6f, .65f, .9f));
            rim.transform.SetAsFirstSibling();
            for (int c = 0; c < 2; c++)
            {
                var card = UIKit.Panel($"Card{c + 1}", panel.transform, new Vector2(320, 240), new Color(.2f, .22f, .25f, 1));
                card.rectTransform.anchoredPosition = new Vector2(c == 0 ? -168 : 168, 105);
                UIKit.Label("Caption", card.transform, c == 0 ? "1. ยกมือเล็ง\nแตะขยะ = ดูด" : "2. ย่ำเท้าอยู่กับที่\n= เร่งความเร็ว", 32, Color.white)
                    .rectTransform.anchoredPosition = new Vector2(0, -170);
                if (c == 0)
                {
                    var ring = UIKit.Img("Aim", card.transform, UIKit.Load("fx_ring"), new Vector2(170, 170), false);
                    ring.color = GameSettings.PlayerColors[0];
                    cardIcon = UIKit.Img("Trash", ring.transform, UIKit.Load("wh/t_battery"), new Vector2(100, 100));
                }
                else
                    for (int k = 0; k < 3; k++)
                    {
                        chevrons[k] = UIKit.Label($"Chevron{k}", card.transform, "▲", 70, new Color(.3f, .9f, 1f));
                        chevrons[k].rectTransform.anchoredPosition = new Vector2(0, -60 + k * 55);
                    }
            }
            var foot = UIKit.Panel("Footer", panel.transform, new Vector2(690, 70), new Color(.05f, .05f, .06f, 1));
            foot.rectTransform.anchoredPosition = new Vector2(0, -210);
            UIKit.Label("Warn", foot.transform, "◎  ขยับมือตลอด อย่าเล็งค้าง ไม่งั้นวงเล็งจะแดงล็อค", 30, new Color(1f, .3f, .25f));

            // เช็กลิสต์ + READY! ของแต่ละฝั่ง (เหนือช่อง GOAL)
            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                float side = left ? 1 : -1;
                var corner = new Vector2(left ? 0 : 1, 0);
                box[i] = UIKit.Panel($"P{i + 1}Practice", rt, new Vector2(340, 170), new Color(.05f, .1f, .2f, .88f));
                UIKit.Anchor(box[i].rectTransform, corner, new Vector2(40 * side, 200));
                boxText[i] = UIKit.Label("Text", box[i].transform, "", 32, Color.white, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleLeft);
                boxText[i].rectTransform.anchoredPosition = new Vector2(-140, 0);
                boxText[i].rectTransform.pivot = new Vector2(0, .5f);
                boxText[i].alignment = TextAnchor.MiddleLeft;

                readyBadge[i] = UIKit.Panel($"P{i + 1}Ready", rt, new Vector2(300, 100), new Color(.05f, .25f, .08f, .95f));
                UIKit.Anchor(readyBadge[i].rectTransform, corner, new Vector2(60 * side, 390));
                UIKit.Label("Text", readyBadge[i].transform, "READY!", 66, Green);
            }
            root.SetActive(false);
        }

        public void Show() { root.SetActive(true); time = bothReadyTime = 0; }
        public void Hide() => root.SetActive(false);

        // คืน true = ปิดหน้าต่างสอนแล้วเข้า 3-2-1-GO!
        public bool Tick(PlayerLane[] lanes, bool skip)
        {
            float dt = Time.deltaTime;
            time += dt;
            int left = Mathf.Max(0, Mathf.CeilToInt(MaxSeconds - time));
            header.text = $"• HOW TO PLAY •\nSTART IN <color=#FFD23C>{left}s</color>";

            bool all = true;
            for (int i = 0; i < 2; i++)
            {
                var l = lanes[i];
                bool ready = l.DidVacuum && l.DidStep;
                all &= ready;
                boxText[i].text = "PRACTICE\n" + Check(l.DidVacuum) + " ดูดขยะ 1 ชิ้น\n" + Check(l.DidStep) + " ย่ำเท้าวิ่ง";
                boxText[i].color = ready ? Green : Color.white;
                box[i].color = ready ? new Color(.05f, .3f, .1f, .92f) : new Color(.05f, .1f, .2f, .88f);
                readyBadge[i].gameObject.SetActive(ready);
                readyBadge[i].rectTransform.localScale = Vector3.one * (1 + .05f * Mathf.Sin(Time.time * 8));
            }

            // แอนิเมชันการ์ด: วงเล็งโยก, ลูกศรวิ่งขึ้น
            cardIcon.rectTransform.parent.localPosition = new Vector3(Mathf.Sin(time * 3) * 50, Mathf.Cos(time * 2) * 12, 0);
            for (int k = 0; k < 3; k++)
                chevrons[k].color = new Color(.3f, .9f, 1f, (Mathf.Sin(time * 8 - k * 1.2f) + 1) * .5f);

            bothReadyTime = all ? bothReadyTime + dt : 0;
            return bothReadyTime >= .8f || time >= MaxSeconds || (skip && time > .3f);
        }

        static string Check(bool done) => done ? "<color=#4CFF5A>[✓]</color>" : "[  ]";
    }
}
