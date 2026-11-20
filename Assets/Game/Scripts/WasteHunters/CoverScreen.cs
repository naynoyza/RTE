// =====================================================================================================
// CoverScreen — หน้าปกของเกม
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : เล่นวิดีโอปกวนซ้ำเต็มจอ + แถบด้านล่างบอกชื่อภารกิจ และให้ผู้เล่นเดินเข้าบริเวณกล้อง
// ดึงข้อมูล: วิดีโอ Assets/StreamingAssets/cover.mp4 (VideoPlayer -> RenderTexture -> RawImage)
// ใช้โดย  : WasteHunters (สถานะ Cover) — เมื่อกล้องเห็นผู้เล่นคนแรก เกมจะไปหน้าจุดยืน
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RTE
{
    public class CoverScreen
    {
        readonly GameObject root;
        readonly VideoPlayer video;
        readonly Text prompt;

        public CoverScreen(Transform canvas, GameObject videoHost)
        {
            var rt = UIKit.Rect("CoverScreen", canvas); UIKit.Stretch(rt);
            root = rt.gameObject;
            var black = UIKit.Img("Black", rt, null, Vector2.zero, false); UIKit.Stretch(black.rectTransform);
            black.color = Color.black;

            // วิดีโอ: เล่นลง RenderTexture แล้วแสดงด้วย RawImage เต็มจอ
            var texture = new RenderTexture(1920, 1080, 0);
            var screen = UIKit.Rect("Video", rt); UIKit.Stretch(screen);
            var raw = screen.gameObject.AddComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false;
            video = videoHost.AddComponent<VideoPlayer>();
            video.playOnAwake = false; video.isLooping = true;
            video.source = VideoSource.Url;
            video.url = System.IO.Path.Combine(Application.streamingAssetsPath, "cover.mp4");
            video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = texture;
            video.audioOutputMode = VideoAudioOutputMode.None;

            // แถบข้อความด้านล่าง
            var band = UIKit.Img("Band", rt, null, new Vector2(1920, 200), false);
            UIKit.Anchor(band.rectTransform, new Vector2(.5f, 0), new Vector2(0, 40));
            band.color = new Color(0, 0, 0, .6f);
            var mission = UIKit.Label("Mission", band.transform, "ภารกิจที่ 1  •  WASTE HUNTERS  –  เก็บขยะ กู้อากาศให้เมือง", 34, new Color(.6f, 1f, .6f));
            mission.rectTransform.anchoredPosition = new Vector2(0, 52);
            prompt = UIKit.Label("Prompt", band.transform, "", 54, Color.white);
            prompt.rectTransform.anchoredPosition = new Vector2(0, -22);
        }

        public void Show() { root.SetActive(true); if (!video.isPlaying) video.Play(); }
        public void Hide() { root.SetActive(false); video.Pause(); }

        // ทุกเฟรม: ข้อความกะพริบ ถ้าไม่มีกล้องบอกให้กด Space แทน
        public void Tick(bool cameraOnline)
        {
            prompt.text = cameraOnline
                ? "กรุณาเดินเข้ามาในบริเวณกล้อง (ห่าง 2 – 3 เมตร) เพื่อเริ่มเกม"
                : "กรุณาเดินเข้ามาในบริเวณกล้อง   (ไม่พบกล้อง – กด Space เพื่อเริ่ม)";
            prompt.color = Color.Lerp(Color.white, new Color(1, .85f, .3f), Mathf.PingPong(Time.time, 1));
        }
    }
}
