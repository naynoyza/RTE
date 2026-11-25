// =====================================================================================================
// FullscreenGameView — กด Play เมื่อไหร่ ถ้าเครื่องต่อจอมากกว่า 1 จอ จะเปิดหน้า Game แยกออกมาเต็มจอ "อีกจอ"
// (จอที่ไม่ได้เปิดหน้าต่างหลักของ Unity อยู่) ไม่มีแถบเครื่องมือ/ขอบ/taskbar บัง — ตัว Editor ยังโชว์แท็บ Scene ไว้ตามเดิม
// ขนาดภาพตามจอนั้นจริง ๆ (Free Aspect) จะเป็น 1080p, 2K, 4K หรือโปรเจกเตอร์ก็เต็มจอหมด
// -----------------------------------------------------------------------------------------------------
// เมนู:  RTE/Fullscreen Game View/Auto on Play     เปิด/ปิดการทำงานอัตโนมัติตอนกด Play (ค่าเริ่มต้น: เปิด)
//        RTE/Fullscreen Game View/Toggle  (F11)   เปิด/ปิดหน้าเต็มจอเองระหว่างเล่น
// ออกจาก Play mode (Ctrl+P) แล้วหน้าเต็มจอจะปิดเอง | มีจอเดียว = ไม่ทำอะไร (เล่นในหน้า Game ปกติ)
// หมายเหตุ: ใช้ Win32 + API ภายในของ Unity (GameView) ผ่าน reflection — ถ้าบางส่วนใช้ไม่ได้ จะมี warning ใน Console
// =====================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace RTE.EditorTools
{
    [InitializeOnLoad]
    public static class FullscreenGameView
    {
        const string PrefKey = "RTE.FullscreenGameView.Auto";
        const string PopupIdKey = "RTE.FullscreenGameView.PopupId";   // SessionState: อยู่รอดข้าม domain reload
        const string MenuAuto = "RTE/Fullscreen Game View/Auto on Play";
        const string MenuToggle = "RTE/Fullscreen Game View/Toggle _F11";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        static bool Auto
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static FullscreenGameView()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
#if UNITY_EDITOR_WIN
                    if (Auto && FindOtherMonitor(out _)) KeepEditorLayout();
#endif
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    if (Auto) Open();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    // ปิดเฟรมถัดไป: GameView เองก็ฟังอีเวนต์นี้อยู่ ถ้าปิดทันทีจะเกิด MissingReferenceException
                    EditorApplication.delayCall += Close;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    EditorApplication.delayCall += Close;
                    RestorePlayBehavior();
                    break;
            }
        }

        // ------------------------------------------------------------------ ไม่ให้หน้า Game ใน Editor ขยาย/แย่งแท็บตอนกด Play
        const string SavedBehaviorKey = "RTE.FullscreenGameView.SavedBehavior";   // "instanceId:ค่าเดิม;..."

        /// <summary>ตั้งหน้า Game ใน Editor เป็น Play Unfocused ชั่วคราว (ไม่ขยายเต็ม Editor, ไม่แย่งแท็บ Scene)
        /// แล้วคืนค่าเดิมตอนกลับ Edit mode</summary>
        static void KeepEditorLayout()
        {
            var saved = new List<string>();
            foreach (var view in AllGameViews())
            {
                var p = view.GetType().GetProperty("enterPlayModeBehavior", Any);
                if (p == null || !p.CanWrite) { Debug.LogWarning("[FullscreenGameView] ตั้ง enterPlayModeBehavior ไม่ได้"); return; }
                var current = p.GetValue(view);
                if (current.ToString() == "PlayUnfocused") continue;
                saved.Add($"{view.GetInstanceID()}:{current}");
                p.SetValue(view, Enum.Parse(p.PropertyType, "PlayUnfocused"));
            }
            SessionState.SetString(SavedBehaviorKey, string.Join(";", saved));
        }

        static void RestorePlayBehavior()
        {
            var saved = SessionState.GetString(SavedBehaviorKey, "");
            SessionState.EraseString(SavedBehaviorKey);
            foreach (var entry in saved.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int id)) continue;
                var view = EditorUtility.InstanceIDToObject(id) as EditorWindow;
                var p = view != null ? view.GetType().GetProperty("enterPlayModeBehavior", Any) : null;
                if (p != null && p.CanWrite) p.SetValue(view, Enum.Parse(p.PropertyType, parts[1]));
            }
        }

        static EditorWindow[] AllGameViews() =>
            GameViewType == null ? new EditorWindow[0]
                : Resources.FindObjectsOfTypeAll(GameViewType).Cast<EditorWindow>().ToArray();

        // ------------------------------------------------------------------ เมนู
        [MenuItem(MenuAuto, false, 0)]
        static void ToggleAuto() => Auto = !Auto;

        [MenuItem(MenuAuto, true)]
        static bool ToggleAutoValidate() { Menu.SetChecked(MenuAuto, Auto); return true; }

        /// <summary>เปิด/ปิดหน้าเต็มจอเองระหว่าง Play. ตัวอย่าง: กด F11 ตอนเล่นอยู่</summary>
        [MenuItem(MenuToggle, false, 1)]
        public static void Toggle()
        {
            if (FindPopup() != null) Close();
            else Open();
        }

        [MenuItem(MenuToggle, true)]
        static bool ToggleValidate() => EditorApplication.isPlaying;

        // ------------------------------------------------------------------ เปิด/ปิด
        /// <summary>เปิดหน้า Game เต็มจอบนจอที่ไม่ใช่จอหลักของ Unity (ถ้ามีจอเดียว ไม่ทำอะไร)</summary>
        public static void Open()
        {
#if UNITY_EDITOR_WIN
            if (GameViewType == null) { Debug.LogWarning("[FullscreenGameView] ไม่พบ UnityEditor.GameView"); return; }
            Close();
            if (!FindOtherMonitor(out var target)) return;          // มีจอเดียว

            var view = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
            SessionState.SetInt(PopupIdKey, view.GetInstanceID());
            TrySet(view, "showToolbar", false);                     // ซ่อนแถบ Display/Scale/Stats ด้านบน
            TrySet(view, "selectedSizeIndex", 0);                   // 0 = Free Aspect -> ภาพเท่าขนาดจอจริง

            var before = new HashSet<IntPtr>(ThreadWindows());
            view.ShowPopup();                                       // หน้าต่างไม่มีขอบ
            var hwnd = ThreadWindows().FirstOrDefault(h => !before.Contains(h));
            if (hwnd == IntPtr.Zero) { Debug.LogWarning("[FullscreenGameView] หาหน้าต่างที่เพิ่งเปิดไม่เจอ"); return; }

            // วางด้วย Win32 เป็นพิกเซลจริงของจอนั้น (Unity เองจะบีบหน้าต่างให้หลบ taskbar)
            SetWindowPos(hwnd, HWND_TOPMOST, target.left, target.top,
                target.right - target.left, target.bottom - target.top, SWP_SHOWWINDOW);
            view.Focus();

            // Unity สลับแท็บในตัว Editor ไปที่ Game ตอนกด Play -> สลับกลับมาที่ Scene (เกมเล่นอยู่อีกจอแล้ว)
            // แล้วคืนโฟกัสให้หน้าเต็มจอ เพื่อให้คีย์บอร์ด (Space/R/F1) ยังเข้าเกม
            EditorApplication.delayCall += () =>
            {
                if (FindPopup() == null) return;
                ShowSceneTab();
                view.Focus();
            };
#endif
        }

        /// <summary>เอาแท็บ Scene ขึ้นมาด้านหน้าใน Editor หลัก (ถ้าเปิดไว้)</summary>
        static void ShowSceneTab()
        {
            var scene = SceneView.lastActiveSceneView;
            if (scene == null && SceneView.sceneViews.Count > 0) scene = (SceneView)SceneView.sceneViews[0];
            if (scene != null) scene.ShowTab();
        }

        static void Close()
        {
            var popup = FindPopup();
            if (popup != null) popup.Close();
            SessionState.EraseInt(PopupIdKey);
        }

        static EditorWindow FindPopup()
        {
            int id = SessionState.GetInt(PopupIdKey, 0);
            return id == 0 ? null : EditorUtility.InstanceIDToObject(id) as EditorWindow;
        }

        static bool TrySet(object target, string property, object value)
        {
            try
            {
                var p = target.GetType().GetProperty(property, Any);
                if (p == null || !p.CanWrite) { Debug.LogWarning($"[FullscreenGameView] ตั้ง {property} ไม่ได้"); return false; }
                p.SetValue(target, value);
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"[FullscreenGameView] ตั้ง {property} ไม่ได้: {e.Message}"); return false; }
        }

