// =====================================================================================================
// ReportCapture — แคปภาพ gameplay ทุกฉากอัตโนมัติ (ไว้ทำรายงาน) ให้บอตเล่นเกม 1 -> เกม 2 จนจบ
// -----------------------------------------------------------------------------------------------------
// เมนู:  RTE/Report Capture/Run   เปิดฉาก WasteHunters, ตั้งหน้า Game เป็น 1920x1080, เปิดบอต (ซ่อนป้าย BOT) แล้วกด Play
//        RTE/Report Capture/Stop  หยุดกลางคัน
// ไฟล์ออก: Artwork/Gameplay/NNN_<WH|LS>-<State>[-event].png  (ลำดับ NNN ตามเวลาที่ถ่าย)
// ระหว่างเกม 1 จะเรียก DebugGo ให้เกิดอีเวนต์ fever / dust / overdrive / stun / noair เพื่อให้มีภาพครบทุกอีเวนต์
// จอที่สองเปิดเต็มจอตามปกติระหว่างแคป (ดูเทสบนจอใหญ่ได้) — จบรอบแล้วปรับทุกภาพเป็น 1920x1080
// หมายเหตุ: เครื่องมือทำรายงานเท่านั้น ใช้ reflection กับ GameView ภายในของ Unity
// =====================================================================================================
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTE.EditorTools
{
    [InitializeOnLoad]
    public static class ReportCapture
    {
        const string RunKey = "RTE.ReportCapture.Run";             // SessionState: อยู่รอดข้าม domain reload ตอนกด Play
        const string OutDir = "Artwork/Gameplay";
        const float TimeoutSeconds = 480f;
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // ช่วงห่างระหว่างภาพ (วิ) และจำนวนภาพสูงสุดต่อสถานะ
        static (float every, int cap) Plan(string state) => state switch
        {
            "Cover" => (1.5f, 5),
            "CutsceneStart" => (2f, 7),
            "CutsceneEnd" => (1.5f, 6),
            "ToGame2" => (.5f, 6),
            "Mission1" => (.5f, 6),
            "Select" => (1.0f, 40),
            "Practice" => (1.5f, 8),
            "Countdown" => (0.7f, 5),
            "Play" => (3f, 40),
            "Finale" => (0.6f, 9),
            "Result" => (1.0f, 8),
            _ => (2f, 4),
        };

        // อีเวนต์ที่สั่งให้เกิดระหว่างเกม 1 (วินาทีนับจากเริ่ม Play)
        static readonly (float at, string cmd)[] Events =
        {
            (12f, "fever"), (32f, "dust"), (48f, "overdrive"), (60f, "stun"), (68f, "noair"),
        };

        static int shot;
        static string lastKey;
        static float stateStart, lastShot, runStart;
        static int stateShots, nextEvent;
        static string eventTag;
        static float eventTagUntil;

        static ReportCapture()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += c =>
            {
                if (c == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RunKey, false)) Finish("ออกจาก Play mode");
            };
        }

        [MenuItem("RTE/Report Capture/Run")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ReportCapture] ออกจาก Play mode ก่อน"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/WasteHunters.unity");
            Directory.CreateDirectory(OutDir);
            // จอที่สองยังเปิดเต็มจอตามปกติ (เห็นเทสบนจอใหญ่) — ภาพที่ได้ทุกขนาดจะถูกปรับเป็น 1920x1080 ตอนจบรอบ
            SetGameViewSize(1920, 1080);
            SessionState.SetBool(RunKey, true);
            shot = 0; lastKey = null; nextEvent = 0;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("RTE/Report Capture/Stop")]
        public static void Stop()
        {
            Finish("หยุดเอง");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        static void Finish(string why)
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            SessionState.EraseBool(RunKey);
            BotPlayer.On = false; BotPlayer.ShowLabel = true;
            // ไฟล์สุดท้ายเขียนเสร็จตอนจบเฟรม -> ปรับขนาดในเฟรมถัดไป
            EditorApplication.delayCall += () =>
            {
                int fixedCount = NormalizeShots(1920, 1080);
                Debug.Log($"[ReportCapture] จบ ({why}) — {shot} ภาพใน {OutDir} (ปรับขนาด {fixedCount} ภาพเป็น 1920x1080)");
            };
        }

        /// <summary>ปรับทุกภาพใน OutDir ที่ไม่ใช่ w x h ให้เป็น w x h (ภาพจากจอที่สองอาจเล็ก/ใหญ่กว่า)</summary>
        static int NormalizeShots(int w, int h)
        {
            int n = 0;
            if (!Directory.Exists(OutDir)) return 0;
            foreach (var file in Directory.GetFiles(OutDir, "*.png"))
            {
                var src = new Texture2D(2, 2);
                if (!src.LoadImage(File.ReadAllBytes(file)) || (src.width == w && src.height == h)) { UnityEngine.Object.DestroyImmediate(src); continue; }
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
                dst.ReadPixels(new Rect(0, 0, w, h), 0, 0); dst.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(file, dst.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(src); UnityEngine.Object.DestroyImmediate(dst);
                n++;
            }
            return n;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(RunKey, false) || !EditorApplication.isPlaying) return;
            float now = Time.realtimeSinceStartup;
            if (runStart <= 0) runStart = now;

            BotPlayer.On = true; BotPlayer.Both = true; BotPlayer.ShowLabel = false;

            string game, state; MonoBehaviour wh = null;
            var ls = UnityEngine.Object.FindFirstObjectByType<LastSeed>();
            if (ls != null) { game = "LS"; state = ls.StateName; }
            else
            {
                wh = UnityEngine.Object.FindFirstObjectByType<WasteHunters>();
                if (wh == null) return;
                game = "WH"; state = ((WasteHunters)wh).StateName;
            }

            string key = game + "-" + state;
            if (key != lastKey)
            {
                lastKey = key; stateStart = now; stateShots = 0; lastShot = -99;
                if (key == "WH-Play") nextEvent = 0;
            }
            float inState = now - stateStart;

            // เกม 1: สั่งอีเวนต์ตามเวลา แล้วแคปภาพอีเวนต์นั้นหลังเริ่ม 1.2 วิ
            if (key == "WH-Play" && nextEvent < Events.Length && inState >= Events[nextEvent].at)
            {
                ((WasteHunters)wh).DebugGo(Events[nextEvent].cmd);
                eventTag = Events[nextEvent].cmd; eventTagUntil = now + 1.2f;
                nextEvent++;
            }
            if (eventTag != null && now >= eventTagUntil) { Capture($"{key}-{eventTag}"); eventTag = null; }

            // หน้าปกค้าง (บอตไม่ผ่าน) -> ข้ามไปหน้าเลือกตัวละคร
            if (key == "WH-Cover" && inState > 20f) ((WasteHunters)wh).DebugGo("select");

            var (every, cap) = Plan(state);
            if (inState >= .5f && stateShots < cap && now - lastShot >= every)
            {
                Capture(key);
                stateShots++; lastShot = now;
            }

            bool done = key == "LS-Result" && (stateShots >= Plan("Result").cap || inState > 9f);
            if (done || now - runStart > TimeoutSeconds)
            {
                Finish(done ? "ครบ" : "หมดเวลา");
                EditorApplication.isPlaying = false;
                runStart = 0;
            }
        }

        static void Capture(string label)
        {
            shot++;
            string path = Path.Combine(OutDir, $"{shot:000}_{label}.png");
            ScreenCapture.CaptureScreenshot(path);   // ขนาดตามหน้าเกมที่เล่นอยู่ (จอที่สอง) — ปรับเป็น 1920x1080 ตอนจบรอบ
        }

        // ------------------------------------------------------------------ ตั้งขนาดหน้า Game (reflection)
        static void SetGameViewSize(int w, int h)
        {
            try
            {
                var asm = typeof(EditorWindow).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var inst = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(inst, new object[] { (int)GameViewSizeGroupType.Standalone });
                var gt = group.GetType();
                int total = (int)gt.GetMethod("GetTotalCount").Invoke(group, null), idx = -1;
                for (int i = 0; i < total && idx < 0; i++)
                {
                    var s = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var st = s.GetType();
                    if ((int)st.GetProperty("width").GetValue(s) == w && (int)st.GetProperty("height").GetValue(s) == h
                        && Convert.ToInt32(st.GetProperty("sizeType").GetValue(s)) == 1) idx = i;
                }
                if (idx < 0)
                {
                    var enumT = asm.GetType("UnityEditor.GameViewSizeType");
                    var size = asm.GetType("UnityEditor.GameViewSize")
                        .GetConstructor(new[] { enumT, typeof(int), typeof(int), typeof(string) })
                        .Invoke(new object[] { Enum.ToObject(enumT, 1), w, h, $"{w}x{h}" });
                    gt.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    idx = total;
                }
                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gvType, false, null, false);
                gvType.GetProperty("selectedSizeIndex", Any).SetValue(gv, idx);
            }
            catch (Exception e) { Debug.LogWarning("[ReportCapture] ตั้งขนาดหน้า Game ไม่ได้: " + e.Message); }
        }
    }
}
