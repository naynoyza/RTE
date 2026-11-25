// =====================================================================================================
// CharacterSelect — หน้าเลือกตัวละคร (พื้นหลัง Background menu + UI ฮอโลแกรมนีออน + การ์ดตัวละครคู่)
// -----------------------------------------------------------------------------------------------------
// ผู้เล่นยกแขนค้าง HoldSeconds:  ด้านซ้าย = MALE, ด้านขวา = FEMALE
// ตัวที่ถูกอีกคนเลือกแล้วจะล็อค (เลือกซ้ำไม่ได้ ต้องใช้อีกตัว) เมื่อเลือกได้ โมเดล 3D จะขึ้นยืนบนวงกลมฝั่งตัวเอง
// ได้ครบ 2 คน -> นับถอยหลัง 3 วิ -> ซูมเข้าแบบ cinematic + จอดำ -> Tick() คืน Done
// P1 = ฝั่งซ้าย (วงชมพู), P2 = ฝั่งขวา (วงฟ้า)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class CharacterSelect
    {
        public enum Result { Selecting, Done }
        const float HoldSeconds = 3.5f, CountSeconds = 3f, ZoomSeconds = 1.6f;

        readonly RectTransform root, zoomLayer;
        readonly Image fade;
        readonly Text title, sub, centerStatus, big;
        readonly Image[] cards = new Image[2], cardLock = new Image[2], sidePanel = new Image[2];
        readonly Text[] cardTag = new Text[2];
        readonly Image[] progress = new Image[2], progressTrack = new Image[2];
        readonly Text[] progressNum = new Text[2];
        const float ProgressY = -110;   // วงโหลดลอยเหนือวงเลือก (ไม่จมไปกับวงบนพื้น)
        readonly Text[] pLabel = new Text[2];
        readonly ModelStage[] stages = new ModelStage[2];
        readonly RingFx[] ringFx = new RingFx[2];
        readonly float[] hold = new float[2];
        readonly Vector2[] feetSpot = new Vector2[2];   // จุดกลางวงกลมที่เท้าตัวละครต้องเหยียบ (พิกัด UI)
        const float ModelH = 860f;
        readonly int[] holdSide = { -1, -1 };
        float countT, zoomT;
        int phase;   // 0 เลือก, 1 นับถอยหลัง, 2 ซูม

        static Color PColor(int i) => GameSettings.PlayerColors[i];   // P1 ฟ้า, P2 ส้ม — สีเดียวทั้งเกม

        public CharacterSelect(Transform canvas)
        {
            root = UIKit.Rect("CharacterSelect", canvas); UIKit.Stretch(root);
            zoomLayer = UIKit.Rect("ZoomLayer", root); UIKit.Stretch(zoomLayer);
            var bg = UIKit.Img("Background", zoomLayer, UIKit.Load("intro/bg_menu"), Vector2.zero, false);
            UIKit.Stretch(bg.rectTransform);

            title = UIKit.Label("Title", zoomLayer, "SELECT CHARACTER", 70, Color.white);
            title.rectTransform.anchoredPosition = new Vector2(0, 475);
            sub = UIKit.Label("Sub", zoomLayer, "ยกแขนค้างไว้ 3 วินาที   ◀ แขนซ้าย = MALE   •   FEMALE = แขนขวา ▶", 34, new Color(.75f, .95f, 1f));
            sub.rectTransform.anchoredPosition = new Vector2(0, 405);

            // แผงกลาง + การ์ด 2 ใบในช่อง
            var center = UIKit.Img("HoloCenter", zoomLayer, UIKit.Load("intro/holo_center"), new Vector2(720, 517), true);
            center.rectTransform.anchoredPosition = new Vector2(0, 120);
            for (int c = 0; c < 2; c++)
            {
                cards[c] = UIKit.Img(c == 0 ? "CardMale" : "CardFemale", center.transform,
                    UIKit.Load(c == 0 ? "intro/card_male" : "intro/card_female"), new Vector2(285, 262), true);
                cards[c].rectTransform.anchoredPosition = new Vector2(c == 0 ? -152 : 152, 4);
                cardLock[c] = UIKit.Img("Lock", cards[c].transform, null, new Vector2(250, 262), false);
                cardLock[c].color = new Color(0, 0, 0, 0);
                cardTag[c] = UIKit.Label("Tag", cards[c].transform, "", 30, Color.white);
                cardTag[c].rectTransform.anchoredPosition = new Vector2(0, 150);
            }
            centerStatus = UIKit.Label("Status", center.transform, "", 30, Color.white);
            centerStatus.rectTransform.anchoredPosition = new Vector2(0, -186);

            // แผงยกแขนซ้าย/ขวา
            for (int s = 0; s < 2; s++)
            {
                sidePanel[s] = UIKit.Img(s == 0 ? "RaiseLeft" : "RaiseRight", zoomLayer,
                    UIKit.Load(s == 0 ? "intro/holo_left" : "intro/holo_right"), new Vector2(210, 141), true);
                sidePanel[s].rectTransform.anchoredPosition = new Vector2(s == 0 ? -490 : 490, 160);   // ห่างแผงกลาง ไม่ชนโมเดล
                UIKit.Label("Cap", sidePanel[s].transform, s == 0 ? "ยกแขนซ้าย = MALE" : "ยกแขนขวา = FEMALE", 28, Color.white)
                    .rectTransform.anchoredPosition = new Vector2(0, -102);
            }

            // วงกลม + โมเดล + แถบความคืบหน้าของผู้เล่นแต่ละคน
            for (int i = 0; i < 2; i++)
            {
                // แท่นยืน: สีประจำผู้เล่น (ui2/ring_p1 ฟ้า, ring_p2 ส้ม) อยู่ในจอทั้งวง ไม่ชนป้ายชื่อด้านล่าง
                float x = i == 0 ? -715 : 715;
                const float RingW = 470, RingY = -318, RingScale = RingW / 560f;
                var ring = UIKit.Img($"Ring{i + 1}", zoomLayer, UIKit.Load(i == 0 ? "ui2/ring_p1" : "ui2/ring_p2"), new Vector2(RingW, 317 * RingScale), true);
                ring.rectTransform.anchoredPosition = new Vector2(x, RingY);
                feetSpot[i] = new Vector2(x + (i == 0 ? -15 : 8) * RingScale, RingY - 62 * RingScale);   // กลางวงในของรูปวงกลม
                var fxBack = UIKit.Rect($"RingFxBack{i + 1}", zoomLayer); UIKit.Stretch(fxBack);
                stages[i] = new ModelStage(zoomLayer, i, new Vector2(ModelH * .58f, ModelH));
                var fxFront = UIKit.Rect($"RingFxFront{i + 1}", zoomLayer); UIKit.Stretch(fxFront);
                ringFx[i] = new RingFx(fxBack, fxFront, feetSpot[i], PColor(i));
                PlaceModel(i);

                // วงโหลดยกแขนค้าง: รางจาง ๆ + วงเติมสีผู้เล่น + ตัวเลขนับถอยหลังตรงกลาง
                progressTrack[i] = UIKit.Img($"ProgressTrack{i + 1}", zoomLayer, UIKit.Load("ui2/hold_ring"), new Vector2(190, 190), false);
                progressTrack[i].rectTransform.anchoredPosition = new Vector2(x, ProgressY);
                progress[i] = UIKit.Img($"Progress{i + 1}", zoomLayer, UIKit.Load("ui2/hold_ring"), new Vector2(190, 190), false);
                progress[i].type = Image.Type.Filled; progress[i].fillMethod = Image.FillMethod.Radial360;
                progress[i].fillOrigin = (int)Image.Origin360.Top; progress[i].fillClockwise = true;
                progress[i].color = PColor(i);
                progress[i].rectTransform.anchoredPosition = new Vector2(x, ProgressY);
                progressNum[i] = UIKit.Label($"ProgressNum{i + 1}", progress[i].transform, "", 64, Color.white);
                pLabel[i] = UIKit.Label($"PLabel{i + 1}", zoomLayer, "", 36, PColor(i));
                pLabel[i].rectTransform.anchoredPosition = new Vector2(x, -495);
            }

            big = UIKit.Label("Big", root, "", 220, Color.white);
            big.rectTransform.anchoredPosition = new Vector2(0, -250);
            fade = UIKit.Img("Fade", root, null, Vector2.zero, false); UIKit.Stretch(fade.rectTransform);
            fade.color = new Color(0, 0, 0, 0);
            Hide();
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
            GameSession.Clear();
            phase = 0; countT = zoomT = 0;
            zoomLayer.localScale = Vector3.one; zoomLayer.anchoredPosition = Vector2.zero;
            fade.color = new Color(0, 0, 0, 0); big.text = "";
            for (int i = 0; i < 2; i++) { hold[i] = 0; holdSide[i] = -1; stages[i].Set(Hero.None); stages[i].SetActive(true); progress[i].fillAmount = 0; progressNum[i].text = ""; progressTrack[i].color = Color.clear; }
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++) stages[i]?.SetActive(false);
        }

        public Result Tick(TrackedPlayer[] players, bool online, bool skip)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < 2; i++) { stages[i].Tick(); PlaceModel(i); ringFx[i].Tick(hold[i] / HoldSeconds, GameSession.Heroes[i] != Hero.None); }

            if (phase == 0)
            {
                for (int i = 0; i < 2; i++) UpdatePlayer(i, players[i], online);
                if (skip)                                   // ทดสอบ: Space = เลือกให้ครบอัตโนมัติ
                    for (int i = 0; i < 2; i++)
                        if (GameSession.Heroes[i] == Hero.None) Pick(i, GameSession.Heroes[1 - i] == Hero.Male ? Hero.Female :
                                                                        GameSession.Heroes[1 - i] == Hero.Female ? Hero.Male : (Hero)i);
                UpdateCards();
                bool both = GameSession.Heroes[0] != Hero.None && GameSession.Heroes[1] != Hero.None;
                centerStatus.text = both ? "พร้อมออกภารกิจ!" : "เลือกตัวละครของคุณ";
                if (both) phase = 1;
            }
            else if (phase == 1)                            // นับถอยหลัง 3-2-1
            {
                countT += dt;
                int n = Mathf.CeilToInt(CountSeconds - countT);
                big.text = n > 0 ? n.ToString() : "";
                big.rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(Mathf.Clamp01((countT % 1f) / .3f));
                centerStatus.text = $"เริ่มภารกิจใน {Mathf.Max(n, 0)}";
                if (countT >= CountSeconds) { phase = 2; big.text = ""; }
            }
            else                                            // ซูมเข้า cinematic แล้วจอดำ
            {
                zoomT += dt;
                float t = Mathf.Clamp01(zoomT / ZoomSeconds);
                float e = t * t * (3 - 2 * t);
                zoomLayer.localScale = Vector3.one * Mathf.Lerp(1f, 2.6f, e * e);
                zoomLayer.anchoredPosition = new Vector2(0, Mathf.Lerp(0, 250, e));
                zoomLayer.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, -3, e));
                fade.color = new Color(0, 0, 0, Mathf.Clamp01((t - .45f) / .55f));
                if (t >= 1) { zoomLayer.localRotation = Quaternion.identity; return Result.Done; }
            }
            return Result.Selecting;
        }

        void UpdatePlayer(int i, TrackedPlayer p, bool online)
        {
            var mine = GameSession.Heroes[i];
            if (mine != Hero.None)
            {
                progress[i].fillAmount = 0; progressNum[i].text = ""; progressTrack[i].color = Color.clear;
                pLabel[i].text = $"P{i + 1}  {(mine == Hero.Male ? "MALE" : "FEMALE")} ✓";
                return;
            }
            if (BotPlayer.Controls(i)) BotPlayer.SelectPing[i] = Time.unscaledTime;
            int side = ArmRaise.Side(p, i, online);
            var want = side < 0 ? Hero.None : (Hero)side;
            bool taken = want != Hero.None && GameSession.Heroes[1 - i] == want;
            if (want == Hero.None || taken || side != holdSide[i]) hold[i] = 0;
            holdSide[i] = side;
            if (want != Hero.None && !taken) hold[i] += Time.deltaTime;

            progress[i].fillAmount = hold[i] / HoldSeconds;
            bool holding = hold[i] > 0;
            progressTrack[i].color = holding ? new Color(1, 1, 1, .2f) : Color.clear;
            progressNum[i].text = holding ? Mathf.CeilToInt(HoldSeconds - hold[i]).ToString() : "";
            progress[i].rectTransform.localScale = Vector3.one * (holding ? 1 + .04f * Mathf.Sin(Time.time * 8) : 1);
            if (taken) pLabel[i].text = $"P{i + 1}: ตัวนี้ถูกเลือกแล้ว ใช้อีกตัว";
            else if (want != Hero.None) pLabel[i].text = $"P{i + 1}: เลือก {(want == Hero.Male ? "MALE" : "FEMALE")}... {Mathf.CeilToInt(HoldSeconds - hold[i])}";
            else pLabel[i].text = online && !p.present ? $"P{i + 1}: กลับมายืนหน้ากล้อง" : $"P{i + 1}: ยกแขนเพื่อเลือก";
            pLabel[i].color = taken ? new Color(1, .4f, .3f) : PColor(i);
            if (hold[i] >= HoldSeconds) Pick(i, want);
        }

        public void DebugPick(int only = -1) { for (int i = 0; i < 2; i++) if (only < 0 || only == i) { stages[i].Set(GameSession.Heroes[i]); ringFx[i].Burst(); } }

        // เลื่อนภาพโมเดลให้ "เท้า" ตรงกับกลางวงกลมพอดี
        void PlaceModel(int i)
        {
            float fy = stages[i].FeetViewportY;
            stages[i].Image.rectTransform.anchoredPosition = new Vector2(feetSpot[i].x, feetSpot[i].y + (.5f - fy) * ModelH);
        }

        void Pick(int i, Hero h)
        {
            GameSession.Heroes[i] = h;
            stages[i].Set(h);
            ringFx[i].Burst();
            hold[i] = 0;
        }

        void UpdateCards()
        {
            for (int c = 0; c < 2; c++)
            {
                int owner = GameSession.Heroes[0] == (Hero)c ? 0 : GameSession.Heroes[1] == (Hero)c ? 1 : -1;
                bool aiming = owner < 0 && (holdSide[0] == c && GameSession.Heroes[0] == Hero.None || holdSide[1] == c && GameSession.Heroes[1] == Hero.None);
                cardLock[c].color = owner >= 0 ? new Color(0, 0, 0, .45f) : new Color(0, 0, 0, 0);
                cardTag[c].text = owner >= 0 ? $"P{owner + 1} ✓" : "";
                cardTag[c].color = owner >= 0 ? PColor(owner) : Color.white;
                cards[c].rectTransform.localScale = Vector3.one * (aiming ? 1.06f + .03f * Mathf.Sin(Time.time * 8) : 1f);
                sidePanel[c].color = owner >= 0 ? new Color(.5f, .5f, .5f, .6f) : Color.white;
            }
        }
    }
}
