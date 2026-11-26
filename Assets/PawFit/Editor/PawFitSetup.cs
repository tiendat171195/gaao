using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PawFit.EditorTools
{
    /// <summary>
    /// Menu PawFit ▸ Setup Project: tạo + gán URP Asset, tạo scene Main, đặt tên sản phẩm.
    /// Chạy 1 lần sau khi mở project lần đầu (chạy lại cũng không sao).
    /// </summary>
    public static class PawFitSetup
    {
        private const string Root = "Assets/PawFit";
        private const string SettingsDir = Root + "/Settings";
        private const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("PawFit/Setup Project")]
        public static void Setup()
        {
            EnsureFolder(Root, "Settings");
            EnsureFolder(Root, "Scenes");
            SetupUrp();
            SetupScene();
            PlayerSettings.productName = "PawFit";
            PlayerSettings.companyName = "PawFit";
            AssetDatabase.SaveAssets();
            Debug.Log("[PawFit] Setup xong: URP + scene Main. Bấm Play để chạy, xem Console.");
        }

        private static void SetupUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
            {
                Debug.Log("[PawFit] Project đã dùng URP.");
                return;
            }
            string rendererPath = SettingsDir + "/PawFit_URP_Renderer.asset";
            string assetPath = SettingsDir + "/PawFit_URP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, assetPath);
            }
            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)   // gán cho mọi mức chất lượng
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("[PawFit] Đã tạo và gán URP Asset: " + assetPath);
        }

        private static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
