// =====================================================================================================
// TrashCatalog — รายการขยะทั้งหมดในเกม
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : บอกขยะแต่ละชนิดว่าใช้รูปไหน ชื่อไทยอะไร ได้กี่คะแนน และใหญ่แค่ไหนบนจอ
//           คะแนนตามความอันตรายต่อสิ่งแวดล้อม: น้อย 10, ปานกลาง 20, สูง 35, วิกฤต 50
//           ขยะพิเศษ (เรืองแสงทอง) ได้โบนัสเพิ่ม RareBonus, เก็บต่อเนื่องได้โบนัส Combo
// ดึงข้อมูล: รูปจาก Resources/Sprites/wh/<sprite>.png
// ใช้โดย  : PlayerLane (สุ่มเกิดขยะ, คิดคะแนน)
// =====================================================================================================
namespace RTE
{
    public struct TrashKind
    {
        public string Sprite;   // ชื่อรูปในโฟลเดอร์ wh
        public string Name;     // ชื่อที่แสดงตอนเก็บได้
        public int Score;       // คะแนนพื้นฐาน
        public float Size;      // ตัวคูณขนาดบนจอ
    }

    public static class TrashCatalog
    {
        public static readonly TrashKind[] All =
        {
            new TrashKind { Sprite = "t_tube",    Name = "สายไฟ/หลอดไฟเก่า", Score = 10, Size = .9f },
            new TrashKind { Sprite = "t_phone",   Name = "โทรศัพท์เก่า",      Score = 20, Size = .8f },
            new TrashKind { Sprite = "t_board",   Name = "แผงวงจร",          Score = 35, Size = .9f },
            new TrashKind { Sprite = "t_laptop",  Name = "คอมพิวเตอร์เก่า",   Score = 35, Size = 1.1f },
            new TrashKind { Sprite = "t_tv",      Name = "ทีวีเก่า",          Score = 35, Size = 1.2f },
            new TrashKind { Sprite = "t_aircon",  Name = "แอร์เก่า",          Score = 35, Size = 1.2f },
            new TrashKind { Sprite = "t_battery", Name = "แบตเตอรี่เก่า",     Score = 50, Size = .8f },
        };

        public const int RareBonus = 30;      // ขยะพิเศษเรืองแสง
        public const float RareChance = .1f;  // โอกาสเกิดขยะพิเศษ 10%

        // โบนัสเมื่อเก็บต่อเนื่องครบ 3 / 5 / 8 ชิ้น (ได้ครั้งเดียวตอนถึงจำนวนนั้น)
        public static int ComboBonus(int combo) => combo == 3 ? 10 : combo == 5 ? 20 : combo == 8 ? 40 : 0;
    }
}
