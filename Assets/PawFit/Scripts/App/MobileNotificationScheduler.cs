using System;
using System.Collections.Generic;
using PawFit.Garden;
using UnityEngine;
#if PAWFIT_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using Unity.Notifications;
#endif

namespace PawFit.App
{
    /// <summary>
    /// Hẹn thông báo cục bộ bằng package Mobile Notifications 2.5 của Unity (API chung Android + iOS:
    /// <c>Unity.Notifications.NotificationCenter</c>). Chỉ chuyển lệnh, không có luật: lịch do NotificationPlanner tính.
    /// Editor / máy tính / thiếu package: không hẹn được (Supported = false), lịch vẫn xem trước được ở bảng DEV.
    /// Hẹn theo khoảng chờ (bao lâu nữa) thay vì giờ cụ thể: không lệch vì múi giờ hay giờ máy bị chỉnh.
    /// </summary>
    public sealed class MobileNotificationScheduler : INotificationScheduler
    {
        /// <summary>Kênh thông báo Android (người chơi thấy tên kênh trong Cài đặt của máy).</summary>
        public const string ChannelId = "pawfit_pet";

        /// <summary>Icon nhỏ Android (dấu chân trắng) — PawFit ▸ Setup / menu build tự đăng ký vào Project Settings ▸ Mobile Notifications.</summary>
        public const string SmallIconId = "pawfit_small";

#if PAWFIT_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private NotificationsPermissionRequest _request;
        private Action<bool> _pending;
        private bool _ready;

        public bool Supported => true;

        private void Init()
        {
            if (_ready) return;
            NotificationCenterArgs args = NotificationCenterArgs.Default;
            args.AndroidChannelId = ChannelId;
            args.AndroidChannelName = "Nhắc chăm thú";
            args.AndroidChannelDescription = "Báo khi bé đói, buồn ngủ, lấm lem hay bị ốm lúc bạn không mở game";
            args.AndroidSmallIcon = SmallIconId;
            // iOS cần Alert mới hiện khung thông báo; Android: Alert = kênh quan trọng cao (hiện nổi trên màn hình)
            args.PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Badge | NotificationPresentation.Sound;
            NotificationCenter.Initialize(args);
            _ready = true;
        }

        public void RequestPermission(Action<bool> done)
        {
            Init();
            _pending = done;
            _request = NotificationCenter.RequestPermission();
            Tick();   // đã cho phép / đã từ chối từ trước thì trả lời ngay
        }

        /// <summary>GameBootstrap gọi mỗi frame: chờ người chơi trả lời hộp thoại xin quyền của máy.</summary>
        public void Tick()
        {
            if (_request == null || _request.Status == NotificationsPermissionStatus.RequestPending) return;
            bool ok = _request.Status == NotificationsPermissionStatus.Granted;
            _request = null;
            Action<bool> done = _pending;
            _pending = null;
            done?.Invoke(ok);
        }

        public void Replace(IReadOnlyList<PlannedNotification> plan, DateTime nowUtc)
        {
            Init();
            NotificationCenter.CancelAllScheduledNotifications();
            foreach (PlannedNotification p in plan)
            {
                TimeSpan wait = p.FireUtc - nowUtc;
                if (wait < TimeSpan.FromSeconds(1)) wait = TimeSpan.FromSeconds(1);
                var n = new Notification { Title = p.Title, Text = p.Body, ShowInForeground = false };
                NotificationCenter.ScheduleNotification(n, new NotificationIntervalSchedule(wait));
            }
        }

        public void ClearAll()
        {
            Init();
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
            NotificationCenter.ClearBadge();
        }

        public void OpenSystemSettings()
        {
            Init();
            NotificationCenter.OpenNotificationSettings();
        }
#else
        public bool Supported => false;

        public void RequestPermission(Action<bool> done) => done?.Invoke(false);

        public void Tick() { }

        public void Replace(IReadOnlyList<PlannedNotification> plan, DateTime nowUtc)
        {
            if (plan.Count > 0) Debug.Log("[PawFit] (không hẹn được thông báo ở đây) " + plan.Count + " thông báo, đầu tiên: " + plan[0].Title);
        }

        public void ClearAll() { }

        public void OpenSystemSettings() { }
#endif
    }
}
