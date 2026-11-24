// =====================================================================================================
// RoadScroller — ฉากหลังเกม 1 แบบหลายชั้นซ้อนกัน (parallax) ให้รู้สึกว่ากำลังเดินลึกเข้าไปในเมืองขยะ
// -----------------------------------------------------------------------------------------------------
//   ชั้น 4 ท้องฟ้า + ดวงอาทิตย์   : นิ่ง (ไกลมาก) ลอยเบา ๆ
//   ชั้น 3 เมืองไกล + สะพานลอย    : ซูมช้ามาก
//   ชั้น 2 ตึกพัง + เสาไฟข้างถนน  : ซูมปานกลาง
//   ชั้น 1 กองขยะ + ถนน (ใกล้สุด) : ซูมเร็ว
//   ทุกชั้นซูมจาก "จุดลับตา" ของตัวเองที่วางตรงเส้นขอบฟ้าเดียวกัน -> ใกล้ = เคลื่อนเร็ว, ไกล = ช้า (ความลึก)
//   ชั้นที่เคลื่อนมีภาพ 2 ชุดสลับกัน: ชุดเก่าซูมผ่านกล้องแล้วจางหาย ชุดใหม่โผล่จากลึก ๆ ด้านหลัง = เดินได้ไม่สิ้นสุด
//   ยิ่งเดินเร็ว (ย่ำเท้า) ทุกชั้นยิ่งเคลื่อนเร็ว
// ดึงข้อมูล: Resources/Sprites/g1/layer1..4 (Tools/make_g1_layers.py จาก UI Background/Game1-1..4.png)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class RoadScroller
    {
        class Layer
        {
            public RectTransform group;
            public Image[] copies;
            public float metersPerCycle, maxZoom, fadeOut;   // fadeOut = เริ่มจางที่ phase นี้ (ชั้นไกลจางสั้น ๆ ไม่ให้เห็นภาพซ้อน)
        }

        readonly Layer sky, city, ruins, near;
        readonly Image haze;

        // ขนาดบนจอที่ซูม 1 เท่า (คำนวณให้คลุมจอแนวตั้ง 960x1080 ของผู้เล่นได้พอดี) + จุดลับตา (pivot) จาก make_g1_layers.py
        public RoadScroller(Transform parent, float horizonY)
        {
            var root = UIKit.Rect("Parallax", parent); UIKit.Stretch(root);
            root.SetAsFirstSibling();
            sky = Make(root, "Sky", "g1/layer4", new Vector2(2095, 1180), new Vector2(.68f, .48f), horizonY, 1, 0, 1f, 1f);
            city = Make(root, "City", "g1/layer3", new Vector2(1777, 1000), new Vector2(.60f, .34f), horizonY, 2, 70f, 1.12f, .88f);
            haze = UIKit.Img("Haze", root, UIKit.Load("fx_glow"), new Vector2(1500, 420), false);   // หมอกแดดที่เส้นขอบฟ้า
            haze.rectTransform.anchoredPosition = new Vector2(0, horizonY + 40);
            haze.color = new Color(1f, .7f, .45f, .28f);
            ruins = Make(root, "Ruins", "g1/layer2", new Vector2(1777, 1000), new Vector2(.60f, .18f), horizonY, 2, 18f, 1.45f, .82f);
            near = Make(root, "Near", "g1/layer1", new Vector2(1869, 1330), new Vector2(.52f, .384f), horizonY, 2, 6.5f, 1.75f, .78f);
            SetDistance(0);
        }

        static Layer Make(RectTransform root, string name, string sprite, Vector2 size, Vector2 pivot, float horizonY, int count, float metersPerCycle, float maxZoom, float fadeOut)
        {
            var l = new Layer { group = UIKit.Rect(name, root), copies = new Image[count], metersPerCycle = metersPerCycle, maxZoom = maxZoom, fadeOut = fadeOut };
            UIKit.Stretch(l.group);
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Img($"{name}{i}", l.group, UIKit.Load(sprite), size, false);
                img.rectTransform.pivot = pivot;
                img.rectTransform.anchoredPosition = new Vector2(0, horizonY);
                l.copies[i] = img;
            }
            return l;
        }

        public void SetDistance(float metersWalked)
        {
            foreach (var l in new[] { city, ruins, near })
                for (int i = 0; i < l.copies.Length; i++)
                    Apply(l, l.copies[i], (metersWalked / l.metersPerCycle + i / (float)l.copies.Length) % 1f);

            // ท้องฟ้าลอยเบา ๆ ไม่ซูม (ไกลมาก) + หมอกขอบฟ้ากระเพื่อม
            sky.copies[0].rectTransform.localScale = Vector3.one * (1f + .004f * Mathf.Sin(metersWalked * .05f));
            haze.color = new Color(1f, .7f, .45f, .24f + .06f * Mathf.Sin(Time.time * .7f));
        }

        // phase 0..1: เริ่มเล็ก (ลึก) ค่อย ๆ จางเข้า -> ซูมเข้าหากล้อง -> ช่วงท้ายจางหาย แล้วไปอยู่ด้านหลังสุดของชั้นตัวเอง
        static void Apply(Layer l, Image img, float phase)
        {
            float z = Mathf.Lerp(1f, l.maxZoom, phase * phase * .6f + phase * .4f);   // ใกล้กล้องแล้วเคลื่อนเร็วขึ้น (มุมมองจริง)
            img.rectTransform.localScale = Vector3.one * z;
            float fadeIn = 1f - l.fadeOut < .3f ? .05f : .12f;
            float a = Mathf.Min(Mathf.Clamp01(phase / fadeIn), phase < l.fadeOut ? 1f : Mathf.Clamp01((1f - phase) / (1f - l.fadeOut)));
            img.color = new Color(1, 1, 1, a);
            if (phase < .03f) img.rectTransform.SetAsFirstSibling();                     // ชุดใหม่ = อยู่หลังชุดเก่า
        }
    }
}
