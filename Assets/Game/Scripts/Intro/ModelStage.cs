// =====================================================================================================
// ModelStage — แสดงโมเดล 3D ของตัวละครบน UI (เรนเดอร์ลง RenderTexture แล้วโชว์ด้วย RawImage)
// -----------------------------------------------------------------------------------------------------
// แต่ละช่อง (P1 / P2) มีกล้อง + ไฟของตัวเอง วางห่างจากฉากจริง ๆ ไม่ให้ไปโผล่ในเกม
// ดึงข้อมูล: Resources/Models/male.prefab, female.prefab (สร้างโดยเมนู RTE > Setup Intro Assets)
// =====================================================================================================
using UnityEngine;
using UnityEngine.UI;

namespace RTE
{
    public class ModelStage
    {
        readonly Transform pivot;
        readonly Camera cam;
        public readonly RawImage Image;
        GameObject current; Hero currentHero = Hero.None;
        float appear;

        public ModelStage(Transform uiParent, int slot, Vector2 size)
        {
            var origin = new Vector3(500 + slot * 50, -500, 0);
            var host = new GameObject($"ModelStage{slot + 1}").transform;
            host.position = origin;
            pivot = new GameObject("Pivot").transform; pivot.SetParent(host, false);

            var rt = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(host, false);
            cam.transform.localPosition = new Vector3(0, 1.0f, -4.6f);
            cam.transform.localRotation = Quaternion.Euler(4, 0, 0);
            cam.fieldOfView = 30; cam.nearClipPlane = .1f; cam.farClipPlane = 20;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.targetTexture = rt; cam.depth = -10;

            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Spot; key.range = 12; key.spotAngle = 60; key.intensity = 30;
            key.transform.SetParent(host, false); key.transform.localPosition = new Vector3(1.5f, 3f, -3f);
            key.transform.LookAt(host.position + Vector3.up);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.type = LightType.Point; rim.range = 8; rim.intensity = 12;
            rim.color = GameSettings.PlayerColors[slot];   // ไฟขอบสีประจำผู้เล่น
            rim.transform.SetParent(host, false); rim.transform.localPosition = new Vector3(-1.5f, 2f, 1.5f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Point; fill.range = 8; fill.intensity = 8;
            fill.transform.SetParent(host, false); fill.transform.localPosition = new Vector3(-1.5f, 1.2f, -2.5f);

            var img = UIKit.Rect($"Model{slot + 1}", uiParent);
            img.sizeDelta = size;
            Image = img.gameObject.AddComponent<RawImage>();
            Image.texture = rt; Image.raycastTarget = false;
        }

        public void Set(Hero hero)
        {
            if (hero == currentHero) return;
            currentHero = hero;
            if (current != null) Object.Destroy(current);
            current = null;
            if (hero == Hero.None) return;
            var prefab = Resources.Load<GameObject>(hero == Hero.Male ? "Models/male" : "Models/female");
            if (prefab == null) { Debug.LogWarning("[ModelStage] missing model prefab – run RTE > Setup Intro Assets"); return; }
            current = Object.Instantiate(prefab, pivot);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.Euler(0, 180, 0);
            // ปรับขนาดให้สูง ~1.8 ม. และเท้าแตะพื้น
            var rs = current.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y > .01f) current.transform.localScale *= 1.8f / b.size.y;
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                current.transform.position += new Vector3(pivot.position.x - b.center.x, pivot.position.y - b.min.y, 0);
            }
            appear = 0;
        }

        public void Tick()
        {
            appear = Mathf.Min(1, appear + Time.deltaTime * 2.5f);
            pivot.localScale = Vector3.one * UIKit.EaseOutBack(appear);
            pivot.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * .6f) * 12, 0);
        }

        // ตำแหน่งเท้า (จุด pivot บนพื้น) ในภาพ RenderTexture: 0 = ล่างสุด, 1 = บนสุด
        // ใช้กระดูกเท้า/นิ้วเท้าจริงของโมเดล (แอนิเมชันอาจยกตัวขึ้น) จุดต่ำสุด = พื้นรองเท้า
        public float FeetViewportY
        {
            get
            {
                var p = pivot.position;
                var an = current != null ? current.GetComponentInChildren<Animator>() : null;
                if (an != null && an.isHuman)
                {
                    float y = float.MaxValue;
                    foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
                    {
                        var t = an.GetBoneTransform(b);
                        if (t != null) y = Mathf.Min(y, t.position.y);
                    }
                    if (y < float.MaxValue) p.y = y - .05f * pivot.localScale.y;
                }
                return cam.WorldToViewportPoint(p).y;
            }
        }

        public void SetActive(bool on) { cam.enabled = on; Image.enabled = on; }
    }
}
