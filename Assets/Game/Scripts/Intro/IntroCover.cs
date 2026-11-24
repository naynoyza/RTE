// =====================================================================================================
// IntroCover — หน้าปกใหม่: วิดีโอ Video background/Sceen0.mp4 วนเต็มจอ + UI 0 (กรอบฮอโลแกรม 2 ผู้เล่น)
// -----------------------------------------------------------------------------------------------------
// UI 0 จะ "โผล่เป็นช่วง ๆ" (ทุก CycleSeconds โชว์ ShowSeconds) และ "ค้างไว้" ทันทีเมื่อมีผู้เล่นมายืนหน้ากล้อง
// ใต้กรอบแต่ละฝั่งมีสถานะ: รอ / ใกล้-ไกลเกินไป / มาแล้ว ✓
// ครบ 2 คนค้าง HoldSeconds -> Tick() คืน true (ไปหน้าเลือกตัวละคร)
// ไม่มีกล้อง: Space = ข้าม
// ดึงข้อมูล: StreamingAssets/intro.mp4, Resources/Sprites/intro/ui0.png
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RTE
{
    public class IntroCover
    {
        const float CycleSeconds = 12f, ShowSeconds = 6f, HoldSeconds = 2f;

        readonly GameObject root;
        readonly VideoPlayer video;
        readonly CanvasGroup uiGroup;
        readonly Text title, sub, hint;
        readonly Text[] status = new Text[2];
        readonly Image[] statusBox = new Image[2];
        readonly Image[] check = new Image[2];
        float alpha, bothTime, shownAt;

        static readonly Color Orange = new Color(1f, .6f, .15f), Cyan = new Color(.2f, .95f, 1f);

        public IntroCover(Transform canvas, GameObject videoHost)
        {
            var rt = UIKit.Rect("IntroCover", canvas); UIKit.Stretch(rt);
            root = rt.gameObject;
            var black = UIKit.Img("Black", rt, null, Vector2.zero, false); UIKit.Stretch(black.rectTransform);
            black.color = Color.black;

            var texture = new RenderTexture(1920, 1080, 0);
            var screen = UIKit.Rect("Video", rt); UIKit.Stretch(screen);
            var raw = screen.gameObject.AddComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false;
            video = videoHost.AddComponent<VideoPlayer>();
            video.playOnAwake = false; video.isLooping = true;
            video.source = VideoSource.Url;
            video.url = System.IO.Path.Combine(Application.streamingAssetsPath, "intro.mp4");
            video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = texture;
            video.audioOutputMode = VideoAudioOutputMode.None;

            // ชั้น UI 0 (จางเข้า/ออกทั้งก้อน)
            var ui = UIKit.Rect("UI0", rt); UIKit.Stretch(ui);
            uiGroup = ui.gameObject.AddComponent<CanvasGroup>();
            var dim = UIKit.Img("Dim", ui, null, Vector2.zero, false); UIKit.Stretch(dim.rectTransform);
            dim.color = new Color(0, 0, 0, .45f);
            var frame = UIKit.Img("Frame", ui, UIKit.Load("intro/ui0"), new Vector2(1920, 1072), true);

            title = UIKit.Label("Title", ui, "2 PLAYERS REQUIRED", 64, Cyan);
            title.rectTransform.anchoredPosition = new Vector2(0, 430);
            sub = UIKit.Label("Sub", ui, "เชิญยืนหน้ากล้องเซนเซอร์ (ห่าง 2 – 3 เมตร) เพื่อเริ่มกอบกู้โลก", 34, Color.white);
            sub.rectTransform.anchoredPosition = new Vector2(0, 365);

            for (int i = 0; i < 2; i++)
            {
                var c = i == 0 ? Orange : Cyan;
                float x = i == 0 ? -455 : 455;
                statusBox[i] = UIKit.Panel($"StatusBox{i + 1}", ui, new Vector2(520, 120), new Color(0, 0, 0, .65f));
                statusBox[i].rectTransform.anchoredPosition = new Vector2(x, -255);
                status[i] = UIKit.Label("Status", statusBox[i].transform, "", 34, c);
                check[i] = UIKit.Img($"Check{i + 1}", ui, UIKit.Load("fx_ring"), new Vector2(330, 330), false);
                check[i].rectTransform.anchoredPosition = new Vector2(x, -10);
            }

            hint = UIKit.Label("Hint", rt, "", 30, new Color(1, 1, 1, .85f));
            UIKit.Anchor(hint.rectTransform, new Vector2(.5f, 0), new Vector2(0, 18));
        }

        public void Show() { root.SetActive(true); if (!video.isPlaying) video.Play(); bothTime = 0; shownAt = Time.time; alpha = 0; }
        public void Hide() { root.SetActive(false); video.Pause(); }

        // คืน true เมื่อครบ 2 คนค้างพอ (หรือกด Space)
        public bool Tick(TrackedPlayer[] players, bool online, bool skip)
        {
            bool p1 = online && players[0].present, p2 = online && players[1].present;
            bool anyone = p1 || p2 || (online && (players[0].approach > 0 || players[1].approach > 0));

            // UI 0 โผล่เป็นช่วง ๆ หรือค้างเมื่อมีคน
            float phase = (Time.time - shownAt) % CycleSeconds;
            bool want = anyone || phase < ShowSeconds;
            alpha = Mathf.MoveTowards(alpha, want ? 1 : 0, Time.deltaTime * 1.6f);
            uiGroup.alpha = alpha;
            uiGroup.transform.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, alpha);

            Side(0, players[0], p1, online);
            Side(1, players[1], p2, online);

            bothTime = p1 && p2 ? bothTime + Time.deltaTime : 0;
            if (p1 && p2)
            {
                title.text = $"พร้อมแล้ว! ไปหน้าเลือกตัวละครใน {Mathf.CeilToInt(HoldSeconds - bothTime)}";
                title.color = Color.green;
            }
            else
            {
                title.text = p1 || p2 ? "รอผู้เล่นอีก 1 คน..." : "2 PLAYERS REQUIRED";
                title.color = Color.Lerp(Cyan, Color.white, Mathf.PingPong(Time.time, 1));
            }
            hint.text = online ? "" : "ไม่พบกล้อง – กด Space เพื่อข้าม (ทดสอบ)";
            return bothTime >= HoldSeconds || skip;
        }

        void Side(int i, TrackedPlayer p, bool ready, bool online)
        {
            string who = $"PLAYER {i + 1}: ";
            string text;
            if (ready) text = who + "มาแล้ว ✓";
            else if (online && p.approach > 0 && p.approach < 2f) text = who + "ใกล้เกินไป ถอยหลัง";
            else if (online && p.approach > 3f) text = who + "ไกลเกินไป เดินเข้ามา";
            else if (online && p.approach > 0) text = who + "ตรวจจับ... ยืนนิ่ง ๆ";
            else text = who + "ตรวจจับ...\n(WAITING...)";
            status[i].text = text;
            status[i].color = ready ? Color.green : (i == 0 ? Orange : Cyan);
            check[i].color = ready ? new Color(.3f, 1f, .4f, .9f) : new Color(1, 1, 1, .12f + .1f * Mathf.Sin(Time.time * 4));
            check[i].rectTransform.localScale = Vector3.one * (ready ? 1 + .04f * Mathf.Sin(Time.time * 6) : 1);
        }
    }
}
