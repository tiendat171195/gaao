using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;

namespace PawFit.Tests
{
    /// <summary>Cài đặt ở hòm thư: âm thanh bật / tắt, lưu riêng file "settings".</summary>
    public class SettingsTests
    {
        [Test]
        public void Sound_DefaultsOn_TogglePersists()
        {
            var save = new InMemorySaveService(new NewtonsoftSerializer());
            var bus = new EventBus();
            var seen = new List<SettingsChanged>();
            bus.Subscribe<SettingsChanged>(seen.Add);
            var s = new SettingsService(save, bus);
            Assert.IsTrue(s.SoundOn, "lần đầu: có âm thanh");
            s.ToggleSound();
            Assert.IsFalse(s.SoundOn);
            Assert.AreEqual(1, seen.Count);
            Assert.IsFalse(seen[0].Settings.SoundOn);
            s.SetSound(false);
            Assert.AreEqual(1, seen.Count, "không đổi thì không báo, không ghi file");
            var reopened = new SettingsService(save, new EventBus());
            Assert.IsFalse(reopened.SoundOn, "mở lại app vẫn tắt tiếng");
            StringAssert.Contains("SoundOn", save.RawJson(SettingsService.SaveKey));
        }

        [Test]
        public void Settings_AreSeparateFromGameSave()
        {
            var save = new InMemorySaveService(new NewtonsoftSerializer());
            new SettingsService(save, new EventBus()).SetSound(false);
            Assert.IsNull(save.RawJson("game"), "cài đặt không nằm trong file game");
        }
    }
}