#if UNITY_EDITOR_WIN
        // ------------------------------------------------------------------ Win32: หาจอและหน้าต่าง
        /// <summary>จอแรกที่ไม่ใช่จอที่หน้าต่างหลักของ Unity อยู่ (พิกเซลจริง). false = มีจอเดียว</summary>
        static bool FindOtherMonitor(out RECT rect)
        {
            var main = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            var mainMonitor = main != IntPtr.Zero ? MonitorFromWindow(main, MONITOR_DEFAULTTOPRIMARY) : IntPtr.Zero;

            var monitors = new List<(IntPtr handle, MONITORINFO info)>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr dc, ref RECT r, IntPtr data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(m, ref info)) monitors.Add((m, info));
                return true;
            }, IntPtr.Zero);

            // ไม่รู้ว่า Unity อยู่จอไหน -> ถือว่าอยู่จอหลักของ Windows
            if (mainMonitor == IntPtr.Zero)
                mainMonitor = monitors.FirstOrDefault(m => (m.info.dwFlags & MONITORINFOF_PRIMARY) != 0).handle;

            foreach (var m in monitors)
                if (m.handle != mainMonitor) { rect = m.info.rcMonitor; return true; }
            rect = default;
            return false;
        }

        static List<IntPtr> ThreadWindows()
        {
            var list = new List<IntPtr>();
            EnumThreadWindows(GetCurrentThreadId(), (h, _) => { if (IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_SHOWWINDOW = 0x0040, MONITOR_DEFAULTTOPRIMARY = 1, MONITORINFOF_PRIMARY = 1;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

        delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);
        delegate bool WindowEnumProc(IntPtr hwnd, IntPtr data);

        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, WindowEnumProc callback, IntPtr data);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
#endif
    }
}
