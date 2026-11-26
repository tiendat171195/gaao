using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PawFit.EditorTools
{
    /// <summary>
    /// Menu PawFit ▸ Setup Project (chạy lại bao nhiêu lần cũng được):
    ///  1. Tạo + gán URP Asset (kèm dữ liệu hậu kỳ để Bloom / Vignette chạy).
    ///  2. Tạo material gốc Resources/PawFit/LitBase (để bản build có shader URP Lit cho đồ vật dựng bằng code).
    ///  3. Bật Input cũ (Active Input Handling = Both) — điều khiển chạm / chuột đang dùng Input Manager.
    ///  4. Cho phép xoay màn dọc + ngang (vườn dọc, đấu ngang — game tự khóa hướng theo từng màn).
    ///  5. Tạo scene Main + đưa vào Build Settings.
    /// </summary>
    public static class PawFitSetup
    {
        private const string Root = "Assets/PawFit";
        private const string SettingsDir = Root + "/Settings";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string LitBasePath = Root + "/Resources/PawFit/LitBase.mat";

        [MenuItem("PawFit/Setup Project")]
        public static void Setup()
        {
            EnsureFolder(Root, "Settings");
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Resources");
            EnsureFolder(Root + "/Resources", "PawFit");
            SetupUrp();
            SetupMaterials();
            SetupOrientation();
            bool needRestart = SetupInputHandling();
            SetupScene();
            PlayerSettings.productName = "PawFit";
            PlayerSettings.companyName = "PawFit";
            AssetDatabase.SaveAssets();
            Debug.Log("[PawFit] Setup xong. Đặt Game view 1080x1920 (dọc), mở scene Main rồi bấm Play.");
            if (needRestart)
                EditorUtility.DisplayDialog("PawFit", "Đã bật Input cũ (Active Input Handling = Both).\nHãy khởi động lại Unity để áp dụng.", "OK");
        }

        private static void SetupUrp()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            string rendererPath = SettingsDir + "/PawFit_URP_Renderer.asset";
            if (urp == null)
            {
                string assetPath = SettingsDir + "/PawFit_URP.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
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
            TryAssignPostProcessData(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath));
        }

        /// <summary>
        /// Renderer tạo bằng script có thể thiếu dữ liệu hậu kỳ (khi đó Bloom / Vignette không chạy).
        /// Dùng reflection để không lỗi compile nếu bản URP đổi tên trường.
        /// </summary>
        private static void TryAssignPostProcessData(UniversalRendererData renderer)
        {
            if (renderer == null) return;
            FieldInfo field = typeof(UniversalRendererData).GetField("postProcessData", BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.GetValue(renderer) as Object != null) return;
            var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (data == null || !field.FieldType.IsInstanceOfType(data)) return;
            field.SetValue(renderer, data);
            EditorUtility.SetDirty(renderer);
            Debug.Log("[PawFit] Đã gán PostProcessData cho renderer.");
        }

        private static void SetupMaterials()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(LitBasePath) != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogWarning("[PawFit] Không tìm thấy shader URP Lit — kiểm tra URP đã cài và đang được dùng chưa.");
                return;
            }
            AssetDatabase.CreateAsset(new Material(lit), LitBasePath);
            Debug.Log("[PawFit] Đã tạo " + LitBasePath);
        }

        private static void SetupOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        /// <summary>Trả true nếu vừa đổi cài đặt (cần khởi động lại Unity).</summary>
        private static bool SetupInputHandling()
        {
            Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0) return false;
            var so = new SerializedObject(settings[0]);
            SerializedProperty p = so.FindProperty("activeInputHandler");   // 0 = cũ, 1 = mới, 2 = cả hai
            if (p == null || p.intValue != 1) return false;
            p.intValue = 2;
            so.ApplyModifiedProperties();
            return true;
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
