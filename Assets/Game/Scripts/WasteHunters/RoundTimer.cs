// =====================================================================================================
// RoundTimer — นาฬิกานับถอยหลังกลางจอด้านบน
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : แสดงเวลาที่เหลือแบบ นาที:วินาที (10 วินาทีสุดท้ายเป็นสีแดง)
// ดึงข้อมูล: กรอบนาฬิกา Resources/Sprites/wh/timer.png, เวลาที่เหลือจาก WasteHunters
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class RoundTimer
    {
        readonly Text text;
        readonly Image frame;
        bool rush;

        public RoundTimer(Transform parent)
        {
            frame = UIKit.Img("Timer", parent, UIKit.Load("wh/timer"), new Vector2(430, 178));
            UIKit.Anchor(frame.rectTransform, new Vector2(.5f, 1), new Vector2(0, -6));
            text = UIKit.Label("Time", frame.transform, "", 82, Color.white);
            text.rectTransform.anchoredPosition = new Vector2(48, 4);
        }

        int lastShown = -1;

        /// <summary>วินาทีสุดท้ายที่นับเสียงติ๊ก (5..1)</summary>
        public const int TickFrom = 5;

        public void Set(float secondsLeft)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0, secondsLeft));
            if (s != lastShown && s > 0 && s <= TickFrom && lastShown > s) Sfx.Play("tick", .8f, 1 + .06f * (TickFrom - s));   // ติ๊ก 5-4-3-2-1 ก่อนหมดเวลา
            lastShown = s;
            text.text = $"{s / 60:00}:{s % 60:00}";
            text.color = s <= 10 ? new Color(1, .35f, .3f) : rush ? new Color(1f, .7f, .2f) : Color.white;
            frame.rectTransform.localScale = Vector3.one * (rush ? 1 + .05f * Mathf.Abs(Mathf.Sin(Time.time * 6)) : 1);
        }

        /// <summary>ช่วง FINAL RUSH: เลขเวลาเป็นสีส้มและนาฬิกาเต้นตามจังหวะ</summary>
        public void SetRush(bool on) => rush = on;
    }
}
