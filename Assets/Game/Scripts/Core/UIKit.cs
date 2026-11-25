// =====================================================================================================
// UIKit — ตัวช่วยสร้าง UI ด้วยโค้ด
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : สร้างรูป / ข้อความ / กล่องเปล่า และจัดตำแหน่งบน Canvas โดยไม่ต้องลากวางใน Editor
// ดึงข้อมูล: รูปจาก Resources/Sprites/<ชื่อ>.png, ฟอนต์จาก Resources/Fonts/ThaiFontWH.ttf
// ใช้โดย  : ทุกหน้าจอของเกม (หน้าปก, จุดยืน, HUD, จอผู้เล่น)
// =====================================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public static class UIKit
    {
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static Font font;

        // ฟอนต์ไทยของเกม (Leelawadee UI Bold ที่ยกวรรณยุกต์ไว้แล้ว) ถ้าหาไม่เจอใช้ฟอนต์ของ Windows
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/ThaiFontWH");
                if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Leelawadee UI", "Tahoma", "Arial" }, 48);
                return font;
            }
        }

        // Text ของ Unity วางวรรณยุกต์ซ้อนบนสระบนไม่เป็น (มันจะทับกันจนมองไม่เห็น)
        // จึงสลับวรรณยุกต์ที่อยู่หลังสระบน (ั ิ ี ึ ื ็) หรืออยู่บน ำ ไปใช้ตัว "ยกสูง" ที่รหัส U+F70A..U+F70E ในฟอนต์ ThaiFontWH
        public static string FixThai(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            System.Text.StringBuilder sb = null;
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c < '่' || c > '์') continue;                       // ไม่ใช่วรรณยุกต์ ่ ้ ๊ ๋ ์
                char prev = s[i - 1];
                bool afterUpperVowel = prev == 'ั' || (prev >= 'ิ' && prev <= 'ื') || prev == '็';
                bool beforeSaraAm = i + 1 < s.Length && s[i + 1] == 'ำ';
                if (!afterUpperVowel && !beforeSaraAm) continue;
                sb ??= new System.Text.StringBuilder(s);
                sb[i] = (char)(0xF70A + (c - 0x0E48));
            }
            return sb == null ? s : sb.ToString();
        }

        // โหลดรูป Resources/Sprites/<name> เป็น Sprite (จำไว้ใน cache โหลดครั้งเดียว)
        public static Sprite Load(string name)
        {
            if (spriteCache.TryGetValue(name, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("Sprites/" + name);
            if (s == null)
            {
                var tex = Resources.Load<Texture2D>("Sprites/" + name);
                if (tex == null) { Debug.LogWarning("[UIKit] missing sprite " + name); return null; }
                tex.wrapMode = TextureWrapMode.Clamp;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100);
                s.name = name;
            }
            return spriteCache[name] = s;
        }

        // GameObject เปล่าที่มี RectTransform (ใช้เป็นกลุ่ม/กรอบ)
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // รูปภาพ 1 ชิ้น
        public static Image Img(string name, Transform parent, Sprite sprite, Vector2 size, bool keepAspect = true)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = keepAspect;
            img.raycastTarget = false;
            return img;
        }

        // กรอบสี่เหลี่ยมมุมมน (ใช้รูป fx_circle แบบ 9-slice) เช่น พื้นหลังแถบ/แผง
        public static Image Panel(string name, Transform parent, Vector2 size, Color color)
        {
            var img = Img(name, parent, Load("fx_circle"), size, false);
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        // ข้อความภาษาไทย (ตัวหนา มีเงา ไม่ตัดบรรทัด)
        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(400, size * 1.5f);
            var t = rt.gameObject.AddComponent<ThaiText>();
            t.font = Font; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            // ขอบตัวอักษรเข้ม (สไตล์เดียวกับป้ายภาพในเกม อ่านออกทั้งบนพื้นสว่างและมืด)
            // ใช้ Outline ซึ่งเป็นลูกของ Shadow: โค้ดเดิมที่เรียก GetComponent<Shadow>() ยังปรับค่าได้เหมือนเดิม
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.03f, .05f, .1f, .9f);
            outline.effectDistance = new Vector2(Mathf.Clamp(size / 18f, 1.5f, 4f), -Mathf.Clamp(size / 18f, 1.5f, 4f));
            return t;
        }

        // วางชิ้น UI ชิดจุดยึด (เช่น (0,1) = มุมบนซ้าย) แล้วเลื่อนออกมา pos พิกเซล
        /// <summary>ใส่ขอบตัวอักษรหนา (อ่านออกบนพื้นหลังทุกแบบ) — ตัวอย่าง: UIKit.Outline(label, Color.black, 3)</summary>
        public static void Outline(Text t, Color color, float width = 3)
        {
            var o = t.GetComponent<Outline>() ?? t.gameObject.AddComponent<Outline>();
            o.effectColor = color; o.effectDistance = new Vector2(width, -width);
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }

        // ยืดชิ้น UI ให้เต็มกรอบแม่
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // แอนิเมชันเด้ง: 0..1 -> ขยายเกินนิดแล้วกลับมา 1
        public static float EaseOutBack(float t) { const float c = 1.70158f; t -= 1; return 1 + (c + 1) * t * t * t + c * t * t; }
    }
}
