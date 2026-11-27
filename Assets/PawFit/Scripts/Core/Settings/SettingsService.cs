using System;

namespace PawFit.Core
{
    /// <summary>Cài đặt của máy này (không phải dữ liệu game: không đồng bộ lên cloud ở Phase 4).</summary>
    public sealed class GameSettingsData
    {
        public bool SoundOn = true;
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
    }
}
