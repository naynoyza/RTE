// =====================================================================================================
// WasteEvents — อีเวนต์ระหว่างเกม 1 ที่เกิดพร้อมกันทั้งสองฝั่ง (อ้างอิง Ref/event2.jpg, event3.jpg)
// -----------------------------------------------------------------------------------------------------
//   DAILY DUMP FEVER : นาที 0:35 และ 1:40 ของรอบ, 8 วิ — ป้ายใหญ่กลางจอ, ขยะร่วงจากฟ้า 2 ระลอก, คะแนน x2
//   DUST STORM       : นาที 1:05 — ฝุ่นบังหน้ากากทั้งสองฝั่ง ต้องปัดมือเช็ด (หายเองใน 12 วิ)
// อีเวนต์รายฝั่ง (OVERDRIVE จาก combo, DANGER ขยะช็อตไฟ) อยู่ใน PlayerLane
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class WasteEvents
    {
        const float FeverSeconds = 8f;
        static readonly float[] FeverAt = { 35f, 100f };
        const float DustAt = 65f;

        readonly Image banner, glow;
        readonly Text bannerText;
        float bannerTime, feverLeft, secondWaveAt = -1;
        int feverIndex; bool dustDone;

        public WasteEvents(Transform canvas)
        {
            // ป้ายโลหะแถบเหลือง-ดำ จาก UI/11.jpg (ข้อความในรูปถูกปิดด้วยแผ่นเข้ม แล้วเขียนใหม่ได้ทุกอีเวนต์)
            banner = UIKit.Img("EventBanner", canvas, UIKit.Load("fx/fever_sign"), new Vector2(700, 210));
            banner.rectTransform.anchoredPosition = new Vector2(0, 230);
            var plate = UIKit.Panel("Plate", banner.transform, new Vector2(560, 120), new Color(.16f, .17f, .19f, 1));
            plate.rectTransform.anchoredPosition = new Vector2(0, 2);
            glow = UIKit.Img("Glow", banner.transform, UIKit.Load("fx_glow"), new Vector2(900, 380), false);
            glow.transform.SetAsFirstSibling();
            bannerText = UIKit.Label("Text", banner.transform, "", 58, new Color(1f, .85f, .3f));
            banner.gameObject.SetActive(false);
        }

        public void Reset() { bannerTime = feverLeft = 0; secondWaveAt = -1; feverIndex = 0; dustDone = false; banner.gameObject.SetActive(false); }

        // elapsed = วินาทีที่เล่นไปแล้วในรอบนี้
        public void Tick(float elapsed, PlayerLane[] lanes)
        {
            float dt = Time.deltaTime;
            if (feverIndex < FeverAt.Length && elapsed >= FeverAt[feverIndex]) { feverIndex++; StartFever(lanes); }
            if (!dustDone && elapsed >= DustAt) { dustDone = true; StartDust(lanes); }

            if (feverLeft > 0)
            {
                feverLeft -= dt;
                if (secondWaveAt > 0 && feverLeft <= secondWaveAt) { secondWaveAt = -1; foreach (var l in lanes) l.DumpBurst(5); }
            }
            if (bannerTime > 0 && (bannerTime -= dt) <= 0) banner.gameObject.SetActive(false);
            banner.rectTransform.localScale = Vector3.one * (1 + .03f * Mathf.Sin(Time.time * 9));
            glow.color = new Color(1f, .75f, .2f, .35f + .25f * Mathf.Sin(Time.time * 6));
        }

        public void StartFever(PlayerLane[] lanes)
        {
            feverLeft = FeverSeconds; secondWaveAt = FeverSeconds * .5f;
            foreach (var l in lanes) { l.StartFever(FeverSeconds); l.DumpBurst(6); }
            Show("DAILY DUMP FEVER!\n2X SCORE", new Color(1f, .85f, .3f), 3f);
        }

        public void StartDust(PlayerLane[] lanes)
        {
            foreach (var l in lanes) l.StartDust();
            Show("พายุฝุ่น!\nปัดมือเช็ดหน้ากาก", new Color(1f, .75f, .45f), 2.2f);
        }

        void Show(string text, Color c, float seconds)
        {
            bannerText.text = text; bannerText.color = c;
            bannerTime = seconds; banner.gameObject.SetActive(true);
        }
    }
}
