// =====================================================================================================
// GameSession — ข้อมูลที่ต้องจำข้ามหน้าจอ/ข้ามฉาก (ตัวละครที่ผู้เล่นแต่ละคนเลือก)
// =====================================================================================================
namespace RTE
{
    public enum Hero { None = -1, Male = 0, Female = 1 }

    public static class GameSession
    {
        public static readonly Hero[] Heroes = { Hero.None, Hero.None };   // [0] = P1, [1] = P2
        public static bool Active;   // true = กำลังเล่นชุดภารกิจ (เริ่มที่หน้าเลือกตัวละคร จบหลังเกม 2)

        public const int CoverScene = 0;               // WasteHunters.unity (หน้าปก + เกม 1)
        public const string Game2Scene = "The Last Seed";

        // กลับหน้าปก (วิดีโอแรก) จากฉากไหนก็ได้
        public static void BackToCover() { Clear(); Active = false; UnityEngine.SceneManagement.SceneManager.LoadScene(CoverScene); }
        public static void Clear() { Heroes[0] = Heroes[1] = Hero.None; }
    }
}
