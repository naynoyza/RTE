// =====================================================================================================
// StandbyScreen — หน้าจุดยืน (รอผู้เล่นครบ 2 คนก่อนเริ่ม)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : แสดงจุดยืน P1 (ซ้าย) / P2 (ขวา) พร้อมสถานะของแต่ละจุด และโครงกระดูกสดของผู้เล่น
//           ครบ 2 คนค้างไว้ StandbyHold วินาที -> บอกให้เริ่มเกม
//           ไม่มีใครเลยนาน StandbyEmpty วินาที -> บอกให้กลับหน้าปก
// ดึงข้อมูล: TrackedPlayer.present (ล็อคแล้ว), dist (ระยะผู้เล่น), approach (ระยะคนที่ยังเข้าไม่ได้), kp (กระดูก)
// ใช้โดย  : WasteHunters (สถานะ Standby)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class StandbyScreen
    {
        public enum Result { Waiting, Start, Abandoned }

        readonly GameObject root;
        readonly Text title;
        readonly Image[] rings = new Image[2];
        readonly Text[] status = new Text[2];
        readonly SkeletonView[] skeletons = new SkeletonView[2];
        readonly GameSettings settings;
        float bothReadyTime, emptyTime;

        public StandbyScreen(Transform canvas, GameSettings settings)
        {
            this.settings = settings;
            var rt = UIKit.Rect("StandbyScreen", canvas); UIKit.Stretch(rt);
            root = rt.gameObject;

            var bg = UIKit.Img("Road", rt, UIKit.Load("wh/road"), Vector2.zero, false);
            UIKit.Stretch(bg.rectTransform); bg.color = new Color(.45f, .45f, .5f);
            title = UIKit.Label("Title", rt, "", 76, Color.white);
            UIKit.Anchor(title.rectTransform, new Vector2(.5f, 1), new Vector2(0, -60));
            var sub = UIKit.Label("Sub", rt, "ยืนห่างกล้อง 2 – 3 เมตร ให้กล้องเห็นทั้งตัว   •   ต้องมีผู้เล่นครบ 2 คนจึงจะเริ่มเกม", 38, new Color(1f, .9f, .6f));
            UIKit.Anchor(sub.rectTransform, new Vector2(.5f, 1), new Vector2(0, -170));

            var zone = UIKit.Panel("CameraZone", rt, new Vector2(1500, 360), new Color(1, 1, 1, .08f));
            zone.rectTransform.anchoredPosition = new Vector2(0, -230);
            UIKit.Label("ZoneLabel", zone.transform, "พื้นที่กล้อง  2 – 3 ม.", 30, new Color(1, 1, 1, .7f)).rectTransform.anchoredPosition = new Vector2(0, -150);

            for (int i = 0; i < 2; i++)
            {
                var color = GameSettings.PlayerColors[i];
                float x = i == 0 ? -420 : 420;
                var spot = UIKit.Img($"Spot{i + 1}", rt, UIKit.Load("fx_glow"), new Vector2(520, 200), false);
                spot.rectTransform.anchoredPosition = new Vector2(x, -250);
                spot.color = new Color(color.r, color.g, color.b, .5f);
                rings[i] = UIKit.Img("Ring", spot.transform, UIKit.Load("fx_ring"), new Vector2(460, 170), false);
                UIKit.Label("Name", spot.transform, $"P{i + 1}", 90, Color.white).rectTransform.anchoredPosition = new Vector2(0, 8);
                status[i] = UIKit.Label("Status", spot.transform, "", 36, Color.white);
                status[i].rectTransform.anchoredPosition = new Vector2(0, 145);
                skeletons[i] = new SkeletonView(rt, $"P{i + 1}Skeleton", new Vector2(300, 360), color, 12);
                skeletons[i].Root.anchoredPosition = new Vector2(x, 100);
            }
        }

        public void Show() { root.SetActive(true); bothReadyTime = emptyTime = 0; }
        public void Hide() => root.SetActive(false);

        // ทุกเฟรม: อัปเดตจุดยืนทั้งสอง แล้วตัดสินว่าจะเริ่มเกม / รอต่อ / กลับหน้าปก
        public Result Tick(TrackedPlayer[] players, bool cameraOnline, bool skipKey)
        {
            bool p1 = cameraOnline && players[0].present, p2 = cameraOnline && players[1].present;
            for (int i = 0; i < 2; i++) UpdateSpot(i, players[i], i == 0 ? p1 : p2);

            bothReadyTime = p1 && p2 ? bothReadyTime + Time.deltaTime : 0;
            emptyTime = cameraOnline && !p1 && !p2 ? emptyTime + Time.deltaTime : 0;
            title.text = p1 && p2 ? $"ครบ 2 คน! เริ่มใน {Mathf.CeilToInt(settings.StandbyHold - bothReadyTime)}" : "เข้ายืนบนจุดของตัวเอง";

            if (bothReadyTime >= settings.StandbyHold || skipKey) return Result.Start;
            if (emptyTime > settings.StandbyEmpty) return Result.Abandoned;
            return Result.Waiting;
        }

        // จุดยืน 1 จุด: พร้อม (เขียว + ระยะ) / ใกล้เกินไป / ไกลเกินไป / รอ
        void UpdateSpot(int i, TrackedPlayer p, bool ready)
        {
            rings[i].color = ready ? Color.green : GameSettings.PlayerColors[i];
            rings[i].rectTransform.localScale = Vector3.one * (ready ? 1f + .05f * Mathf.Sin(Time.time * 6) : 1f);
            string text;
            if (ready) text = $"P{i + 1} พร้อม ✓" + (p.dist > 0 ? $"  ({p.dist:0.0} ม.)" : "");
            else if (p.approach > 0 && p.approach < 2f) text = $"ใกล้เกินไป ({p.approach:0.0} ม.) ถอยหลัง";
            else if (p.approach > 3f) text = $"ไกลเกินไป ({p.approach:0.0} ม.) เดินเข้ามา";
            else if (p.approach > 0) text = "ยืนนิ่ง ๆ ...";
            else text = $"รอ P{i + 1} ...";
            status[i].text = text;
            status[i].color = ready ? Color.green : Color.white;
            skeletons[i].Set(ready ? p.kp : null, Time.deltaTime);
        }
    }
}
