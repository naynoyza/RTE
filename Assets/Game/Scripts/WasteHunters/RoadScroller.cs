// =====================================================================================================
// RoadScroller — ภาพถนนที่ทำให้รู้สึกว่ากำลังเดินไปข้างหน้า
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : ใช้ภาพถนน 2 ชั้นซ้อนกัน แต่ละชั้นค่อย ๆ ซูมเข้าจากจุดลับตา (ปลายถนน) แล้วจางหายไป
//           ชั้นหนึ่งจางออก อีกชั้นอยู่ข้างใต้พอดี -> เดินได้ไม่มีที่สิ้นสุด ยิ่งเดินเร็วยิ่งซูมเร็ว
// ดึงข้อมูล: Resources/Sprites/wh/road.jpg, ระยะทางที่เดินไปแล้ว (เมตร) จาก PlayerLane
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class RoadScroller
    {
        readonly Image layerA, layerB;
        const float MetersPerCycle = 5f;   // เดินกี่เมตรจึงซูมครบ 1 รอบ
        const float MaxZoom = 1.45f;

        public RoadScroller(Transform parent, float horizonY)
        {
            layerA = Layer("RoadA", parent, horizonY);
            layerB = Layer("RoadB", parent, horizonY);
        }

        // ภาพถนน 1 ชั้น: จุดหมุนอยู่ที่ปลายถนนในภาพ (50.5% จากซ้าย, 46% จากล่าง) จึงซูมแล้วเหมือนเดินเข้าไป
        static Image Layer(string name, Transform parent, float horizonY)
        {
            var img = UIKit.Img(name, parent, UIKit.Load("wh/road"), new Vector2(2230, 1245), false);
            img.rectTransform.pivot = new Vector2(.505f, .46f);
            img.rectTransform.anchoredPosition = new Vector2(0, horizonY - 40);
            return img;
        }

        public void SetDistance(float metersWalked)
        {
            Apply(layerA, (metersWalked / MetersPerCycle) % 1f);
            Apply(layerB, (metersWalked / MetersPerCycle + .5f) % 1f);
        }

        // phase 0..1: ครึ่งแรกชัดเต็ม ครึ่งหลังค่อย ๆ จาง, เริ่มรอบใหม่ย้ายไปอยู่ชั้นล่างสุด
        static void Apply(Image layer, float phase)
        {
            layer.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, MaxZoom, phase);
            layer.color = new Color(1, 1, 1, phase < .5f ? 1f : Mathf.Clamp01((1f - phase) / .5f));
            if (phase < .02f) layer.rectTransform.SetSiblingIndex(0);
        }
    }
}
