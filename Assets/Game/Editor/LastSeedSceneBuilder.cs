// =====================================================================================================
// LastSeedSceneBuilder — เมนู RTE > Build The Last Seed Scene (ทำงานใน Editor เท่านั้น)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : 1) ตั้งรูปทุกไฟล์ใน Resources/Sprites/seed เป็น Sprite (ไม่ย่อขนาด, ไม่มี mipmap, ขนาดสูงสุด 8192
//              เพราะ tree_atlas กว้าง 6144 พิกเซล)
//           2) สร้างฉากใหม่: กล้องหลัก + GameObject "Game" (TrackingReceiver + LastSeed)
//           3) บันทึกเป็น Assets/Scenes/The Last Seed.unity และเพิ่มลง Build Settings (ต่อจาก Waste Hunters)
//           (ฉาก/UI/เอฟเฟกต์ทั้งหมดถูกสร้างด้วยโค้ดตอนกด Play)
// =====================================================================================================
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTE.EditorTools
{
    public static class LastSeedSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/The Last Seed.unity";
        const string SpriteFolder = "Assets/Game/Resources/Sprites/seed";

        [MenuItem("RTE/Build The Last Seed Scene")]
        public static void Build()
        {
            ImportSprites();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 5.4f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var game = new GameObject("Game");
            game.AddComponent<TrackingReceiver>();
            game.AddComponent<LastSeed>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[RTE] Built " + ScenePath);
        }

        [MenuItem("RTE/Reimport The Last Seed Sprites")]
        public static void ImportSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                bool strip = path.Contains("_strip");
                bool same = ti.textureType == TextureImporterType.Sprite && !ti.mipmapEnabled && ti.maxTextureSize == 8192
                            && ti.wrapMode == (strip ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
                if (same) continue;
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
                ti.maxTextureSize = 8192;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.wrapMode = strip ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
        }
    }
}
