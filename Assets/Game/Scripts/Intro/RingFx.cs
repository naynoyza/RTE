// =====================================================================================================
// RingFx — แสง/เอฟเฟกต์ของวงจุดยืนในหน้าเลือกตัวละคร
// -----------------------------------------------------------------------------------------------------
//   ชั้นหลังโมเดล : เสาแสงพุ่งขึ้นจากวง (light_beam), วงแหวนเทคหมุน 2 ชั้น (tech_ring บีบเป็นวงรีบนพื้น), แสงฟุ้งที่พื้น
//   ชั้นหน้าโมเดล : อนุภาคแสงลอยขึ้น, แสงวาบ 4 แฉก (flare) + คลื่นวงแหวนขยายออก ตอนเลือกตัวละครสำเร็จ
//   ยิ่งยกแขนค้างใกล้เลือกสำเร็จ แสงยิ่งแรง; เลือกแล้วเสาแสงสว่างค้าง
// ดึงข้อมูล: Resources/Sprites/fx/light_beam, tech_ring, flare (สร้างโดย Tools/make_fx_sprites.py)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class RingFx
    {
        const int ParticleCount = 18;

        readonly Color color;
        readonly Vector2 center;
        readonly Image beam, beamCore, ringA, ringB, floorGlow, flare, shock;
        readonly Image[] parts = new Image[ParticleCount];
        readonly float[] partPhase = new float[ParticleCount], partX = new float[ParticleCount], partSpeed = new float[ParticleCount];
        float burstT = 99, level;

        // back = ชั้นหลังโมเดล (สร้างก่อนโมเดล), front = ชั้นหน้าโมเดล (สร้างหลังโมเดล)
        public RingFx(Transform back, Transform front, Vector2 center, Color color)
        {
            this.center = center; this.color = color;

            floorGlow = Make("FloorGlow", back, "fx_glow", new Vector2(620, 220), center);
            ringA = Make("TechRingA", back, "fx/tech_ring", new Vector2(470, 470), center);
            ringB = Make("TechRingB", back, "fx/tech_ring", new Vector2(360, 360), center);
            beam = Make("LightBeam", back, "fx/light_beam", new Vector2(420, 900), center + new Vector2(0, 420));
            beamCore = Make("LightCore", back, "fx/light_beam", new Vector2(150, 820), center + new Vector2(0, 380));

            for (int k = 0; k < ParticleCount; k++)
            {
                parts[k] = Make("Spark", front, "fx_circle", Vector2.one * Random.Range(6f, 14f), center);
                partPhase[k] = Random.value; partX[k] = Random.Range(-170f, 170f); partSpeed[k] = Random.Range(.25f, .55f);
            }
            shock = Make("ShockRing", front, "fx/tech_ring", new Vector2(400, 400), center);
            flare = Make("Flare", front, "fx/flare", new Vector2(600, 600), center + new Vector2(0, 200));
        }

        static Image Make(string name, Transform parent, string sprite, Vector2 size, Vector2 pos)
        {
            var img = UIKit.Img(name, parent, UIKit.Load(sprite), size, false);
            img.rectTransform.anchoredPosition = pos;
            img.color = Color.clear;
            return img;
        }

        public void Burst() => burstT = 0;

        // hold01 = ความคืบหน้าการยกแขน 0..1, picked = เลือกตัวละครแล้ว
        public void Tick(float hold01, bool picked)
        {
            float dt = Time.deltaTime, t = Time.time;
            float target = picked ? 1f : .35f + .65f * hold01;
            level = Mathf.MoveTowards(level, target, dt * 2.5f);
            float pulse = .85f + .15f * Mathf.Sin(t * (picked ? 3 : 6));

            // วงแหวนบนพื้น: หมุนสวนกัน บีบให้เป็นวงรีตามมุมมองพื้น
            ringA.rectTransform.localRotation = Quaternion.Euler(70, 0, t * 40);
            ringB.rectTransform.localRotation = Quaternion.Euler(70, 0, -t * 75);
            ringA.color = WithA(Color.Lerp(color, Color.white, .45f), (.65f + .35f * level) * pulse);
            ringB.color = WithA(Color.Lerp(color, Color.white, .7f), (.5f + .5f * level) * pulse);
            floorGlow.color = WithA(color, .35f + .45f * level);

            // เสาแสง: ส่ายเล็กน้อย กว้าง/สว่างตามระดับ
            beam.color = WithA(color, (.12f + .4f * level) * pulse);
            beam.rectTransform.localScale = new Vector3(.8f + .4f * level + .05f * Mathf.Sin(t * 2.3f), 1, 1);
            beamCore.color = WithA(Color.Lerp(color, Color.white, .6f), (.08f + .45f * level) * pulse);

            // อนุภาคลอยขึ้นวนซ้ำ (เร็วขึ้นเมื่อระดับสูง)
            for (int k = 0; k < ParticleCount; k++)
            {
                partPhase[k] = (partPhase[k] + dt * partSpeed[k] * (.6f + level)) % 1f;
                float p = partPhase[k];
                var rt = parts[k].rectTransform;
                rt.anchoredPosition = center + new Vector2(partX[k] * (1 - p * .4f) + Mathf.Sin(t * 2 + k) * 10, -20 + p * 620);
                parts[k].color = WithA(Color.Lerp(color, Color.white, .5f), Mathf.Sin(p * Mathf.PI) * (.3f + .7f * level));
            }

            // ตอนเลือกสำเร็จ: แสงวาบ + คลื่นวงแหวน
            burstT += dt;
            float b = Mathf.Clamp01(burstT / .7f);
            flare.color = WithA(Color.Lerp(Color.white, color, b), 1 - b);
            flare.rectTransform.localScale = Vector3.one * Mathf.Lerp(.3f, 1.6f, Mathf.Sqrt(b));
            flare.rectTransform.localRotation = Quaternion.Euler(0, 0, b * 45);
            shock.color = WithA(color, (1 - b) * .9f);
            shock.rectTransform.localScale = new Vector3(1, .34f, 1) * Mathf.Lerp(.4f, 2.4f, b);
        }

        static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }
}
