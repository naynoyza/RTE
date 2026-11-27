// =====================================================================================================
// WasteHunters — ตัวควบคุมเกมหลัก (ใส่ไว้ที่ GameObject "Game" ในฉาก WasteHunters)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : สร้าง Canvas และทุกหน้าจอ แล้วสลับหน้าจอตามสถานะของเกม
//
//   Cover (วิดีโอ + UI 0, รอครบ 2 คน) ──> Select (ยกแขนเลือกตัวละคร, นับ 3 วิ, ซูม cinematic)
//     ──> Practice (สอนเล่นซ้อนบนฉากจริง: ซ้อมดูด 1 ชิ้น + ย่ำเท้า, READY ทั้งคู่ / 15 วิ) ──> Countdown ──> Play ──(หมดเวลา)──> Result ──(10 วิ)──> Cover
//
// ดึงข้อมูล: TrackingReceiver.I.Players (ข้อมูล P1/P2 จากกล้อง) และค่าปรับเกมใน Settings
// ส่งต่อ  : แต่ละเฟรมส่งข้อมูลผู้เล่นให้ PlayerLane ของคนนั้นไปจัดการเดิน/เล็ง/เก็บขยะ
// เฝ้าผู้เล่น: ตั้งแต่หน้าเลือกตัวละครจนจบเกม 2 (PresenceGuard) หลุดนาน -> กลับหน้าปก
// จบเกม 1 -> โหลดฉาก The Last Seed (เกม 2) -> จบเกม 2 -> กลับหน้าปก
// ปุ่มลัด : Space = ข้ามหน้าปก/เลือกตัวละคร/สอน (ไม่มีกล้อง: P1 Q/E, P2 U/O = ยกแขนซ้าย/ขวา), R = กลับหน้าปก, F1 = ข้อมูลดีบัก, Esc = ออก
// =====================================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RTE
{
    public class WasteHunters : MonoBehaviour
    {
        public GameSettings Settings = new GameSettings();

        enum State { Cover, Select, CutsceneStart, Mission1, Practice, Countdown, Play, Result, CutsceneEnd, ToGame2 }
        State state;

        PlayerLane[] lanes;
        GameObject hudRoot;
        RoundTimer timer;
        CenterOverlay overlay;
        IntroCover cover;
        CharacterSelect select;
        PracticeOverlay practice;
        WasteEvents events;
        PresenceGuard guard;
        Cutscene cutsceneStart, cutsceneEnd;   // หลังเลือกตัวละคร / หลังเกม 1 (StreamingAssets/cutscene_*.mp4)
        MissionTransition toGame1, toGame2;    // การ์ด MISSION 01 ก่อนเกม 1 / MISSION 02 ก่อนเกม 2
        WarpFlash fadeIn;                      // จางจากขาวเข้าหน้าสอนเล่นหลัง cutscene
        Text debugText;
        float timeLeft;
        bool showDebug;

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (FindFirstObjectByType<TrackingReceiver>() == null) gameObject.AddComponent<TrackingReceiver>();
            if (Camera.main == null) new GameObject("Main Camera", typeof(Camera)).tag = "MainCamera";
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
            BuildScreens();
        }

        void Start() => GoCover();

        // ใช้ทดสอบจาก Editor/เครื่องมืออัตโนมัติ: ดูสถานะ และข้ามไปหน้าต่าง ๆ
        public string StateName => state.ToString();
        public void DebugGo(string to)
        {
            switch (to)
            {
                case "cover": GoCover(); break;
                case "select": GoSelect(); break;
                case "pick": GameSession.Heroes[0] = Hero.Male; GameSession.Heroes[1] = Hero.Female; select.DebugPick(); break;
                case "pick1": GameSession.Heroes[0] = Hero.Male; select.DebugPick(0); break;
                case "tutorial": GoPractice(); break;
                case "done1": lanes[0].DidVacuum = lanes[0].DidStep = true; break;
                case "done2": lanes[1].DidVacuum = lanes[1].DidStep = true; break;
                case "fever": events.StartFever(lanes, Settings.FeverSeconds); break;
                case "dust": events.StartDust(lanes); break;
                case "stun": lanes[1].DebugStun(); break;
                case "danger": foreach (var l in lanes) l.SpawnDanger(); break;
                case "overdrive": foreach (var l in lanes) l.StartOverdrive(); break;
                case "play": StopAllCoroutines(); StartCoroutine(CountdownThenPlay()); break;
                case "noair": lanes[1].Air = 0; break;                                    // ทดสอบ: P2 อากาศหมด (อ่อนแรง)
                case "endsoon": timeLeft = Mathf.Min(timeLeft, 3f); break;                 // ทดสอบ: จบรอบใน 3 วิ -> หน้าสรุปผล
            }
        }

        // สร้าง Canvas 1920x1080 และทุกหน้าจอ (ลำดับที่สร้าง = ลำดับการซ้อน ตัวหลังอยู่บน)
        void BuildScreens()
        {
            var canvasGo = new GameObject("WasteHuntersCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.transform;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var hud = UIKit.Rect("HUD", canvas); UIKit.Stretch(hud);
            lanes = new[] { new PlayerLane(Settings, canvas, hud, 1), new PlayerLane(Settings, canvas, hud, 2) };
            lanes[0].Partner = lanes[1]; lanes[1].Partner = lanes[0];   // ปัดส่งลูกอากาศข้ามฝั่ง
            UIKit.Img("Divider", canvas, null, new Vector2(6, 1080), false).color = new Color(.1f, .1f, .1f);
            hud.SetAsLastSibling();                                                        // HUD อยู่เหนือจอผู้เล่นและเส้นแบ่ง
            timer = new RoundTimer(hud);
            hudRoot = hud.gameObject;

            overlay = new CenterOverlay(canvas);
            events = new WasteEvents(hud);
            practice = new PracticeOverlay(canvas);
            select = new CharacterSelect(canvas);
            cover = new IntroCover(canvas, gameObject);

            cutsceneStart = new Cutscene(canvas, "cutscene_start.mp4", "START GAME", "ภารกิจกอบกู้โลกเริ่มต้นขึ้นแล้ว");
            cutsceneEnd = new Cutscene(canvas, "cutscene_game1_end.mp4", "MISSION 01 CLEAR", "อากาศของเมืองกลับมาสะอาดแล้ว");
            toGame1 = new MissionTransition(canvas, "MISSION 01", "WASTE HUNTERS", "เก็บขยะอิเล็กทรอนิกส์  •  กู้อากาศให้เมือง",
                MissionTransition.Cyan, MissionTransition.Yellow, MissionTransition.Pink);
            toGame2 = new MissionTransition(canvas, "MISSION 02", "THE LAST SEED", "ร่วมกันส่งพลังปลูกต้นไม้ให้โลก",
                MissionTransition.Green, MissionTransition.Green, MissionTransition.Cyan);
            fadeIn = WarpFlash.Create(canvas);

            guard = new PresenceGuard(canvas);
            debugText = UIKit.Label("Debug", canvas, "", 22, Color.yellow, TextAnchor.LowerCenter);
            UIKit.Anchor(debugText.rectTransform, new Vector2(.5f, 0), new Vector2(0, 6));
        }

        // ================================================================== เปลี่ยนสถานะ
        void GoCover()
        {
            StopAllCoroutines();
            Bgm.Play("bgm_menu");
            state = State.Cover;
            GameSession.Active = false; GameSession.Clear(); guard.Hide();
            select.Hide(); practice.Hide(); hudRoot.SetActive(false); overlay.Hide();
            cutsceneStart.Hide(); cutsceneEnd.Hide(); toGame1.Hide(); toGame2.Hide();
            foreach (var lane in lanes) lane.Practice = false;
            foreach (var lane in lanes) lane.Reset();
            cover.Show();
        }

        void GoSelect()
        {
            state = State.Select;
            cover.Hide(); select.Show();
            GameSession.Active = true; guard.Reset();
        }

        // Cutscene Start Game: หลังการ์ด START (วาร์ปจบ) -> เล่นวิดีโอ -> หน้าสอนเล่นเกม 1
        void GoCutsceneStart()
        {
            state = State.CutsceneStart;
            select.Hide();
            cutsceneStart.Play();
        }

        // สอนเล่นซ้อนบนฉากเกมจริง: ผู้เล่นเดิน/เล็ง/ดูดได้จริง แต่ขยะเป็นของซ้อม
        void GoPractice()
        {
            cutsceneStart.Hide(); toGame1.Hide();
            if (state == State.Mission1) fadeIn.Play();   // การ์ดภารกิจจบที่จอขาว -> จางเผยหน้าสอนเล่น
            state = State.Practice;
            Bgm.Play("bgm_game1", .8f);
            cover.Hide(); select.Hide(); hudRoot.SetActive(true);
            foreach (var lane in lanes) { lane.Reset(); lane.Practice = true; }
            timer.Set(Settings.RoundSeconds);
            practice.Show();
        }

        IEnumerator CountdownThenPlay()
        {
            state = State.Countdown;
            practice.Hide(); hudRoot.SetActive(true);
            foreach (var lane in lanes) { lane.Practice = false; lane.Reset(); }
            events.Reset();
            timeLeft = Settings.RoundSeconds; timer.Set(timeLeft);
            yield return overlay.Countdown();
            state = State.Play;
        }

        IEnumerator ShowResultThenCover()
        {
            state = State.Result;
            hudRoot.SetActive(false);                     // ซ่อนคะแนน/เวลา ไม่ให้ทับหน้าสรุปผล
            overlay.ShowResult(lanes[0], lanes[1]);
            Sfx.Play("result");
            yield return new WaitForSeconds(Settings.ResultSeconds);
            if (!GameSession.Active) { GoCover(); yield break; }
            // ไปเกม 2: cutscene หลังเกม 1 -> แอนิเมชันวาร์ป MISSION 02 -> ขาวเต็มจอ -> โหลดฉาก (เกม 2 จางจากขาวเอง)
            state = State.CutsceneEnd;
            Bgm.Stop();
            cutsceneEnd.Play();
            while (!cutsceneEnd.Tick(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)) yield return null;
            state = State.ToGame2;
            overlay.Hide();
            toGame2.Play();
            cutsceneEnd.Hide();
            while (!toGame2.Tick()) yield return null;
            GameSession.FromWarp = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSession.Game2Scene);
        }

        // ================================================================== ทุกเฟรม
        void Update()
        {
            var tracking = TrackingReceiver.I;
            if (tracking == null || select == null) return;   // เฟรมที่กำลังสลับฉาก/ออกจาก Play mode
            bool online = tracking.Connected;
            var players = tracking.Players;
            var keys = Keyboard.current;
            bool skip = keys != null && keys.spaceKey.wasPressedThisFrame;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) Application.Quit();
                if (keys.f1Key.wasPressedThisFrame) showDebug = !showDebug;
                if (keys.rKey.wasPressedThisFrame) GoCover();
            }

            switch (state)
            {
                case State.Cover:      // วิดีโอ + UI 0 รอผู้เล่นครบ 2 คน
                    if (cover.Tick(players, online, skip)) GoSelect();
                    break;

                case State.Select:     // ยกแขนเลือกตัวละคร -> นับ 3 วิ -> ซูม cinematic
                    if (select.Tick(players, online, skip) == CharacterSelect.Result.Done) GoCutsceneStart();
                    break;

                case State.CutsceneStart:   // วิดีโอ Start Game (Space = ข้าม)
                    if (cutsceneStart.Tick(skip)) { state = State.Mission1; toGame1.Play(); }
                    break;

                case State.Mission1:        // การ์ด MISSION 01 + เส้นแสงวาร์ป -> ขาวเต็มจอ -> หน้าสอนเล่นเกม 1
                    if (toGame1.Tick()) GoPractice();
                    break;

                case State.Practice:   // สอนเล่นซ้อนบนฉากจริง (READY ทั้งคู่ หรือครบ 15 วิ)
                    if (practice.Tick(lanes, skip)) StartCoroutine(CountdownThenPlay());
                    break;

                case State.Play:       // นับเวลา และเช็กว่าจบหรือยัง
                    timeLeft -= Time.deltaTime;
                    timer.Set(timeLeft);
                    events.Tick(Settings.RoundSeconds - timeLeft, lanes, Settings);
                    bool rush = timeLeft <= Settings.RushSeconds;      // FINAL RUSH: วินาทีท้าย ๆ ขยะถี่ขึ้น + คะแนนคูณ
                    if (rush && !lanes[0].Rush) events.StartRush(Settings);
                    foreach (var l in lanes) l.Rush = rush;
                    timer.SetRush(rush);
                    if (timeLeft <= 0)
                        StartCoroutine(ShowResultThenCover());
                    break;
            }

            // จอผู้เล่นทั้งสองทำงานทุกเฟรม (นอกช่วงเล่นจะหยุดเดินและซ่อนขยะ/ปืนเอง)
            bool playing = state == State.Play || state == State.Practice;
            bool showWorld = playing || state == State.Countdown;
            // เฝ้าผู้เล่นตั้งแต่หน้าเลือกตัวละคร (หน้าปกไม่ต้อง เพราะรอคนอยู่แล้ว)
            if (state == State.Cover) guard.Hide();
            else if (guard.Tick(players, online)) { GoCover(); return; }

            for (int i = 0; i < 2; i++) lanes[i].Tick(players[i], online, playing, showWorld);

            debugText.text = !showDebug ? "" :
                $"tracker {(online ? "ONLINE" : "OFFLINE")} {tracking.TrackerFps:0} fps | " +
                $"P1 {(players[0].present ? "in" : "-")} {players[0].dist:0.00}m hand {players[0].use} | " +
                $"P2 {(players[1].present ? "in" : "-")} {players[1].dist:0.00}m hand {players[1].use} | {state} | hero {GameSession.Heroes[0]}/{GameSession.Heroes[1]}";
        }
    }
}
