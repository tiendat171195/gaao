using System;

namespace PawFit.Core.Audio
{
    /// <summary>Tiếng ngắn (phát 1 lần). Thứ tự chỉ để đặt tên, không lưu vào file.</summary>
    public enum SoundId
    {
        // ---- vườn / thú ----
        Step0, Step1, Step2, Splash, Munch, Gulp, Happy, Giggle, Boing, Cry, Yawn, Chime, Dig, Treasure,
        BirdA, BirdB, BirdC, Pop, BallBounce, Throw, Swish, Pounce,

        // ---- giao diện ----
        UiOpen, UiTap, CoinGain, CoinSpend,

        // ---- trận ----
        WhooshQuick, WhooshHeavy, WhooshSpecial, UltCharge, HitLight, HitHeavy, HitCrit, Block, Counter, Guard, Wrong,
        Combo2, Combo3, Combo4, Combo5, RageFull, KO, Win, Lose, Fight,
    }

    /// <summary>Tiếng lặp: gió + lá (nền vườn), nước hồ, tắm, thở khi ngủ, gừ gừ khi được vuốt ve.</summary>
    public enum LoopId { Wind = 0, Pond = 1, Bath = 2, Sleep = 3, Purr = 4 }

    public enum MusicId { None = 0, Garden = 1, Battle = 2 }

    /// <summary>
    /// Tạo âm thanh bằng code (C# thuần, không dùng Unity, có test): ra mảng mẫu mono 22.050 Hz, giá trị -1..1.
    /// Nhạc + tiếng lặp nối đầu-cuối liền mạch (nốt ngân quá cuối bài thì vòng lại đầu bài; tiếng nhiễu thì trộn mờ chỗ nối).
    /// Cùng 1 tên luôn ra cùng 1 mảng (nhiễu có seed). Lớp Unity (Sfx) chỉ đổi mảng thành AudioClip rồi phát.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 22050;
        private const double TwoPi = Math.PI * 2.0;

        // ------------------------------------------------------------------ tiếng ngắn

        public static float[] Sound(SoundId id)
        {
            switch (id)
            {
                case SoundId.Step0: return Step(11u, 1f);
                case SoundId.Step1: return Step(23u, 0.9f);
                case SoundId.Step2: return Step(37u, 1.1f);
                case SoundId.Splash: return Splash(41u);
                case SoundId.Munch: return Munch();
                case SoundId.Gulp: return Gulp();
                case SoundId.Happy: return Happy();
                case SoundId.Giggle: return Giggle();
                case SoundId.Boing: return Boing();
                case SoundId.Cry: return Cry();
                case SoundId.Yawn: return Yawn();
                case SoundId.Chime: return Chime();
                case SoundId.Dig: return Dig();
                case SoundId.Treasure: return Bells(new[] { 84f, 88f, 91f, 96f }, new[] { 0f, 0.08f, 0.16f, 0.27f }, 1.1f, 4.5f, 0.3f);
                case SoundId.BirdA: return BirdTweets();
                case SoundId.BirdB: return BirdTrill();
                case SoundId.BirdC: return BirdTwoTone();
                case SoundId.Pop: return Pop();
                case SoundId.BallBounce: return Bounce();
                case SoundId.Throw: return Whoosh(0.32f, 700f, 2100f, 61u);
                case SoundId.Swish: return Whoosh(0.2f, 1800f, 3600f, 67u);
                case SoundId.Pounce: return Pounce();
                case SoundId.UiOpen: return UiOpen();
                case SoundId.UiTap: return UiTap();
                case SoundId.CoinGain: return CoinGain();
                case SoundId.CoinSpend: return CoinSpend();
                case SoundId.WhooshQuick: return Whoosh(0.18f, 1500f, 3300f, 71u);
                case SoundId.WhooshHeavy: return WhooshHeavy();
                case SoundId.WhooshSpecial: return WhooshSpecial();
                case SoundId.UltCharge: return UltCharge();
                case SoundId.HitLight: return Hit(0.16f, 140f, 80f, 40f, 0.7f, 81u, false);
                case SoundId.HitHeavy: return Hit(0.3f, 95f, 45f, 16f, 1f, 83u, false);
                case SoundId.HitCrit: return Hit(0.55f, 95f, 45f, 16f, 1f, 89u, true);
                case SoundId.Block: return Clang(false);
                case SoundId.Counter: return Clang(true);
                case SoundId.Guard: return Guard();
                case SoundId.Wrong: return Wrong();
                case SoundId.Combo2: return Bells(new[] { 84f }, new[] { 0f }, 0.4f, 6f, 0.45f);
                case SoundId.Combo3: return Bells(new[] { 86f, 93f }, new[] { 0f, 0.05f }, 0.45f, 6f, 0.4f);
                case SoundId.Combo4: return Bells(new[] { 88f, 95f }, new[] { 0f, 0.05f }, 0.45f, 6f, 0.4f);
                case SoundId.Combo5: return Bells(new[] { 91f, 96f, 100f }, new[] { 0f, 0.04f, 0.08f }, 0.55f, 5f, 0.35f);
                case SoundId.RageFull: return RageFull();
                case SoundId.KO: return KnockOut();
                case SoundId.Win: return Win();
                case SoundId.Lose: return Lose();
                default: return Gong();
            }
        }

