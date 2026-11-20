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

        public RoundTimer(Transform parent)
        {
            var frame = UIKit.Img("Timer", parent, UIKit.Load("wh/timer"), new Vector2(430, 178));
            UIKit.Anchor(frame.rectTransform, new Vector2(.5f, 1), new Vector2(0, -6));
            text = UIKit.Label("Time", frame.transform, "", 82, Color.white);
            text.rectTransform.anchoredPosition = new Vector2(48, 4);
        }

        public void Set(float secondsLeft)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0, secondsLeft));
            text.text = $"{s / 60:00}:{s % 60:00}";
            text.color = s <= 10 ? new Color(1, .35f, .3f) : Color.white;
        }
    }
}
