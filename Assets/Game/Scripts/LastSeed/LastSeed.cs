// =====================================================================================================
// LastSeed — ตัวควบคุมเกมภารกิจที่ 2 THE LAST SEED (ใส่ไว้ที่ GameObject "Game" ในฉาก The Last Seed)
// -----------------------------------------------------------------------------------------------------
//   Tutorial (สอนเล่น: ยกมือค้าง 3 วิ / 20 วิ / Space) ──> Countdown 3-2-1-GO! ──> Play ──(ต้นไม้ 100% / หมดเวลา)──> Result ──> Tutorial
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

        enum State { Tutorial, Countdown, Play, Finale, Result }
        State state;

        // โลก 2D
        SpriteRenderer background, greenery;
        TreeView tree;
        readonly GuardianView[] guardians = new GuardianView[2];
        readonly PoseDetector[] poses = new PoseDetector[2];

        // UI
        GameObject canvasGo;
        SeedHud hud;
        CenterOverlay overlay;
        TutorialScreen tutorial;
        Text debugText;
        bool showDebug, autoplay;
        readonly bool[] botSending = new bool[2];

        // ค่าในรอบเกม
        readonly float[] gauge = new float[2];
        readonly float[] sent = new float[2];          // พลังที่ถึงต้นไม้แล้ว (หน่วยเกจ)
        readonly int[] sends = new int[2];             // จำนวนครั้งที่ส่ง
        readonly float[] sendStart = { -99, -99 };
        readonly bool[] wasSending = new bool[2];
        float growth, timeLeft, syncSeconds, soloSeconds, green;
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

        void Start() => GoTutorial();

        // ใช้ทดสอบจาก Editor: ดูสถานะ / ไปหน้าสอน / เปิดบอตเล่นเอง
        public string StateName => state.ToString();
        public void DebugTutorial() => GoTutorial();
        public void DebugAutoplay() { autoplay = true; }

        void BuildWorld()
        {
            var world = new GameObject("World").transform;
            background = SeedKit.MakeSprite("Background", world, SeedKit.Spr("bg", new Vector2(.5f, .5f)), SeedKit.OrderBg);
            // ชั้นสีเขียวทับพื้นหลัง ค่อย ๆ ชัดขึ้นตอนต้นไม้โตเต็ม (เมืองกลับมาเขียว)
            greenery = SeedKit.MakeSprite("Greenery", world, SeedKit.Spr("bg", new Vector2(.5f, .5f)), SeedKit.OrderBg + 1);
            greenery.color = new Color(.55f, 1f, .55f, 0);
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

            hud = new SeedHud(canvas);
            overlay = new CenterOverlay(canvas);
            tutorial = new TutorialScreen(canvas, "GAME 02", "THE LAST SEED", "ปลูกต้นไม้ด้วยพลังทีม", "ชูแขนตัว V + ผลักฝ่ามือ",
                new[] { "ชูแขนเป็นตัว V ค้างไว้ = เก็บพลัง", "ผลักฝ่ามือไปข้างหน้า = ส่งพลังให้ต้นไม้", "P1 ส่งน้ำ  •  P2 ส่งแสงแดด ให้สมดุลกัน", "ส่งพร้อมกัน = PERFECT SYNC โตเร็ว!" },
                "ชูแขน V  ▶  ผลักมือ", "tut/g2", "seed/bg");
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
            timeLeft = Settings.RoundSeconds;
            tree.Reset(); tree.Celebrate(false);
            greenery.color = new Color(.55f, 1f, .55f, 0);
            hud.SetGauges(0, 0); hud.SetGrowth(0); hud.SetTime(timeLeft); hud.SetSync(false, false, 1);
        }

        void GoTutorial()
        {
            StopAllCoroutines();
            state = State.Tutorial;
            ResetRound();
            hud.Root.SetActive(false); overlay.Hide();
            tutorial.Show();
        }

        IEnumerator CountdownThenPlay()
        {
            state = State.Countdown;
            tutorial.Hide(); hud.Root.SetActive(true);
            ResetRound();
            for (int i = 0; i < 2; i++)
                hud.SetHint(i, "เตรียมตัว...", i == 0 ? "Water Guardian" : "Sun Guardian", i == 0 ? SeedSettings.Water : SeedSettings.Sun);
            yield return overlay.Countdown("ชูแขนเป็นตัว V = รับพลัง   •   ผลักฝ่ามือ = ส่งพลังให้ต้นไม้   •   ส่งพร้อมกัน ต้นไม้โตเร็ว!");
            state = State.Play;
        }

        IEnumerator Finale()
        {
            state = State.Finale;
            tree.Burst(140); tree.Celebrate(true);
            hud.Popup("ต้นไม้โตเต็มที่!");
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                green = Mathf.SmoothStep(0, 1, t / 3.5f);
                yield return null;
            }
            yield return ShowResult(true);
        }

        IEnumerator ShowResult(bool grown)
        {
            state = State.Result;
            float total = sent[0] + sent[1];
            float balance = total > 0 ? Mathf.Min(sent[0], sent[1]) / Mathf.Max(sent[0], sent[1]) : 0;
            float playTime = Settings.RoundSeconds - timeLeft;
            float syncPct = syncSeconds + soloSeconds > 0 ? syncSeconds / (syncSeconds + soloSeconds) : 0;
            overlay.ShowMessage(grown ? "ต้นไม้โตเต็มที่!" : $"หมดเวลา  ต้นไม้โต {Mathf.FloorToInt(growth * 100)}%",
                $"ส่งน้ำ {sends[0]} ครั้ง   •   ส่งแสงแดด {sends[1]} ครั้ง   •   ความสมดุล {balance * 100:0}%\n" +
                $"ส่งพร้อมกัน {syncPct * 100:0}% ของเวลา   •   PERFECT SYNC {perfectSyncs} ครั้ง" +
                (grown ? $"   •   ใช้เวลา {playTime:0} วินาที" : ""));
            yield return new WaitForSeconds(Settings.ResultSeconds);
            GoTutorial();
        }

        // ================================================================== ทุกเฟรม
        void Update()
        {
            float dt = Time.deltaTime;
            var tracking = TrackingReceiver.I;
            bool online = tracking.Connected;
            var players = tracking.Players;
            var keys = Keyboard.current;
            bool skip = keys != null && keys.spaceKey.wasPressedThisFrame;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) Application.Quit();
                if (keys.f1Key.wasPressedThisFrame) showDebug = !showDebug;
                if (keys.f2Key.wasPressedThisFrame) autoplay = !autoplay;
                if (keys.rKey.wasPressedThisFrame) GoTutorial();
            }

            if (state == State.Tutorial && tutorial.Tick(players, online, skip)) StartCoroutine(CountdownThenPlay());

            if (autoplay && state == State.Tutorial) StartCoroutine(CountdownThenPlay());
            Autoplay();

            bool playing = state == State.Play;
            bool[] sending = new bool[2];
            for (int i = 0; i < 2; i++)
            {
                var pose = poses[i].Tick(players[i], online, dt);
                bool lost = poses[i].Lost;
                if (playing && !lost)
                {
                    if (pose == SeedPose.Charge) gauge[i] = Mathf.Min(1, gauge[i] + Settings.ChargePerSecond * dt);
                    sending[i] = pose == SeedPose.Push && gauge[i] > 0;
                }
                if (sending[i] && !wasSending[i]) { sendStart[i] = Time.time; sends[i]++; }
                wasSending[i] = sending[i];
                guardians[i].Tick(state == State.Tutorial ? SeedPose.None : pose, gauge[i], sending[i], poses[i].Reach, lost, dt);
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
            float syncMul = both ? Settings.SyncMultiplier : Settings.SoloMultiplier;
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
                        hud.Popup("PERFECT SYNC!"); tree.Burst(50);
                        break;                                   // เริ่มพร้อมกันเฟรมเดียวกัน = นับครั้งเดียว
                    }

                growth = Mathf.Clamp01(growth);
                int stage = growth >= .7f ? 2 : growth >= .3f ? 1 : 0;
                if (stage > stageShown)
                {
                    stageShown = stage; tree.Burst(90);
                    hud.Popup(stage == 1 ? "ต้นไม้กำลังโต!" : "ต้นไม้ใกล้สมบูรณ์!");
                }

                timeLeft -= dt;
                if (growth >= 1) StartCoroutine(Finale());
                else if (timeLeft <= 0) StartCoroutine(ShowResult(false));
            }

            // ---------------- ภาพ
            float receiving = Mathf.Clamp01((delivered[0] + delivered[1]) / Mathf.Max(1e-4f, dt) / Settings.SendPerSecond * .6f);
            tree.Set(growth, receiving, dt);
            greenery.color = new Color(.55f, 1f, .55f, green * .45f);
            background.color = Color.Lerp(Color.white, new Color(.9f, 1f, .9f), green);

            if (state != State.Tutorial)
            {
                hud.SetGauges(gauge[0], gauge[1]);
                hud.SetGrowth(growth);
                hud.SetTime(timeLeft);
                hud.SetBalance(lean, ratio, balMul, balanced, dt);
                hud.SetSync(both, solo, syncMul);
                for (int i = 0; i < 2; i++) UpdateHint(i, sending);
                for (int i = 0; i < 2; i++) hud.SetSkeleton(i, online && players[i].present ? players[i].kp : null, dt);
                hud.Tick(dt);
            }

            debugText.text = !showDebug ? "" :
                $"tracker {(online ? "ONLINE" : "OFFLINE (A/S, K/L)")} {tracking.TrackerFps:0} fps | " +
                $"P1 {poses[0].Pose} g{gauge[0]:0.00} | P2 {poses[1].Pose} g{gauge[1]:0.00} | " +
                $"growth {growth:0.000} bal {ratio:0.00} | {state}";
        }

        // Auto-play: ชาร์จจนเต็ม -> รอเพื่อนเต็ม -> ผลักพร้อมกัน (P2 ช้ากว่า 0.3 วิ = PERFECT SYNC) จนเกจหมด
        //            P1 ชาร์จเร็วกว่า P2 เล็กน้อยโดยธรรมชาติของจังหวะ ทำให้เห็นทั้งช่วง "ส่งคนเดียว" และ "ส่งพร้อมกัน"
        void Autoplay()
        {
            for (int i = 0; i < 2; i++)
            {
                if (!autoplay || state != State.Play) { poses[i].Forced = SeedPose.None; botSending[i] = false; continue; }
                if (botSending[i] && gauge[i] <= 0) botSending[i] = false;
                bool bothFull = gauge[0] >= 1 && gauge[1] >= 1;
                if (!botSending[i] && (bothFull || (gauge[i] >= 1 && botSending[1 - i])))
                    botSending[i] = i == 0 || Time.time - sendStart[0] > .3f;
                poses[i].Forced = botSending[i] ? SeedPose.Push : SeedPose.Charge;
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
