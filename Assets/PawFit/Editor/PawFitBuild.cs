using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace PawFit.EditorTools
{
    /// <summary>
    /// Menu PawFit ▸ Build Android (Phase 5.3): đặt Player Settings cho Android rồi build ra file APK trong thư mục Builds/Android.
    ///  * Dev: có bảng DEV + quảng cáo giả lập (để test trên điện thoại).
    ///  * Release: không có bảng DEV, không có quảng cáo (chưa có SDK quảng cáo thật).
    /// Ký bằng debug keystore của Unity: cài thẳng vào máy được, chưa đưa lên Google Play được.
    /// </summary>
    public static class PawFitBuild
    {
        /// <summary>Tên gói tạm (gần như cố định sau khi lên store — đổi trước khi phát hành nếu có tên riêng).</summary>
        public const string PackageName = "com.pawfit.game";
        public const string Version = "0.5.3";
        public const int VersionCode = 53;

        private const string ScenePath = "Assets/PawFit/Scenes/Main.unity";
        private const string LitBasePath = "Assets/PawFit/Resources/PawFit/LitBase.mat";
        private const string IconPath = "Assets/PawFit/Art/Icon/PawFit_Icon.png";   // thú Gen 1 đeo nơ, render bằng Blender
        private const string OutDir = "Builds/Android";

        [MenuItem("PawFit/Build Android/Dev (APK)")]
        public static void BuildDev() => Build(true, false);

        [MenuItem("PawFit/Build Android/Dev + chạy trên điện thoại (USB)")]
        public static void BuildDevAndRun() => Build(true, true);

        [MenuItem("PawFit/Build Android/Release (APK)")]
        public static void BuildRelease() => Build(false, false);

        private static void Build(bool dev, bool run)
        {
            if (!Ready(out string problem))
            {
                EditorUtility.DisplayDialog("PawFit · chưa build được", problem, "OK");
                return;
            }
            ConfigureAndroid();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("PawFit", "Không chuyển được sang Android. Mở File ▸ Build Profiles, chọn Android ▸ Switch Platform rồi thử lại.", "OK");
                return;
            }
            EditorUserBuildSettings.buildAppBundle = false;   // APK để cài thẳng vào máy (AAB chỉ để lên Google Play)
            Directory.CreateDirectory(OutDir);
            BuildOptions options = dev ? BuildOptions.Development : BuildOptions.None;
            if (run) options |= BuildOptions.AutoRunPlayer;
            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(OutDir, dev ? "PawFit-dev.apk" : "PawFit.apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
            };
            Debug.Log("[PawFit] Bắt đầu build Android " + (dev ? "Dev" : "Release") + " → " + buildOptions.locationPathName +
                      " (lần đầu IL2CPP mất 5–15 phút)");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[PawFit] Build xong: {s.outputPath} · {s.totalSize / (1024.0 * 1024.0):0.0} MB · {s.totalTime.TotalMinutes:0.0} phút");
                if (!run) EditorUtility.RevealInFinder(s.outputPath);
            }
            else
            {
                Debug.LogError("[PawFit] Build Android lỗi (" + s.result + ", " + s.totalErrors + " lỗi). Gửi tôi các dòng đỏ phía trên trong Console.");
            }
        }

        /// <summary>Đã chạy Setup Project và đã cài module Android chưa.</summary>
        private static bool Ready(out string problem)
        {
            problem = null;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                problem = "Unity này chưa cài Android Build Support.\nUnity Hub ▸ Installs ▸ bản 6.3 ▸ Add modules ▸ tick Android Build Support " +
                          "(kèm OpenJDK + Android SDK & NDK Tools) rồi mở lại project.";
            else if (!File.Exists(ScenePath) || !File.Exists(LitBasePath) || GraphicsSettings.defaultRenderPipeline == null)
                problem = "Chưa chạy menu PawFit ▸ Setup Project (cần scene Main, material LitBase, URP Asset). Chạy xong thì build lại.";
            return problem == null;
        }

        private static void ConfigureAndroid()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);   // máy Android đời mới chỉ chạy app 64-bit
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);   // icon mặc định mọi nền tảng
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
