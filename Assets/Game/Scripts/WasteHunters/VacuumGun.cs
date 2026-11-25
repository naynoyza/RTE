// =====================================================================================================
// VacuumGun — ปืนดูดฝุ่น 3D ในมือผู้เล่น 1 คน
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : แสดงโมเดลแขน+ปืนจาก Meshy (Resources/Models/gun_p{slot}.fbx + gun_p{slot}_mat)
//           ถ้าไม่มีไฟล์ ใช้ปืนที่ประกอบจากชิ้นรูปทรงพื้นฐานแทน (ตัวปืน ปากดูด ใบพัด ถังฝุ่นเรืองแสง ด้าม มือ สายท่อ)
//           มีกล้องของตัวเองถ่ายปืนลง RenderTexture (พื้นหลังโปร่งใส) แล้วแสดงทับจอผู้เล่นด้วย RawImage
//           • ยกมือซ้าย = ปืนอยู่มุมล่างซ้าย, มือขวา = มุมล่างขวา, ไม่ยก = ปืนลดลง
//           • หันปากปืนตามเป้าเล็ง (จำกัดมุมให้ดูเหมือนถืออยู่ตลอด)
//           • ตอนดูด ไฟสีผู้เล่นสว่างขึ้น (ปืนรูปทรงพื้นฐาน: ใบพัดหมุนเร็ว ถังเรืองแสง) ปืนสั่นเล็กน้อย
//           • โมเดล Meshy เป็นมือข้างเดียว (NativeSide) — ถือด้วยมืออีกข้างจะกลับด้านซ้าย-ขวาให้
// ดึงข้อมูล: ตำแหน่งเป้าเล็ง + มือที่ใช้ + กำลังดูดไหม (จาก PlayerLane)
// ส่งออก  : NozzleLocal = ตำแหน่งปากปืนบนจอ (จุดเริ่มลำแสงและจุดที่ขยะลอยเข้า)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class VacuumGun
    {
        readonly Camera cam;
        readonly Transform root, fan, nozzleTip;
        readonly Material glowMat;
        readonly Light fillLight;
        readonly Color col;
        float side = 1, sideTarget = 1, raise, suck, fanAngle;

        // ---- โมเดล Meshy (ปรับได้ที่นี่)
        Transform model, mirror;                                // mirror = จุดหมุนสำหรับกลับด้านซ้าย-ขวา
        int nativeSide = 1;                                     // โมเดลเป็นมือขวา (+1) หรือซ้าย (-1)
        static readonly Vector3 ModelEuler = new Vector3(0, -90, 0); // ปากปืนในไฟล์ชี้ +X -> หมุนให้ชี้ +Z
        const float ModelLength = .68f;                         // ความยาวทั้งชิ้น ปืน+แขน (เมตร) — ทดสอบในเกมแล้ว ใหญ่กว่านี้บังจอ
        static readonly Vector2 GripAt = new Vector2(.12f, -.12f);   // จุดจับ เทียบกลางชิ้น (สัดส่วน: แกนยาว, แกนสูง)
        static readonly Vector2 NozzleAt = new Vector2(.5f, .12f);   // ปากปืน เทียบกลางชิ้น (สัดส่วน)

        const float W = 960, H = 1080;

        // สร้างกล้อง + ไฟ + โมเดลปืน
        public VacuumGun(RectTransform view, int slot, Color col)
        {
            this.col = col;
            var origin = new Vector3(1000 * slot, 0, 0);   // each gun lives far from the other, so each camera only sees its own

            var camGo = new GameObject($"P{slot}GunCamera");
            camGo.transform.position = origin;
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 55; cam.nearClipPlane = .05f; cam.farClipPlane = 20;
            var rt = new RenderTexture((int)W, (int)H, 24, RenderTextureFormat.ARGB32) { name = $"P{slot}Gun", antiAliasing = 4 };
            cam.targetTexture = rt;

            var img = UIKit.Rect("Gun3D", view); UIKit.Stretch(img);
            var raw = img.gameObject.AddComponent<RawImage>(); raw.texture = rt; raw.raycastTarget = false;

            var light = new GameObject("GunLight").AddComponent<Light>();
            light.type = LightType.Point; light.range = 4; light.intensity = 2.2f; light.color = new Color(1f, .85f, .7f);
            light.transform.position = origin + new Vector3(.3f, .6f, .2f);
            var fill = new GameObject("GunFill").AddComponent<Light>();
            fill.type = LightType.Point; fill.range = 3; fill.intensity = 1.2f; fill.color = col;
            fill.transform.position = origin + new Vector3(-.4f, -.2f, .6f);
            fillLight = fill;

            // ---------------- model (metres; +Z = nozzle direction, pivot = top of the grip)
            root = new GameObject($"P{slot}VacuumGun").transform;
            glowMat = Mat(col, 0, .9f); glowMat.EnableKeyword("_EMISSION");
            nozzleTip = new GameObject("NozzleTip").transform; nozzleTip.SetParent(root, false);
            if (BuildMeshyModel(slot)) { root.position = origin; return; }

            var metal = Mat(new Color(.42f, .40f, .38f), .85f, .55f);
            var dark = Mat(new Color(.13f, .13f, .14f), .2f, .35f);
            var accent = Mat(col, .3f, .6f);
            var skin = Mat(new Color(.96f, .72f, .58f), 0, .35f);

            Part(PrimitiveType.Cylinder, "Body", metal, new Vector3(0, .02f, .10f), new Vector3(90, 0, 0), new Vector3(.10f, .17f, .10f));
            Part(PrimitiveType.Cylinder, "Band", accent, new Vector3(0, .02f, .02f), new Vector3(90, 0, 0), new Vector3(.108f, .02f, .108f));
            Part(PrimitiveType.Cylinder, "Neck", metal, new Vector3(0, .02f, .29f), new Vector3(90, 0, 0), new Vector3(.12f, .03f, .12f));
            Part(PrimitiveType.Cylinder, "Mouth", metal, new Vector3(0, .02f, .34f), new Vector3(90, 0, 0), new Vector3(.17f, .025f, .17f));
            Part(PrimitiveType.Cylinder, "MouthInner", dark, new Vector3(0, .02f, .362f), new Vector3(90, 0, 0), new Vector3(.145f, .004f, .145f));
            Part(PrimitiveType.Cylinder, "GlowRing", glowMat, new Vector3(0, .02f, .366f), new Vector3(90, 0, 0), new Vector3(.155f, .002f, .155f));
            fan = new GameObject("Fan").transform; fan.SetParent(root, false); fan.localPosition = new Vector3(0, .02f, .365f);
            for (int i = 0; i < 3; i++)
            {
                var b = Part(PrimitiveType.Cube, "Blade", metal, Vector3.zero, new Vector3(0, 0, i * 60), new Vector3(.13f, .018f, .004f));
                b.SetParent(fan, false); b.localPosition = new Vector3(0, 0, .004f);
            }
            // glowing dust tank lying on top of the barrel
            Part(PrimitiveType.Cylinder, "DustTank", glowMat, new Vector3(0, .085f, .07f), new Vector3(90, 0, 0), new Vector3(.07f, .07f, .07f));
            Part(PrimitiveType.Cylinder, "TankCapF", metal, new Vector3(0, .085f, .145f), new Vector3(90, 0, 0), new Vector3(.078f, .008f, .078f));
            Part(PrimitiveType.Cylinder, "TankCapB", metal, new Vector3(0, .085f, -.005f), new Vector3(90, 0, 0), new Vector3(.078f, .008f, .078f));
            Part(PrimitiveType.Cube, "Rail", accent, new Vector3(0, -.035f, .12f), Vector3.zero, new Vector3(.03f, .02f, .22f));
            Part(PrimitiveType.Cube, "Grip", dark, new Vector3(0, -.09f, -.02f), new Vector3(18, 0, 0), new Vector3(.045f, .13f, .06f));
            Part(PrimitiveType.Cube, "Trigger", accent, new Vector3(0, -.06f, .03f), new Vector3(-10, 0, 0), new Vector3(.015f, .04f, .015f));
            Part(PrimitiveType.Sphere, "Fist", skin, new Vector3(0, -.09f, -.03f), Vector3.zero, new Vector3(.085f, .095f, .09f));
            Part(PrimitiveType.Capsule, "Forearm", skin, new Vector3(0, -.2f, -.17f), new Vector3(55, 0, 0), new Vector3(.075f, .13f, .075f));
            for (int i = 0; i < 7; i++)   // hose to the backpack
            {
                float t = i / 6f;
                Part(PrimitiveType.Sphere, "Hose", dark, new Vector3(0, .0f - t * .22f, -.04f - t * .2f), Vector3.zero, Vector3.one * .055f);
            }
            nozzleTip.localPosition = new Vector3(0, .02f, .37f);
            root.position = origin;
        }

        // โหลดโมเดลแขน+ปืนจาก Meshy: ย่อให้ยาว ModelLength, เลื่อนให้จุดจับอยู่ที่จุดหมุนของ root, หาตำแหน่งปากปืน
        // ใช้ mesh ของผู้เล่นคนนั้นถ้ามี ไม่มีก็ใช้ mesh ของ P1 กับวัสดุสีของตัวเอง — คืนค่า false ถ้าไม่มีไฟล์เลย
        bool BuildMeshyModel(int slot)
        {
            var prefab = Resources.Load<GameObject>($"Models/gun_p{slot}");
            bool own = prefab != null;
            if (!own) prefab = Resources.Load<GameObject>("Models/gun_p1");
            var mat = Resources.Load<Material>($"Models/gun_p{slot}_mat");
            if (prefab == null || mat == null) return false;
            nativeSide = 1;                                      // โมเดลทั้งสองสีจาก Meshy ออกมาในทิศเดียวกัน (มือขวา)

            model = Object.Instantiate(prefab).transform;
            model.name = "MeshyArmGun";
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            mirror = new GameObject("Mirror").transform; mirror.SetParent(root, false);
            model.SetParent(mirror, false);
            model.localRotation = Quaternion.Euler(ModelEuler);
            var b = Bounds(model);
            model.localScale *= ModelLength / Mathf.Max(.001f, b.size.z);
            b = Bounds(model);
            var grip = new Vector3(b.center.x, b.center.y + GripAt.y * b.size.y, b.center.z + GripAt.x * b.size.z);
            model.localPosition -= grip;
            nozzleTip.localPosition = new Vector3(b.center.x, b.center.y + NozzleAt.y * b.size.y, b.center.z + NozzleAt.x * b.size.z) - grip;
            return true;
        }

        // กรอบรวมของทุกชิ้นใน root (root ยังอยู่ที่ 0,0,0 ไม่หมุน ตอนเรียก)
        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // สร้างวัสดุ (สี, ความเป็นโลหะ, ความเงา) ด้วย shader ของ URP
        static Material Mat(Color c, float metallic, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Glossiness", smooth);
            return m;
        }

        // สร้างชิ้นส่วนปืน 1 ชิ้น (กล่อง/ทรงกระบอก/ทรงกลม) ลบตัวชน ตั้งวัสดุ ตำแหน่ง มุม ขนาด
        Transform Part(PrimitiveType type, string name, Material m, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var t = go.transform; t.SetParent(root, false);
            t.localPosition = pos; t.localEulerAngles = euler; t.localScale = scale;
            return t;
        }

        /// aimLocal: crosshair in the lane's local pixels (centre = 0,0). hand: 0 left, 1 right, -1 none (gun lowered).
        // (ทุกเฟรม) รับตำแหน่งเป้าเล็งและมือที่ใช้: ย้ายปืนไปมุมล่างฝั่งมือนั้น,
        //   ยกขึ้นเมื่อยกมือ/ลดลงเมื่อไม่ยก, หันปากปืนตามเป้า (จำกัดมุม), ตอนดูดใบพัดหมุนเร็วและเรืองแสง
        public void Update(Vector2 aimLocal, int hand, bool sucking, float bob, float dt)
        {
            if (hand >= 0) sideTarget = hand == 0 ? -1 : 1;
            side = Mathf.MoveTowards(side, sideTarget, dt * 6);
            raise = Mathf.MoveTowards(raise, hand >= 0 ? 1 : 0, dt * 4);
            suck = Mathf.MoveTowards(suck, sucking ? 1 : 0, dt * 5);

            // rest point in the camera's view: lower corner on the side of the hand that holds it
            var c = cam.transform;
            float depth = .95f;
            Vector3 rest = cam.ViewportToWorldPoint(new Vector3(.5f + .29f * side, Mathf.Lerp(-.12f, .06f, raise) + bob / H, depth));
            rest += Random.insideUnitSphere * .002f * suck;   // a little rumble while sucking
            root.position = rest;

            // point at the crosshair, but keep the tilt small so it always reads as a gun held in front of you
            Vector2 vp = new Vector2((aimLocal.x + W / 2) / W, (aimLocal.y + H / 2) / H);
            Vector3 target = hand >= 0 ? cam.ViewportPointToRay(vp).GetPoint(7f) : rest + c.forward * 3 - c.up * 1.5f;
            var d = c.InverseTransformDirection((target - rest).normalized);
            float yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -32, 32);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(d.y, -1, 1)) * Mathf.Rad2Deg, -22, 28);
            var want = c.rotation * Quaternion.Euler(pitch, yaw, -side * 10);
            root.rotation = Quaternion.Slerp(root.rotation, want, 1 - Mathf.Exp(-dt * 14));

            fillLight.intensity = 1.2f + 3.5f * suck;           // ดูดอยู่ = ไฟสีผู้เล่นสว่างขึ้น
            if (model != null)
            {
                // ถือด้วยมืออีกข้าง = กลับด้านโมเดล (ทำตอนปืนลดลงต่ำสุด จะได้ไม่เห็นการพลิก)
                float wantX = sideTarget * nativeSide >= 0 ? 1 : -1;
                if (mirror.localScale.x != wantX && (raise < .05f || Mathf.Abs(side - sideTarget) > .5f))
                    mirror.localScale = new Vector3(wantX, 1, 1);
                return;
            }
            fanAngle += dt * (180 + 1800 * suck);
            fan.localEulerAngles = new Vector3(0, 0, fanAngle);
            glowMat.SetColor("_EmissionColor", col * (0.6f + 4f * suck));
        }

        /// Where the nozzle is, in the lane's local pixels (for the suction beam).
        // ตำแหน่งปากปืนบนจอ (พิกเซล) ใช้เป็นจุดเริ่มลำแสงและจุดที่ขยะลอยเข้า
        public Vector2 NozzleLocal
        {
            get
            {
                var v = cam.WorldToViewportPoint(nozzleTip.position);
                return new Vector2(v.x * W - W / 2, v.y * H - H / 2);
            }
        }
    }
}
