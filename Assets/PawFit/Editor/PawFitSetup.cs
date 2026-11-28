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
    ///  2. Tạo material gốc Resources/PawFit/LitBase (để bản build có shader URP Lit cho đồ vật dựng bằng code)
    ///     + Resources/PawFit/GlowBase (URP Unlit: đom đóm, đèn tự sáng ban đêm, Phase 10.1).
    ///  3. Đặt Active Input Handling = Input Manager (Old): điều khiển chạm / chuột dùng Input Manager, project KHÔNG cài
    ///     package Input System. Unity 6.3 báo lỗi "Active Input Handling is invalid" nếu chọn New / Both mà thiếu package đó
    ///     (đã soát với mã nguồn Unity 6000.3); còn cảnh báo vàng "Input Manager is a legacy feature" thì bỏ qua được.
    ///  4. Cho phép xoay màn dọc + ngang (vườn dọc, đấu ngang — game tự khóa hướng theo từng màn).
    ///  5. Tạo scene Main + đưa vào Build Settings.
    /// </summary>
    public static class PawFitSetup
    {
        private const string Root = "Assets/PawFit";
        private const string SettingsDir = Root + "/Settings";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string LitBasePath = Root + "/Resources/PawFit/LitBase.mat";
        private const string GlowBasePath = Root + "/Resources/PawFit/GlowBase.mat";

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
            NotificationSetup.Apply();   // icon thông báo Android + giữ lịch khi khởi động lại máy (Phase 4.5)
            PlayerSettings.productName = "PawFit";
            PlayerSettings.companyName = "PawFit";
            AssetDatabase.SaveAssets();
            Debug.Log("[PawFit] Setup xong. Đặt Game view 1080x1920 (dọc), mở scene Main rồi bấm Play.");
            if (needRestart)
                EditorUtility.DisplayDialog("PawFit", "Đã đặt Active Input Handling = Input Manager (Old).\nHãy khởi động lại Unity để áp dụng.", "OK");
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

        /// <summary>Tạo các material gốc còn thiếu (chạy lại không sao; menu Build cũng gọi để bản build có đủ shader).</summary>
        public static void SetupMaterials()
        {
            EnsureFolder(Root, "Resources");
            EnsureFolder(Root + "/Resources", "PawFit");
            EnsureMaterial(LitBasePath, "Universal Render Pipeline/Lit");
            EnsureMaterial(GlowBasePath, "Universal Render Pipeline/Unlit");
        }

        private static void EnsureMaterial(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[PawFit] Không tìm thấy shader " + shaderName + " — kiểm tra URP đã cài và đang được dùng chưa.");
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
            Debug.Log("[PawFit] Đã tạo " + path);
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
            if (p == null || p.intValue == 0) return false;   // không có package Input System => chỉ 0 là hợp lệ
            p.intValue = 0;
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
