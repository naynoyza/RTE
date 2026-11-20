// =====================================================================================================
// CenterOverlay — ข้อความใหญ่กลางจอ (นับถอยหลัง และหน้าสรุปผล)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : ทำจอมืดลง + ตัวอักษรใหญ่ + คำอธิบาย
//           Countdown(): 3-2-1-GO! พร้อมวิธีเล่น
//           ShowResult(): ผู้ชนะ, คะแนน, จำนวนรีไซเคิล, Air Recovery ของทีม
// ดึงข้อมูล: คะแนน/รีไซเคิล/ค่าผลกระทบ จาก PlayerLane ทั้งสองฝั่ง
// ใช้โดย  : WasteHunters (สถานะ Countdown และ Result)
// =====================================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class CenterOverlay
    {
        readonly Image dim, panel;
        readonly Text big, sub;

        public CenterOverlay(Transform canvas)
        {
            dim = UIKit.Img("Dim", canvas, null, Vector2.zero, false); UIKit.Stretch(dim.rectTransform);
            dim.color = new Color(0, 0, 0, .55f);
            panel = UIKit.Panel("ResultPanel", canvas, new Vector2(1320, 460), new Color(.05f, .07f, .1f, .85f));
            panel.rectTransform.anchoredPosition = new Vector2(0, 20);
            big = UIKit.Label("Big", canvas, "", 150, Color.white);
            big.rectTransform.anchoredPosition = new Vector2(0, 80);
            sub = UIKit.Label("Sub", canvas, "", 44, new Color(.85f, 1f, .75f));
            sub.rectTransform.anchoredPosition = new Vector2(0, -60);
            Hide();
        }

        public void Hide() { dim.enabled = panel.enabled = false; big.text = sub.text = ""; }

        // 3-2-1-GO! แต่ละตัวเด้งขยายแล้วค้าง 0.9 วินาที
        public IEnumerator Countdown()
        {
            dim.enabled = true;
            sub.text = "ยกมือเล็งปืนให้แตะขยะ = ดูดทันที  •  เดินอัตโนมัติ ย่ำเท้า = เร่งสปีด";
            foreach (var s in new[] { "3", "2", "1", "GO!" })
            {
                big.text = s;
                for (float t = 0; t < .9f; t += Time.deltaTime)
                {
                    big.rectTransform.localScale = Vector3.one * UIKit.EaseOutBack(Mathf.Min(1, t / .3f));
                    yield return null;
                }
            }
            Hide();
        }

        // Air Recovery = คะแนนผลกระทบของขยะที่เก็บได้ ÷ ของขยะที่เกิดทั้งหมด (ทั้งทีม)
        public void ShowResult(PlayerLane p1, PlayerLane p2)
        {
            dim.enabled = panel.enabled = true;
            big.text = p1.Score == p2.Score ? "เสมอ!" : (p1.Score > p2.Score ? "P1 ชนะ!" : "P2 ชนะ!");
            int collected = p1.ImpactCollected + p2.ImpactCollected;
            int spawned = Mathf.Max(1, p1.ImpactSpawned + p2.ImpactSpawned);
            sub.text = $"P1  {p1.Score} คะแนน  (รีไซเคิล {p1.Recycled})     |     P2  {p2.Score} คะแนน  (รีไซเคิล {p2.Recycled})\n" +
                       $"Air Recovery ของทีม  {100f * collected / spawned:0}%";
        }
    }
}
