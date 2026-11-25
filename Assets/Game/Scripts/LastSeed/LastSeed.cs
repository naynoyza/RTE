// =====================================================================================================
// LastSeed — ตัวควบคุมเกมภารกิจที่ 2 THE LAST SEED (ใส่ไว้ที่ GameObject "Game" ในฉาก The Last Seed)
// -----------------------------------------------------------------------------------------------------
// มาจากเกม 1 (GameSession.Active): เฝ้าผู้เล่นต่อ หลุดนาน/จบเกม -> กลับหน้าปก (ฉาก WasteHunters)
//   Practice (สอนเล่นซ้อนบนฉากจริง 2 สเต็ป: ชูแขน V จนเกจเต็มทั้งคู่ -> ผลักฝ่ามือพร้อมกัน / 15 วิ / Space)
//     ──> Countdown 3-2-1-GO! ──> Play ──(ต้นไม้ 100%; หมดเวลาเฉพาะเมื่อเปิด ShowTimer)──> Result ──> Practice
//   ต้นไม้ 4 ระดับ: ต้นอ่อน 0–25% • ต้นกล้า 25–50% • ต้นไม้วัยรุ่น 50–75% • โตเต็มวัย 75–100%
//
// วิธีเล่น (ทั้งสองคนใช้ท่าเดียวกัน แต่ได้พลังคนละชนิด: P1 ซ้าย = น้ำ, P2 ขวา = แสงแดด)
//   1) ชูแขนเป็นตัว V ค้างไว้  -> เกจของตัวเองเพิ่ม (เต็ม = "พร้อมส่ง!")
//   2) ผลักฝ่ามือไปข้างหน้าค้างไว้ -> เกจลด สายน้ำ/ลำแสงพุ่งไปที่ต้นไม้ เมื่อถึงต้นไม้ Growth Bar จึงเพิ่ม
//   3) ส่งพร้อมกันทั้งคู่ = โตเต็มอัตรา | ส่งคนเดียว = โตช้าลง | เริ่มส่งเกือบพร้อมกัน = PERFECT SYNC โบนัส
//   4) Nature Balance: น้ำกับแสงที่ส่งรวมต้องพอ ๆ กัน ไม่งั้นโตช้าลง
//   5) ระยะ ต้นอ่อน 0–30% -> กำลังโต 31–70% -> สมบูรณ์ 71–100% ; 100% = เมืองกลับมาเขียว
// ไม่มีกล้อง: P1 กด A ชูแขน / S ผลัก, P2 กด K ชูแขน / L ผลัก, Space = ข้ามหน้าจุดยืน
// ปุ่มลัด: R = เริ่มใหม่, F1 = ข้อมูลดีบัก, F2 = Auto-play (บอตเล่นเอง ใช้ทดสอบ/โชว์หน้างาน), Esc = ออก
// =====================================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RTE
{
    public class LastSeed : MonoBehaviour
    {
        public SeedSettings Settings = new SeedSettings();

        enum State { Practice, Countdown, Play, Finale, Result }
        State state;

        // โลก 2D
        SpriteRenderer background, greenery, finaleBg;
        const float FinaleSeconds = 5f;   // ฉากจบ: ซ่อน HUD/แขน, ฉากหลังเปลี่ยนเป็นเมืองที่ฟื้นแล้ว ก่อนขึ้นหน้าสรุปผล
        TreeView tree;
        readonly GuardianView[] guardians = new GuardianView[2];
        readonly PoseDetector[] poses = new PoseDetector[2];

        // UI
        GameObject canvasGo;
        SeedHud hud;
        CenterOverlay overlay;
        SeedPractice practice;
        int practiceStep; float practiceTime, practiceEndT = -1;
        readonly bool[] practiceArrived = new bool[2];
        PresenceGuard guard;
        Text debugText;
        bool showDebug, autoplay;
        readonly bool[] botSending = new bool[2];

        // ค่าในรอบเกม
        readonly float[] gauge = new float[2];
        readonly float[] sent = new float[2];          // พลังที่ถึงต้นไม้แล้ว (หน่วยเกจ)
        readonly int[] sends = new int[2];             // จำนวนครั้งที่ส่ง
        readonly float[] sendStart = { -99, -99 };
        readonly bool[] wasSending = new bool[2];
        float stallTime, lastGrowth; bool assist;   // ตัวช่วย: ต้นไม้ไม่โตนานเท่าไรแล้ว / เปิดตัวช่วยหรือยัง
        float growth, timeLeft, playTime, syncSeconds, soloSeconds, green;   // playTime = เล่นไปแล้วกี่วินาที (แสดงในหน้าสรุปผล)
        int perfectSyncs, stageShown;

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (FindFirstObjectByType<TrackingReceiver>() == null) gameObject.AddComponent<TrackingReceiver>();
            if (Camera.main == null) new GameObject("Main Camera", typeof(Camera)).tag = "MainCamera";
            var cam = Camera.main;
            cam.orthographic = true; cam.orthographicSize = 5.4f;
            cam.transform.position = new Vector3(0, 0, -10); cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = .1f; cam.farClipPlane = 50;

            BuildWorld();
            BuildUI();
        }

        void Start() => GoPractice();

        // ใช้ทดสอบจาก Editor: ดูสถานะ / ไปหน้าสอน / เปิดบอตเล่นเอง
        public string StateName => state.ToString();
        public void DebugTutorial() => GoPractice();
        public int PracticeStep => practiceStep;
        public void DebugGrowth(float g) { state = State.Play; StopAllCoroutines(); practice.Hide(); hud.SetPractice(false); growth = g; timeLeft = 999; }
        public void DebugAutoplay() { autoplay = true; }

        void BuildWorld()
        {
            var world = new GameObject("World").transform;
            background = SeedKit.MakeSprite("Background", world, SeedKit.Spr("bg", new Vector2(.5f, .5f)), SeedKit.OrderBg);
            // ชั้นสีเขียวทับพื้นหลัง ค่อย ๆ ชัดขึ้นตอนต้นไม้โตเต็ม (เมืองกลับมาเขียว)
            greenery = SeedKit.MakeSprite("Greenery", world, SeedKit.Spr("bg", new Vector2(.5f, .5f)), SeedKit.OrderBg + 1);
            greenery.color = new Color(.55f, 1f, .55f, 0);
            // ฉากหลังเมืองที่ฟื้นแล้ว (ui2/finale_bg) ค่อย ๆ ปรากฏตอนต้นไม้โตเต็ม — ขยายให้เต็มขนาดฉากหลังเดิม
            finaleBg = SeedKit.MakeSprite("FinaleBg", world, UIKit.Load("ui2/finale_bg"), SeedKit.OrderBg + 2);
            var bs = background.sprite.bounds.size; var fs = finaleBg.sprite.bounds.size;
            finaleBg.transform.localScale = new Vector3(bs.x / fs.x, bs.y / fs.y, 1);
            finaleBg.color = new Color(1, 1, 1, 0);
            tree = new TreeView(world);
            for (int i = 0; i < 2; i++)
            {
                guardians[i] = new GuardianView(world, i + 1, Settings);
                poses[i] = new PoseDetector(i + 1, Settings);
            }
        }

        void BuildUI()
        {
            canvasGo = new GameObject("LastSeedCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.transform;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            hud = new SeedHud(canvas) { ShowTimer = Settings.ShowTimer };
            overlay = new CenterOverlay(canvas);
            practice = new SeedPractice(canvas);
            guard = new PresenceGuard(canvas);
            debugText = UIKit.Label("Debug", canvas, "", 22, Color.yellow, TextAnchor.LowerCenter);
            UIKit.Anchor(debugText.rectTransform, new Vector2(.5f, 0), new Vector2(0, 6));
        }

        // ================================================================== เปลี่ยนสถานะ
        void ResetRound()
        {
            for (int i = 0; i < 2; i++)
            {
                gauge[i] = sent[i] = 0; sends[i] = 0; sendStart[i] = -99; wasSending[i] = false;
                poses[i].Reset(); guardians[i].Reset();
            }
            growth = 0; green = 0; syncSeconds = soloSeconds = 0; perfectSyncs = 0; stageShown = 0;
            timeLeft = Settings.RoundSeconds; playTime = 0;
            stallTime = lastGrowth = 0; assist = false; hud.SetStallHint(false);
            tree.Reset(); tree.Celebrate(false);
            greenery.color = new Color(.55f, 1f, .55f, 0);
            hud.SetGauges(0, 0); hud.SetGrowth(0); hud.SetTime(timeLeft); hud.SetSync(false, false, 1);
        }

        // สอนเล่นซ้อนบนฉากจริง: แขน 3D และเกจน้ำ/แสงทำงานจริง แต่ต้นไม้ไม่โต
        void GoPractice()
        {
            StopAllCoroutines();
            Bgm.Play("bgm_game2", .8f);
            state = State.Practice;
            ResetRound();
            hud.Root.SetActive(true); overlay.Hide();
            for (int i = 0; i < 2; i++) guardians[i].SetVisible(true);
            hud.SetPractice(true, "HOW TO PLAY • 15s");
            practiceStep = 1; practiceTime = 0; practiceEndT = -1;
            practiceArrived[0] = practiceArrived[1] = false;
            practice.Show();
        }

        IEnumerator CountdownThenPlay()
        {
            state = State.Countdown;
            practice.Hide(); hud.Root.SetActive(true); hud.SetPractice(false);
            ResetRound();
            for (int i = 0; i < 2; i++)
                hud.SetHint(i, "เตรียมตัว...", i == 0 ? "Water Guardian" : "Sun Guardian", i == 0 ? SeedSettings.Water : SeedSettings.Sun);
            yield return overlay.Countdown("ชูแขนเป็นตัว V = รับพลัง   •   ผลักฝ่ามือ = ส่งพลังให้ต้นไม้   •   ส่งพร้อมกัน ต้นไม้โตเร็ว!");
            state = State.Play;
        }

        IEnumerator Finale()
        {
            state = State.Finale;
            tree.Burst(140);
            Sfx.Play("finale");
            Bgm.Play("bgm_finale");
            hud.Root.SetActive(false);                                      // ฉากจบ: ซ่อน HUD/แขน
            for (int i = 0; i < 2; i++) guardians[i].SetVisible(false);
            for (float t = 0; t < FinaleSeconds; t += Time.deltaTime)
            {
                green = Mathf.SmoothStep(0, 1, t / (FinaleSeconds * .8f));
                tree.Fade = 1f - green;                                     // ต้นไม้จางออกพร้อมฉากหลังเปลี่ยน เหลือแต่เมืองที่ฟื้นแล้ว
                yield return null;
            }
            tree.Fade = 0f;
            yield return ShowResult(true);
        }

        IEnumerator ShowResult(bool grown)
        {
            state = State.Result;
            float total = sent[0] + sent[1];
            float balance = total > 0 ? Mathf.Min(sent[0], sent[1]) / Mathf.Max(sent[0], sent[1]) : 0;

            float syncPct = syncSeconds + soloSeconds > 0 ? syncSeconds / (syncSeconds + soloSeconds) : 0;
            hud.Root.SetActive(false);
            for (int i = 0; i < 2; i++) guardians[i].SetVisible(false);
            if (!grown) Sfx.Play("result");                                 // โตเต็ม = มีเสียง finale ไปแล้ว
            // การ์ดซ้าย = น้ำ (P1), ขวา = แสงแดด (P2), วงกลาง = ความสมดุล; ย่อและเลื่อนลงให้เห็นต้นไม้ด้านบน
            overlay.ShowCards(grown ? "ui2/tree_banner" : null, grown ? "" : $"หมดเวลา  ต้นไม้โต {Mathf.FloorToInt(growth * 100)}%",
                new CenterOverlay.Card { Title = "น้ำ", Body = $"ส่ง {sends[0]} ครั้ง" },
                new CenterOverlay.Card { Title = "แสงแดด", Body = $"ส่ง {sends[1]} ครั้ง" },
                "ui2/air_gauge", $"{balance * 100:0}%",
                $"ความสมดุล • พร้อมกัน {syncPct * 100:0}% • PERFECT {perfectSyncs}" + (grown ? $" • {playTime:0} วิ" : ""),
                // ดาว: โตเต็มวัดจากความสมดุล+ความพร้อมกัน, ยังไม่เต็มวัดจากการเติบโต (ได้ไม่เกิน 2 ดาว)
                grown ? CenterOverlay.Stars((balance + syncPct) * .5f) : Mathf.Min(2, CenterOverlay.Stars(growth)), .72f, -190, false);
            yield return new WaitForSeconds(Settings.ResultSeconds);
            if (GameSession.Active) GameSession.BackToCover();   // จบชุดภารกิจ วนกลับหน้าปก
            else GoPractice();
        }

        // ตัวช่วยเมื่อต้นไม้ไม่โต: เตือนท่าก่อน ถ้ายังไม่โตอีกก็เปิดตัวช่วย (ชาร์จเร็ว + ส่งคนเดียวไม่โดนลดมาก) จนจบรอบ
        void TickStall(float dt)
        {
            if (growth > lastGrowth + .002f) { lastGrowth = growth; stallTime = 0; }
            else stallTime += dt;
            hud.SetStallHint(stallTime >= Settings.StallHintSeconds);
            if (!assist && stallTime >= Settings.StallAssistSeconds)
            {
                assist = true;
                hud.Popup("ช่วยเร่งพลังให้แล้ว!"); Sfx.Play("rush", .8f);
            }
        }

        // ================================================================== ทุกเฟรม
        void Update()
        {
            float dt = Time.deltaTime;
            var tracking = TrackingReceiver.I;
            if (tracking == null || guard == null) return;   // เฟรมที่กำลังสลับฉากจากภารกิจ 1 ยังไม่พร้อม
            bool online = tracking.Connected;
            var players = tracking.Players;
            var keys = Keyboard.current;
            bool skip = keys != null && keys.spaceKey.wasPressedThisFrame;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) Application.Quit();
                if (keys.f1Key.wasPressedThisFrame) showDebug = !showDebug;
                if (keys.f2Key.wasPressedThisFrame) autoplay = !autoplay;
                if (keys.rKey.wasPressedThisFrame) GoPractice();
            }

            if (guard.Tick(players, online)) { GameSession.BackToCover(); return; }
            if (state == State.Practice && skip) StartCoroutine(CountdownThenPlay());
            Autoplay();
            BotPose();

            bool playing = state == State.Play;
            Sfx.Loop("water", "loop_water", state == State.Practice || state == State.Countdown || playing, .25f);   // บรรยากาศน้ำไหลเบา ๆ
            bool[] sending = new bool[2];
            for (int i = 0; i < 2; i++)
            {
                var pose = poses[i].Tick(players[i], online, dt);
                bool lost = poses[i].Lost;
                bool practicing = state == State.Practice;
                if ((playing || practicing) && !lost)
                {
                    // ช่วงซ้อมชาร์จเร็วกว่าเกมจริง (~1.5 วิ เต็ม) และส่งได้เฉพาะสเต็ป 2
                    float rate = practicing ? 1f / SeedPractice.ChargeSeconds
                               : Settings.ChargePerSecond * (assist ? Settings.AssistChargeMultiplier : 1f);
                    float g0 = gauge[i];
                    if (pose == SeedPose.Charge) gauge[i] = Mathf.Min(1, gauge[i] + rate * dt);
                    if (g0 < 1 && gauge[i] >= 1) Sfx.Play("charged", .7f);
                    sending[i] = pose == SeedPose.Push && gauge[i] > 0 && (playing || practiceStep == 2);
                }
                if (sending[i] && !wasSending[i]) { sendStart[i] = Time.time; sends[i]++; Sfx.Play("send", .7f, i == 0 ? 1f : 1.12f); }
                wasSending[i] = sending[i];
                guardians[i].Tick(pose, gauge[i], sending[i], poses[i].Reach, lost, dt);
            }

            // ---------------- พลังที่ถึงต้นไม้ -> การเติบโต
            float[] delivered = new float[2];
            for (int i = 0; i < 2; i++)
                if (sending[i] && guardians[i].Arrived)
                {
                    delivered[i] = Mathf.Min(gauge[i], Settings.SendPerSecond * dt);
                    gauge[i] -= delivered[i]; sent[i] += delivered[i];
                }

            bool both = delivered[0] > 0 && delivered[1] > 0;
            bool solo = !both && (delivered[0] > 0 || delivered[1] > 0);
            float syncMul = both ? Settings.SyncMultiplier
                          : assist ? Mathf.Max(Settings.SoloMultiplier, Settings.AssistSoloMultiplier) : Settings.SoloMultiplier;
            if (both) syncSeconds += dt; else if (solo) soloSeconds += dt;

            // Nature Balance: เทียบพลังน้ำกับแสงที่ส่งสำเร็จทั้งหมด (+0.5 ให้ต้นเกมยังถือว่าสมดุล)
            float w = sent[0] + .5f, s = sent[1] + .5f;
            float ratio = Mathf.Min(w, s) / Mathf.Max(w, s);
            bool balanced = ratio >= Settings.BalancedRatio;
            float balMul = Mathf.Lerp(Settings.BalanceMinMultiplier, Settings.BalanceMaxMultiplier, Mathf.InverseLerp(.4f, 1f, ratio));
            float lean = (s - w) / (s + w) * 3f;

            if (playing)
            {
                growth += (delivered[0] + delivered[1]) * Settings.GrowthPerUnit * syncMul * balMul;

                // PERFECT SYNC: คนที่สองเริ่มส่งตามคนแรกภายในเวลาที่กำหนด
                for (int i = 0; i < 2; i++)
                    if (sending[i] && Mathf.Approximately(sendStart[i], Time.time) && sending[1 - i]
                        && Time.time - sendStart[1 - i] <= Settings.PerfectSyncWindow)
                    {
                        perfectSyncs++; growth += Settings.PerfectSyncBonus;
                        hud.Popup("PERFECT SYNC!"); tree.Burst(50); Sfx.Play("sync");
                        break;                                   // เริ่มพร้อมกันเฟรมเดียวกัน = นับครั้งเดียว
                    }

                growth = Mathf.Clamp01(growth);
                int stage = TreeView.StageOf(growth);
                if (stage > stageShown)
                {
                    stageShown = stage; tree.Burst(90); Sfx.Play("stage");
                    hud.Popup($"ระดับ {stage + 1} • {TreeView.StageName[stage]}!");
                }

                timeLeft -= dt; playTime += dt;
                TickStall(dt);
                if (growth >= 1) StartCoroutine(Finale());
                else if (Settings.ShowTimer && timeLeft <= 0) StartCoroutine(ShowResult(false));   // ไม่มีเวลาจำกัด = จบเมื่อต้นไม้โตเต็มเท่านั้น
            }

            // ---------------- ภาพ
            float receiving = Mathf.Clamp01((delivered[0] + delivered[1]) / Mathf.Max(1e-4f, dt) / Settings.SendPerSecond * .6f);
            tree.Set(growth, receiving, dt);
            greenery.color = new Color(.55f, 1f, .55f, green * .45f);
            background.color = Color.Lerp(Color.white, new Color(.9f, 1f, .9f), green);
            finaleBg.color = new Color(1, 1, 1, green);

            if (state == State.Practice)
            {
                TickPractice(dt);
                hud.SetGauges(gauge[0], gauge[1]);
                for (int i = 0; i < 2; i++) hud.SetSkeleton(i, online && players[i].present ? players[i].kp : null, dt, online && players[i].present ? players[i].pose : null);
                hud.Tick(dt);
            }
            else
            {
                hud.SetGauges(gauge[0], gauge[1]);
                hud.SetGrowth(growth);
                hud.SetTime(timeLeft);
                hud.SetBalance(lean, ratio, balMul, balanced, dt);
                hud.SetSync(both, solo, syncMul);
                for (int i = 0; i < 2; i++) UpdateHint(i, sending);
                for (int i = 0; i < 2; i++) hud.SetSkeleton(i, online && players[i].present ? players[i].kp : null, dt, online && players[i].present ? players[i].pose : null);
                hud.Tick(dt);
            }

            debugText.text = !showDebug ? "" :
                $"tracker {(online ? "ONLINE" : "OFFLINE (A/S, K/L)")} {tracking.TrackerFps:0} fps | " +
                $"P1 {poses[0].Pose} g{gauge[0]:0.00} | P2 {poses[1].Pose} g{gauge[1]:0.00} | " +
                $"growth {growth:0.000} bal {ratio:0.00} | {state}";
        }

        // ================================================================== สอนเล่น 2 สเต็ป
        void TickPractice(float dt)
        {
            practiceTime += dt;
            hud.SetPractice(true, $"HOW TO PLAY • {Mathf.Max(0, Mathf.CeilToInt(SeedPractice.MaxSeconds - practiceTime))}s");
            bool[] ready = { gauge[0] >= 1, gauge[1] >= 1 };
            if (practiceStep == 1 && ready[0] && ready[1])
            {
                practiceStep = 2;
                Sfx.Play("ready", .8f);
                practice.Flash("ผลักฝ่ามือพร้อมกัน!", new Color(1f, .85f, .35f));
            }
            if (practiceStep == 2)
                for (int i = 0; i < 2; i++) practiceArrived[i] |= guardians[i].Arrived;

            // ส่งถึงต้นอ่อนทั้งคู่ -> PERFECT SYNC! แล้วพับหน้าต่างเข้าเกม
            if (practiceEndT < 0 && practiceArrived[0] && practiceArrived[1])
            {
                practiceEndT = 0;
                practice.Flash("PERFECT SYNC!  พร้อมลุย!", new Color(.45f, 1f, .5f));
                tree.Burst(90); Sfx.Play("sync");
            }
            if (practiceEndT >= 0) practiceEndT += dt;

            var pose = new[] { poses[0].Pose, poses[1].Pose };
            var lost = new[] { poses[0].Lost, poses[1].Lost };
            practice.Tick(practiceStep, ready, practiceArrived, pose, lost, practiceEndT > .9f);

            if (practiceEndT > 1.5f || (practiceEndT < 0 && practiceTime >= SeedPractice.MaxSeconds))
                StartCoroutine(CountdownThenPlay());
        }

        // Auto-play: ชาร์จจนเต็ม -> รอเพื่อนเต็ม -> ผลักพร้อมกัน (P2 ช้ากว่า 0.3 วิ = PERFECT SYNC) จนเกจหมด
        //            P1 ชาร์จเร็วกว่า P2 เล็กน้อยโดยธรรมชาติของจังหวะ ทำให้เห็นทั้งช่วง "ส่งคนเดียว" และ "ส่งพร้อมกัน"
        void Autoplay()
        {
            for (int i = 0; i < 2; i++)
            {
                if (autoplay && state == State.Practice)                  // ซ้อม: ชูแขนจนเต็มทั้งคู่ แล้วผลัก (P2 ช้ากว่าเล็กน้อย)
                {
                    poses[i].Forced = practiceStep == 1 ? (gauge[i] >= 1 ? SeedPose.None : SeedPose.Charge)
                                    : (i == 0 || practiceTime % 1f > .3f ? SeedPose.Push : SeedPose.None);
                    continue;
                }
                if (!autoplay || state != State.Play) { poses[i].Forced = SeedPose.None; botSending[i] = false; continue; }
                if (botSending[i] && gauge[i] <= 0) botSending[i] = false;
                bool bothFull = gauge[0] >= 1 && gauge[1] >= 1;
                if (!botSending[i] && (bothFull || (gauge[i] >= 1 && botSending[1 - i])))
                    botSending[i] = i == 0 || Time.time - sendStart[0] > .3f;
                poses[i].Forced = botSending[i] ? SeedPose.Push : SeedPose.Charge;
            }
        }

        // บอต (F3): แต่ละคนชูแขนจนเกจเต็ม -> ผลักเมื่อเห็นเพื่อนผลัก (หรือรอเกินความอดทนของตัวเองก็ส่งเอง) จนเกจหมด
        //            ความช้า/ท่าหลุดแบบคนจริงอยู่ใน BotPlayer — ที่นี่บอกแค่ "ควรทำท่าอะไร"
        readonly float[] botFullTime = new float[2];
        readonly bool[] botSendingNow = new bool[2];
        void BotPose()
        {
            if (!BotPlayer.On || (state != State.Play && state != State.Practice)) return;
            for (int b = 0; b < 2; b++)
            {
                if (!BotPlayer.Controls(b)) continue;
                BotPlayer.PosePing[b] = Time.unscaledTime;
                if (state == State.Practice)
                {
                    BotPlayer.WantPose[b] = practiceStep == 1 ? (gauge[b] >= 1 ? SeedPose.None : SeedPose.Charge) : SeedPose.Push;
                    continue;
                }
                if (botSendingNow[b] && gauge[b] <= 0) botSendingNow[b] = false;
                if (gauge[b] < 1) botFullTime[b] = Time.time;
                if (!botSendingNow[b] && gauge[b] >= 1 && (poses[1 - b].Pose == SeedPose.Push || Time.time - botFullTime[b] > BotPlayer.Patience(b)))
                    botSendingNow[b] = true;
                BotPlayer.WantPose[b] = botSendingNow[b] ? SeedPose.Push : SeedPose.Charge;
            }
        }

        // คำแนะนำท่าใต้มือของผู้เล่นแต่ละคน
        void UpdateHint(int i, bool[] sending)
        {
            var color = i == 0 ? SeedSettings.Water : SeedSettings.Sun;
            if (state != State.Play) return;
            if (poses[i].Lost) { hud.SetHint(i, "หลุดจากกล้อง! กลับเข้าจุดยืน", "Step back into the camera", new Color(1f, .5f, .4f)); return; }
            if (sending[i]) { hud.SetHint(i, sending[1 - i] ? "ส่งพร้อมกัน เยี่ยม!" : "ส่งอยู่คนเดียว รอเพื่อน!", "Push forward to grow the tree", color); return; }
            if (gauge[i] >= 1 || (gauge[i] > .3f && sending[1 - i]))
                hud.SetHint(i, sending[1 - i] ? "เพื่อนส่งแล้ว ผลักตามเลย!" : "ผลักฝ่ามือไปข้างหน้า พร้อมเพื่อน", "Push forward to grow the tree", Color.white);
            else
                hud.SetHint(i, "ชูแขนเป็นตัว V ค้างไว้ เพื่อรับพลัง", "Hold both arms on V shape to gain power", Color.white);
        }
    }
}
