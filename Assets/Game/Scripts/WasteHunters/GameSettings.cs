// =====================================================================================================
// GameSettings — ค่าปรับเกมทั้งหมด (แก้ได้ใน Inspector ของ Game > WasteHunters)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : รวมตัวเลขที่ใช้ตัดสินความยาก/ความเร็วของเกมไว้ที่เดียว และสีประจำตัวผู้เล่น
// ใช้โดย  : WasteHunters (เวลา/เงื่อนไขจบ), StandbyScreen (เวลารอครบ 2 คน),
//           PlayerLane (ถุง/เป้าหมาย/รัศมีเล็ง/ขนาดขยะ), WalkController (ความเร็ว)
// =====================================================================================================
using System;
using UnityEngine;

namespace RTE
{
    [Serializable]
    public class GameSettings
    {
        [Header("รอบเกม")]
        public float RoundSeconds = 150f;   // เวลาต่อรอบ (วินาที)
        public int Goal = 20;               // รีไซเคิลครบเท่านี้ชิ้น = จบเกมทันที
        public int BagSize = 5;             // ถุงจุได้กี่ชิ้นก่อนต้องเท

        [Header("หน้าจุดยืน")]
        public float StandbyHold = 2f;      // ผู้เล่นครบ 2 คนต้องยืนค้างกี่วินาทีจึงเริ่ม
        public float StandbyEmpty = 8f;     // ไม่มีใครเลยกี่วินาทีจึงกลับหน้าปก

        [Header("การเดิน (เมตร/วินาที)")]
        public float BaseSpeed = 1.4f;      // เดินอัตโนมัติเมื่อไม่ย่ำเท้า
        public float MaxSpeed = 4.5f;       // เร็วสุดเมื่อย่ำเท้าเต็มที่
        public float FullStepRate = 2.4f;   // ย่ำกี่ก้าว/วินาทีจึงได้ความเร็วสูงสุด

        [Header("การเล็ง")]
        public float HitRadius = .6f;       // เป้าเล็งแตะขยะ = ดูดทันที (รัศมีโดน = ขนาดขยะบนจอ x ค่านี้)
        public float MinHitPx = 70f;        // รัศมีโดนอย่างน้อยกี่พิกเซล (ขยะไกลที่ยังเล็กก็ยังเล็งโดน)

        [Header("ขยะ")]
        public float TrashScale = 1.3f;     // ขนาดขยะบนจอ (1 = เดิม) — ใหญ่ปานกลาง
        public float HardSlowdown = .45f;   // ขยะที่เล็งยาก (ชิ้นเล็ก/อยู่ริมถนน/ขยะพิเศษ) เคลื่อนช้าลงสูงสุดกี่ % = อยู่บนจอนานขึ้น

        public static readonly Color[] PlayerColors = { new Color(.25f, .62f, 1f), new Color(1f, .55f, .12f) }; // P1 ฟ้า, P2 ส้ม
    }
}
