using System;
using PawFit.Core;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.App
{
    /// <summary>
    /// Tiếng giao diện (Phase 5.2), tạo bằng code: "ting-ting" khi nhận Coin, "cạch-ting" (máy tính tiền) khi tiêu Coin.
    /// Chỉ nghe sự kiện CoinsChanged của EconomyService, không chứa luật game.
    /// </summary>
    public sealed class UiSounds : MonoBehaviour
    {
        private AudioSource _audio;
        private AudioClip _gain, _spend;
        private IDisposable _sub;

        public void Init(EventBus bus)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;   // âm thanh 2D, không phụ thuộc vị trí camera
            _audio.volume = 0.45f;
            _sub = bus.Subscribe<CoinsChanged>(e => Play(e.Delta > 0));
        }

        private void OnDestroy() => _sub?.Dispose();

        private void Play(bool gain)
        {
            if (_audio == null) return;
            if (gain && _gain == null) _gain = MakeGain();
            if (!gain && _spend == null) _spend = MakeSpend();
            _audio.PlayOneShot(gain ? _gain : _spend);
        }

        /// <summary>2 nốt chuông cao đi lên (E6 rồi B6).</summary>
        private static AudioClip MakeGain() =>
            SynthAudio.Make("coin_gain", 0.5f, t => SynthAudio.Bell(1318.5f, t, 0f, 9f, 0.32f) + SynthAudio.Bell(1975.5f, t, 0.09f, 8f, 0.3f));

        /// <summary>Tiếng "tách" ngắn của ngăn kéo tiền + 1 nốt chuông trầm hơn.</summary>
        private static AudioClip MakeSpend() =>
            SynthAudio.Make("coin_spend", 0.45f, t =>
            {
                float click = t < 0.02f ? SynthAudio.Noise(t) * (1f - t / 0.02f) * 0.35f : 0f;
                return click + SynthAudio.Bell(1046.5f, t, 0.035f, 7f, 0.3f) + SynthAudio.Bell(2093f, t, 0.035f, 12f, 0.08f);
            });
    }
}
