// =====================================================================================================
// PlayerHud — แผงข้อมูลของผู้เล่น 1 คน (มุมบน/ล่างของจอฝั่งตัวเอง)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : แสดง คะแนน (SCORE), ของในถุง (INVENTORY), รีไซเคิลแล้ว/เป้าหมาย (GOAL),
//           แถบความเร็วเดิน และแผงโครงกระดูกเล็ก (ให้รู้ว่ากล้องกำลังตามตัวเราอยู่)
// ดึงข้อมูล: กรอบจาก Resources/Sprites/wh (score_p1/2, inv_p1/2, goal), ตัวเลขจาก PlayerLane
// วางที่  : P1 ชิดซ้าย, P2 ชิดขวา
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class PlayerHud
    {
        readonly Text score, bag, goal, speedLabel;
        readonly Image speedFill;
        readonly SkeletonView skeleton;
        readonly Color color;
        const float SpeedBarWidth = 292;

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

            var bagFrame = UIKit.Img($"P{slot}Inventory", parent, UIKit.Load(left ? "wh/inv_p1" : "wh/inv_p2"), new Vector2(330, 182));
            UIKit.Anchor(bagFrame.rectTransform, corner, new Vector2(28 * side, -225));
            bag = UIKit.Label("Value", bagFrame.transform, "0", 70, Color.black);
            bag.GetComponent<Shadow>().enabled = false;
            bag.rectTransform.anchoredPosition = new Vector2(60, -10);

            var speedBg = UIKit.Panel($"P{slot}Speed", parent, new Vector2(300, 34), new Color(0, 0, 0, .55f));
            UIKit.Anchor(speedBg.rectTransform, corner, new Vector2(40 * side, -415));
            speedFill = UIKit.Panel("Fill", speedBg.transform, new Vector2(SpeedBarWidth, 26), color);
            speedFill.rectTransform.pivot = speedFill.rectTransform.anchorMin = speedFill.rectTransform.anchorMax = new Vector2(0, .5f);
            speedFill.rectTransform.anchoredPosition = new Vector2(4, 0);
            speedLabel = UIKit.Label("Label", speedBg.transform, "", 26, Color.white);

            var skeletonBg = UIKit.Panel($"P{slot}SkeletonPanel", parent, new Vector2(150, 200), new Color(0, 0, 0, .45f));
            UIKit.Anchor(skeletonBg.rectTransform, corner, new Vector2(40 * side, -460));
            skeleton = new SkeletonView(skeletonBg.transform, "Skeleton", new Vector2(140, 190), color, 5);

            var goalFrame = UIKit.Img($"P{slot}Goal", parent, UIKit.Load("wh/goal"), new Vector2(360, 172));
            UIKit.Anchor(goalFrame.rectTransform, new Vector2(left ? 0 : 1, 0), new Vector2(20 * side, 16));
            goal = UIKit.Label("Value", goalFrame.transform, "", 56, new Color(1f, .85f, .2f));
            goal.rectTransform.anchoredPosition = new Vector2(50, -22);
        }

        // ตัวเลขคะแนน / ถุง (เต็มแล้วเป็นสีแดง) / รีไซเคิล
        public void SetNumbers(int scoreValue, int inBag, int bagSize, int recycled, int goalValue)
        {
            score.text = scoreValue.ToString();
            bag.text = inBag.ToString();
            bag.color = inBag >= bagSize ? new Color(.85f, .15f, .1f) : new Color(.2f, .2f, .22f);
            goal.text = $"{recycled} / {goalValue}";
        }

        // แถบความเร็ว: ยาวตามความเร็ว, เขียวขึ้นตามแรงเร่งจากการย่ำเท้า
        public void SetSpeed(float speed01, float boost01, bool playing)
        {
            speedFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(26, SpeedBarWidth * speed01), 26);
            speedFill.color = Color.Lerp(color, new Color(.3f, 1f, .4f), boost01);
            speedLabel.text = !playing ? "" : boost01 < .15f ? "เดินอัตโนมัติ • ย่ำเท้าเพื่อเร่ง" : boost01 < .9f ? "เร่งสปีด  ▶▶" : "สุดสปีด!  ▶▶▶";
        }

        public void SetSkeleton(float[] kp, float dt) => skeleton.Set(kp, dt);
    }
}
