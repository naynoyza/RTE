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
        static readonly Vector2 BannerSize = new Vector2(780, 260);
        readonly Text rushText;   // ตัวอักษร FINAL RUSH (ใช้เมื่อยังไม่มีรูป ui2/rush_banner)
        float bannerTime, feverLeft, secondWaveAt = -1;
        int feverIndex; bool dustDone;

        public WasteEvents(Transform canvas)
        {
            // ป้ายอีเวนต์เป็นรูปทั้งแผ่น (ui2/fever_banner, ui2/dust_banner) — ข้อความอยู่ในรูปแล้ว
            banner = UIKit.Img("EventBanner", canvas, UIKit.Load("ui2/fever_banner"), BannerSize);
            banner.rectTransform.anchoredPosition = new Vector2(0, 230);
            glow = UIKit.Img("Glow", banner.transform, UIKit.Load("fx_glow"), new Vector2(1000, 420), false);
            glow.transform.SetAsFirstSibling();
            rushText = UIKit.Label("RushText", banner.transform, "", 72, new Color(1f, .78f, .25f));
            rushText.rectTransform.sizeDelta = new Vector2(1100, 260);
            UIKit.Outline(rushText, new Color(.35f, .05f, 0), 4);
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
            foreach (var l in lanes) l.BannerUp = bannerTime > 0;   // ป้ายรายฝั่งหลบลงใต้ป้ายนี้
            banner.rectTransform.localScale = Vector3.one * (1 + .03f * Mathf.Sin(Time.time * 9));
            glow.color = new Color(1f, .75f, .2f, .35f + .25f * Mathf.Sin(Time.time * 6));
        }

        public void StartFever(PlayerLane[] lanes)
        {
            feverLeft = FeverSeconds; secondWaveAt = FeverSeconds * .5f;
            Sfx.Play("fever");
            foreach (var l in lanes) { l.StartFever(FeverSeconds); l.DumpBurst(6); }
            Show("ui2/fever_banner", 3f);
        }

        // FINAL RUSH: ใช้รูป ui2/rush_banner ถ้ามี ไม่มีก็แสดงเป็นตัวอักษรใหญ่มีขอบ (ไม่ใช้รูปป้ายอื่นที่มีคำอยู่แล้ว)
        public void StartRush(GameSettings settings)
        {
            Sfx.Play("rush");
            bool hasArt = Resources.Load<Texture2D>("Sprites/ui2/rush_banner") != null;
            Show(hasArt ? "ui2/rush_banner" : "ui2/fever_banner", 3f);
            banner.color = hasArt ? Color.white : Color.clear;
            rushText.text = hasArt ? "" : $"FINAL RUSH!\n{settings.RushSeconds:0} วินาทีสุดท้าย • คะแนน x{settings.RushScoreMultiplier}";
        }

        public void StartDust(PlayerLane[] lanes)
        {
            foreach (var l in lanes) l.StartDust();
            Sfx.Play("dust");
            Show("ui2/dust_banner", 2.2f);
        }

        void Show(string sprite, float seconds)
        {
            banner.sprite = UIKit.Load(sprite);
            banner.color = Color.white; rushText.text = "";
            bannerTime = seconds; banner.gameObject.SetActive(true);
        }
    }
}
