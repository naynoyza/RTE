// =====================================================================================================
// SeedKit — ตัวช่วยสร้างฉาก 2D และเอฟเฟกต์ของภารกิจที่ 2 ด้วยโค้ด
// -----------------------------------------------------------------------------------------------------
// ระบบพิกัด: กล้อง Orthographic size 5.4 -> จอ 1920x1080 พิกเซล = 19.2 x 10.8 หน่วย (100 พิกเซล = 1 หน่วย)
//            Px(x, y) แปลงพิกัดภาพอ้างอิง (มุมบนซ้าย = 0,0) เป็นตำแหน่งในโลก
// ดึงข้อมูล: รูปจาก Resources/Sprites/seed/*, shader จาก Resources/Shaders/SeedFxShader (RTE/SeedFx)
//            ตำแหน่งชิ้นส่วนที่ตัดจาก Ref จาก Resources/Sprites/seed/layout.json (สร้างโดย Tools/make_seed_sprites.py)
// ใช้โดย  : TreeView, GuardianView, LastSeed
// =====================================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTE
{
    public static class SeedKit
    {
        // ---------------------------------------------------------------- ลำดับการซ้อน (มาก = อยู่บน)
        public const int OrderBg = 0, OrderGround = 5, OrderTreeGlow = 8, OrderTree = 10, OrderTreeFx = 15,
                         OrderStream = 20, OrderArms = 30, OrderHandFx = 40;

        public static Vector3 Px(float x, float y) => new Vector3((x - 960f) / 100f, (540f - y) / 100f, 0);

        // ---------------------------------------------------------------- layout.json
        [Serializable] class Item { public string name; public float x, y, w, h; }
        [Serializable] class Layout { public Item[] items; }
        static Dictionary<string, Rect> layout;

        /// กรอบของชิ้นส่วน (x,y = มุมบนซ้าย, หน่วยพิกเซลจอ 1920x1080)
        public static Rect Box(string name)
        {
            if (layout == null)
            {
                layout = new Dictionary<string, Rect>();
                var json = Resources.Load<TextAsset>("Sprites/seed/layout");
                if (json != null)
                    foreach (var it in JsonUtility.FromJson<Layout>(json.text).items)
                        layout[it.name] = new Rect(it.x, it.y, it.w, it.h);
            }
            return layout.TryGetValue(name, out var r) ? r : new Rect(0, 0, 100, 100);
        }

        /// จุดกึ่งกลางของชิ้นส่วนในโลก
        public static Vector3 BoxCenter(string name) { var r = Box(name); return Px(r.center.x, r.center.y); }

        // ---------------------------------------------------------------- texture / sprite / material
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static Shader shader;

        public static Texture2D Tex(string name, bool repeat = false)
        {
            if (!textures.TryGetValue(name, out var t) || t == null)
            {
                t = Resources.Load<Texture2D>("Sprites/seed/" + name);
                if (t == null) { Debug.LogWarning("[SeedKit] missing texture " + name); t = Texture2D.whiteTexture; }
                textures[name] = t;
            }
            if (repeat) t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        public static Sprite Spr(string name, Vector2 pivot, float pixelsPerUnit = 100f)
        {
            var t = Tex(name);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        public static Material Mat(string texName, bool additive, Color tint, float scroll = 0)
        {
            if (shader == null) shader = Shader.Find("RTE/SeedFx");
            var m = new Material(shader) { mainTexture = texName == null ? Texture2D.whiteTexture : Tex(texName, scroll != 0) };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Scroll", scroll);
            m.color = tint;
            return m;
        }

        // ---------------------------------------------------------------- GameObject
        public static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, int order, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.sharedMaterial = Mat(null, additive, Color.white);
            return sr;
        }

        /// ParticleSystem เปล่า ๆ (ยังไม่ปล่อยอนุภาค) ให้ผู้เรียกตั้งค่าโมดูลเพิ่มเอง แล้วสั่ง Play
        public static ParticleSystem Particles(string name, Transform parent, string tex, bool additive, int order,
                                               float lifetime, float size, float speed, Color color, int max = 400)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .7f, lifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .6f, speed);
            main.startColor = color;
            main.maxParticles = max;
            var em = ps.emission; em.rateOverTime = 0;
            var shape = ps.shape; shape.enabled = false;
            // จางเข้า-จางออก และหดเล็กลงตอนท้าย = เอฟเฟกต์ลื่น ไม่โผล่/หายวูบ
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, .3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat(tex, additive, Color.white);
            r.sortingOrder = order;
            ps.Play();
            return ps;
        }

        public static void Rate(ParticleSystem ps, float perSecond) { var em = ps.emission; em.rateOverTime = perSecond; }

        public static LineRenderer Line(string name, Transform parent, string tex, bool additive, int order, Color color, float scroll, int points = 28)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = points;
            lr.textureMode = LineTextureMode.Tile;
            lr.sharedMaterial = Mat(tex, additive, color, scroll);
            lr.sharedMaterial.mainTextureScale = new Vector2(.35f, 1);
            lr.sortingOrder = order;
            lr.numCapVertices = 6;
            lr.alignment = LineAlignment.View;
            lr.enabled = false;
            return lr;
        }

        /// เด้งแบบนุ่ม: เข้าหาค่าเป้าหมายด้วยความเร็ว k (ไม่ขึ้นกับ frame rate)
        public static float Damp(float current, float target, float k, float dt) => Mathf.Lerp(current, target, 1 - Mathf.Exp(-k * dt));
        public static Vector3 Damp(Vector3 current, Vector3 target, float k, float dt) => Vector3.Lerp(current, target, 1 - Mathf.Exp(-k * dt));
    }
}
