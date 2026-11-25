// =====================================================================================================
// GameSettings — ค่าปรับเกมทั้งหมด (แก้ได้ใน Inspector ของ Game > WasteHunters)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : รวมตัวเลขที่ใช้ตัดสินความยาก/ความเร็วของเกมไว้ที่เดียว และสีประจำตัวผู้เล่น
// ใช้โดย  : WasteHunters (เวลา/เงื่อนไขจบ),
//           PlayerLane (รัศมีเล็ง/ระยะดูด/ขนาดขยะ), WalkController (ความเร็ว)
// =====================================================================================================
using System;
using UnityEngine;

namespace RTE
{
    [Serializable]
    public class GameSettings
    {
        [Header("รอบเกม")]
        public float RoundSeconds = 90f;    // เวลาต่อรอบ (วินาที) — เล่นจนหมดเวลาเสมอ ไม่มีเป้าจำนวนชิ้น
        public float ResultSeconds = 8f;    // หน้าสรุปผลค้างกี่วินาทีก่อนไปเกม 2

        [Header("ช่วงเร่งท้ายเกม (FINAL RUSH)")]
        public float RushSeconds = 20f;     // กี่วินาทีสุดท้ายของรอบ
        [Range(.2f, 1f)]
        public float RushSpawnScale = .55f; // ระยะเดินระหว่างขยะแต่ละชิ้น x ค่านี้ (.55 = ขยะถี่ขึ้น ~1.8 เท่า)
        public int RushScoreMultiplier = 2; // คะแนนคูณ (ไม่ซ้อนกับ FEVER — ใช้ค่าที่มากกว่า)

        [Header("อีเวนต์ (วินาทีที่เล่นไปแล้วในรอบ)")]
        public float[] FeverAt = { 20f, 60f }; // DAILY DUMP FEVER เริ่มที่วินาทีเหล่านี้
        public float FeverSeconds = 6f;     // FEVER นานกี่วินาที
        public float DustAt = 40f;          // DUST STORM เริ่มที่วินาทีนี้

        [Header("หน้าจุดยืน")]
        public float StandbyHold = 2f;      // ผู้เล่นครบ 2 คนต้องยืนค้างกี่วินาทีจึงเริ่ม
        public float StandbyEmpty = 8f;     // ไม่มีใครเลยกี่วินาทีจึงกลับหน้าปก

        [Header("การเดิน (เมตร/วินาที)")]
        public float BaseSpeed = 1.4f;      // เดินอัตโนมัติเมื่อไม่ย่ำเท้า
        public float MaxSpeed = 4.5f;       // เร็วสุดเมื่อย่ำเท้าเต็มที่
        public float FullStepRate = 2.4f;   // ย่ำกี่ก้าว/วินาทีจึงได้ความเร็วสูงสุด

        [Header("การเล็ง")]
        public float HitRadiusPx = 58f;     // รัศมีดูด (พิกเซล) = ขนาดวงเป้าเล็งสีฟ้า; ขยะต้องอยู่ในวงนี้จึงดูดได้
        [Range(.1f, 1f)]
        public float SuckRange = .25f;      // ดูดได้เมื่อขยะเข้ามาใกล้กว่า (ค่านี้ x ระยะเกิด 18 ม.) — .25 = 4.5 ม. ≈ ครึ่งล่างของจอ
        public float OverdriveAimScale = 1.6f; // OVERDRIVE: วงเล็ง + รัศมีดูดใหญ่ขึ้นกี่เท่า (เดิม 2.4 ใหญ่เกินไป)

        [Header("ขยะ")]
        public float TrashScale = 1.3f;     // ขนาดขยะบนจอ (1 = เดิม) — ใหญ่ปานกลาง
        public float HardSlowdown = .45f;   // ขยะที่เล็งยาก (ชิ้นเล็ก/อยู่ริมถนน/ขยะพิเศษ) เคลื่อนช้าลงสูงสุดกี่ % = อยู่บนจอนานขึ้น

        public static readonly Color[] PlayerColors = { new Color(.25f, .62f, 1f), new Color(1f, .55f, .12f) }; // P1 ฟ้า, P2 ส้ม
    }
}
