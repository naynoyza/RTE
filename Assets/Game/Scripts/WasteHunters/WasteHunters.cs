// =====================================================================================================
// WasteHunters — ตัวควบคุมเกมหลัก (ใส่ไว้ที่ GameObject "Game" ในฉาก WasteHunters)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : สร้าง Canvas และทุกหน้าจอ แล้วสลับหน้าจอตามสถานะของเกม
//
//   Cover (วิดีโอ + UI 0, รอครบ 2 คน) ──> Select (ยกแขนเลือกตัวละคร, นับ 3 วิ, ซูม cinematic)
//     ──> Tutorial (สอนเล่น) ──> Countdown ──> Play ──(หมดเวลา/ถึงเป้า)──> Result ──(10 วิ)──> Cover
//
// ดึงข้อมูล: TrackingReceiver.I.Players (ข้อมูล P1/P2 จากกล้อง) และค่าปรับเกมใน Settings
// ส่งต่อ  : แต่ละเฟรมส่งข้อมูลผู้เล่นให้ PlayerLane ของคนนั้นไปจัดการเดิน/เล็ง/เก็บขยะ
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

        enum State { Cover, Select, Tutorial, Countdown, Play, Result }
        State state;

        PlayerLane[] lanes;
        GameObject hudRoot;
        RoundTimer timer;
        CenterOverlay overlay;
        IntroCover cover;
        CharacterSelect select;
        TutorialScreen tutorial;
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
                case "tutorial": GoTutorial(); break;
                case "play": StopAllCoroutines(); StartCoroutine(CountdownThenPlay()); break;
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
            UIKit.Img("Divider", canvas, null, new Vector2(6, 1080), false).color = new Color(.1f, .1f, .1f);
            hud.SetAsLastSibling();                                                        // HUD อยู่เหนือจอผู้เล่นและเส้นแบ่ง
            timer = new RoundTimer(hud);
            hudRoot = hud.gameObject;

            overlay = new CenterOverlay(canvas);
            tutorial = new TutorialScreen(canvas, "GAME 01", "WASTE HUNTERS", "เก็บขยะ กู้อากาศ", "ย่ำเท้า + ยกมือเล็ง",
                new[] { "เดินอัตโนมัติ ย่ำเท้าเพื่อเร่งความเร็ว", "ขยะจะลอยเข้ามาบนถนนของตัวเอง", "ยกมือเล็งปืนให้แตะขยะ = ดูดทันที", "เก็บให้ครบเป้าก่อนหมดเวลา ชนะ!" },
                "ย่ำเท้า  ▶  ยกมือ", "tut/g1", "wh/road");
            select = new CharacterSelect(canvas);
            cover = new IntroCover(canvas, gameObject);

            debugText = UIKit.Label("Debug", canvas, "", 22, Color.yellow, TextAnchor.LowerCenter);
            UIKit.Anchor(debugText.rectTransform, new Vector2(.5f, 0), new Vector2(0, 6));
        }

        // ================================================================== เปลี่ยนสถานะ
        void GoCover()
        {
            StopAllCoroutines();
            state = State.Cover;
            select.Hide(); tutorial.Hide(); hudRoot.SetActive(false); overlay.Hide();
            foreach (var lane in lanes) lane.Reset();
            cover.Show();
        }

        void GoSelect()
        {
            state = State.Select;
            cover.Hide(); select.Show();
        }

        void GoTutorial()
        {
            state = State.Tutorial;
            select.Hide(); tutorial.Show();
        }

        IEnumerator CountdownThenPlay()
        {
            state = State.Countdown;
            tutorial.Hide(); hudRoot.SetActive(true);
            foreach (var lane in lanes) lane.Reset();
            timeLeft = Settings.RoundSeconds; timer.Set(timeLeft);
            yield return overlay.Countdown();
            state = State.Play;
        }

        IEnumerator ShowResultThenCover()
        {
            state = State.Result;
            overlay.ShowResult(lanes[0], lanes[1]);
            yield return new WaitForSeconds(10f);
            GoCover();
        }

        // ================================================================== ทุกเฟรม
        void Update()
        {
            var tracking = TrackingReceiver.I;
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
                    if (select.Tick(players, online, skip) == CharacterSelect.Result.Done) GoTutorial();
                    break;

                case State.Tutorial:   // หน้าต่างสอนก่อนเล่น
                    if (tutorial.Tick(players, online, skip)) StartCoroutine(CountdownThenPlay());
                    break;

                case State.Play:       // นับเวลา และเช็กว่าจบหรือยัง
                    timeLeft -= Time.deltaTime;
                    timer.Set(timeLeft);
                    if (timeLeft <= 0 || lanes[0].Recycled >= Settings.Goal || lanes[1].Recycled >= Settings.Goal)
                        StartCoroutine(ShowResultThenCover());
                    break;
            }

            // จอผู้เล่นทั้งสองทำงานทุกเฟรม (นอกช่วงเล่นจะหยุดเดินและซ่อนขยะ/ปืนเอง)
            bool playing = state == State.Play;
            bool showWorld = playing || state == State.Countdown;
            for (int i = 0; i < 2; i++) lanes[i].Tick(players[i], online, playing, showWorld);

            debugText.text = !showDebug ? "" :
                $"tracker {(online ? "ONLINE" : "OFFLINE")} {tracking.TrackerFps:0} fps | " +
                $"P1 {(players[0].present ? "in" : "-")} {players[0].dist:0.00}m hand {players[0].use} | " +
                $"P2 {(players[1].present ? "in" : "-")} {players[1].dist:0.00}m hand {players[1].use} | {state} | hero {GameSession.Heroes[0]}/{GameSession.Heroes[1]}";
        }
    }
}
