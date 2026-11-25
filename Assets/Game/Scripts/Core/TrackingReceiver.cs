// =====================================================================================================
// TrackingReceiver — รับข้อมูลผู้เล่นจากกล้อง (Python/tracker.py)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : เปิดพอร์ต UDP 5065 รอข้อความ JSON ที่ tracker ส่งมา 30 ครั้ง/วินาที
//           แล้วเก็บเป็นข้อมูล P1 = Players[0], P2 = Players[1]
// ดึงข้อมูล: ผู้เล่นถูกล็อคไหม, ระยะห่าง, จำนวนก้าวย่ำเท้า, มือที่ใช้เล็ง + ตำแหน่งมือ, จุดกระดูก 17 จุด
// ไม่มีกล้อง: ใช้เมาส์เป็น P1 และปุ่ม WASD เป็น P2 แทน (ไว้ทดสอบ)
// ใช้โดย  : ทุกส่วนของเกมอ่านผ่าน TrackingReceiver.I.Players
// =====================================================================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTE
{
    // ข้อมูลผู้เล่น 1 คนในแต่ละแพ็กเก็ต (ชื่อตัวแปรต้องตรงกับ key ใน JSON ของ tracker)
    [Serializable]
    public class TrackedPlayer
    {
        public int slot;                 // 1 = P1, 2 = P2
        public bool present;             // คนนี้ถูกล็อคเป็นผู้เล่นอยู่
        public float dist = -1;          // ระยะผู้เล่นจากกล้อง (เมตร, -1 = ไม่รู้)
        public float approach = -1;      // ระยะของคนที่ยังไม่ได้เป็นผู้เล่นในฝั่งนี้ (ใช้บอก ใกล้/ไกลเกินไป)
        public int steps;                // จำนวนก้าวย่ำเท้าสะสม (เพิ่มขึ้นทุกครั้งที่สลับเท้า)
        public bool hand;                // มีมือยกขึ้นเล็งอยู่ไหม
        public int use = -1;             // มือที่เล็ง: 0 = ซ้าย, 1 = ขวา, -1 = ไม่ได้ยก
        public float x = .5f, y = .5f;   // ตำแหน่งมือที่เล็งบนจอของตัวเอง (0..1, y = 1 คือบนสุด)
        public float[] kp;               // จุดกระดูก 17 จุด จาก YOLO (ตัว + ขา) [x, y, ความมั่นใจ] x 17 (0..1 ของภาพกล้อง, y ลงล่าง)
        public TrackedHand handL = new TrackedHand();   // มือฝั่งซ้ายจอ (= มือซ้ายของผู้เล่น เพราะภาพกลับซ้าย-ขวา) จาก MediaPipe
        public TrackedHand handR = new TrackedHand();   // มือฝั่งขวาจอ
    }

    // มือ 1 ข้างแบบละเอียดจาก MediaPipe (21 จุด)
    // จุด: 0 ข้อมือ | นิ้วโป้ง 1-4 | ชี้ 5-8 | กลาง 9-12 | นาง 13-16 | ก้อย 17-20 (4, 8, 12, 16, 20 = ปลายนิ้ว)
    [Serializable]
    public class TrackedHand
    {
        public bool ok;                  // เห็นมือข้างนี้ไหม
        public string g = "none";        // ท่ามือ: open / fist / point / peace / pinch / other / none
        public int[] f;                  // นิ้วที่เหยียด [โป้ง, ชี้, กลาง, นาง, ก้อย] 1 = เหยียด
        public float pinch = 1;          // ระยะปลายนิ้วโป้ง-ชี้ เทียบขนาดฝ่ามือ (< 0.35 = หยิบ)
        public float px = .5f, py = .5f; // กลางฝ่ามือ (0..1 ของภาพกล้อง, y ลงล่าง)
        public float[] lm;               // 21 จุด [x, y, z] x 21 (x, y = 0..1 ของภาพกล้อง, z = ลึกเทียบข้อมือ)
    }

    // ข้อความ JSON ทั้งก้อนที่ tracker ส่งมา
    [Serializable]
    public class TrackPacket { public float fps; public TrackedPlayer[] players; }

    public class TrackingReceiver : MonoBehaviour
    {
        public static TrackingReceiver I;           // ให้ส่วนอื่นเรียกใช้ได้ทันที
        public int port = 5065;
        public float timeout = 1f;                  // ไม่มีข้อมูลนานกว่านี้ = ถือว่ากล้องไม่ได้ต่อ

        public readonly TrackedPlayer[] Players = { new TrackedPlayer { slot = 1 }, new TrackedPlayer { slot = 2 } };
        public bool Connected { get; private set; } // กำลังได้รับข้อมูลจาก tracker อยู่
        public float TrackerFps { get; private set; }

        UdpClient udp; Thread thread; volatile bool running;
        string latestJson; readonly object jsonLock = new object();
        float lastPacketTime = -99f;
        Vector2 p2Keys = new Vector2(.5f, .5f);

        void Awake() => I = this;

        // เปิด socket แล้วแยก thread ไปรอข้อมูล (การรอจะไม่ทำให้เกมกระตุก)
        void OnEnable()
        {
            I = this;
            if (!Application.isPlaying) return;
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                running = true;
#if UNITY_EDITOR
                // แก้สคริปต์ระหว่าง Play -> Unity โหลดโค้ดใหม่: ปิด socket ก่อน ไม่ให้ค้างพอร์ต
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CloseSocket;
#endif
                thread = new Thread(ReceiveLoop) { IsBackground = true };
                thread.Start();
            }
            catch (Exception e) { Debug.LogWarning("[Tracking] cannot open UDP port: " + e.Message); }
        }

        // (thread แยก) รับข้อความเรื่อย ๆ เก็บไว้เฉพาะข้อความล่าสุด
        void ReceiveLoop()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    var bytes = udp.Receive(ref from);
                    lock (jsonLock) latestJson = Encoding.UTF8.GetString(bytes);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return; }
            }
        }

        // (ทุกเฟรม) แปลง JSON ล่าสุดเป็นข้อมูลผู้เล่น ถ้าไม่มีข้อมูลเลยใช้เมาส์/คีย์บอร์ดแทน
        void Update()
        {
            string json;
            lock (jsonLock) { json = latestJson; latestJson = null; }
            if (json != null)
            {
                try
                {
                    var packet = JsonUtility.FromJson<TrackPacket>(json);
                    if (packet.players != null)
                        foreach (var p in packet.players)
                            if (p.slot == 1 || p.slot == 2) Players[p.slot - 1] = p;
                    TrackerFps = packet.fps;
                    lastPacketTime = Time.unscaledTime;
                }
                catch (Exception e) { Debug.LogWarning("[Tracking] bad packet: " + e.Message); }
            }
            // บอต: F3 = บอตทั้ง 2 คน, Shift+F3 = บอตเฉพาะ P1 (เล่นกับคนจริง)
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
            {
                bool solo = kb.shiftKey.isPressed;
                BotPlayer.On = !(BotPlayer.On && BotPlayer.Both == !solo);
                BotPlayer.Both = !solo;
            }

            // บอตเล่นครบ 2 คน = ไม่ต้องมีกล้อง ถือว่าต่ออยู่
            Connected = Time.unscaledTime - lastPacketTime < timeout || (BotPlayer.On && BotPlayer.Both);
            if (!Connected) UseMouseAndKeys();
            for (int i = 0; i < 2; i++)
                if (BotPlayer.Controls(i)) BotPlayer.Fill(Players[i], i, Time.unscaledDeltaTime);
        }

        void OnGUI()
        {
            if (!BotPlayer.On || !BotPlayer.ShowLabel) return;
            var st = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = new Color(1f, .65f, .2f);
            string who = BotPlayer.Both ? $"BOT P1 {BotPlayer.ProfileName(0)} + P2 {BotPlayer.ProfileName(1)} (F3)" : $"BOT P1 {BotPlayer.ProfileName(0)} (Shift+F3)";
            GUI.Label(new Rect(Screen.width * .5f - 250, Screen.height - 36, 500, 30), Connected ? who : who + " — รอ tracker", st);
        }

        // โหมดทดสอบไม่มีกล้อง: P1 เล็งด้วยเมาส์ (จอซ้าย), P2 เล็งด้วย WASD
        // มือที่ถือปืน = ฝั่งของจอที่ตัวชี้อยู่ (ครึ่งซ้าย = มือซ้าย)
        void UseMouseAndKeys()
        {
            var mouse = Mouse.current; var keys = Keyboard.current;
            var p1 = Players[0]; var p2 = Players[1];
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                p1.present = p1.hand = true;
                p1.x = Mathf.Clamp01(pos.x / (Screen.width * .5f));
                p1.y = Mathf.Clamp01(pos.y / Screen.height);
            }
            if (keys != null)
            {
                Vector2 move = Vector2.zero;
                if (keys.wKey.isPressed) move.y += 1;
                if (keys.sKey.isPressed) move.y -= 1;
                if (keys.dKey.isPressed) move.x += 1;
                if (keys.aKey.isPressed) move.x -= 1;
                p2Keys += move * Time.deltaTime * .7f;
                p2Keys = new Vector2(Mathf.Clamp01(p2Keys.x), Mathf.Clamp01(p2Keys.y));
                p2.present = p2.hand = true; p2.x = p2Keys.x; p2.y = p2Keys.y;
            }
            foreach (var p in Players) p.use = p.x < .5f ? 0 : 1;
        }

        // ออกจาก Play: หยุด thread และปิดพอร์ต
        void OnDisable() => CloseSocket();

        void CloseSocket()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= CloseSocket;
#endif
            running = false;
            try { udp?.Close(); } catch { }
            udp = null;
        }
    }
}
