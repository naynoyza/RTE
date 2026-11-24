// =====================================================================================================
// CharacterSelect — หน้าเลือกตัวละคร (พื้นหลัง Background menu + UI ฮอโลแกรมนีออน + การ์ดตัวละครคู่)
// -----------------------------------------------------------------------------------------------------
// ผู้เล่นยกแขนค้าง HoldSeconds:  ด้านซ้าย = MALE, ด้านขวา = FEMALE
// ตัวที่ถูกอีกคนเลือกแล้วจะล็อค (เลือกซ้ำไม่ได้ ต้องใช้อีกตัว) เมื่อเลือกได้ โมเดล 3D จะขึ้นยืนบนวงกลมฝั่งตัวเอง
// ได้ครบ 2 คน -> LaunchSequence: READY CHECK (ชูแขน 2 ข้างพร้อมกัน) -> TELEPORT เข้าเสาแสง -> WARP + การ์ดภารกิจ -> แฟลช -> Done
// P1 = ฝั่งซ้าย (วงชมพู), P2 = ฝั่งขวา (วงฟ้า)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class CharacterSelect
    {
        public enum Result { Selecting, Done }
        const float HoldSeconds = 3.5f;

        readonly RectTransform root, zoomLayer;
        readonly Image fade;
        readonly Text title, sub, centerStatus, big;
        readonly Image[] cards = new Image[2], cardLock = new Image[2], sidePanel = new Image[2];
        readonly Text[] cardTag = new Text[2];
        readonly Image[] progress = new Image[2];
        readonly Text[] pLabel = new Text[2];
        readonly ModelStage[] stages = new ModelStage[2];
        readonly RingFx[] ringFx = new RingFx[2];
        readonly float[] hold = new float[2];
        readonly Vector2[] feetSpot = new Vector2[2];   // จุดกลางวงกลมที่เท้าตัวละครต้องเหยียบ (พิกัด UI)
        const float ModelH = 1000f;
        readonly int[] holdSide = { -1, -1 };
        float countT, zoomT;
        int phase;   // 0 เลือก, 1 READY CHECK, 2 ออกภารกิจ (teleport/warp/flash)
        readonly float[] readyHold = new float[2];
        readonly bool[] ready = new bool[2];
        LaunchSequence launch;

        static readonly Color Pink = new Color(1f, .35f, .8f), Cyan = new Color(.25f, .95f, 1f);
        static Color PColor(int i) => i == 0 ? Pink : Cyan;

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
                    UIKit.Load(s == 0 ? "intro/holo_left" : "intro/holo_right"), new Vector2(250, 168), true);
                sidePanel[s].rectTransform.anchoredPosition = new Vector2(s == 0 ? -480 : 480, 150);
                UIKit.Label("Cap", sidePanel[s].transform, s == 0 ? "ยกแขนซ้าย = MALE" : "ยกแขนขวา = FEMALE", 28, Color.white)
                    .rectTransform.anchoredPosition = new Vector2(0, -120);
            }

            // วงกลม + โมเดล + แถบความคืบหน้าของผู้เล่นแต่ละคน
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -700 : 700;
                var ring = UIKit.Img($"Ring{i + 1}", zoomLayer, UIKit.Load(i == 0 ? "intro/ring_pink" : "intro/ring_cyan"), new Vector2(560, 317), true);
                ring.rectTransform.anchoredPosition = new Vector2(x, -360);
                feetSpot[i] = new Vector2(x + (i == 0 ? -15 : 8), -422);          // กลางวงในของรูปวงกลม
                var fxBack = UIKit.Rect($"RingFxBack{i + 1}", zoomLayer); UIKit.Stretch(fxBack);
                stages[i] = new ModelStage(zoomLayer, i, new Vector2(ModelH * .58f, ModelH));
                var fxFront = UIKit.Rect($"RingFxFront{i + 1}", zoomLayer); UIKit.Stretch(fxFront);
                ringFx[i] = new RingFx(fxBack, fxFront, feetSpot[i], PColor(i));
                PlaceModel(i);

                progress[i] = UIKit.Img($"Progress{i + 1}", zoomLayer, UIKit.Load("fx_ring"), new Vector2(150, 150), false);
                progress[i].type = Image.Type.Filled; progress[i].fillMethod = Image.FillMethod.Radial360;
                progress[i].color = PColor(i);
                progress[i].rectTransform.anchoredPosition = new Vector2(x, -300);
                pLabel[i] = UIKit.Label($"PLabel{i + 1}", zoomLayer, "", 36, PColor(i));
                pLabel[i].rectTransform.anchoredPosition = new Vector2(x, -505);
            }

            big = UIKit.Label("Big", root, "", 220, Color.white);
            big.rectTransform.anchoredPosition = new Vector2(0, -250);
            launch = new LaunchSequence(root);
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
            for (int i = 0; i < 2; i++)
            {
                hold[i] = 0; holdSide[i] = -1; readyHold[i] = 0; ready[i] = false; ringFx[i].Boost = 0;
                stages[i].Set(Hero.None); stages[i].SetActive(true);
                stages[i].Image.rectTransform.localScale = Vector3.one; stages[i].Image.color = Color.white;
            }
            launch.Reset();
            title.text = "SELECT CHARACTER";
            sub.text = "ยกแขนค้างไว้ 3 วินาที   ◀ แขนซ้าย = MALE   •   FEMALE = แขนขวา ▶";
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++) stages[i]?.SetActive(false);
        }

        public Result Tick(TrackedPlayer[] players, bool online, bool skip)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < 2; i++)
            {
                stages[i].Tick(); PlaceModel(i);
                ringFx[i].Tick(phase == 1 ? readyHold[i] / LaunchSequence.ReadyHold : hold[i] / HoldSeconds, GameSession.Heroes[i] != Hero.None);
            }

            if (phase == 0)
            {
                for (int i = 0; i < 2; i++) UpdatePlayer(i, players[i], online);
                if (skip)                                   // ทดสอบ: Space = เลือกให้ครบอัตโนมัติ
                    for (int i = 0; i < 2; i++)
                        if (GameSession.Heroes[i] == Hero.None) Pick(i, GameSession.Heroes[1 - i] == Hero.Male ? Hero.Female :
                                                                        GameSession.Heroes[1 - i] == Hero.Female ? Hero.Male : (Hero)i);
                UpdateCards();
                bool both = GameSession.Heroes[0] != Hero.None && GameSession.Heroes[1] != Hero.None;
                centerStatus.text = both ? "เลือกครบแล้ว!" : "เลือกตัวละครของคุณ";
                if (both) { phase = 1; countT = 0; }
            }
            else if (phase == 1)                            // READY CHECK: ชูแขน 2 ข้างค้าง 1 วิ ทั้งคู่
            {
                countT += dt;
                for (int i = 0; i < 2; i++)
                {
                    if (ready[i]) continue;
                    bool up = ArmRaise.BothUp(players[i], i, online);
                    readyHold[i] = up ? readyHold[i] + dt : Mathf.Max(0, readyHold[i] - dt * 2);
                    progress[i].fillAmount = readyHold[i] / LaunchSequence.ReadyHold;
                    pLabel[i].text = $"P{i + 1}: ชูแขน 2 ข้าง!";
                    if (readyHold[i] >= LaunchSequence.ReadyHold)
                    {
                        ready[i] = true; ringFx[i].Burst(); progress[i].fillAmount = 0;
                        pLabel[i].text = $"P{i + 1}  พร้อม! ✓";
                    }
                }
                launch.ReadyCheck(countT, ready);
                centerStatus.text = "READY CHECK";
                title.text = "READY CHECK";
                sub.text = "ชูแขนทั้ง 2 ข้างค้างไว้ 1 วินาที  เพื่อยืนยันว่าพร้อมออกภารกิจ";
                if ((ready[0] && ready[1]) || countT >= LaunchSequence.AutoSeconds || skip)
                {
                    phase = 2; zoomT = 0;
                    for (int i = 0; i < 2; i++) { ringFx[i].Burst(); progress[i].fillAmount = 0; }
                }
            }
            else                                            // ออกภารกิจ: teleport -> warp + การ์ดภารกิจ -> แฟลช
            {
                zoomT += dt;
                // TELEPORT: เสาแสงแรงขึ้น ตัวละครยืดเป็นลำแสงแล้วลอยหายขึ้นไป
                float tp = Mathf.Clamp01(zoomT / LaunchSequence.TeleportSeconds);
                for (int i = 0; i < 2; i++)
                {
                    ringFx[i].Boost = Mathf.SmoothStep(0, 1, Mathf.Clamp01(zoomT / .5f));
                    float squeeze = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1f, tp));
                    var img = stages[i].Image;
                    img.rectTransform.localScale = new Vector3(Mathf.Lerp(1, .05f, squeeze), Mathf.Lerp(1, 1.6f, squeeze), 1);
                    img.rectTransform.anchoredPosition += new Vector2(0, squeeze * squeeze * 500);
                    img.color = new Color(1 + squeeze, 1 + squeeze, 1 + squeeze, 1 - Mathf.InverseLerp(.75f, 1f, tp));
                    pLabel[i].text = "";
                }
                // พื้นหลังซูมเข้าเล็กน้อยระหว่างวาร์ป
                float wt = Mathf.Clamp01((zoomT - LaunchSequence.TeleportSeconds) / LaunchSequence.WarpSeconds);
                zoomLayer.localScale = Vector3.one * (1 + .5f * wt * wt);
                if (launch.Launch(zoomT, GameSession.Heroes)) return Result.Done;
            }
            return Result.Selecting;
        }

        void UpdatePlayer(int i, TrackedPlayer p, bool online)
        {
            var mine = GameSession.Heroes[i];
            if (mine != Hero.None)
            {
                progress[i].fillAmount = 0;
                pLabel[i].text = $"P{i + 1}  {(mine == Hero.Male ? "MALE" : "FEMALE")} ✓";
                return;
            }
            int side = ArmRaise.Side(p, i, online);
            var want = side < 0 ? Hero.None : (Hero)side;
            bool taken = want != Hero.None && GameSession.Heroes[1 - i] == want;
            if (want == Hero.None || taken || side != holdSide[i]) hold[i] = 0;
            holdSide[i] = side;
            if (want != Hero.None && !taken) hold[i] += Time.deltaTime;

            progress[i].fillAmount = hold[i] / HoldSeconds;
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
