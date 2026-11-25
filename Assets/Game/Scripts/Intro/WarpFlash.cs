// =====================================================================================================
// WarpFlash — แสงขาวเต็มจอตอนวาร์ปเสร็จ แล้วค่อย ๆ จางเผยฉากเกม (ทำงานเองหลังหน้าเลือกตัวละครถูกซ่อนไปแล้ว)
// -----------------------------------------------------------------------------------------------------
// ใช้โดย : CharacterSelect (เรียก Play() ตอนจบการวาร์ป)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class WarpFlash : MonoBehaviour
    {
        public float FadeSeconds = .7f;
        Image img;
        float t = -1;

        /// <summary>สร้างแผ่นแสงเต็มจอบน canvas (เริ่มแบบใส). ตัวอย่าง: var f = WarpFlash.Create(canvas); f.Set(1); f.Play();</summary>
        public static WarpFlash Create(Transform canvas)
        {
            var image = UIKit.Img("WarpFlash", canvas, null, Vector2.zero, false);
            UIKit.Stretch(image.rectTransform);
            image.raycastTarget = false;
            var f = image.gameObject.AddComponent<WarpFlash>();
            f.img = image;
            f.Set(0);
            return f;
        }

        /// <summary>ตั้งความสว่างเอง (ระหว่างวาร์ป) และให้อยู่บนสุดของจอ</summary>
        public void Set(float a)
        {
            t = -1;
            img.color = new Color(1, 1, 1, Mathf.Clamp01(a));
            img.enabled = a > 0;
            if (a > 0) transform.SetAsLastSibling();
        }

        /// <summary>เริ่มจางจากขาวเต็มจอ (เรียกตอนเปลี่ยนเข้าฉากเกม)</summary>
        public void Play() { Set(1); t = 0; }

        void Update()
        {
            if (t < 0) return;
            t += Time.unscaledDeltaTime;
            float a = 1 - Mathf.Clamp01(t / FadeSeconds);
            img.color = new Color(1, 1, 1, a * a);
            if (a <= 0) { img.enabled = false; t = -1; }
        }
    }
}
