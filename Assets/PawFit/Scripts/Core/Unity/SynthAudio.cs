using System;
using UnityEngine;

namespace PawFit.Core.Unity
{
    /// <summary>
    /// Âm thanh tạo bằng code (không cần file âm thanh): tiếng thú (PetMoodFx), tiếng Coin (UiSounds)...
    /// Sau này có file âm thanh thật thì chỉ thay chỗ gọi, không đổi luật game.
    /// </summary>
    public static class SynthAudio
    {
        public const int Rate = 22050;

        /// <summary>Pha của sóng sin tần số `freq` tại thời điểm `t` (giây).</summary>
        public static float Phase(float freq, float t) => 2f * Mathf.PI * freq * t;

        /// <summary>1 nốt chuông: bật nhanh rồi tắt dần theo hàm mũ, bắt đầu ở `start` giây.</summary>
        public static float Bell(float freq, float t, float start, float decay, float gain)
        {
            if (t < start) return 0f;
            float u = t - start;
            return Mathf.Sin(Phase(freq, u)) * Mathf.Exp(-u * decay) * gain * Mathf.Clamp01(u * 400f);
        }

        /// <summary>Nhiễu trắng giả (cố định theo t, không cần Random) cho tiếng "tách".</summary>
        public static float Noise(float t)
        {
            float n = Mathf.Sin(t * 12345.678f) * 43758.547f;
            return (n - Mathf.Floor(n)) * 2f - 1f;
        }

        /// <summary>Tạo AudioClip mono dài `seconds` giây từ hàm mẫu sample(t) (giá trị -1..1).</summary>
        public static AudioClip Make(string name, float seconds, Func<float, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
