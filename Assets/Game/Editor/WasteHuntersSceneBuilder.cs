// =====================================================================================================
// WasteHuntersSceneBuilder — เมนู RTE > Build Waste Hunters Scene (ทำงานใน Editor เท่านั้น)
// -----------------------------------------------------------------------------------------------------
// หน้าที่ : 1) ตั้งรูปทุกไฟล์ใน Resources/Sprites/wh ให้เป็น Sprite
//           2) สร้างฉากใหม่: กล้องหลัก + GameObject "Game" (TrackingReceiver + WasteHunters)
//           3) บันทึกเป็น Assets/Scenes/WasteHunters.unity และตั้งเป็นฉากแรกของ Build
//           (UI ทั้งหมดถูกสร้างด้วยโค้ดตอนกด Play จึงไม่ต้องจัดวางในฉาก)
// =====================================================================================================
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RTE.EditorTools
{
    public static class WasteHuntersSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/WasteHunters.unity";

        [MenuItem("RTE/Build Waste Hunters Scene")]
        public static void Build()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Resources/Sprites/wh" }))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (ti.textureType == TextureImporterType.Sprite && !ti.mipmapEnabled) continue;
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false; ti.maxTextureSize = 4096;
                ti.SaveAndReimport();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera"; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var game = new GameObject("Game");
            game.AddComponent<TrackingReceiver>();
            game.AddComponent<WasteHunters>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[RTE] Built " + ScenePath);
        }
    }
}
