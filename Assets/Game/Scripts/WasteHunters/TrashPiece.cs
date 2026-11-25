// =====================================================================================================
// TrashPiece — ขยะ 1 ชิ้นบนถนน
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : เก็บตำแหน่งในโลก 3 มิติ (ลึก z เมตรข้างหน้า, ซ้าย-ขวา lat เมตร) แล้วแปลงเป็นตำแหน่ง/ขนาดบนจอ
//           แบบมุมมองจริง (ไกล = เล็กและอยู่ใกล้ปลายถนน, ใกล้ = ใหญ่และต่ำลง)
//           มีแสงรอบตัว: ฟ้า = ขยะธรรมดา, ทองกะพริบ = ขยะพิเศษ; ยังไม่เข้าระยะดูด = แสงจาง
//           Danger = แผงวงจรช็อตไฟ (แดง/ม่วงกระตุก) ห้ามดูด, Drop = ร่วงลงมาจากฟ้า (อีเวนต์ Dump Fever)
// ดึงข้อมูล: รูปขยะจาก TrashCatalog
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

        public readonly bool IsRare;
        public readonly bool Danger;         // ขยะช็อตไฟ: ดูดแล้วโดนช็อต (STUNNED)
        public float Drop;                   // 1 = ยังอยู่บนฟ้า -> 0 = ตกถึงพื้น
        public bool InRange;                 // เข้ามาในระยะดูดแล้ว (PlayerLane ตั้งให้ทุกเฟรม)
        public readonly TrashKind Kind;
        public float Z, Lat;                 // ตำแหน่งในโลก
        public float SuckProgress = -1;      // -1 = ยังไม่ถูกดูด, 0..1 = กำลังลอยเข้าปากปืน
        public float Pace = 1f;              // ความเร็วที่เข้าหาผู้เล่น (ชิ้นที่เล็งยาก < 1 = อยู่บนจอนานกว่า)
        public float RareLife = 14f;         // ขยะพิเศษอยู่ได้ไม่นาน
        public Vector2 ScreenPos; public float ScreenSize;

        public readonly RectTransform Rect;
        readonly Image image, glow;
        Image spark, warnImg;

        readonly float scale;

        // ความยากในการเล็ง 0..1: ชิ้นเล็ก + อยู่ริมถนน + ขยะพิเศษ
        public float Hardness =>
            Mathf.Clamp01((1.2f - Kind.Size) / .4f * .5f + Mathf.Abs(Lat) / 1.3f * .4f + (IsRare ? .4f : 0));

        public TrashPiece(Transform parent, float lat, float scale, float hardSlowdown, bool danger = false)
        {
            this.scale = scale;
            Lat = lat; Z = SpawnZ; Danger = danger;
            Kind = danger ? TrashCatalog.DangerKind : TrashCatalog.All[Random.Range(0, TrashCatalog.All.Length)];
            IsRare = !danger && Random.value < TrashCatalog.RareChance;
            if (IsRare) Z = SpawnZ * .6f;   // ขยะพิเศษโผล่ใกล้กว่า แต่หายเร็ว
            image = UIKit.Img(Kind.Sprite, parent, UIKit.Load("wh/" + Kind.Sprite), new Vector2(100, 100));
            Rect = image.rectTransform;

            glow = UIKit.Img("Glow", Rect, UIKit.Load("fx_glow"), Vector2.zero, false);
            UIKit.Stretch(glow.rectTransform);
            glow.rectTransform.sizeDelta = new Vector2(80, 80);
            glow.transform.SetAsFirstSibling();
            glow.color = Danger ? new Color(1f, .15f, .45f) : IsRare ? new Color(1f, .9f, .3f) : new Color(.35f, .7f, 1f);
            if (Danger)
            {
                spark = UIKit.Img("Spark", Rect, UIKit.Load("fx/lightning"), Vector2.zero, false);
                UIKit.Stretch(spark.rectTransform); spark.rectTransform.sizeDelta = new Vector2(90, 90);
                warnImg = UIKit.Img("Danger", Rect, UIKit.Load("ui2/danger_tag"), new Vector2(190, 92));   // ป้าย DANGER ห้ามดูด
                warnImg.rectTransform.anchorMin = warnImg.rectTransform.anchorMax = new Vector2(.5f, 1);
                warnImg.rectTransform.anchoredPosition = new Vector2(0, 55);
            }
            Pace = 1f - hardSlowdown * Hardness;
            Place();
        }

        public int BaseScore => Kind.Score;
        public int Points => Kind.Score + (IsRare ? TrashCatalog.RareBonus : 0);

        // แปลงตำแหน่งโลก -> จอ และอัปเดตความทึบ/แสง/การโยก
        public void Place()
        {
            float z = Mathf.Max(.5f, Z);
            ScreenSize = Mathf.Max(60f, 400f * Kind.Size * scale / z);   // ไกลแค่ไหนก็ไม่เล็กกว่า 60 px
            ScreenPos = new Vector2(Lat * SideScale / z, HorizonY - GroundScale / z + ScreenSize * .45f);
            Rect.sizeDelta = Vector2.one * ScreenSize;
            if (Drop > 0) { Drop = Mathf.Max(0, Drop - Time.deltaTime * 1.6f); ScreenPos.y += Drop * Drop * 700f; }
            Rect.anchoredPosition = ScreenPos;

            float fadeIn = Mathf.Clamp01((SpawnZ - Z) / 3f);                   // ค่อย ๆ ปรากฏจากระยะไกล
            image.color = new Color(image.color.r, image.color.g, image.color.b, fadeIn);
            if (Danger) DangerFx(fadeIn);
            float glowA = Danger ? (Random.value < .5f ? 1f : .5f) : IsRare ? .6f + .4f * Mathf.Sin(Time.time * 10) : .55f;
            if (!InRange) glowA *= .25f;                                       // ยังไกลเกินดูด = แสงจาง
            glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, glowA * fadeIn);
            float wobble = Mathf.Sin(Time.time * 2 + Lat * 5) * 8;
            Rect.localRotation = Quaternion.Euler(0, 0, wobble);
        }

        // ไฟช็อตกระตุกรอบแผงวงจร (สุ่มทุกเฟรม = ดูเหมือนประกายไฟ)
        void DangerFx(float a)
        {
            image.color = new Color(1f, .55f + .45f * Random.value, .7f, a);
            spark.color = new Color(1, 1, 1, a * (Random.value < .7f ? 1f : .35f));
            spark.rectTransform.localScale = new Vector3(Random.value < .5f ? 1 : -1, 1, 1) * (1f + .3f * Random.value);
            spark.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.value * 360);
            warnImg.color = new Color(1, 1, 1, a * (Mathf.Sin(Time.time * 12) > 0 ? 1 : .55f));
            warnImg.transform.rotation = Quaternion.identity;
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
