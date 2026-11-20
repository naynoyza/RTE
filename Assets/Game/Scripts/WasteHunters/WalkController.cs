// =====================================================================================================
// WalkController — ความเร็วเดินของผู้เล่น 1 คน
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : เดินไปข้างหน้าอัตโนมัติที่ BaseSpeed และเร่งได้ถึง MaxSpeed เมื่อย่ำเท้า
//           นับก้าวใน 2 วินาทีล่าสุด -> ก้าว/วินาที -> ส่วนเร่ง 0..1
// ดึงข้อมูล: TrackedPlayer.steps (จำนวนก้าวสะสมจากกล้อง)
//           ไม่มีกล้อง: กดสลับปุ่ม Z/X (P1) หรือ N/M (P2) แทนการย่ำ
// ผลลัพธ์ : Speed (เมตร/วินาที), Boost (0..1), Walked (ระยะที่เดินไปแล้ว), Bob (การโยกขึ้นลงของมือ)
// =====================================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTE
{
    public class WalkController
    {
        readonly GameSettings settings;
        readonly int slot;
        readonly List<float> stepTimes = new List<float>();
        int lastTrackerSteps = -1, lastKey;
        float phase;

        public float Speed { get; private set; }
        public float Boost { get; private set; }
        public float Walked { get; private set; }
        public float Bob => Mathf.Sin(phase * Mathf.PI) * 18f * Mathf.Clamp01(Speed / 2f);   // พิกเซล

        public WalkController(GameSettings settings, int slot) { this.settings = settings; this.slot = slot; }

        public void Reset() { stepTimes.Clear(); Speed = Walked = 0; }

        // ทุกเฟรม: นับก้าวใหม่ -> คำนวณความเร็ว -> สะสมระยะทาง (moving = false ความเร็วค่อย ๆ ลดเป็น 0)
        public float Tick(TrackedPlayer p, bool cameraOnline, bool moving, float dt)
        {
            CountSteps(p, cameraOnline);
            float stepRate = stepTimes.Count / 2f;
            Boost = Mathf.Clamp01(stepRate / settings.FullStepRate);
            float target = moving ? settings.BaseSpeed + (settings.MaxSpeed - settings.BaseSpeed) * Boost : 0;
            Speed = Mathf.Lerp(Speed, target, dt * (target > Speed ? 3f : 2f));
            float meters = Speed * dt;
            Walked += meters;
            phase += meters * 2.4f;
            return meters;
        }

        // ก้าวใหม่ = ตัวนับจากกล้องเพิ่มขึ้น หรือกดปุ่มสลับซ้าย-ขวา; เก็บเวลาไว้ 2 วินาทีล่าสุด
        void CountSteps(TrackedPlayer p, bool cameraOnline)
        {
            float now = Time.time;
            if (cameraOnline && p.present)
            {
                if (lastTrackerSteps >= 0)
                    for (int i = 0; i < Mathf.Min(4, p.steps - lastTrackerSteps); i++) stepTimes.Add(now);
                lastTrackerSteps = p.steps;
            }
            else
            {
                lastTrackerSteps = -1;
                var k = Keyboard.current;
                if (k != null)
                {
                    if ((slot == 1 ? k.zKey : k.nKey).wasPressedThisFrame && lastKey != 1) { stepTimes.Add(now); lastKey = 1; }
                    if ((slot == 1 ? k.xKey : k.mKey).wasPressedThisFrame && lastKey != 2) { stepTimes.Add(now); lastKey = 2; }
                }
            }
            stepTimes.RemoveAll(t => now - t > 2f);
        }
    }
}
