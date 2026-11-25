// =====================================================================================================
// PlayerHud — แผงข้อมูลของผู้เล่น 1 คน (มุมบน/ล่างของจอฝั่งตัวเอง)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : แสดง คะแนน (SCORE), จำนวนชิ้นที่รีไซเคิลแล้ว (กรอบ wh/goal), ข้อความเตือนข้างกรอบนั้น,
//           แถบความเร็วเดิน และแผงโครงกระดูกเล็ก (ให้รู้ว่ากล้องกำลังตามตัวเราอยู่)
// ดึงข้อมูล: กรอบจาก Resources/Sprites/wh (score_p1/2, goal), ตัวเลขจาก PlayerLane
// วางที่  : P1 ชิดซ้าย, P2 ชิดขวา
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class PlayerHud
    {
        readonly Text score, goal, warning;
        readonly Image speedFill, speedBadge;
        readonly SkeletonView skeleton;
        readonly Color color;
        static readonly Vector2 SpeedSize = new Vector2(340, 73);      // กรอบเกจความเร็ว ui2/speed_frame
        static readonly string[] SpeedBadges = { "ui2/speed_walk", "ui2/speed_boost", "ui2/speed_max" };

        public PlayerHud(Transform parent, int slot)
        {
            bool left = slot == 1;
            color = GameSettings.PlayerColors[slot - 1];
            var corner = new Vector2(left ? 0 : 1, 1);           // มุมบนของฝั่งตัวเอง
            float side = left ? 1 : -1;                            // เลื่อนเข้าด้านในจอ

            var scoreFrame = UIKit.Img($"P{slot}Score", parent, UIKit.Load(left ? "wh/score_p1" : "wh/score_p2"), new Vector2(370, 222));
            UIKit.Anchor(scoreFrame.rectTransform, corner, new Vector2(20 * side, -10));
            score = UIKit.Label("Value", scoreFrame.transform, "0", 72, Color.white);
            score.rectTransform.anchoredPosition = new Vector2(left ? 75 : 70, -30);

            // เกจความเร็ว: กรอบรองเท้า + ไส้สีรุ้ง (เติมตามความเร็ว) + ป้ายระดับ เดิน/เร่ง/สุดสปีด ทับวงรองเท้า
            var speedFrame = UIKit.Img($"P{slot}Speed", parent, UIKit.Load("ui2/speed_frame"), SpeedSize);
            UIKit.Anchor(speedFrame.rectTransform, corner, new Vector2(30 * side, -232));
            speedFill = UIKit.Img("Fill", speedFrame.transform, UIKit.Load("ui2/speed_fill"), new Vector2(SpeedSize.x * .78f, SpeedSize.y * .4f), false);
            speedFill.type = Image.Type.Filled; speedFill.fillMethod = Image.FillMethod.Horizontal;
            speedFill.rectTransform.anchoredPosition = new Vector2(SpeedSize.x * .07f, 0);
            speedFill.transform.SetAsFirstSibling();
            speedBadge = UIKit.Img("Badge", speedFrame.transform, UIKit.Load(SpeedBadges[0]), new Vector2(92, 70));
            speedBadge.rectTransform.anchoredPosition = new Vector2(-SpeedSize.x * .42f, 0);

            var skeletonBg = UIKit.Panel($"P{slot}SkeletonPanel", parent, new Vector2(150, 200), new Color(0, 0, 0, .45f));
            UIKit.Anchor(skeletonBg.rectTransform, corner, new Vector2(40 * side, -330));   // ใต้เกจความเร็ว (กรอบสูง 73)
            skeleton = new SkeletonView(skeletonBg.transform, "Skeleton", new Vector2(140, 190), color, 5);

            // กรอบนับชิ้นที่รีไซเคิล (ui2/recycled_counter) ตัวเลขอยู่ในช่องดิจิทัลด้านขวา
            var goalFrame = UIKit.Img($"P{slot}Goal", parent, UIKit.Load("ui2/recycled_counter"), new Vector2(380, 157));
            UIKit.Anchor(goalFrame.rectTransform, new Vector2(left ? 0 : 1, 0), new Vector2(20 * side, 16));
            goal = UIKit.Label("Value", goalFrame.transform, "", 58, new Color(1f, .85f, .2f));
            goal.rectTransform.anchoredPosition = new Vector2(62, -14);

            // ข้อความเตือน (เช่น เล็งค้างที่เดิม) วางข้างช่อง GOAL ไม่บังพื้นที่เล่น
            warning = UIKit.Label($"P{slot}Warning", parent, "", 36, new Color(1f, .45f, .35f),
                                  left ? TextAnchor.LowerLeft : TextAnchor.LowerRight);
            UIKit.Anchor(warning.rectTransform, new Vector2(left ? 0 : 1, 0), new Vector2(420 * side, 50));   // ข้างกรอบ RECYCLED (กว้าง 380)
            warning.rectTransform.sizeDelta = new Vector2(420, 110);
            var shadow = warning.GetComponent<Shadow>();                            // เงาดำ (Label ใส่มาให้แล้ว)
            shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(5, -5);
        }

        public void SetWarning(string text) { if (warning.text != text) warning.text = text; }

        /// <summary>ตัวเลขคะแนน และจำนวนชิ้นที่รีไซเคิลแล้ว</summary>
        public void SetNumbers(int scoreValue, int collected)
        {
            score.text = scoreValue.ToString();
            goal.text = collected.ToString();
        }

        /// <summary>เกจความเร็ว: ไส้เติมตามความเร็ว, ป้ายเปลี่ยนตามแรงเร่งจากการย่ำเท้า (เดิน / เร่ง / สุดสปีด)</summary>
        public void SetSpeed(float speed01, float boost01, bool playing)
        {
            speedFill.fillAmount = Mathf.Max(.06f, speed01);
            int level = boost01 < .15f ? 0 : boost01 < .9f ? 1 : 2;
            speedBadge.sprite = UIKit.Load(SpeedBadges[level]);
            speedBadge.rectTransform.localScale = Vector3.one * (level == 2 && playing ? 1 + .08f * Mathf.Sin(Time.time * 14) : 1);
        }

        public void SetSkeleton(float[] kp, float dt) => skeleton.Set(kp, dt);
    }
}
