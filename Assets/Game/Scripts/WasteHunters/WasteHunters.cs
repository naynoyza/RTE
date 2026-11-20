// =====================================================================================================
// WasteHunters — ตัวควบคุมเกมหลัก (ใส่ไว้ที่ GameObject "Game" ในฉาก WasteHunters)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : สร้าง Canvas และทุกหน้าจอ แล้วสลับหน้าจอตามสถานะของเกม
//
//   Cover ──(กล้องเห็นผู้เล่น)──> Standby ──(ครบ 2 คนค้าง 2 วิ)──> Countdown ──> Play ──(หมดเวลา/ถึงเป้า)──> Result ──(10 วิ)──> Cover
//                                    └──(ไม่มีใครเลย 8 วิ)──> Cover
//
// ดึงข้อมูล: TrackingReceiver.I.Players (ข้อมูล P1/P2 จากกล้อง) และค่าปรับเกมใน Settings
// ส่งต่อ  : แต่ละเฟรมส่งข้อมูลผู้เล่นให้ PlayerLane ของคนนั้นไปจัดการเดิน/เล็ง/เก็บขยะ
// ปุ่มลัด : Space = ข้ามหน้าปก/จุดยืน, R = กลับหน้าปก, F1 = ข้อมูลดีบัก, Esc = ออก
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

        enum State { Cover, Standby, Countdown, Play, Result }
        State state;

        PlayerLane[] lanes;
        GameObject hudRoot;
        RoundTimer timer;
        CenterOverlay overlay;
        StandbyScreen standby;
        CoverScreen cover;
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
            standby = new StandbyScreen(canvas, Settings);
            cover = new CoverScreen(canvas, gameObject);

            debugText = UIKit.Label("Debug", canvas, "", 22, Color.yellow, TextAnchor.LowerCenter);
            UIKit.Anchor(debugText.rectTransform, new Vector2(.5f, 0), new Vector2(0, 6));
        }

        // ================================================================== เปลี่ยนสถานะ
        void GoCover()
        {
            StopAllCoroutines();
            state = State.Cover;
            standby.Hide(); hudRoot.SetActive(false); overlay.Hide();
            foreach (var lane in lanes) lane.Reset();
            cover.Show();
        }

        void GoStandby()
        {
            state = State.Standby;
            cover.Hide(); standby.Show();
        }

        IEnumerator CountdownThenPlay()
        {
            state = State.Countdown;
            standby.Hide(); hudRoot.SetActive(true);
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
                case State.Cover:      // รอให้กล้องเห็นผู้เล่นคนแรก
                    cover.Tick(online);
                    if ((online && (players[0].present || players[1].present)) || skip) GoStandby();
                    break;

                case State.Standby:    // รอครบ 2 คน
                    var r = standby.Tick(players, online, skip);
                    if (r == StandbyScreen.Result.Start) StartCoroutine(CountdownThenPlay());
                    else if (r == StandbyScreen.Result.Abandoned) GoCover();
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
                $"P2 {(players[1].present ? "in" : "-")} {players[1].dist:0.00}m hand {players[1].use} | {state}";
        }
    }
}
