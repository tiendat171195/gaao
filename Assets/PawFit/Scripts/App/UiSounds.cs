using System;
using PawFit.Core;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.App
{
    /// <summary>
    /// Tiếng Coin (Phase 5.2): "ting-ting" khi nhận, "cạch-ting" (máy tính tiền) khi tiêu. Phát qua Sfx (Phase 8.1).
    /// Chỉ nghe sự kiện CoinsChanged của EconomyService, không chứa luật game.
    /// </summary>
    public sealed class UiSounds : MonoBehaviour
    {
        private IDisposable _sub;

        public void Init(EventBus bus) =>
            _sub = bus.Subscribe<CoinsChanged>(e => Sfx.Play(e.Delta > 0 ? SoundId.CoinGain : SoundId.CoinSpend, 0.45f));

        private void OnDestroy() => _sub?.Dispose();
    }
}
