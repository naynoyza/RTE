// =====================================================================================================
// IntroAssetSetup — เมนู RTE > Setup Intro Assets (Editor เท่านั้น)
// -----------------------------------------------------------------------------------------------------
// 1) ตั้งรูปใน Resources/Sprites/intro และ tut ให้เป็น Sprite
// 2) ตั้ง FBX ตัวละคร (Assets/Game/Models/Male|Female) เป็น Humanoid + แอนิเมชันวนซ้ำ
// 3) สร้าง Material (URP Lit + albedo/normal/metallic) และ AnimatorController ที่เล่นคลิปในไฟล์
// 4) บันทึก prefab เป็น Resources/Models/male.prefab, female.prefab (ModelStage โหลดตอนเล่น)
// =====================================================================================================
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RTE.EditorTools
{
    public static class IntroAssetSetup
    {
        [MenuItem("RTE/Setup Intro Assets")]
        public static void Setup()
        {
            foreach (var folder in new[] { "Assets/Game/Resources/Sprites/intro", "Assets/Game/Resources/Sprites/tut" })
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (ti.textureType == TextureImporterType.Sprite && !ti.mipmapEnabled && ti.maxTextureSize >= 2048) continue;
                    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                    ti.mipmapEnabled = false; ti.maxTextureSize = 2048; ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
            BuildHero("male"); BuildHero("female");
            AssetDatabase.SaveAssets();
            Debug.Log("[RTE] Intro assets ready");
        }

        static void BuildHero(string id)
        {
            string dir = "Assets/Game/Models/" + char.ToUpper(id[0]) + id.Substring(1);
            string fbx = $"{dir}/{id}.fbx";

            var normal = (TextureImporter)AssetImporter.GetAtPath($"{dir}/{id}_normal.png");
            if (normal != null && normal.textureType != TextureImporterType.NormalMap) { normal.textureType = TextureImporterType.NormalMap; normal.SaveAndReimport(); }
            foreach (var lin in new[] { "metallic", "roughness" })
            {
                var t = (TextureImporter)AssetImporter.GetAtPath($"{dir}/{id}_{lin}.png");
                if (t != null && t.sRGBTexture) { t.sRGBTexture = false; t.SaveAndReimport(); }
            }

            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (mi == null) { Debug.LogError("[RTE] missing " + fbx); return; }
            bool dirty = false;
            if (mi.animationType != ModelImporterAnimationType.Human) { mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
            var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            foreach (var c in clips) if (!c.loopTime) { c.loopTime = true; dirty = true; }
            if (dirty) { mi.clipAnimations = clips; mi.SaveAndReimport(); }

            // Material
            string matPath = $"{dir}/{id}_mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_albedo.png");
            mat.SetTexture("_BaseMap", albedo); mat.SetTexture("_MainTex", albedo);
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{id}_normal.png");
            if (nm != null) { mat.SetTexture("_BumpMap", nm); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetFloat("_Smoothness", .45f); mat.SetFloat("_Metallic", .2f);
            EditorUtility.SetDirty(mat);

            // Animator controller
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            string ctrlPath = $"{dir}/{id}.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            if (clip != null) ctrl.layers[0].stateMachine.AddState("Idle").motion = clip;

            // Prefab
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(go, $"Assets/Game/Resources/Models/{id}.prefab");
            Object.DestroyImmediate(go);
            Debug.Log($"[RTE] hero {id}: clip={(clip ? clip.name : "none")}");
        }
    }
}
