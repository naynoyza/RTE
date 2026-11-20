// =====================================================================================================
// SeedSettings — ค่าปรับเกมภารกิจที่ 2 THE LAST SEED (แก้ได้ใน Inspector ของ Game > LastSeed)
// -----------------------------------------------------------------------------------------------------
// หัวใจของเกม: ผู้เล่น 2 คนชาร์จพลัง (ชูแขนตัว V) แล้วส่งพลัง (ผลักฝ่ามือ) ให้ต้นไม้
//   • ส่งพร้อมกันทั้งคู่   = ต้นไม้โตเต็มอัตรา (SyncMultiplier)
//   • ส่งอยู่คนเดียว      = ต้นไม้โตช้าลง (SoloMultiplier)  <- "ทำท่าไม่พร้อมกันจะโตช้าลง"
//   • เริ่มส่งห่างกันไม่เกิน PerfectSyncWindow วินาที = PERFECT SYNC ได้โบนัสทันที
//   • น้ำกับแสงที่ส่งไปรวมกันต้องพอ ๆ กัน (Nature Balance) ถ้าเอียงไปฝั่งเดียว ต้นไม้โตช้าลงอีก
// =====================================================================================================
using System;
using UnityEngine;

namespace RTE
{
    [Serializable]
    public class SeedSettings
    {
        [Header("รอบเกม")]
        public float RoundSeconds = 120f;       // เวลาต่อรอบ
        public float StandbyHold = 2f;          // ครบ 2 คนค้างกี่วินาทีจึงเริ่ม
        public float StandbyEmpty = 8f;         // (ใช้กับหน้าจุดยืน) ไม่มีใครกี่วินาที
        public float ResultSeconds = 12f;       // หน้าสรุปผลค้างกี่วินาที

        [Header("เกจพลัง (0..1)")]
        public float ChargePerSecond = .35f;    // ชูแขนค้าง ~3 วินาที = เต็ม
        public float SendPerSecond = .5f;       // ผลักค้าง ~2 วินาที = หมด
        public float StreamTravelSeconds = .35f;// น้ำ/แสงใช้เวลาวิ่งจากมือถึงต้นไม้ (ยังไม่นับจนกว่าจะถึง)

        [Header("การเติบโต")]
        public float GrowthPerUnit = .035f;     // พลัง 1 หน่วยที่ถึงต้นไม้ = โตกี่ส่วน (1 = 100%)
        public float SyncMultiplier = 1f;       // ส่งพร้อมกันทั้งคู่
        [Range(0, 1)] public float SoloMultiplier = .35f;   // ส่งคนเดียว (ไม่พร้อมกัน) -> โตช้าลง
        public float PerfectSyncWindow = .6f;   // เริ่มส่งห่างกันไม่เกินกี่วินาที = PERFECT SYNC
        public float PerfectSyncBonus = .02f;   // โบนัสการเติบโตทันที
        public float BalanceMinMultiplier = .6f;   // น้ำ/แสงไม่สมดุลสุด ๆ
        public float BalanceMaxMultiplier = 1.25f; // สมดุลพอดี (โบนัส)
        [Range(0, 1)] public float BalancedRatio = .8f; // น้อย/มาก ≥ ค่านี้ = ถือว่า "สมดุล"

        [Header("ตรวจท่า (จากจุดกระดูก YOLO 17 จุด)")]
        public float ChargeSpread = 1.0f;       // ท่า V: ข้อมือห่างกัน > ความกว้างไหล่ x ค่านี้ และสูงกว่าจมูก
        public float PushElbowAngle = 150f;     // ท่าผลัก: มุมศอก > ค่านี้ (แขนเหยียด)
        public float PushForeshorten = .9f;     // หรือ แขนชี้เข้ากล้อง (ไหล่-ข้อมือสั้นกว่าไหล่กว้าง x ค่านี้)
        public float PoseEnterSeconds = .12f;   // Hysteresis: ท่าต้องค้างกี่วินาทีจึงนับว่าเริ่ม
        public float PoseExitSeconds = .3f;     // และหายไปกี่วินาทีจึงนับว่าเลิก (กันท่ากะพริบ)

        public static readonly Color Water = new Color(.35f, .78f, 1f);
        public static readonly Color Sun = new Color(1f, .78f, .25f);
    }
}
