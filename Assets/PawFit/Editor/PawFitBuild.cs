using System;
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
    /// Android:
    ///  * APK (Dev / Release): cài thẳng vào máy. Chưa đặt khóa ký riêng thì Unity ký bằng debug keystore.
    ///  * AAB (Google Play): bắt buộc khóa ký riêng (upload key) — tạo 1 lần ở Publishing Settings, xem Docs/BUILD.md mục 15.
    /// iOS ra project Xcode: mở bằng Xcode, chọn Team (Apple ID) rồi bấm Run để cài vào iPhone (xem Docs/BUILD.md).
    /// </summary>
    public static class PawFitBuild
    {
        /// <summary>
        /// Tên gói (Android) / Bundle ID (iOS) bạn chốt: CỐ ĐỊNH VĨNH VIỄN sau lần đầu tải lên Google Play / App Store.
        /// Đổi tên gói = điện thoại coi là app khác (phải gỡ bản cũ, dữ liệu bản cũ không sang).
        /// </summary>
        public const string PackageName = "com.tiedee.pawfit";
        public const string Version = "0.6.0";

        /// <summary>
        /// Số bản build (Android versionCode / iOS build number) = số phút từ 01/01/2026 (UTC): lần build sau luôn lớn hơn
        /// lần trước. Google Play / TestFlight bắt buộc số này tăng mỗi lần tải lên, cài đè bản cũ cũng không bị lỗi hạ phiên bản.
        /// </summary>
        public static int BuildNumber() => (int)Math.Floor((DateTime.UtcNow - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMinutes);

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

        [MenuItem("PawFit/Build Android/Release (AAB, Google Play)")]
        public static void BuildAndroidStore() => BuildAndroid(false, false, true);

        private static void BuildAndroid(bool dev, bool run, bool appBundle = false)
        {
            if (appBundle && !CheckUploadKey()) return;
            if (!Prepare(BuildTargetGroup.Android, BuildTarget.Android, "Android Build Support (kèm OpenJDK + Android SDK & NDK Tools)"))
                return;
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);   // máy Android đời mới chỉ chạy app 64-bit
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            int build = BuildNumber();
            PlayerSettings.Android.bundleVersionCode = build;
            EditorUserBuildSettings.buildAppBundle = appBundle;   // APK để cài thẳng vào máy, AAB để lên Google Play
            if (!dev && !appBundle && !PlayerSettings.Android.useCustomKeystore)
                Debug.Log("[PawFit] Release APK ký bằng debug keystore: cài thử được, chưa lên Google Play được (dùng menu Release (AAB))");
            BuildOptions options = dev ? BuildOptions.Development : BuildOptions.None;
            if (run) options |= BuildOptions.AutoRunPlayer;
            string file = appBundle ? "PawFit-" + Version + "-" + build + ".aab" : dev ? "PawFit-dev.apk" : "PawFit.apk";
            Run(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine("Builds/Android", file), options, !run);
        }

        /// <summary>
        /// Google Play cần bản ký bằng khóa riêng (upload key), không nhận debug keystore. Unity không lưu mật khẩu khóa:
        /// mỗi lần mở Unity phải nhập lại ở Player ▸ Publishing Settings. Thiếu gì thì báo + mở đúng chỗ đó.
        /// </summary>
        private static bool CheckUploadKey()
        {
            string keystore = PlayerSettings.Android.keystoreName;
            string problem = null;
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(keystore))
                problem = "Chưa chọn khóa ký riêng (upload key). Lần đầu: tạo khóa bằng Keystore Manager (Docs/BUILD.md mục 15).";
            else if (!File.Exists(Path.GetFullPath(keystore)))
                problem = "Không thấy file khóa ký: " + keystore + "\nChọn lại file ở Publishing Settings ▸ Custom Keystore.";
            else if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName))
                problem = "Chưa chọn Alias (tên khóa) ở Publishing Settings ▸ Custom Keystore.";
            else if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                problem = "Chưa nhập mật khẩu khóa ký (Unity không lưu mật khẩu, mỗi lần mở Unity nhập lại 1 lần).";
            if (problem == null) return true;
            EditorUtility.DisplayDialog("PawFit · chưa build AAB được",
                                        problem + "\n\nĐang mở Project Settings ▸ Player: chọn tab Android ▸ Publishing Settings.", "OK");
            UnityEditor.SettingsService.OpenProjectSettings("Project/Player");
            return false;
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
            PlayerSettings.iOS.buildNumber = BuildNumber().ToString();   // TestFlight cũng bắt buộc số này tăng
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
            // Máy chủ riêng: bản Dev được nối http:// (máy Mac trong nhà, vd http://192.168.1.10:5080);
            // bản Release bắt buộc https:// (máy chủ thật sau Caddy). Android / iOS mặc định chặn http.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            NotificationSetup.Apply();   // thông báo nhắc chăm thú: icon Android + cài đặt package
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
