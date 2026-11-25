// =====================================================================================================
// Bgm — เพลงพื้นหลังวนซ้ำ จาก Resources/Audio/<ชื่อ> เปลี่ยนเพลงแบบค่อย ๆ จางข้าม (crossfade) และเล่นต่อข้ามฉากได้
// -----------------------------------------------------------------------------------------------------
// ตัวอย่าง: Bgm.Play("bgm_menu");   Bgm.Play("bgm_game1", .5f);   Bgm.Stop();
// เพลง: bgm_menu (หน้าแรก/เลือกตัวละคร) | bgm_game1 (Waste Hunters) | bgm_game2 (The Last Seed) | bgm_finale (ฉากจบเกม 2)
// =====================================================================================================
using UnityEngine;

namespace RTE
{
    public class Bgm : MonoBehaviour
    {
        /// <summary>ความดังรวมของเพลงพื้นหลัง (0..1) — เบากว่าเสียงเอฟเฟค</summary>
        public static float Volume = .45f;
        const float FadeSeconds = 1.2f;

        static Bgm I;
        AudioSource a, b;            // a = เพลงที่กำลังดัง, b = เพลงที่กำลังจางออก
        string current;
        float target = 1f;

        /// <summary>เล่นเพลง name วนซ้ำ (ถ้าเล่นอยู่แล้วไม่เริ่มใหม่). volume = ความดังของเพลงนี้</summary>
        public static void Play(string name, float volume = 1f)
        {
            if (!Application.isPlaying) return;
            if (I == null)
            {
                var go = new GameObject("Bgm");
                DontDestroyOnLoad(go);
                I = go.AddComponent<Bgm>();
                I.a = I.Make(); I.b = I.Make();
            }
            I.target = volume;
            if (I.current == name) return;
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) return;
            I.current = name;
            (I.a, I.b) = (I.b, I.a);           // เพลงเดิมย้ายไปจางออก
            I.a.clip = clip; I.a.volume = 0; I.a.Play();
        }

        /// <summary>ค่อย ๆ หยุดเพลง</summary>
        public static void Stop()
        {
            if (I == null) return;
            I.current = null;
            (I.a, I.b) = (I.b, I.a);
            I.a.Stop();
        }

        AudioSource Make()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0;
            return s;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / FadeSeconds;
            if (a.isPlaying) a.volume = Mathf.MoveTowards(a.volume, target * Volume, step * Volume);
            b.volume = Mathf.MoveTowards(b.volume, 0, step * Volume);
            if (b.isPlaying && b.volume <= 0) b.Stop();
        }
    }
}
