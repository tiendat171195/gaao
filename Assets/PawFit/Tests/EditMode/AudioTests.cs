using System;
using NUnit.Framework;
using PawFit.Core.Audio;

namespace PawFit.Tests
{
    /// <summary>Âm thanh tạo bằng code (Phase 8.1): đủ to, không vỡ tiếng, nhạc + tiếng lặp nối vòng liền mạch.</summary>
    public class AudioTests
    {
        private static void AssertClean(float[] s, string name, float minPeak = 0.05f)
        {
            Assert.IsNotNull(s, name);
            Assert.Greater(s.Length, 100, name + " quá ngắn");
            float peak = 0f;
            foreach (float v in s)
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), name + " có NaN");
                Assert.LessOrEqual(Math.Abs(v), 1f, name + " vượt -1..1 (vỡ tiếng)");
                peak = Math.Max(peak, Math.Abs(v));
            }
            Assert.GreaterOrEqual(peak, minPeak, name + " gần như câm");
        }

        /// <summary>Chỗ nối cuối → đầu không "bụp": bước nhảy ở chỗ nối không lớn hơn bước nhảy lớn nhất bên trong mảng.</summary>
        private static void AssertSeamless(float[] s, string name)
        {
            float maxStep = 0f;
            for (int i = 1; i < s.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(s[i] - s[i - 1]));
            float seam = Math.Abs(s[0] - s[s.Length - 1]);
            Assert.LessOrEqual(seam, maxStep * 1.05f + 1e-4f, name + " nối vòng bị gãy");
        }

        [Test]
        public void EverySound_IsAudible_AndClipFree()
        {
            foreach (SoundId id in (SoundId[])Enum.GetValues(typeof(SoundId)))
            {
                float[] s = SoundSynth.Sound(id);
                AssertClean(s, id.ToString());
                Assert.Less(s.Length, SoundSynth.Rate * 2, id + ": tiếng ngắn phải dưới 2 giây");
            }
        }

        [Test]
        public void Loops_AreSeamless()
        {
            foreach (LoopId id in (LoopId[])Enum.GetValues(typeof(LoopId)))
            {
                float[] s = SoundSynth.Loop(id);
                AssertClean(s, id.ToString());
                AssertSeamless(s, id.ToString());
            }
        }

        [Test]
        public void Music_LengthMatchesTempo_AndLoopsSeamlessly()
        {
            foreach (MusicId id in new[] { MusicId.Garden, MusicId.Battle })
            {
                float[] s = SoundSynth.Music(id);
                AssertClean(s, id.ToString(), 0.3f);
                AssertSeamless(s, id.ToString());
                Assert.AreEqual(SoundSynth.MusicSeconds(id) * SoundSynth.Rate, s.Length, SoundSynth.Rate * 0.01, id + ": 8 ô nhịp");
            }
            Assert.AreEqual(32f * 60f / 90f, SoundSynth.MusicSeconds(MusicId.Garden), 1e-3f, "nhạc vườn 90 nhịp / phút");
            Assert.Less(SoundSynth.MusicSeconds(MusicId.Battle), SoundSynth.MusicSeconds(MusicId.Garden), "nhạc trận nhanh hơn");
        }

        [Test]
        public void SameName_SameSamples()
        {
            CollectionAssert.AreEqual(SoundSynth.Sound(SoundId.Splash), SoundSynth.Sound(SoundId.Splash));
            CollectionAssert.AreEqual(SoundSynth.Loop(LoopId.Wind), SoundSynth.Loop(LoopId.Wind));
            CollectionAssert.AreNotEqual(SoundSynth.Sound(SoundId.Step0), SoundSynth.Sound(SoundId.Step1), "3 tiếng bước chân khác nhau");
            Assert.AreEqual(440f, SoundSynth.Midi(69), 1e-3f);
        }
    }
}
