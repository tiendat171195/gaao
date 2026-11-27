using System;
using System.Collections.Generic;

namespace PawFit.Core
{
    /// <summary>Cài đặt của máy này (không phải dữ liệu game: không đồng bộ lên cloud ở Phase 4).</summary>
    public sealed class GameSettingsData
    {
        public bool SoundOn = true;

        /// <summary>Mẹo hướng dẫn đã hiện / người chơi đã tự làm (mỗi mẹo chỉ hiện 1 lần).</summary>
        public List<string> SeenTips = new List<string>();

        /// <summary>Thông báo nhắc chăm thú khi tắt app: null = chưa hỏi người chơi, true = bật, false = tắt.</summary>
        public bool? NotificationsOn;
    }

    /// <summary>Cài đặt vừa đổi (App bật / tắt âm thanh theo).</summary>
    public readonly struct SettingsChanged
    {
        public readonly GameSettingsData Settings;
        public SettingsChanged(GameSettingsData settings) { Settings = settings; }
    }

    /// <summary>
    /// Cài đặt người chơi chỉnh ở hòm thư (C# thuần). Lưu file riêng "settings", tách khỏi file game
    /// nên xóa / đồng bộ dữ liệu game không đụng tới lựa chọn của máy này.
    /// </summary>
    public sealed class SettingsService
    {
        public const string SaveKey = "settings";

        private readonly ISaveService _save;
        private readonly EventBus _bus;
        private readonly GameSettingsData _data;

        public SettingsService(ISaveService save, EventBus bus)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _data = _save.TryLoad(SaveKey, out GameSettingsData loaded) && loaded != null ? loaded : new GameSettingsData();
        }

        public GameSettingsData Current => _data;
        public bool SoundOn => _data.SoundOn;

        public void SetSound(bool on)
        {
            if (_data.SoundOn == on) return;
            _data.SoundOn = on;
            _save.Save(SaveKey, _data);
            _bus.Publish(new SettingsChanged(_data));
        }

        public void ToggleSound() => SetSound(!_data.SoundOn);

        public bool HasSeenTip(string id) => _data.SeenTips != null && _data.SeenTips.Contains(id);

        public void MarkTipSeen(string id)
        {
            if (_data.SeenTips == null) _data.SeenTips = new List<string>();
            if (_data.SeenTips.Contains(id)) return;
            _data.SeenTips.Add(id);
            _save.Save(SaveKey, _data);
        }

        /// <summary>Bật / tắt thông báo nhắc chăm thú (hòm thư, hoặc lúc game hỏi lần đầu).</summary>
        public void SetNotifications(bool on)
        {
            if (_data.NotificationsOn == on) return;
            _data.NotificationsOn = on;
            _save.Save(SaveKey, _data);
            _bus.Publish(new SettingsChanged(_data));
        }

        /// <summary>Bảng DEV: coi như chưa hỏi bật thông báo.</summary>
        public void ResetNotificationsForDebug()
        {
            _data.NotificationsOn = null;
            _save.Save(SaveKey, _data);
        }

        /// <summary>Hòm thư ▸ Xem lại hướng dẫn: hiện lại mọi mẹo.</summary>
        public void ResetTips()
        {
            _data.SeenTips = new List<string>();
            _save.Save(SaveKey, _data);
        }
    }
}
