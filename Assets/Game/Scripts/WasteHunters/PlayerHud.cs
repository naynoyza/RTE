// =====================================================================================================
// PlayerHud — แผงข้อมูลของผู้เล่น 1 คน (มุมบน/ล่างของจอฝั่งตัวเอง)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : แผงผู้เล่น (ui2/p1_panel, p2_panel): หน้าตัวละครที่เลือก + หลอดอากาศ AIR + คะแนน SCORE,
//           จำนวนชิ้นที่รีไซเคิลแล้ว (กรอบ ui2/recycled_counter), ข้อความเตือนข้างกรอบนั้น,
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
        readonly Image speedFill, speedBadge, airFill, face;
        readonly Sprite airNormal, airLow;
        readonly bool left;
        float airShown = 1;
        readonly SkeletonView skeleton;
        readonly Color color;
        static readonly Vector2 SpeedSize = new Vector2(340, 73);      // กรอบเกจความเร็ว ui2/speed_frame
        static readonly string[] SpeedBadges = { "ui2/speed_walk", "ui2/speed_boost", "ui2/speed_max" };

        public PlayerHud(Transform parent, int slot)
        {
            left = slot == 1;
            color = GameSettings.PlayerColors[slot - 1];
            var corner = new Vector2(left ? 0 : 1, 1);           // มุมบนของฝั่งตัวเอง
            float side = left ? 1 : -1;                            // เลื่อนเข้าด้านในจอ

            // แผงผู้เล่น: รูป 844x444 (P2 853x443) ย่อเหลือกว้าง PanelW; ตำแหน่งช่องต่าง ๆ วัดจากรูป (พิกเซลรูป x k)
            float k = PanelW / (left ? 844f : 853f);
            var panel = UIKit.Img($"P{slot}Panel", parent, UIKit.Load(left ? "ui2/p1_panel" : "ui2/p2_panel"), new Vector2(PanelW, (left ? 444f : 443f) * k));
            UIKit.Anchor(panel.rectTransform, corner, new Vector2(16 * side, -8));
            // หน้าตัวละครในวงกลม (ui2/face_male, face_female ตามที่เลือกในหน้าเลือกตัวละคร)
            face = UIKit.Img("Face", panel.transform, null, new Vector2(100, 100), false);
            face.rectTransform.anchoredPosition = left ? new Vector2(-123, -3) : new Vector2(123, -3);
            // หลอดอากาศ: ไส้เติมตามอากาศ (P1 เติมจากซ้าย, P2 เติมจากขวา) — ต่ำ = ไส้แดง
            airNormal = UIKit.Load(left ? "ui2/air_fill_p1" : "ui2/air_fill_p2"); airLow = UIKit.Load("ui2/air_fill_low");
            airFill = UIKit.Img("AirFill", panel.transform, airNormal, new Vector2(174, 46), false);
            airFill.type = Image.Type.Filled; airFill.fillMethod = Image.FillMethod.Horizontal;
            airFill.fillOrigin = (int)(left ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
            airFill.rectTransform.anchoredPosition = left ? new Vector2(88, 13) : new Vector2(-88, 13);
            score = UIKit.Label("Value", panel.transform, "0", 40, Color.white);
            score.rectTransform.anchoredPosition = left ? new Vector2(78, -68) : new Vector2(-28, -68);

            // เกจความเร็ว: กรอบรองเท้า + ไส้สีรุ้ง (เติมตามความเร็ว) + ป้ายระดับ เดิน/เร่ง/สุดสปีด ทับวงรองเท้า
            var speedFrame = UIKit.Img($"P{slot}Speed", parent, UIKit.Load("ui2/speed_frame"), SpeedSize);
            UIKit.Anchor(speedFrame.rectTransform, corner, new Vector2(30 * side, -244));
            speedFill = UIKit.Img("Fill", speedFrame.transform, UIKit.Load("ui2/speed_fill"), new Vector2(SpeedSize.x * .78f, SpeedSize.y * .4f), false);
            speedFill.type = Image.Type.Filled; speedFill.fillMethod = Image.FillMethod.Horizontal;
            speedFill.rectTransform.anchoredPosition = new Vector2(SpeedSize.x * .07f, 0);
            speedFill.transform.SetAsFirstSibling();
            speedBadge = UIKit.Img("Badge", speedFrame.transform, UIKit.Load(SpeedBadges[0]), new Vector2(92, 70));
            speedBadge.rectTransform.anchoredPosition = new Vector2(-SpeedSize.x * .42f, 0);

            var skeletonBg = UIKit.Panel($"P{slot}SkeletonPanel", parent, new Vector2(150, 200), new Color(0, 0, 0, .45f));
            UIKit.Anchor(skeletonBg.rectTransform, corner, new Vector2(40 * side, -342));   // ใต้เกจความเร็ว (กรอบสูง 73)
            skeleton = new SkeletonView(skeletonBg.transform, "Skeleton", new Vector2(140, 190), color, 5);

            // กรอบนับชิ้นที่รีไซเคิล (ui2/recycled_counter) ตัวเลขอยู่ในช่องดิจิทัลด้านขวา
            var goalFrame = UIKit.Img($"P{slot}Goal", parent, UIKit.Load("ui2/recycled_counter"), new Vector2(380, 157));
            UIKit.Anchor(goalFrame.rectTransform, new Vector2(left ? 0 : 1, 0), new Vector2(20 * side, 16));
            goal = UIKit.Label("Value", goalFrame.transform, "", 58, new Color(1f, .85f, .2f));
            goal.rectTransform.anchoredPosition = new Vector2(46, -2);   // กลางช่องดิจิทัล (ฟอนต์ไทยวางตัวเลขต่ำ จึงยกขึ้นกว่ากลางกรอบเล็กน้อย)

            // ข้อความเตือนรายฝั่ง วางข้างช่อง GOAL ไม่บังพื้นที่เล่น
            warning = UIKit.Label($"P{slot}Warning", parent, "", 36, new Color(1f, .45f, .35f),
                                  left ? TextAnchor.LowerLeft : TextAnchor.LowerRight);
            UIKit.Anchor(warning.rectTransform, new Vector2(left ? 0 : 1, 0), new Vector2(420 * side, 50));   // ข้างกรอบ RECYCLED (กว้าง 380)
            warning.rectTransform.sizeDelta = new Vector2(420, 110);
            // ขอบดำใช้ค่ามาตรฐานของ UIKit.Label (ขอบรอบตัวอักษรเท่ากันทุกด้าน เหมือนข้อความอื่น)
        }

        const float PanelW = 430f;

        public void SetWarning(string text) { if (warning.text != text) warning.text = text; }

        /// <summary>หน้าตัวละครในวงกลมของแผง (None = ใช้ค่าเริ่มต้นตามฝั่ง: P1 ชาย, P2 หญิง)</summary>
        public void SetHero(Hero hero)
        {
            if (hero == Hero.None) hero = left ? Hero.Male : Hero.Female;
            face.sprite = UIKit.Load(hero == Hero.Male ? "ui2/face_male" : "ui2/face_female");
        }

        /// <summary>หลอดอากาศ 0..1 (เติม/ลดแบบนุ่ม), low = เปลี่ยนเป็นไส้แดงกะพริบ</summary>
        public void SetAir(float air01, bool low, bool playing)
        {
            airShown = Mathf.MoveTowards(airShown, Mathf.Clamp01(air01), Time.deltaTime * 1.5f);
            airFill.fillAmount = airShown;
            airFill.sprite = low ? airLow : airNormal;
            airFill.color = low && playing ? new Color(1, 1, 1, .7f + .3f * Mathf.Abs(Mathf.Sin(Time.time * 6))) : Color.white;
        }

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

        public void SetSkeleton(float[] kp, float dt, float[] pose = null) => skeleton.Set(kp, dt, pose);
    }
}