        /// <summary>Bước chân trên cỏ: tiếng sột soạt ngắn + tiếng "bịch" trầm rất nhỏ.</summary>
        private static float[] Step(uint seed, float pitch)
        {
            float[] b = Buf(0.12f);
            var n = new Noise(seed);
            var hp = new OnePole(700f * pitch);
            var lp = new OnePole(3200f * pitch);
            var thump = new Sine(70f * pitch);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float x = n.Next();
                float band = lp.Run(x - hp.Run(x));
                float env = Attack(t, 0.003f) * Exp(t, 45f) + 0.35f * Attack(t - 0.03f, 0.004f) * Exp(t - 0.03f, 70f);
                b[i] = band * env + thump.Next() * Exp(t, 60f) * 0.45f;
            }
            return Normalize(b, 0.8f);
        }

        /// <summary>Té nước: tiếng ào (nhiễu tắt dần) + giọt nước bắn (nốt cao vút lên rất ngắn).</summary>
        private static float[] Splash(uint seed)
        {
            float[] b = Buf(0.8f);
            var n = new Noise(seed);
            var lp = new OnePole(4000f);
            var hp = new OnePole(1500f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float x = n.Next();
                float body = lp.Run(x) * Attack(t, 0.005f) * Exp(t, 7f);
                float tail = (x - hp.Run(x)) * Exp(t, 12f) * 0.4f;
                b[i] = body + tail;
            }
            for (int k = 0; k < 14; k++)
            {
                float start = 0.05f + n.Unit() * 0.55f;
                Chirp(b, start, 0.03f, 900f + n.Unit() * 1300f, 1.6f, 0.22f, 120f, false);
            }
            return Normalize(b, 0.85f);
        }

        /// <summary>Nhai "nhóp nhép": 3 lần cắn giòn + tiếng "nom" trầm.</summary>
        private static float[] Munch()
        {
            float[] b = Buf(0.75f);
            var n = new Noise(97u);
            foreach (float s in new[] { 0f, 0.24f, 0.48f })
            {
                var hp = new OnePole(1500f);
                int i0 = N(s);
                for (int i = 0; i < N(0.1f); i++)
                {
                    float t = T(i);
                    float x = n.Next();
                    Put(b, i0 + i, (x - hp.Run(x)) * Attack(t, 0.002f) * Exp(t, 35f) * 0.55f, false);
                }
                Sweep(b, s + 0.01f, 0.09f, 260f, 170f, 0.6f, 0.3f, false);
            }
            return Normalize(b, 0.8f);
        }

        private static float[] Gulp()
        {
            float[] b = Buf(0.42f);
            Sweep(b, 0f, 0.13f, 320f, 140f, 0.8f, 0.2f, false);
            Chirp(b, 0.16f, 0.05f, 500f, 1.8f, 0.6f, 40f, false);
            return Normalize(b, 0.8f);
        }

        /// <summary>Kêu vui "mi!": vút lên, sáng.</summary>
        private static float[] Happy()
        {
            float[] b = Buf(0.3f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i), u = t / 0.3f;
                float f = 900f + 600f * (1f - (1f - u) * (1f - u)) + 18f * (float)Math.Sin(TwoPi * 30.0 * t);
                ph += TwoPi * f / Rate;
                float env = Attack(t, 0.015f) * (float)Math.Sin(Math.PI * Math.Min(1f, u));
                b[i] = env * (float)(Math.Sin(ph) + 0.25 * Math.Sin(2 * ph) + 0.1 * Math.Sin(3 * ph));
            }
            return Normalize(b, 0.75f);
        }

        /// <summary>Cười khúc khích (bị cù): 5 tiếng "mi" ngắn cao dần.</summary>
        private static float[] Giggle()
        {
            float[] b = Buf(0.55f);
            for (int k = 0; k < 5; k++)
            {
                float s = k * 0.095f, f0 = 950f + 90f * k;
                Sweep(b, s, 0.07f, f0, f0 * 1.35f, 0.7f, 0.25f, false);
            }
            return Normalize(b, 0.75f);
        }

        /// <summary>Nhảy "boing": lò xo lên rồi xuống, rung nhỏ dần.</summary>
        private static float[] Boing()
        {
            float[] b = Buf(0.35f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i), u = t / 0.35f;
                float f = (u < 0.4f ? 220f + 300f * (u / 0.4f) : 520f - 140f * ((u - 0.4f) / 0.6f)) * (1f + 0.04f * (float)Math.Sin(TwoPi * 22.0 * t) * (1f - u));
                ph += TwoPi * f / Rate;
                b[i] = (float)Math.Sin(ph) * Attack(t, 0.008f) * Exp(t, 6f);
            }
            return Normalize(b, 0.7f);
        }

        /// <summary>Đòi ăn "mi-meo": 2 nốt cao dần (như bản cũ của PetMoodFx).</summary>
        private static float[] Cry()
        {
            float[] b = Buf(0.42f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                if (t >= 0.17f && t < 0.2f) continue;
                bool second = t >= 0.2f;
                float note = second ? (t - 0.2f) / 0.22f : t / 0.17f;
                float f0 = second ? 820f : 640f, f1 = second ? 1180f : 900f;
                float f = (f0 + (f1 - f0) * Clamp01(note)) * (1f + 0.02f * (float)Math.Sin(t * 70f));
                ph += TwoPi * f / Rate;
                float env = (float)Math.Sin(Clamp01(note) * Math.PI);
                b[i] = env * 0.5f * (float)(Math.Sin(ph) + 0.25 * Math.Sin(2 * ph));
            }
            return Normalize(b, 0.75f);
        }

        private static float[] Yawn()
        {
            float[] b = Buf(0.85f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i), u = t / 0.85f;
                float f = 520f - 280f * u;
                ph += TwoPi * f / Rate;
                float env = (float)Math.Sin(u * Math.PI) * (1f - 0.3f * u);
                b[i] = env * 0.4f * (float)(Math.Sin(ph) + 0.35 * Math.Sin(3 * ph));
            }
            return Normalize(b, 0.7f);
        }

        private static float[] Chime() =>
            Bells(new[] { 72f, 76f, 79f, 84f }, new[] { 0f, 0.12f, 0.24f, 0.36f }, 1.3f, 3.2f, 0.25f);

        /// <summary>Bới đất: 4 tiếng cào sột soạt + "bịch" đất nhỏ.</summary>
        private static float[] Dig()
        {
            float[] b = Buf(0.58f);
            var n = new Noise(131u);
            for (int k = 0; k < 4; k++)
            {
                float s = k * 0.13f;
                var hp = new OnePole(400f);
                var lp = new OnePole(2500f);
                var thud = new OnePole(300f);
                int i0 = N(s);
                for (int i = 0; i < N(0.12f); i++)
                {
                    float t = T(i);
                    float x = n.Next();
                    float scratch = lp.Run(x - hp.Run(x)) * Attack(t, 0.01f) * Exp(t, 25f);
                    Put(b, i0 + i, scratch + thud.Run(x) * Exp(t, 40f) * 0.8f, false);
                }
            }
            return Normalize(b, 0.75f);
        }

        /// <summary>Chim: 3 tiếng "chíp" nhanh.</summary>
        private static float[] BirdTweets()
        {
            float[] b = Buf(0.42f);
            for (int k = 0; k < 3; k++) Sweep(b, k * 0.12f, 0.07f, 2800f, 3600f, 0.6f, 0.05f, false, 40f);
            return Normalize(b, 0.6f);
        }

        /// <summary>Chim: tiếng rúc líu lo rồi 1 nốt đi xuống.</summary>
        private static float[] BirdTrill()
        {
            float[] b = Buf(0.55f);
            for (int k = 0; k < 8; k++) Sweep(b, k * 0.035f, 0.028f, 3200f, 3350f, 0.5f, 0.05f, false);
            Sweep(b, 0.32f, 0.16f, 3000f, 2200f, 0.6f, 0.05f, false, 25f);
            return Normalize(b, 0.6f);
        }

        /// <summary>Chim: 2 nốt "cú-cu" trầm hơn.</summary>
        private static float[] BirdTwoTone()
        {
            float[] b = Buf(0.42f);
            Sweep(b, 0f, 0.12f, 1950f, 1850f, 0.6f, 0.1f, false);
            Sweep(b, 0.17f, 0.18f, 1550f, 1450f, 0.6f, 0.1f, false);
            return Normalize(b, 0.55f);
        }

        /// <summary>Bong bóng vỡ "póc".</summary>
        private static float[] Pop()
        {
            float[] b = Buf(0.1f);
            Chirp(b, 0f, 0.035f, 500f, 3.2f, 0.9f, 60f, false);
            var n = new Noise(151u);
            for (int i = 0; i < N(0.004f); i++) b[i] += n.Next() * 0.4f;
            return Normalize(b, 0.7f);
        }

        /// <summary>Bóng cao su nảy "bụp".</summary>
        private static float[] Bounce()
        {
            float[] b = Buf(0.2f);
            Sweep(b, 0f, 0.12f, 160f, 90f, 1f, 0.15f, false);
            var n = new Noise(157u);
            var lp = new OnePole(1500f);
            for (int i = 0; i < b.Length; i++) b[i] = b[i] * Exp(T(i), 30f) + lp.Run(n.Next()) * Exp(T(i), 80f) * 0.5f;
            return Normalize(b, 0.8f);
        }

        /// <summary>Vồ: tiếng "vụt" ngắn + "bịch" lúc chạm đất.</summary>
        private static float[] Pounce()
        {
            float[] w = Whoosh(0.16f, 900f, 2200f, 163u);
            float[] b = Buf(0.32f);
            Array.Copy(w, b, Math.Min(w.Length, b.Length));
            for (int i = 0; i < b.Length; i++) b[i] *= 0.6f;
            Sweep(b, 0.15f, 0.12f, 140f, 80f, 0.9f, 0.1f, false);
            return Normalize(b, 0.75f);
        }

        /// <summary>Tiếng gió vút: nhiễu lọc dải, tâm dải trượt từ f0 lên f1, to lên rồi nhỏ dần.</summary>
        private static float[] Whoosh(float seconds, float f0, float f1, uint seed)
        {
            float[] b = Buf(seconds);
            var n = new Noise(seed);
            var hp = new OnePole(f0);
            var lp = new OnePole(f0 * 2f);
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                float fc = f0 + (f1 - f0) * u;
                hp.SetCutoff(fc * 0.6f);
                lp.SetCutoff(fc * 1.6f);
                float x = n.Next();
                float env = (float)Math.Pow(Math.Sin(Math.PI * u), 1.5);
                b[i] = lp.Run(x - hp.Run(x)) * env;
            }
            return Normalize(b, 0.75f);
        }

        private static float[] WhooshHeavy()
        {
            float[] b = Whoosh(0.32f, 450f, 1300f, 73u);
            var rumble = new Sine(65f);
            for (int i = 0; i < b.Length; i++) b[i] += rumble.Next() * (float)Math.Sin(Math.PI * i / b.Length) * 0.35f;
            return Normalize(b, 0.8f);
        }

        private static float[] WhooshSpecial()
        {
            float[] b = Whoosh(0.36f, 900f, 2600f, 79u);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i), u = i / (float)b.Length;
                ph += TwoPi * (1800.0 + 120.0 * Math.Sin(TwoPi * 9.0 * t)) / Rate;
                b[i] += (float)Math.Sin(ph) * (float)Math.Sin(Math.PI * u) * 0.25f;
            }
            return Normalize(b, 0.8f);
        }

        /// <summary>Tụ lực Tuyệt chiêu: tiếng ù trầm vút lên cao, rung nhanh dần + nhiễu rít.</summary>
        private static float[] UltCharge()
        {
            float[] b = Buf(0.75f);
            var n = new Noise(173u);
            var hp = new OnePole(2000f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i), u = t / 0.75f;
                double f = 180.0 * Math.Pow(5.0, u);
                ph += TwoPi * f / Rate;
                float trem = 0.7f + 0.3f * (float)Math.Sin(TwoPi * (6.0 + 20.0 * u) * t);
                float x = n.Next();
                b[i] = ((float)(Math.Sin(ph) + 0.3 * Math.Sin(2 * ph)) * trem * 0.6f + (x - hp.Run(x)) * u * 0.35f) * Attack(t, 0.05f) * (u < 0.92f ? 1f : (1f - u) / 0.08f);
            }
            return Normalize(b, 0.8f);
        }

        /// <summary>Đòn trúng: "bốp" (nhiễu tắt nhanh) + "thịch" trầm trượt xuống; chí mạng thêm tiếng chuông sáng.</summary>
        private static float[] Hit(float seconds, float f0, float f1, float decay, float gain, uint seed, bool crit)
        {
            float[] b = Buf(seconds);
            var n = new Noise(seed);
            var lp = new OnePole(2200f);
            var hp = new OnePole(2500f);
            Sweep(b, 0f, Math.Min(seconds, 0.18f), f0, f1, 1f, 0.2f, false);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float x = n.Next();
                float smack = lp.Run(x) * Attack(t, 0.001f) * Exp(t, decay);
                float crunch = (x - hp.Run(x)) * Exp(t, 30f) * 0.3f;
                b[i] = b[i] * Exp(t, decay * 0.8f) + smack * 0.9f + crunch;
            }
            if (crit)
            {
                float[] ring = Bells(new[] { 96f, 103f }, new[] { 0.01f, 0.03f }, seconds, 6f, 0.3f);
                for (int i = 0; i < b.Length && i < ring.Length; i++) b[i] += ring[i] * 0.7f;
            }
            return Normalize(b, 0.9f * gain);
        }

        /// <summary>Đỡ đòn "keng" (các họa âm lệch nhau như kim loại); phản đòn thêm tiếng "xoẹt" vút lên.</summary>
        private static float[] Clang(bool counter)
        {
            float[] b = Buf(counter ? 0.6f : 0.42f);
            float[] parts = { 520f, 1340f, 2080f, 2890f, 3700f };
            float[] decays = { 6f, 9f, 12f, 15f, 20f };
            float[] amps = { 0.5f, 0.35f, 0.25f, 0.18f, 0.1f };
            for (int p = 0; p < parts.Length; p++)
            {
                var s = new Sine(parts[p]);
                for (int i = 0; i < b.Length; i++) b[i] += s.Next() * Exp(T(i), decays[p]) * amps[p] * Attack(T(i), 0.001f);
            }
            var n = new Noise(191u);
            for (int i = 0; i < N(0.006f); i++) b[i] += n.Next() * 0.5f;
            if (counter) Sweep(b, 0.05f, 0.3f, 1200f, 3400f, 0.5f, 0.1f, false, 30f);
            return Normalize(b, 0.85f);
        }

        private static float[] Guard()
        {
            float[] b = Buf(0.18f);
            var n = new Noise(193u);
            var lp = new OnePole(900f);
            for (int i = 0; i < b.Length; i++)
            {
                float u = i / (float)b.Length;
                b[i] = lp.Run(n.Next()) * (float)Math.Sin(Math.PI * u) * 1.2f;
            }
            Sweep(b, 0f, 0.15f, 300f, 200f, 0.3f, 0.1f, false);
            return Normalize(b, 0.6f);
        }

        /// <summary>Vuốt sai "bộp" méo, trầm xuống.</summary>
        private static float[] Wrong()
        {
            float[] b = Buf(0.3f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i), u = t / 0.3f;
                double f = 240.0 - 90.0 * u + 8.0 * Math.Sin(TwoPi * 14.0 * t);
                ph += TwoPi * f / Rate;
                b[i] = (float)(Math.Sin(ph) + 0.3 * Math.Sin(3 * ph)) * Attack(t, 0.005f) * Exp(t, 9f);
            }
            return Normalize(b, 0.65f);
        }

        /// <summary>Đầy Nộ: hợp âm A trưởng lấp lánh, to dần rồi tắt.</summary>
        private static float[] RageFull()
        {
            float[] b = Buf(0.9f);
            foreach (float m in new[] { 69f, 73f, 76f, 81f })
            {
                var s = new Sine(Midi(m));
                for (int i = 0; i < b.Length; i++)
                {
                    float t = T(i), u = t / 0.9f;
                    float trem = 0.75f + 0.25f * (float)Math.Sin(TwoPi * 8.0 * t);
                    b[i] += s.Next() * trem * (float)Math.Sin(Math.PI * Math.Pow(u, 0.6)) * 0.25f;
                }
            }
            return Normalize(b, 0.7f);
        }

        /// <summary>Hạ gục: "bùm" trầm + rền + giọng tụt xuống.</summary>
        private static float[] KnockOut()
        {
            float[] b = Buf(1.2f);
            Sweep(b, 0f, 0.6f, 70f, 35f, 1f, 0.1f, false);
            var n = new Noise(197u);
            var lp = new OnePole(400f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                b[i] = b[i] * Exp(t, 3f) + lp.Run(n.Next()) * Exp(t, 4f) * 1.2f;
            }
            Sweep(b, 0.05f, 0.8f, 700f, 180f, 0.35f, 0.2f, false);
            return Normalize(b, 0.95f);
        }

        private static float[] Win()
        {
            float[] b = Bells(new[] { 72f, 76f, 79f, 84f }, new[] { 0f, 0.12f, 0.24f, 0.4f }, 1.5f, 3f, 0.3f);
            Pad(b, 0.4f, new[] { 72f, 76f, 79f }, 0.9f, 0.12f, false, 0.05f, 0.5f);
            return Normalize(b, 0.8f);
        }

        private static float[] Lose()
        {
            float[] b = Bells(new[] { 76f, 74f, 72f, 69f }, new[] { 0f, 0.22f, 0.44f, 0.7f }, 1.5f, 2.4f, 0.3f);
            return Normalize(b, 0.7f);
        }

        /// <summary>Cồng mở trận: họa âm trầm lệch nhau ngân dài + tiếng gõ.</summary>
        private static float[] Gong()
        {
            float[] b = Buf(0.9f);
            float[] parts = { 110f, 172f, 263f, 349f, 451f };
            float[] decays = { 2.5f, 3f, 4f, 5f, 7f };
            for (int p = 0; p < parts.Length; p++)
            {
                var s = new Sine(parts[p]);
                for (int i = 0; i < b.Length; i++) b[i] += s.Next() * Exp(T(i), decays[p]) * (0.5f - p * 0.07f) * Attack(T(i), 0.002f);
            }
            var n = new Noise(199u);
            var lp = new OnePole(1200f);
            for (int i = 0; i < N(0.05f); i++) b[i] += lp.Run(n.Next()) * Exp(T(i), 60f);
            return Normalize(b, 0.85f);
        }

        private static float[] UiOpen()
        {
            float[] b = Buf(0.13f);
            Chirp(b, 0f, 0.04f, 660f, 1.15f, 0.7f, 50f, false);
            Chirp(b, 0.045f, 0.07f, 990f, 1.1f, 0.6f, 40f, false);
            return Normalize(b, 0.6f);
        }

        private static float[] UiTap()
        {
            float[] b = Buf(0.05f);
            var s = new Sine(1400f);
            var n = new Noise(211u);
            for (int i = 0; i < b.Length; i++) b[i] = s.Next() * Exp(T(i), 90f) + n.Next() * Exp(T(i), 300f) * 0.2f;
            return Normalize(b, 0.5f);
        }

        /// <summary>"Ting-ting" khi nhận Coin (như bản cũ của UiSounds).</summary>
        private static float[] CoinGain() => Normalize(Bells(new[] { 88f, 95f }, new[] { 0f, 0.09f }, 0.5f, 8.5f, 0.32f), 0.75f);

        /// <summary>"Cạch-ting" khi tiêu Coin.</summary>
        private static float[] CoinSpend()
        {
            float[] b = Bells(new[] { 84f, 96f }, new[] { 0.035f, 0.035f }, 0.45f, 8f, 0.25f);
            var n = new Noise(223u);
            for (int i = 0; i < N(0.02f); i++) b[i] += n.Next() * (1f - i / (float)N(0.02f)) * 0.35f;
            return Normalize(b, 0.7f);
        }

        // ------------------------------------------------------------------ tiếng lặp

        public static float[] Loop(LoopId id)
        {
            switch (id)
            {
                case LoopId.Wind: return Wind();
                case LoopId.Pond: return Pond();
                case LoopId.Bath: return Bath();
                case LoopId.Sleep: return SleepBreath();
                default: return Purr();
            }
        }

        /// <summary>
        /// Gió nhẹ + lá cỏ xào xạc (12 giây): nhiễu lọc thấp, độ đục + độ to trôi chậm theo chu kỳ khít 12 giây;
        /// thêm những đợt lá xào xạc (nhiễu cao, có lúc ngắt quãng như lá chạm nhau). Nối vòng bằng trộn mờ 1 giây.
        /// </summary>
        private static float[] Wind()
        {
            const float len = 12f, fade = 1f;
            float[] b = Buf(len + fade);
            var n = new Noise(229u);
            var lp = new OnePole(500f);
            var lp2 = new OnePole(500f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                double lfo1 = Math.Sin(TwoPi * t / len), lfo2 = Math.Sin(TwoPi * 2.0 * t / len + 1.3);
                lp.SetCutoff(420f + 260f * (float)lfo1 + 120f * (float)lfo2);
                lp2.SetCutoff(700f);
                float gust = 0.55f + 0.25f * (float)lfo2 + 0.2f * (float)Math.Sin(TwoPi * 3.0 * t / len);
                b[i] = lp2.Run(lp.Run(n.Next())) * gust * 2.2f;
            }
            var r = new Noise(233u);
            for (int k = 0; k < 14; k++)   // lá xào xạc
            {
                float s = r.Unit() * len, dur = 0.35f + r.Unit() * 0.8f, amp = 0.08f + r.Unit() * 0.12f;
                var hp = new OnePole(2600f);
                var lp3 = new OnePole(7000f);
                int i0 = N(s);
                for (int i = 0; i < N(dur); i++)
                {
                    float t = T(i), u = t / dur;
                    float x = r.Next();
                    float crackle = (r.Unit() < 0.7f) ? 1f : 0.2f;   // ngắt quãng: lá chạm nhau
                    Put(b, i0 + i, lp3.Run(x - hp.Run(x)) * crackle * amp * (float)Math.Sin(Math.PI * u), true);
                }
            }
            return Normalize(SeamFade(b, N(len), N(fade)), 0.55f);
        }

        /// <summary>Nước hồ (6 giây): tiếng óc ách thấp + bọt nước lách tách.</summary>
        private static float[] Pond()
        {
            const float len = 6f, fade = 0.8f;
            float[] b = Buf(len + fade);
            var n = new Noise(239u);
            var lp = new OnePole(300f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                b[i] = lp.Run(n.Next()) * (0.6f + 0.4f * (float)Math.Sin(TwoPi * t / 2.0)) * 1.6f;
            }
            for (int k = 0; k < 40; k++) Chirp(b, n.Unit() * len, 0.025f + n.Unit() * 0.02f, 300f + n.Unit() * 600f, 1.8f, 0.12f + n.Unit() * 0.1f, 90f, true);
            return Normalize(SeamFade(b, N(len), N(fade)), 0.5f);
        }

        /// <summary>Đang tắm (2,4 giây): té nước nhỏ + bọt sủi.</summary>
        private static float[] Bath()
        {
            const float len = 2.4f;
            float[] b = Buf(len);
            var n = new Noise(241u);
            for (int k = 0; k < 4; k++)
            {
                float s = k * 0.6f + n.Unit() * 0.2f, dur = 0.15f + n.Unit() * 0.15f;
                var hp = new OnePole(500f);
                var lp = new OnePole(2200f);
                int i0 = N(s);
                for (int i = 0; i < N(dur); i++)
                {
                    float t = T(i);
                    float x = n.Next();
                    Put(b, i0 + i, lp.Run(x - hp.Run(x)) * Attack(t, 0.004f) * Exp(t, 14f) * 0.9f, true);
                }
            }
            for (int k = 0; k < 12; k++) Chirp(b, n.Unit() * len, 0.03f, 400f + n.Unit() * 800f, 1.7f, 0.25f, 70f, true);
            return Normalize(b, 0.65f);
        }

        /// <summary>Thở khi ngủ (4 giây): hít vào sột nhẹ, thở ra dài kèm tiếng "khò" rất trầm.</summary>
        private static float[] SleepBreath()
        {
            const float len = 4f;
            float[] b = Buf(len);
            var n = new Noise(251u);
            var lpIn = new OnePole(550f);
            var lpOut = new OnePole(380f);
            var snore = new Sine(105f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                float x = n.Next();
                float inhale = t < 1.5f ? (float)Math.Sin(Math.PI * t / 1.5f) : 0f;
                float exhale = t >= 1.7f && t < 3.9f ? (float)Math.Sin(Math.PI * (t - 1.7f) / 2.2f) : 0f;
                float zz = snore.Next() * (0.6f + 0.4f * (float)Math.Sin(TwoPi * 30.0 * t));
                b[i] = lpIn.Run(x) * inhale * 1.3f + (lpOut.Run(x) * 1.1f + zz * 0.12f) * exhale;
            }
            return Normalize(b, 0.5f);
        }

        /// <summary>Gừ gừ khi được vuốt ve (1,2 giây): chuỗi nhịp 25 lần / giây (khít 30 nhịp), phồng lên xẹp xuống.</summary>
        private static float[] Purr()
        {
            const float len = 1.2f;
            float[] b = Buf(len);
            var n = new Noise(257u);
            var lp = new OnePole(600f);
            var tone = new Sine(120f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = T(i);
                double pulse = Math.Pow(0.5 + 0.5 * Math.Sin(TwoPi * 25.0 * t), 3);
                float swell = 0.65f + 0.35f * (float)Math.Sin(TwoPi * t / len);
                b[i] = (float)pulse * swell * (lp.Run(n.Next()) * 1.4f + tone.Next() * 0.45f);
            }
            return Normalize(b, 0.6f);
        }

        // ------------------------------------------------------------------ nhạc nền

        public static float[] Music(MusicId id) => id == MusicId.Battle ? BattleMusic() : GardenMusic();

        /// <summary>Độ dài 1 vòng nhạc (giây): 8 ô nhịp 4/4.</summary>
        public static float MusicSeconds(MusicId id) => 32f * 60f / (id == MusicId.Battle ? 128f : 90f);

        /// <summary>
        /// Nhạc vườn: êm, vui, 90 nhịp/phút, Đô trưởng (C – Am – F – G...). Mộc cầm chơi giai điệu, đệm nền mềm,
        /// bè trầm nhẹ, tiếng lắc + gõ gỗ rất nhỏ. 8 ô nhịp, vòng liền mạch.
        /// </summary>
        private static float[] GardenMusic()
        {
            const float bpm = 90f;
            float beat = 60f / bpm;
            float[] b = Buf(MusicSeconds(MusicId.Garden));
            // hợp âm theo ô nhịp: (nhịp bắt đầu, số nhịp, các nốt đệm, nốt trầm)
            var chords = new (float at, float len, float[] pad, float bass)[]
            {
                (0, 4, new[] { 60f, 64f, 67f }, 48f), (4, 4, new[] { 57f, 60f, 64f }, 45f),
                (8, 4, new[] { 57f, 60f, 65f }, 41f), (12, 4, new[] { 59f, 62f, 67f }, 43f),
                (16, 4, new[] { 60f, 64f, 67f }, 48f), (20, 4, new[] { 57f, 60f, 64f }, 45f),
                (24, 2, new[] { 57f, 60f, 65f }, 41f), (26, 2, new[] { 59f, 62f, 67f }, 43f),
                (28, 4, new[] { 60f, 64f, 67f }, 48f),
            };
            foreach (var c in chords)
            {
                Pad(b, c.at * beat, c.pad, c.len * beat, 0.055f, true, 0.35f, 0.7f);
                Bass(b, c.at * beat, Midi(c.bass), beat * 1.6f, 0.2f, 3f, true);
                if (c.len >= 4) Bass(b, (c.at + 2) * beat, Midi(c.bass + 7), beat * 1.6f, 0.15f, 3f, true);
            }
            var melody = new (float at, float midi, float len)[]
            {
                (0, 76, 1), (1, 79, 0.5f), (1.5f, 81, 0.5f), (2, 79, 1), (3, 76, 0.5f), (3.5f, 74, 0.5f),
                (4, 72, 1), (5, 76, 0.5f), (5.5f, 74, 0.5f), (6, 72, 1), (7, 69, 1),
                (8, 69, 0.5f), (8.5f, 72, 0.5f), (9, 74, 1), (10, 77, 1), (11, 76, 0.5f), (11.5f, 74, 0.5f),
                (12, 74, 1.5f), (13.5f, 71, 0.5f), (14, 67, 1),
                (16, 76, 1), (17, 79, 0.5f), (17.5f, 81, 0.5f), (18, 84, 1), (19, 81, 0.5f), (19.5f, 79, 0.5f),
                (20, 76, 1), (21, 74, 0.5f), (21.5f, 72, 0.5f), (22, 74, 1), (23, 76, 1),
                (24, 77, 1), (25, 76, 0.5f), (25.5f, 74, 0.5f), (26, 74, 1), (27, 71, 1),
                (28, 72, 2),
            };
            foreach (var m in melody) Mallet(b, m.at * beat, Midi(m.midi), 0.24f, 3.2f / Math.Max(0.5f, m.len), true);
            for (int k = 0; k < 64; k++)   // tiếng lắc mỗi nửa nhịp + gõ gỗ ở nhịp 2 và 4
            {
                Shaker(b, k * beat * 0.5f, k % 2 == 0 ? 0.05f : 0.03f, (uint)(300 + k), true);
                if (k % 4 == 2) Wood(b, k * beat * 0.5f, 0.07f, true);
            }
            return Normalize(b, 0.7f);
        }

        /// <summary>
        /// Nhạc trận: dồn dập, 128 nhịp/phút, La thứ (Am – F – C – G – Am – F – E – E). Trống (kick / snare / hi-hat),
        /// bè trầm móc 1/8, rải hợp âm 1/16, dằn hợp âm đầu mỗi ô. 8 ô nhịp, vòng liền mạch.
        /// </summary>
        private static float[] BattleMusic()
        {
            const float bpm = 128f;
            float beat = 60f / bpm;
            float[] b = Buf(MusicSeconds(MusicId.Battle));
            float[] roots = { 45f, 41f, 48f, 43f, 45f, 41f, 40f, 40f };
            float[][] tones =
            {
                new[] { 69f, 72f, 76f, 81f }, new[] { 65f, 69f, 72f, 77f }, new[] { 72f, 76f, 79f, 84f }, new[] { 67f, 71f, 74f, 79f },
                new[] { 69f, 72f, 76f, 81f }, new[] { 65f, 69f, 72f, 77f }, new[] { 68f, 71f, 76f, 80f }, new[] { 68f, 71f, 76f, 83f },
            };
            int[] bassPattern = { 0, 0, 12, 0, 0, 0, 7, 12 };
            for (int bar = 0; bar < 8; bar++)
            {
                float bar0 = bar * 4f * beat;
                for (int e = 0; e < 8; e++)
                    Saw(b, bar0 + e * beat * 0.5f, Midi(roots[bar] + bassPattern[e]), beat * 0.45f, 0.14f, 9f, true);
                for (int s = 0; s < 16; s++)   // rải hợp âm 1/16
                    Square(b, bar0 + s * beat * 0.25f, Midi(tones[bar][s % 4]), beat * 0.22f, s % 4 == 0 ? 0.05f : 0.035f, 14f, true);
                Pad(b, bar0, new[] { tones[bar][0] - 12f, tones[bar][1] - 12f, tones[bar][2] - 12f }, beat * 1.2f, 0.05f, true, 0.01f, 0.25f);
                for (int q = 0; q < 4; q++)
                {
                    float at = bar0 + q * beat;
                    if (q == 0 || q == 2) Kick(b, at, 0.5f, true);
                    if (q == 2) Kick(b, at + beat * 0.5f, 0.35f, true);
                    if (q == 1 || q == 3) Snare(b, at, 0.32f, (uint)(400 + bar * 4 + q), true);
                    Hat(b, at, 0.09f, (uint)(500 + bar * 8 + q * 2), true);
                    Hat(b, at + beat * 0.5f, 0.06f, (uint)(501 + bar * 8 + q * 2), true);
                }
            }
            return Normalize(b, 0.72f);
        }

        // ------------------------------------------------------------------ nhạc cụ (ghi vào mảng, wrap = vòng lại đầu bài)

        /// <summary>Mộc cầm: âm gốc + họa âm ×3,93 / ×9,2 tắt nhanh hơn.</summary>
        private static void Mallet(float[] b, float start, float freq, float gain, float decay, bool wrap)
        {
            int s = N(start), n = N(Math.Min(3f, 6f / decay));
            var p1 = new Sine(freq);
            var p2 = new Sine(freq * 3.93f);
            var p3 = new Sine(freq * 9.2f);
            for (int i = 0; i < n; i++)
            {
                float t = T(i);
                float env = Attack(t, 0.003f);
                float v = p1.Next() * Exp(t, decay) + 0.28f * p2.Next() * Exp(t, decay * 3f) + 0.08f * p3.Next() * Exp(t, decay * 7f);
                Put(b, s + i, v * env * gain, wrap);
            }
        }

        /// <summary>Đệm nền: mỗi nốt 2 giọng lệch nhẹ (dày, ấm), vào chậm `attack` giây, ngân thêm `release` giây.</summary>
        private static void Pad(float[] b, float start, float[] midis, float seconds, float gain, bool wrap, float attack, float release)
        {
            int s = N(start), n = N(seconds + release);
            foreach (float m in midis)
            {
                float f = Midi(m);
                var a = new Sine(f * 0.997f);
                var c = new Sine(f * 1.003f);
                var o = new Sine(f * 2f);
                for (int i = 0; i < n; i++)
                {
                    float t = T(i);
                    float env = Math.Min(1f, t / Math.Max(0.001f, attack)) * (t <= seconds ? 1f : Math.Max(0f, 1f - (t - seconds) / Math.Max(0.001f, release)));
                    Put(b, s + i, (a.Next() + c.Next() + 0.3f * o.Next()) * env * gain, wrap);
                }
            }
        }

        private static void Bass(float[] b, float start, float freq, float seconds, float gain, float decay, bool wrap)
        {
            int s = N(start), n = N(seconds);
            var p1 = new Sine(freq);
            var p2 = new Sine(freq * 2f);
            for (int i = 0; i < n; i++)
            {
                float t = T(i);
                float env = Attack(t, 0.01f) * Exp(t, decay) * Math.Min(1f, (seconds - t) / 0.05f);
                Put(b, s + i, (p1.Next() + 0.2f * p2.Next()) * env * gain, wrap);
            }
        }

        /// <summary>Răng cưa êm (6 họa âm): bè trầm trận đấu.</summary>
        private static void Saw(float[] b, float start, float freq, float seconds, float gain, float decay, bool wrap)
        {
            int s = N(start), n = N(seconds);
            var h = new Sine[6];
            for (int k = 0; k < h.Length; k++) h[k] = new Sine(freq * (k + 1));
            for (int i = 0; i < n; i++)
            {
                float t = T(i);
                float v = 0f;
                for (int k = 0; k < h.Length; k++) v += h[k].Next() / (k + 1);
                Put(b, s + i, v * Attack(t, 0.004f) * Exp(t, decay) * Math.Min(1f, (seconds - t) / 0.02f) * gain, wrap);
            }
        }

        /// <summary>Vuông êm (họa âm lẻ 1 / 3 / 5): rải hợp âm.</summary>
        private static void Square(float[] b, float start, float freq, float seconds, float gain, float decay, bool wrap)
        {
            int s = N(start), n = N(seconds);
            var p1 = new Sine(freq);
            var p3 = new Sine(freq * 3f);
            var p5 = new Sine(freq * 5f);
            for (int i = 0; i < n; i++)
            {
                float t = T(i);
                float v = p1.Next() + p3.Next() / 3f + p5.Next() / 5f;
                Put(b, s + i, v * Attack(t, 0.002f) * Exp(t, decay) * Math.Min(1f, (seconds - t) / 0.01f) * gain, wrap);
            }
        }

        private static void Kick(float[] b, float start, float gain, bool wrap)
        {
            int s = N(start);
            double ph = 0;
            for (int i = 0; i < N(0.25f); i++)
            {
                float t = T(i);
                double f = 45.0 + 75.0 * Math.Exp(-t * 30.0);
                ph += TwoPi * f / Rate;
                Put(b, s + i, (float)Math.Sin(ph) * Exp(t, 12f) * gain, wrap);
            }
        }

        private static void Snare(float[] b, float start, float gain, uint seed, bool wrap)
        {
            int s = N(start);
            var n = new Noise(seed);
            var hp = new OnePole(1200f);
            var tone = new Sine(190f);
            for (int i = 0; i < N(0.2f); i++)
            {
                float t = T(i);
                float x = n.Next();
                Put(b, s + i, ((x - hp.Run(x)) * Exp(t, 18f) + tone.Next() * Exp(t, 25f) * 0.5f) * gain, wrap);
            }
        }

        private static void Hat(float[] b, float start, float gain, uint seed, bool wrap)
        {
            int s = N(start);
            var n = new Noise(seed);
            var hp = new OnePole(6000f);
            for (int i = 0; i < N(0.05f); i++)
            {
                float x = n.Next();
                Put(b, s + i, (x - hp.Run(x)) * Exp(T(i), 60f) * gain, wrap);
            }
        }

        private static void Shaker(float[] b, float start, float gain, uint seed, bool wrap)
        {
            int s = N(start);
            var n = new Noise(seed);
            var hp = new OnePole(3500f);
            var lp = new OnePole(9000f);
            for (int i = 0; i < N(0.08f); i++)
            {
                float t = T(i);
                float x = n.Next();
                Put(b, s + i, lp.Run(x - hp.Run(x)) * Attack(t, 0.012f) * Exp(t, 30f) * gain, wrap);
            }
        }

        private static void Wood(float[] b, float start, float gain, bool wrap)
        {
            int s = N(start);
            var p = new Sine(820f);
            var q = new Sine(1640f * 1.01f);
            for (int i = 0; i < N(0.08f); i++)
            {
                float t = T(i);
                Put(b, s + i, (p.Next() + 0.4f * q.Next()) * Exp(t, 55f) * gain, wrap);
            }
        }

        /// <summary>Các tiếng chuông (âm gốc + họa âm ×2,76 như chuông thật) theo midi + lúc bắt đầu.</summary>
        private static float[] Bells(float[] midis, float[] starts, float seconds, float decay, float gain)
        {
            float[] b = Buf(seconds);
            for (int k = 0; k < midis.Length; k++)
            {
                float f = Midi(midis[k]);
                var p1 = new Sine(f);
                var p2 = new Sine(f * 2.76f);
                int s = N(starts[k]);
                for (int i = 0; s + i < b.Length; i++)
                {
                    float t = T(i);
                    b[s + i] += (p1.Next() * Exp(t, decay) + 0.3f * p2.Next() * Exp(t, decay * 2.2f)) * Attack(t, 0.002f) * gain;
                }
            }
            return b;
        }

        /// <summary>Nốt ngắn trượt cao độ (nhân `ratio` trong `seconds` giây), tắt theo `decay` (giọt nước, póc, bong bóng).</summary>
        private static void Chirp(float[] b, float start, float seconds, float f0, float ratio, float gain, float decay, bool wrap)
        {
            int s = N(start), n = N(seconds);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = T(i), u = t / seconds;
                ph += TwoPi * f0 * Math.Pow(ratio, u) / Rate;
                Put(b, s + i, (float)Math.Sin(ph) * Attack(t, 0.002f) * Exp(t, decay) * gain, wrap);
            }
        }

        /// <summary>Nốt trượt tuyến tính f0 → f1, bao hình sin (lên rồi xuống); `h2` = độ to họa âm 2; `vibrato` Hz (0 = không).</summary>
        private static void Sweep(float[] b, float start, float seconds, float f0, float f1, float gain, float h2, bool wrap, float vibrato = 0f)
        {
            int s = N(start), n = N(seconds);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = T(i), u = t / seconds;
                double f = f0 + (f1 - f0) * u;
                if (vibrato > 0f) f *= 1.0 + 0.03 * Math.Sin(TwoPi * vibrato * t);
                ph += TwoPi * f / Rate;
                float env = (float)Math.Sin(Math.PI * u);
                Put(b, s + i, (float)(Math.Sin(ph) + h2 * Math.Sin(2 * ph)) * env * gain, wrap);
            }
        }

        // ------------------------------------------------------------------ cơ bản

        /// <summary>Nhiễu trắng có seed (xorshift): cùng seed ra cùng chuỗi.</summary>
        private sealed class Noise
        {
            private uint _s;

            public Noise(uint seed) => _s = seed * 2654435761U | 1U;

            private uint NextU()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return _s;
            }

            /// <summary>-1..1</summary>
            public float Next() => (NextU() & 0xFFFFFF) / 8388608f - 1f;

            /// <summary>0..1</summary>
            public float Unit() => (NextU() & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Bộ lọc thấp 1 cực (cao = x − thấp(x)).</summary>
        private struct OnePole
        {
            private float _y, _a;

            public OnePole(float cutoff)
            {
                _y = 0f;
                _a = Coef(cutoff);
            }

            public void SetCutoff(float cutoff) => _a = Coef(cutoff);

            public float Run(float x)
            {
                _y += _a * (x - _y);
                return _y;
            }

            private static float Coef(float fc) => 1f - (float)Math.Exp(-TwoPi * Math.Max(10f, Math.Min(fc, Rate * 0.45f)) / Rate);
        }

        /// <summary>Dao động sin tần số cố định bằng phép quay (nhanh, không gọi Math.Sin mỗi mẫu).</summary>
        private struct Sine
        {
            private double _c, _s;
            private readonly double _dc, _ds;

            public Sine(double freq)
            {
                double w = TwoPi * freq / Rate;
                _dc = Math.Cos(w);
                _ds = Math.Sin(w);
                _c = 1.0;
                _s = 0.0;
            }

            public float Next()
            {
                double s = _s, c = _c;
                _s = s * _dc + c * _ds;
                _c = c * _dc - s * _ds;
                return (float)s;
            }
        }

        public static float Midi(float m) => 440f * (float)Math.Pow(2.0, (m - 69.0) / 12.0);

        private static int N(float seconds) => Math.Max(0, (int)Math.Round(seconds * Rate));

        private static float T(int i) => i / (float)Rate;

        private static float[] Buf(float seconds) => new float[Math.Max(1, N(seconds))];

        private static float Attack(float t, float seconds) => t <= 0f ? 0f : Math.Min(1f, t / seconds);

        private static float Exp(float t, float decay) => t <= 0f ? (t == 0f ? 1f : 0f) : (float)Math.Exp(-t * decay);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static void Put(float[] b, int index, float v, bool wrap)
        {
            if (wrap) b[((index % b.Length) + b.Length) % b.Length] += v;
            else if (index >= 0 && index < b.Length) b[index] += v;
        }

        /// <summary>Mảng dài len + fade mẫu → vòng dài len mẫu: trộn mờ đoạn thừa cuối vào đầu (nối liền mạch).</summary>
        private static float[] SeamFade(float[] b, int len, int fade)
        {
            var o = new float[len];
            Array.Copy(b, o, len);
            for (int i = 0; i < fade && len + i < b.Length; i++)
            {
                float k = i / (float)fade;
                o[i] = b[i] * k + b[len + i] * (1f - k);
            }
            return o;
        }

        /// <summary>Kéo đỉnh về `peak` (mọi tiếng to ngang nhau trước khi chỉnh âm lượng lúc phát).</summary>
        private static float[] Normalize(float[] b, float peak)
        {
            float max = 0f;
            foreach (float v in b) max = Math.Max(max, Math.Abs(v));
            if (max < 1e-6f) return b;
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
            return b;
        }
    }
}
