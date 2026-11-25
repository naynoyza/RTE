// =====================================================================================================
// AudioListenerGuard — กันเกมเงียบ: ถ้าฉากไม่มี AudioListener (ฉากที่สร้างด้วยสคริปต์ไม่มี) จะสร้างให้เองอัตโนมัติ
// -----------------------------------------------------------------------------------------------------
// ไม่ต้องวางอะไรในฉาก ทำงานเองทุกครั้งที่โหลดฉาก
// ถ้าฉากมี AudioListener ของตัวเองอยู่แล้ว ตัวที่สร้างไว้จะปิดตัวเอง (กัน warning "2 audio listeners")
// เสียงทั้งหมดในเกมเป็นเสียง 2D (Sfx/Bgm) ตำแหน่งของ listener จึงไม่มีผล
// =====================================================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RTE
{
    public static class AudioListenerGuard
    {
        static AudioListener own;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            own = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        /// <summary>ให้มี AudioListener ที่เปิดอยู่ 1 ตัวพอดี. ตัวอย่าง: AudioListenerGuard.Ensure();</summary>
        public static void Ensure()
        {
            int others = 0;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l != own && l.enabled && l.gameObject.activeInHierarchy) others++;

            if (others > 0) { if (own != null) own.enabled = false; return; }
            if (own == null)
            {
                var go = new GameObject("AudioListener (auto)");
                Object.DontDestroyOnLoad(go);
                own = go.AddComponent<AudioListener>();
            }
            own.enabled = true;
        }
    }
}
