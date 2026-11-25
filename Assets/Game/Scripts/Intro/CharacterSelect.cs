// =====================================================================================================
// CharacterSelect — หน้าเลือกตัวละคร (พื้นหลัง Background menu + UI ฮอโลแกรมนีออน + การ์ดตัวละครคู่)
// -----------------------------------------------------------------------------------------------------
// ผู้เล่นยกแขนค้าง HoldSeconds:  ด้านซ้าย = MALE, ด้านขวา = FEMALE
// ระหว่างยกแขน: การ์ดฝั่งที่เล็งเรืองแสงสีผู้เล่น + ป้าย "P1 กำลังเลือก" + แถบเติม + เส้นพลังงานจากวงของผู้เล่นไปหาการ์ด
// เลือกได้: การ์ดแฟลช + คลื่นวงแหวน + ป้าย "P1 ✓" เด้งขึ้น, โมเดล 3D ขึ้นยืนบนวงกลมฝั่งตัวเอง
// ตัวที่ถูกอีกคนเลือกแล้วจะล็อค (เลือกซ้ำไม่ได้ ต้องใช้อีกตัว)
// ได้ครบ 2 คน -> LaunchSequence: READY CHECK (ชูแขน 2 ข้างพร้อมกัน) -> TELEPORT เข้าเสาแสง -> WARP + การ์ดภารกิจ -> แฟลช
//   -> WarpFlash จางจากขาวเผยฉากเกม -> Tick() คืน Done
// P1 = ฝั่งซ้าย, P2 = ฝั่งขวา (สีจาก GameSettings.PlayerColors)
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
        readonly Text title, sub, centerStatus, big;
        readonly Image[] cards = new Image[2], cardLock = new Image[2], sidePanel = new Image[2];
        readonly Text[] cardTag = new Text[2];
        readonly Image[] cardGlow = new Image[2], cardFill = new Image[2], cardShock = new Image[2], cardFlash = new Image[2];
        readonly Text[] cardChooser = new Text[2];
        readonly Image[,] cardFrame = new Image[2, 4];   // กรอบสีผู้เล่นรอบการ์ด (บน ล่าง ซ้าย ขวา)
        readonly float[] cardPickT = { 9, 9 };
        readonly Image[] progress = new Image[2], progressTrack = new Image[2];
        readonly Text[] progressNum = new Text[2];
        const float ProgressY = -110;   // วงโหลดลอยเหนือวงเลือก (ไม่จมไปกับวงบนพื้น)
        readonly Text[] pLabel = new Text[2];
        readonly ModelStage[] stages = new ModelStage[2];
        readonly RingFx[] ringFx = new RingFx[2];
        readonly float[] hold = new float[2];
        readonly Vector2[] feetSpot = new Vector2[2];   // จุดกลางวงกลมที่เท้าตัวละครต้องเหยียบ (พิกัด UI)
        readonly Vector2[] ringPos = new Vector2[2];
        const float ModelH = 860f;
        readonly int[] holdSide = { -1, -1 };

        // เส้นพลังงานจากวงผู้เล่นไปหาการ์ดที่เล็ง
        const int LinkDots = 5;
        readonly Image[] link = new Image[2];
        readonly Image[,] linkDot = new Image[2, LinkDots];
        static readonly Vector2 CenterPos = new Vector2(0, 120);
        static Vector2 CardPos(int c) => CenterPos + new Vector2(c == 0 ? -152 : 152, 4);

        readonly WarpFlash flash;          // ขาวเต็มจอตอนจบ แล้วจางเผยฉากเกม

        float countT, zoomT;
        int phase;   // 0 เลือก, 1 READY CHECK, 2 ออกภารกิจ (teleport/warp/flash)
        readonly float[] readyHold = new float[2];
        readonly bool[] ready = new bool[2];
        readonly LaunchSequence launch;

        static Color PColor(int i) => GameSettings.PlayerColors[i];   // P1 ฟ้า, P2 ส้ม — สีเดียวทั้งเกม
        static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

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

            // เส้นพลังงาน (อยู่หลังแผงกลาง)
            for (int i = 0; i < 2; i++)
            {
                link[i] = UIKit.Panel($"Link{i + 1}", zoomLayer, new Vector2(10, 6), Color.clear);
                link[i].rectTransform.pivot = new Vector2(0, .5f);
                link[i].raycastTarget = false;
                for (int d = 0; d < LinkDots; d++)
                {
                    linkDot[i, d] = UIKit.Img("LinkDot", zoomLayer, UIKit.Load("fx_glow"), new Vector2(34, 34), false);
                    linkDot[i, d].color = Color.clear;
                }
            }

            // แผงกลาง + การ์ด 2 ใบในช่อง (แสงเรืองอยู่ใต้การ์ด)
            var center = UIKit.Img("HoloCenter", zoomLayer, UIKit.Load("intro/holo_center"), new Vector2(720, 517), true);
            center.rectTransform.anchoredPosition = CenterPos;
            for (int c = 0; c < 2; c++)
            {
                var pos = new Vector2(c == 0 ? -152 : 152, 4);
                cardGlow[c] = UIKit.Img("CardGlow", center.transform, UIKit.Load("fx_glow"), new Vector2(440, 420), false);
                cardGlow[c].rectTransform.anchoredPosition = pos;
                cardGlow[c].color = Color.clear;
                cards[c] = UIKit.Img(c == 0 ? "CardMale" : "CardFemale", center.transform,
                    UIKit.Load(c == 0 ? "intro/card_male" : "intro/card_female"), new Vector2(285, 262), true);
                cards[c].rectTransform.anchoredPosition = pos;
                cardLock[c] = UIKit.Img("Lock", cards[c].transform, null, new Vector2(250, 262), false);
                cardLock[c].color = new Color(0, 0, 0, 0);
                cardFlash[c] = UIKit.Img("Flash", cards[c].transform, null, new Vector2(250, 262), false);
                cardFlash[c].color = Color.clear;
                // แถบเติมด้านล่างการ์ด = ความคืบหน้าของคนที่กำลังเลือก
                var track = UIKit.Panel("FillTrack", cards[c].transform, new Vector2(230, 12), new Color(0, 0, 0, .55f));
                track.rectTransform.anchoredPosition = new Vector2(0, -118);
                cardFill[c] = UIKit.Panel("Fill", track.transform, new Vector2(0, 12), Color.white);
                cardFill[c].rectTransform.pivot = new Vector2(0, .5f);
                cardFill[c].rectTransform.anchoredPosition = new Vector2(-115, 0);
                cardTag[c] = UIKit.Label("Tag", cards[c].transform, "", 30, Color.white);
                cardTag[c].rectTransform.anchoredPosition = new Vector2(0, 150);
                UIKit.Outline(cardTag[c], new Color(0, 0, 0, .8f), 2);
                // ตราประทับ "LOCKED IN" กลางการ์ดเมื่อเลือกแล้ว
                cardChooser[c] = UIKit.Label("Stamp", cards[c].transform, "", 46, Color.white);
                cardChooser[c].rectTransform.anchoredPosition = new Vector2(0, 10);
                cardChooser[c].rectTransform.localRotation = Quaternion.Euler(0, 0, 12);
                UIKit.Outline(cardChooser[c], new Color(0, 0, 0, .9f), 3);
                for (int e = 0; e < 4; e++)
                {
                    bool hor = e < 2;
                    cardFrame[c, e] = UIKit.Panel("Frame", cards[c].transform, hor ? new Vector2(276, 7) : new Vector2(7, 268), Color.clear);
                    cardFrame[c, e].rectTransform.anchoredPosition = hor ? new Vector2(0, e == 0 ? 131 : -131) : new Vector2(e == 2 ? -135 : 135, 0);
                }
                cardShock[c] = UIKit.Img("Shock", center.transform, UIKit.Load("fx/tech_ring"), new Vector2(360, 360), false);
                cardShock[c].rectTransform.anchoredPosition = pos;
                cardShock[c].color = Color.clear;
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
                ringPos[i] = new Vector2(x, ProgressY);
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

            launch = new LaunchSequence(root);
            flash = WarpFlash.Create(canvas);
            Hide();
        }

        public void Show()
        {
            root.gameObject.SetActive(true);
            GameSession.Clear();
            phase = 0; countT = zoomT = 0;
            zoomLayer.localScale = Vector3.one; zoomLayer.anchoredPosition = Vector2.zero; zoomLayer.localRotation = Quaternion.identity;
            big.text = "";
            flash.Set(0);
            launch.Reset();
            title.text = "SELECT CHARACTER";
            sub.text = "ยกแขนค้างไว้ 3 วินาที   ◀ แขนซ้าย = MALE   •   FEMALE = แขนขวา ▶";
            for (int i = 0; i < 2; i++)
            {
                hold[i] = 0; holdSide[i] = -1; stages[i].Set(Hero.None); stages[i].SetActive(true);
                stages[i].Image.rectTransform.localScale = Vector3.one; stages[i].Image.color = Color.white;
                progress[i].fillAmount = 0; progressNum[i].text = ""; progressTrack[i].color = Color.clear;
                readyHold[i] = 0; ready[i] = false; ringFx[i].Boost = 0;
                cardPickT[i] = 9;
            }
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
                    progressTrack[i].color = new Color(1, 1, 1, .2f);
                    progressNum[i].text = "";
                    pLabel[i].text = $"P{i + 1}: ชูแขน 2 ข้าง!";
                    if (readyHold[i] >= LaunchSequence.ReadyHold)
                    {
                        ready[i] = true; ringFx[i].Burst(); progress[i].fillAmount = 0; progressTrack[i].color = Color.clear;
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
                    for (int i = 0; i < 2; i++) { ringFx[i].Burst(); progress[i].fillAmount = 0; progressTrack[i].color = Color.clear; }
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
                    img.color = new Color(1, 1, 1, 1 - Mathf.InverseLerp(.75f, 1f, tp));
                    pLabel[i].text = "";
                }
                centerStatus.text = "";
                // พื้นหลังซูมเข้าเล็กน้อยระหว่างวาร์ป
                float wt = Mathf.Clamp01((zoomT - LaunchSequence.TeleportSeconds) / LaunchSequence.WarpSeconds);
                zoomLayer.localScale = Vector3.one * (1 + .5f * wt * wt);
                if (launch.Launch(zoomT, GameSession.Heroes))
                {
                    zoomLayer.localScale = Vector3.one;
                    flash.Play();                           // ขาวเต็มจอ แล้วจางเผยฉากเกมเอง (ไม่ตัดฉากกระทันหัน)
                    return Result.Done;
                }
            }

            UpdateCards();
            UpdateLinks();
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
            cardPickT[(int)h] = 0;
            hold[i] = 0;
        }

        // ผู้เล่นที่กำลังยกแขนเล็งการ์ด c อยู่ (ยังไม่ได้เลือก) — คืน -1 ถ้าไม่มี, ถ้า 2 คนพร้อมกันคืนคนที่ค้างนานกว่า
        int Chooser(int c)
        {
            int who = -1;
            for (int i = 0; i < 2; i++)
                if (GameSession.Heroes[i] == Hero.None && holdSide[i] == c && hold[i] > 0 && (who < 0 || hold[i] > hold[who])) who = i;
            return who;
        }

        void UpdateCards()
        {
            float now = Time.time, dt = Time.deltaTime;
            for (int c = 0; c < 2; c++)
            {
                int owner = GameSession.Heroes[0] == (Hero)c ? 0 : GameSession.Heroes[1] == (Hero)c ? 1 : -1;
                int who = owner < 0 ? Chooser(c) : -1;
                bool both = owner < 0 && who >= 0 && holdSide[1 - who] == c && hold[1 - who] > 0 && GameSession.Heroes[1 - who] == Hero.None;
                float h01 = who >= 0 ? hold[who] / HoldSeconds : 0;

                // แสงเรืองใต้การ์ด: สีของคนที่เล็ง (สว่างขึ้นตามความคืบหน้า) / สีเจ้าของเมื่อเลือกแล้ว
                Color glowC = owner >= 0 ? PColor(owner) : who >= 0 ? PColor(who) : Color.clear;
                float glowA = owner >= 0 ? .55f + .1f * Mathf.Sin(now * 3) : who >= 0 ? .35f + .55f * h01 + .1f * Mathf.Sin(now * 10) : 0;
                cardGlow[c].color = WithA(glowC, glowA);
                for (int e = 0; e < 4; e++)   // กรอบสีผู้เล่น: กะพริบเร็วขณะเลือก, นิ่งเมื่อเลือกแล้ว
                {
                    Color fc = both && e >= 2 ? PColor(e == 2 ? 0 : 1) : glowC;   // 2 คนเล็งการ์ดเดียว: ซ้าย P1 ขวา P2
                    cardFrame[c, e].color = WithA(Color.Lerp(fc, Color.white, .2f), owner >= 0 ? .95f : who >= 0 ? .5f + .5f * h01 * (.7f + .3f * Mathf.Sin(now * 14)) : 0);
                }
                cardGlow[c].rectTransform.localScale = Vector3.one * (1 + .08f * Mathf.Sin(now * (owner >= 0 ? 2 : 8)));

                cardLock[c].color = owner >= 0 ? new Color(0, 0, 0, .35f) : new Color(0, 0, 0, 0);
                cards[c].rectTransform.localScale = Vector3.one * (who >= 0 ? 1.05f + .05f * h01 + .02f * Mathf.Sin(now * 10) : 1f);
                sidePanel[c].color = owner >= 0 ? new Color(.5f, .5f, .5f, .6f) : who >= 0 ? Color.Lerp(Color.white, PColor(who), .5f + .3f * Mathf.Sin(now * 8)) : Color.white;

                // ป้ายบน: ใครเป็นเจ้าของ / ใครกำลังเลือก
                cardFill[c].rectTransform.sizeDelta = new Vector2(230 * h01, 12);
                cardFill[c].color = who >= 0 ? PColor(who) : Color.clear;
                if (owner >= 0)
                {
                    cardTag[c].text = $"P{owner + 1} ✓";
                    cardTag[c].color = PColor(owner);
                    cardTag[c].rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(Mathf.Clamp01(cardPickT[c] / .35f));
                    cardChooser[c].text = "LOCKED IN";
                    cardChooser[c].color = Color.Lerp(PColor(owner), Color.white, .3f);
                    cardChooser[c].rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, Smooth(cardPickT[c] / .25f));
                }
                else if (who >= 0)
                {
                    cardTag[c].text = both ? $"<color=#{ColorUtility.ToHtmlStringRGB(PColor(0))}>P1</color> + <color=#{ColorUtility.ToHtmlStringRGB(PColor(1))}>P2</color> ▼"
                                           : $"P{who + 1} กำลังเลือก {Mathf.RoundToInt(h01 * 100)}% ▼";
                    cardTag[c].color = PColor(who);
                    cardTag[c].rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(now * 10));
                    cardChooser[c].text = "";
                }
                else { cardTag[c].text = ""; cardChooser[c].text = ""; }

                // ตอนเลือกสำเร็จ: การ์ดแฟลชขาว + คลื่นวงแหวนสีผู้เล่น
                cardPickT[c] += dt;
                float b = Mathf.Clamp01(cardPickT[c] / .6f);
                Color pc = owner >= 0 ? PColor(owner) : Color.white;
                cardFlash[c].color = WithA(Color.Lerp(Color.white, pc, b), (1 - b) * .85f);
                cardShock[c].color = WithA(pc, (1 - b) * .9f);
                cardShock[c].rectTransform.localScale = Vector3.one * Mathf.Lerp(.5f, 2.2f, Mathf.Sqrt(b));
                cardShock[c].rectTransform.localRotation = Quaternion.Euler(0, 0, b * 90);
            }
        }

        // เส้นพลังงานจากวงโหลดของผู้เล่นไปยังการ์ดที่เล็ง มีจุดแสงวิ่งตามเส้น
        void UpdateLinks()
        {
            float now = Time.time;
            for (int i = 0; i < 2; i++)
            {
                bool active = phase == 0 && GameSession.Heroes[i] == Hero.None && holdSide[i] >= 0 && hold[i] > 0;
                if (!active)
                {
                    link[i].color = Color.clear;
                    for (int d = 0; d < LinkDots; d++) linkDot[i, d].color = Color.clear;
                    continue;
                }
                float h01 = hold[i] / HoldSeconds;
                Vector2 card = CardPos(holdSide[i]);
                Vector2 a = ringPos[i] + (card - ringPos[i]).normalized * 100f;
                Vector2 b = card + (a - card).normalized * 160f;                     // หยุดที่ขอบการ์ด
                Vector2 d0 = b - a;
                var rt = link[i].rectTransform;
                rt.anchoredPosition = a;
                rt.sizeDelta = new Vector2(d0.magnitude, 4 + 6 * h01);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d0.y, d0.x) * Mathf.Rad2Deg);
                link[i].color = WithA(PColor(i), .35f + .5f * h01 + .1f * Mathf.Sin(now * 20));
                for (int d = 0; d < LinkDots; d++)
                {
                    float q = (now * (.8f + 1.6f * h01) + d / (float)LinkDots) % 1f;
                    linkDot[i, d].rectTransform.anchoredPosition = Vector2.Lerp(a, b, q);
                    linkDot[i, d].rectTransform.localScale = Vector3.one * (.6f + .8f * h01);
                    linkDot[i, d].color = WithA(Color.Lerp(PColor(i), Color.white, .5f), Mathf.Sin(q * Mathf.PI));
                }
            }
        }
    }
}
