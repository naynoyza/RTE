// =====================================================================================================
// Cutscene — เล่นวิดีโอคั่นฉากเต็มจอ (จางเข้าจากดำ -> เล่นจนจบ -> จางออก)
// -----------------------------------------------------------------------------------------------------
// ไฟล์วิดีโอ: Assets/StreamingAssets/<file>  เช่น cutscene_start.mp4 (หลังเลือกตัวละคร), cutscene_game1_end.mp4 (หลังเกม 1)
// ยังไม่มีไฟล์ -> ขึ้นการ์ดชื่อฉาก (title/sub) ค้าง FallbackSeconds วิแทน  ใส่ไฟล์ทีหลังได้เลยไม่ต้องแก้โค้ด
// Space = ข้าม
// ใช้: var c = new Cutscene(canvas, "cutscene_start.mp4", "START GAME", "..."); c.Play(); ... if (c.Tick(skip)) { c.Hide(); ไปต่อ }
// =====================================================================================================
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RTE
{
    public class Cutscene
    {
        const float FadeSeconds = .5f, FallbackSeconds = 4f, PrepareTimeout = 5f;

        readonly GameObject root;
        readonly RawImage screen;
        readonly Image black;
        readonly CanvasGroup fallback;
        readonly VideoPlayer video;
        readonly string path;
        float t, length;
        bool useVideo, ended, closing;
        float closeT;

        /// <summary>สร้างชั้น cutscene บน canvas (ซ่อนไว้จนกว่าจะ Play)</summary>
        public Cutscene(Transform canvas, string file, string title, string sub)
        {
            path = Path.Combine(Application.streamingAssetsPath, file);
            var rt = UIKit.Rect("Cutscene_" + Path.GetFileNameWithoutExtension(file), canvas); UIKit.Stretch(rt);
            root = rt.gameObject;
            var bg = UIKit.Img("Bg", rt, null, Vector2.zero, false); UIKit.Stretch(bg.rectTransform); bg.color = Color.black;

            var tex = new RenderTexture(1920, 1080, 0);
            var s = UIKit.Rect("Video", rt); UIKit.Stretch(s);
            screen = s.gameObject.AddComponent<RawImage>(); screen.texture = tex; screen.raycastTarget = false;
            var host = new GameObject("CutsceneVideo_" + file);
            host.transform.SetParent(root.transform, false);
            video = host.AddComponent<VideoPlayer>();
            video.playOnAwake = false; video.isLooping = false;
            video.source = VideoSource.Url; video.url = path;
            video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = tex;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.loopPointReached += _ => ended = true;

            // การ์ดสำรองตอนยังไม่มีวิดีโอ
            var fb = UIKit.Rect("Fallback", rt); UIKit.Stretch(fb);
            fallback = fb.gameObject.AddComponent<CanvasGroup>();
            var glow = UIKit.Img("Glow", fb, UIKit.Load("fx_glow"), new Vector2(1600, 700), false);
            glow.color = new Color(.3f, .9f, 1f, .35f);
            UIKit.Label("Title", fb, title, 120, Color.white).rectTransform.anchoredPosition = new Vector2(0, 40);
            UIKit.Label("Sub", fb, sub, 40, new Color(.75f, .95f, 1f)).rectTransform.anchoredPosition = new Vector2(0, -70);

            black = UIKit.Img("Fade", rt, null, Vector2.zero, false); UIKit.Stretch(black.rectTransform);
            root.SetActive(false);
        }

        /// <summary>เริ่มเล่น (อยู่บนสุดของจอ)</summary>
        public void Play()
        {
            root.SetActive(true); root.transform.SetAsLastSibling();
            t = 0; ended = closing = false;
            useVideo = File.Exists(path);
            if (!useVideo) Debug.Log($"[Cutscene] ยังไม่มีวิดีโอ {path} — แสดงการ์ดแทน");
            screen.enabled = useVideo; fallback.alpha = useVideo ? 0 : 1;
            length = FallbackSeconds;
            if (useVideo) { Bgm.Stop(); video.Prepare(); video.Play(); }
        }

        public void Hide() { if (video.isPlaying) video.Stop(); root.SetActive(false); }

        /// <summary>เรียกทุกเฟรม คืน true เมื่อจบ (จางเป็นดำแล้ว)</summary>
        public bool Tick(bool skip)
        {
            t += Time.deltaTime;
            if (useVideo && video.isPrepared && video.length > 0) length = (float)video.length;
            bool timeUp = useVideo ? ended || (!video.isPrepared && t > PrepareTimeout) : t >= length;
            if (!closing && (skip || timeUp)) { closing = true; closeT = 0; }

            float a = 1 - Mathf.Clamp01(t / FadeSeconds);                 // จางเข้า
            if (closing) { closeT += Time.deltaTime; a = Mathf.Clamp01(closeT / FadeSeconds); }
            black.color = new Color(0, 0, 0, a);
            if (!useVideo) fallback.transform.localScale = Vector3.one * (1 + .02f * t);
            return closing && closeT >= FadeSeconds;
        }
    }
}
