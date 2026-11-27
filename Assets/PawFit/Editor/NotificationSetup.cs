using UnityEditor;
using UnityEngine;
#if PAWFIT_NOTIFICATIONS
using Unity.Notifications;
#endif

namespace PawFit.EditorTools
{
    /// <summary>
    /// Cài đặt package Mobile Notifications cho PawFit (gọi ở PawFit ▸ Setup Project và trước mỗi lần build):
    ///  * Icon nhỏ Android "pawfit_small" = dấu chân trắng nền trong (Android chỉ dùng kênh alpha; thiếu icon thì hiện ô vuông trắng).
    ///  * Khởi động lại điện thoại vẫn giữ lịch thông báo.
    ///  * iOS không tự xin quyền lúc mở app: game tự hỏi đúng lúc (sau khi chơi một lúc, có thẻ giải thích trước).
    /// </summary>
    public static class NotificationSetup
    {
        public const string SmallIconPath = "Assets/PawFit/Art/Icon/PawFit_NotifySmall.png";
        public const string SmallIconId = "pawfit_small";   // trùng MobileNotificationScheduler.SmallIconId

        public static void Apply()
        {
#if PAWFIT_NOTIFICATIONS
            var importer = AssetImporter.GetAtPath(SmallIconPath) as TextureImporter;
            if (importer != null && (!importer.isReadable || !importer.alphaIsTransparency || importer.textureType != TextureImporterType.Default))
            {
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = true;   // package đọc điểm ảnh để tạo icon Android
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(SmallIconPath);
            NotificationSettings.AndroidSettings.ClearDrawableResources();
            if (icon != null) NotificationSettings.AndroidSettings.AddDrawableResource(SmallIconId, icon, NotificationIconType.Small);
            else Debug.LogWarning("[PawFit] Không thấy icon thông báo " + SmallIconPath + " (Android sẽ dùng icon app)");
            NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
            NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
#else
            Debug.LogWarning("[PawFit] Chưa có package com.unity.mobile.notifications 2.5.0 (Window ▸ Package Manager): game chạy được nhưng không có thông báo");
#endif
        }
    }
}
