// =====================================================================================================
// TrashPiece — ขยะ 1 ชิ้น (หรือถังรีไซเคิล) บนถนน
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : เก็บตำแหน่งในโลก 3 มิติ (ลึก z เมตรข้างหน้า, ซ้าย-ขวา lat เมตร) แล้วแปลงเป็นตำแหน่ง/ขนาดบนจอ
//           แบบมุมมองจริง (ไกล = เล็กและอยู่ใกล้ปลายถนน, ใกล้ = ใหญ่และต่ำลง)
//           มีแสงรอบตัว: ฟ้า = ขยะธรรมดา, ทองกะพริบ = ขยะพิเศษ, เขียว = ถังรีไซเคิล
// ดึงข้อมูล: รูปขยะจาก TrashCatalog / รูปถัง Resources/Sprites/bin.png
// ใช้โดย  : PlayerLane (สร้าง, เลื่อน, เล็ง, ดูด)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class TrashPiece
    {
        // ค่ามุมมองของจอผู้เล่น (พิกเซลจากกึ่งกลางจอครึ่ง)
        public const float HorizonY = 60f;   // ความสูงของปลายถนน
        const float GroundScale = 820f;      // y บนจอ = HorizonY - GroundScale / z
        const float SideScale = 620f;        // x บนจอ = lat * SideScale / z
        public const float SpawnZ = 18f;     // เกิดที่ระยะ 18 เมตร

        public readonly bool IsBin, IsRare;
        public readonly TrashKind Kind;
        public float Z, Lat;                 // ตำแหน่งในโลก
        public float SuckProgress = -1;      // -1 = ยังไม่ถูกดูด, 0..1 = กำลังลอยเข้าปากปืน
        public float Pace = 1f;              // ความเร็วที่เข้าหาผู้เล่น (ชิ้นที่เล็งยาก < 1 = อยู่บนจอนานกว่า)
        public float RareLife = 10f;         // ขยะพิเศษอยู่ได้ไม่นาน
        public Vector2 ScreenPos; public float ScreenSize;

        public readonly RectTransform Rect;
        readonly Image image, glow;

        readonly float scale;

        // ความยากในการเล็ง 0..1: ชิ้นเล็ก + อยู่ริมถนน + ขยะพิเศษ
        public float Hardness => IsBin ? 0 :
            Mathf.Clamp01((1.2f - Kind.Size) / .4f * .5f + Mathf.Abs(Lat) / 1.3f * .4f + (IsRare ? .4f : 0));

        public TrashPiece(Transform parent, bool isBin, float lat, float scale, float hardSlowdown)
        {
            this.scale = scale;
            IsBin = isBin; Lat = lat; Z = SpawnZ;
            if (isBin)
            {
                image = UIKit.Img("RecycleBin", parent, UIKit.Load("bin"), new Vector2(100, 100));
                image.color = new Color(.7f, 1f, .7f);
            }
            else
            {
                Kind = TrashCatalog.All[Random.Range(0, TrashCatalog.All.Length)];
                IsRare = Random.value < TrashCatalog.RareChance;
                if (IsRare) Z = SpawnZ * .6f;   // ขยะพิเศษโผล่ใกล้กว่า แต่หายเร็ว
                image = UIKit.Img(Kind.Sprite, parent, UIKit.Load("wh/" + Kind.Sprite), new Vector2(100, 100));
            }
            Rect = image.rectTransform;

            glow = UIKit.Img("Glow", Rect, UIKit.Load("fx_glow"), Vector2.zero, false);
            UIKit.Stretch(glow.rectTransform);
            glow.rectTransform.sizeDelta = new Vector2(80, 80);
            glow.transform.SetAsFirstSibling();
            glow.color = isBin ? new Color(.3f, 1f, .4f) : IsRare ? new Color(1f, .9f, .3f) : new Color(.35f, .7f, 1f);
            Pace = 1f - hardSlowdown * Hardness;
            Place();
        }

        public int BaseScore => Kind.Score;
        public int Points => Kind.Score + (IsRare ? TrashCatalog.RareBonus : 0);

        // แปลงตำแหน่งโลก -> จอ และอัปเดตความทึบ/แสง/การโยก
        public void Place()
        {
            float z = Mathf.Max(.5f, Z);
            ScreenSize = Mathf.Max(IsBin ? 0 : 60f, (IsBin ? 600f : 400f * Kind.Size * scale) / z);   // ไกลแค่ไหนก็ไม่เล็กกว่า 60 px
            ScreenPos = new Vector2(Lat * SideScale / z, HorizonY - GroundScale / z + ScreenSize * .45f);
            Rect.sizeDelta = Vector2.one * ScreenSize;
            Rect.anchoredPosition = ScreenPos;

            float fadeIn = Mathf.Clamp01((SpawnZ - Z) / 3f);                   // ค่อย ๆ ปรากฏจากระยะไกล
            image.color = new Color(image.color.r, image.color.g, image.color.b, fadeIn);
            float glowA = IsRare ? .6f + .4f * Mathf.Sin(Time.time * 10) : .55f;
            glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, glowA * fadeIn);
            float wobble = IsBin ? 0 : Mathf.Sin(Time.time * 2 + Lat * 5) * 8;
            Rect.localRotation = Quaternion.Euler(0, 0, wobble);
        }

        // แอนิเมชันถูกดูด: หมุน หดเล็ก ลอยเข้าปากปืน; คืนค่า true เมื่อถึงปากปืนแล้ว
        public bool SuckToward(Vector2 nozzle, float dt, float duration)
        {
            SuckProgress += dt / duration;
            float e = SuckProgress * SuckProgress;
            Rect.anchoredPosition = Vector2.Lerp(ScreenPos, nozzle, e);
            Rect.sizeDelta = Vector2.one * Mathf.Lerp(ScreenSize, ScreenSize * .15f, e);
            Rect.localRotation = Quaternion.Euler(0, 0, SuckProgress * 540);
            return SuckProgress >= 1;
        }

        public void Destroy() => Object.Destroy(Rect.gameObject);
    }
}
