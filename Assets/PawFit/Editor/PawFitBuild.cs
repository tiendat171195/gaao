using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace PawFit.EditorTools
{
    /// <summary>
    /// Menu PawFit ▸ Build Android / Build iOS (Phase 5.3): đặt Player Settings rồi build vào thư mục Builds/.
    ///  * Dev: có bảng DEV + quảng cáo giả lập (để test trên điện thoại).
    ///  * Release: không có bảng DEV, không có quảng cáo (chưa có SDK quảng cáo thật).
    /// Android ra file APK ký bằng debug keystore (cài thẳng vào máy được, chưa lên Google Play được).
    /// iOS ra project Xcode: mở bằng Xcode, chọn Team (Apple ID) rồi bấm Run để cài vào iPhone (xem Docs/BUILD.md).
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

        // ------------------------------------------------------------------
        // Android
        // ------------------------------------------------------------------

        [MenuItem("PawFit/Build Android/Dev (APK)")]
        public static void BuildAndroidDev() => BuildAndroid(true, false);

        [MenuItem("PawFit/Build Android/Dev + chạy trên điện thoại (USB)")]
        public static void BuildAndroidDevAndRun() => BuildAndroid(true, true);

        [MenuItem("PawFit/Build Android/Release (APK)")]
        public static void BuildAndroidRelease() => BuildAndroid(false, false);

        private static void BuildAndroid(bool dev, bool run)
        {
            if (!Prepare(BuildTargetGroup.Android, BuildTarget.Android, "Android Build Support (kèm OpenJDK + Android SDK & NDK Tools)"))
                return;
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);   // máy Android đời mới chỉ chạy app 64-bit
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            EditorUserBuildSettings.buildAppBundle = false;   // APK để cài thẳng vào máy (AAB chỉ để lên Google Play)
            BuildOptions options = dev ? BuildOptions.Development : BuildOptions.None;
            if (run) options |= BuildOptions.AutoRunPlayer;
            Run(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine("Builds/Android", dev ? "PawFit-dev.apk" : "PawFit.apk"),
                options, !run);
        }

        // ------------------------------------------------------------------
        // iOS (cần Mac + Xcode)
        // ------------------------------------------------------------------

        [MenuItem("PawFit/Build iOS/Dev (project Xcode)")]
        public static void BuildIosDev() => BuildIos(true);

        [MenuItem("PawFit/Build iOS/Release (project Xcode)")]
        public static void BuildIosRelease() => BuildIos(false);

        private static void BuildIos(bool dev)
        {
            if (!Prepare(BuildTargetGroup.iOS, BuildTarget.iOS, "iOS Build Support"))
                return;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PackageName);
            PlayerSettings.iOS.buildNumber = VersionCode.ToString();
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;   // Xcode tự lo chứng chỉ khi bạn chọn Team (Apple ID)
            Run(BuildTarget.iOS, BuildTargetGroup.iOS, dev ? "Builds/iOS/PawFit-dev" : "Builds/iOS/PawFit",
                dev ? BuildOptions.Development : BuildOptions.None, true);
        }

        // ------------------------------------------------------------------
        // Dùng chung
        // ------------------------------------------------------------------

        /// <summary>Kiểm tra đã cài module + đã chạy Setup, đặt phần cài đặt chung, chuyển nền tảng. false = không build.</summary>
        private static bool Prepare(BuildTargetGroup group, BuildTarget target, string module)
        {
            string problem = null;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                problem = "Unity này chưa cài " + module + ".\nUnity Hub ▸ Installs ▸ bản 6.3 ▸ ⚙ ▸ Add modules ▸ tick module đó rồi mở lại project.";
            else if (!File.Exists(ScenePath) || !File.Exists(LitBasePath) || GraphicsSettings.defaultRenderPipeline == null)
                problem = "Chưa chạy menu PawFit ▸ Setup Project (cần scene Main, material LitBase, URP Asset). Chạy xong thì build lại.";
            if (problem != null)
            {
                EditorUtility.DisplayDialog("PawFit · chưa build được", problem, "OK");
                return false;
            }
            PlayerSettings.bundleVersion = Version;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);   // icon mặc định mọi nền tảng
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (EditorUserBuildSettings.activeBuildTarget != target && !EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                EditorUtility.DisplayDialog("PawFit", "Không chuyển được nền tảng. Mở File ▸ Build Profiles, chọn nền tảng ▸ Switch Platform rồi thử lại.", "OK");
                return false;
            }
            return true;
        }

        private static void Run(BuildTarget target, BuildTargetGroup group, string path, BuildOptions options, bool reveal)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = options,
            };
            bool dev = (options & BuildOptions.Development) != 0;
            Debug.Log("[PawFit] Bắt đầu build " + target + " " + (dev ? "Dev" : "Release") + " → " + path + " (lần đầu IL2CPP mất 5–20 phút)");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[PawFit] Build xong: {s.outputPath} · {s.totalSize / (1024.0 * 1024.0):0.0} MB · {s.totalTime.TotalMinutes:0.0} phút");
                if (reveal) EditorUtility.RevealInFinder(s.outputPath);
            }
            else
            {
                Debug.LogError("[PawFit] Build " + target + " lỗi (" + s.result + ", " + s.totalErrors + " lỗi). Gửi tôi các dòng đỏ phía trên trong Console.");
            }
        }
    }
}
