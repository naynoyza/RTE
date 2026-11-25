// =====================================================================================================
// Sfx — เล่นเสียงเอฟเฟคสั้น ๆ จาก Resources/Audio/<ชื่อ> (ไม่ต้องวางอะไรในฉาก สร้างตัวเล่นเสียงเองครั้งแรกที่เรียก)
// -----------------------------------------------------------------------------------------------------
// ตัวอย่าง: Sfx.Play("collect");  Sfx.Play("tick", .5f);  Sfx.Play("send", .7f, 1.1f);
// ชื่อเสียง: tick lock ready teleport warp flash count_beep count_go collect rare stun overdrive fever dust rush danger
//            result sync stage finale send charged join alert step
//            (ไฟล์ต้นฉบับอยู่ใน "Sound effect/" ที่ root ของ repo; join/alert/step มาจาก "Audio Interface")
// =====================================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RTE
{
    public static class Sfx
    {
        /// <summary>ความดังรวมของเสียงเอฟเฟคทั้งหมด (0..1)</summary>
        public static float Volume = .8f;

        const int Voices = 10;                 // เล่นซ้อนกันได้สูงสุดกี่เสียง
        const float MinGap = .05f;             // เสียงเดียวกันซ้ำถี่กว่านี้ = ข้าม (กันเสียงแตก)
        static AudioSource[] voices;
        static int next;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();

        /// <summary>เล่นเสียง name ครั้งเดียว. ตัวอย่าง: Sfx.Play("lock");</summary>
        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying) return;
            if (lastPlay.TryGetValue(name, out var t) && Time.unscaledTime - t < MinGap) return;
            if (!clips.TryGetValue(name, out var clip))
                clips[name] = clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) return;
            lastPlay[name] = Time.unscaledTime;
            Init();
            var src = voices[next]; next = (next + 1) % Voices;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume * Volume);
        }

        static readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();

        /// <summary>เสียงวนซ้ำที่เปิด/ปิดตามสถานะ (เรียกทุกเฟรม) — ค่อย ๆ ดัง/เบา ไม่ตัดกระตุก.
        /// key แยกตัวเล่นต่อผู้เล่น เช่น Sfx.Loop("vac1", "loop_vacuum", sucking, .5f);</summary>
        public static void Loop(string key, string name, bool on, float volume = 1f)
        {
            if (!Application.isPlaying) return;
            if (!loops.TryGetValue(key, out var src) || src == null)
            {
                if (!on) return;
                var clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null) return;
                Init();
                src = voices[0].gameObject.AddComponent<AudioSource>();
                src.clip = clip; src.loop = true; src.playOnAwake = false; src.spatialBlend = 0; src.volume = 0;
                loops[key] = src;
            }
            float want = on ? volume * Volume : 0;
            src.volume = Mathf.MoveTowards(src.volume, want, Time.unscaledDeltaTime * 2f);
            if (want > 0 && !src.isPlaying) src.Play();
            else if (want <= 0 && src.volume <= 0 && src.isPlaying) src.Pause();
        }

        static void Init()
        {
            if (voices != null && voices[0] != null) return;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = go.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
            }
        }
    }
}
