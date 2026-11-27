using System;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Giai đoạn sức khỏe (GDD G3). Ốm / Nguy kịch là "bệnh thật": chăm đủ không tự khỏi, phải chữa.</summary>
    public enum HealthStage { Healthy = 0, Tired = 1, Sick = 2, Critical = 3, Dead = 4 }

    /// <summary>Cách chữa (GDD G4).</summary>
    public enum TreatmentKind { CarefulCare = 0, Medicine = 1 }

    /// <summary>3 việc của 1 lượt chăm kỹ: cho ăn cháo, đắp chăn (ngủ trong ổ), lau người.</summary>
    public enum CarefulCareKind { Porridge = 0, Blanket = 1, Wipe = 2 }

    /// <summary>Kết quả của 1 lần tính sức khỏe.</summary>
    public enum HealthChange
    {
        None, BecameHealthy, BecameTired, FellSick, Worsened,
        RecoveryStarted,    // lượt chăm kỹ đầu tiên / uống thuốc: bắt đầu hồi phục
        CarefulRoundDone,   // thêm 1 lượt chăm kỹ khi đang hồi phục: hồi phục tiếp 12h nữa
        Relapsed, Cured, Died,
    }

    /// <summary>Đang hồi phục (sau khi chăm kỹ đủ 1 lượt hoặc uống thuốc).</summary>
    public sealed class RecoveryState
    {
        public TreatmentKind Method;

        /// <summary>Đã hồi phục được bao nhiêu giờ / cần bao nhiêu giờ (24h nếu Ốm, 48h nếu Nguy kịch).</summary>
        public double ProgressHours;
        public double RequiredHours;

        public DateTime StartedUtc;

        /// <summary>Lần chăm gần nhất. Quá 24h không chăm thì tái phát.</summary>
        public DateTime LastCareUtc;

        /// <summary>Hồi phục chỉ tiến triển tới mốc này. Mỗi lượt chăm kỹ (hoặc thuốc + chăm thường) đẩy mốc thêm 12h.</summary>
        public DateTime CoveredUntilUtc;

        public RecoveryState Clone() => (RecoveryState)MemberwiseClone();
    }

    /// <summary>Đã làm việc nào của lượt chăm kỹ hiện tại (và lúc nào).</summary>
    public sealed class CarefulCareMarks
    {
        public DateTime? PorridgeUtc;
        public DateTime? BlanketUtc;
        public DateTime? WipeUtc;

        public CarefulCareMarks Clone() => (CarefulCareMarks)MemberwiseClone();
    }

    /// <summary>Sức khỏe của 1 con thú (lưu trong file).</summary>
    public sealed class PetHealth
    {
        public HealthStage Stage = HealthStage.Healthy;

        /// <summary>Số giờ bị bỏ bê liên tục (GDD G2). Về 0 khi được chăm đủ (nếu chưa ốm). Dừng lại khi đang hồi phục.</summary>
        public double NeglectHours;

        /// <summary>null = không đang hồi phục.</summary>
        public RecoveryState Recovery;

        public CarefulCareMarks Marks = new CarefulCareMarks();

        public DateTime? DiedAtUtc;

        /// <summary>Bản sao sâu (để mô phỏng tới trước mà không đụng dữ liệu thật — vd dự đoán thông báo). Thêm trường mới nhớ chép ở đây.</summary>
        public PetHealth Clone() => new PetHealth
        {
            Stage = Stage,
            NeglectHours = NeglectHours,
            Recovery = Recovery?.Clone(),
            Marks = Marks?.Clone() ?? new CarefulCareMarks(),
            DiedAtUtc = DiedAtUtc,
        };
    }

    /// <summary>
    /// Luật sức khỏe (C# thuần, không trạng thái): bỏ bê -> Mệt -> Ốm -> Nguy kịch -> Chết; chữa -> hồi phục -> khỏi;
    /// đang hồi phục mà bỏ mặc -> tái phát. Mọi số lấy từ <see cref="HealthConfig"/>.
    /// </summary>
    public static class HealthRules
    {
        public static bool IsIll(HealthStage s) => s == HealthStage.Sick || s == HealthStage.Critical;

        /// <summary>Ốm / Nguy kịch thì cấm tập luyện + đấu Rank (GDD G3).</summary>
        public static bool CanTrain(HealthStage s) => s == HealthStage.Healthy || s == HealthStage.Tired;

        /// <summary>Hệ số chỉ số theo giai đoạn (Phase 2.2 nhân vào chỉ số).</summary>
        public static float StatFactor(HealthStage s, HealthConfig c)
        {
            switch (s)
            {
                case HealthStage.Tired: return c.TiredStatFactor;
                case HealthStage.Sick: return c.SickStatFactor;
                case HealthStage.Critical: return c.CriticalStatFactor;
                case HealthStage.Dead: return 0f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Áp 1 bước thời gian `hours` kết thúc lúc `endUtc` (nhu cầu đã được tính xong cho bước này).
        /// </summary>
        public static HealthChange Step(PetHealth h, PetNeeds needs, double hours, DateTime endUtc, GameConfig cfg)
        {
            if (h == null) throw new ArgumentNullException(nameof(h));
            HealthConfig c = cfg.Health;
            if (h.Stage == HealthStage.Dead || hours <= 0) return HealthChange.None;
            bool fullyCared = needs.AllAtLeast(cfg.Needs.FullCareThreshold);

            // chưa ốm: chăm đủ thì đồng hồ bỏ bê về 0, không thì cứ tăng
            if (!IsIll(h.Stage))
            {
                HealthStage before = h.Stage;
                h.NeglectHours = fullyCared ? 0 : h.NeglectHours + hours;
                h.Stage = h.NeglectHours >= c.SickAfterHours ? HealthStage.Sick
                        : h.NeglectHours >= c.TiredAfterHours ? HealthStage.Tired
                        : HealthStage.Healthy;
                if (h.Stage == before) return HealthChange.None;
                return h.Stage == HealthStage.Sick ? HealthChange.FellSick
                     : h.Stage == HealthStage.Tired ? HealthChange.BecameTired
                     : HealthChange.BecameHealthy;
            }

            // đang hồi phục: bệnh không nặng thêm, tiến triển trong khoảng còn được "che" bởi lượt chăm gần nhất
            RecoveryState r = h.Recovery;
            if (r != null)
            {
                DateTime start = endUtc.AddHours(-hours);
                if (r.StartedUtc > start) start = r.StartedUtc;
                DateTime until = r.CoveredUntilUtc < endUtc ? r.CoveredUntilUtc : endUtc;
                if (until > start) r.ProgressHours += (until - start).TotalHours;
                if (r.ProgressHours >= r.RequiredHours - 1e-6)
                {
                    h.Stage = HealthStage.Healthy;
                    h.NeglectHours = 0;
                    h.Recovery = null;
                    h.Marks = new CarefulCareMarks();
                    return HealthChange.Cured;
                }
                if ((endUtc - r.LastCareUtc).TotalHours > c.RelapseAfterHours)
                {
                    h.Recovery = null;
                    h.Marks = new CarefulCareMarks();
                    return HealthChange.Relapsed;
                }
                return HealthChange.None;
            }

            // ốm mà chưa chữa: chỉ nặng thêm khi tiếp tục bị bỏ bê (chăm đủ thì dừng lại, nhưng không tự khỏi)
            if (!fullyCared) h.NeglectHours += hours;
            if (h.NeglectHours >= c.DeathAfterHours)
            {
                h.Stage = HealthStage.Dead;
                h.DiedAtUtc = endUtc.AddHours(-(h.NeglectHours - c.DeathAfterHours));
                h.Recovery = null;
                return HealthChange.Died;
            }
            if (h.Stage == HealthStage.Sick && h.NeglectHours >= c.CriticalAfterHours)
            {
                h.Stage = HealthStage.Critical;
                return HealthChange.Worsened;
            }
            return HealthChange.None;
        }

        /// <summary>
        /// Gọi ngay sau mỗi lần chăm (cho ăn, lau, vuốt ve...): đang hồi phục thì tính là "có chăm" (chống tái phát);
        /// chưa ốm mà chăm đủ thì đồng hồ bỏ bê về 0 (Mệt -> khỏe lại ngay, GDD G4).
        /// </summary>
        public static HealthChange AfterCare(PetHealth h, PetNeeds needs, DateTime now, GameConfig cfg)
        {
            if (h.Stage == HealthStage.Dead) return HealthChange.None;
            RecoveryState r = h.Recovery;
            if (r != null)
            {
                r.LastCareUtc = now;
                if (r.Method == TreatmentKind.Medicine) Extend(r, now, cfg.Health);   // đã uống thuốc: chăm thường là đủ
            }
            return CheckFullCare(h, needs, cfg);
        }

        /// <summary>Chưa ốm mà cả 4 nhu cầu đạt ngưỡng: xóa đồng hồ bỏ bê.</summary>
        public static HealthChange CheckFullCare(PetHealth h, PetNeeds needs, GameConfig cfg)
        {
            if (IsIll(h.Stage) || h.Stage == HealthStage.Dead || !needs.AllAtLeast(cfg.Needs.FullCareThreshold))
                return HealthChange.None;
            h.NeglectHours = 0;
            if (h.Stage == HealthStage.Healthy) return HealthChange.None;
            h.Stage = HealthStage.Healthy;
            return HealthChange.BecameHealthy;
        }

        /// <summary>
        /// Đánh dấu 1 việc chăm kỹ khi thú đang ốm. Đủ cháo + chăn + lau trong 12h = 1 lượt:
        /// bắt đầu hồi phục (lượt đầu) hoặc giữ cho hồi phục tiếp thêm 12h (các lượt sau).
        /// </summary>
        public static HealthChange MarkCarefulCare(PetHealth h, CarefulCareKind kind, DateTime now, HealthConfig c)
        {
            if (!IsIll(h.Stage)) return HealthChange.None;
            CarefulCareMarks m = h.Marks ?? (h.Marks = new CarefulCareMarks());
            switch (kind)
            {
                case CarefulCareKind.Porridge: m.PorridgeUtc = now; break;
                case CarefulCareKind.Blanket: m.BlanketUtc = now; break;
                default: m.WipeUtc = now; break;
            }
            if (h.Recovery != null) h.Recovery.LastCareUtc = now;
            m.PorridgeUtc = Fresh(m.PorridgeUtc, now, c.CarefulRoundWindowHours);
            m.BlanketUtc = Fresh(m.BlanketUtc, now, c.CarefulRoundWindowHours);
            m.WipeUtc = Fresh(m.WipeUtc, now, c.CarefulRoundWindowHours);
            if (m.PorridgeUtc == null || m.BlanketUtc == null || m.WipeUtc == null) return HealthChange.None;
            h.Marks = new CarefulCareMarks();   // xong 1 lượt, lượt sau làm lại từ đầu
            HealthChange began = BeginOrExtend(h, TreatmentKind.CarefulCare, now, c);
            return began == HealthChange.RecoveryStarted ? began : HealthChange.CarefulRoundDone;
        }

        /// <summary>Uống thuốc khi đang ốm: bắt đầu hồi phục ngay (Phase 5.1 trừ Coin trước khi gọi).</summary>
        public static HealthChange GiveMedicine(PetHealth h, DateTime now, HealthConfig c) =>
            IsIll(h.Stage) ? BeginOrExtend(h, TreatmentKind.Medicine, now, c) : HealthChange.None;

        /// <summary>Tiến độ hồi phục 0..1 (0 nếu không đang hồi phục).</summary>
        public static float RecoveryProgress01(PetHealth h)
        {
            RecoveryState r = h?.Recovery;
            if (r == null || r.RequiredHours <= 0) return 0f;
            double p = r.ProgressHours / r.RequiredHours;
            return p <= 0 ? 0f : (p >= 1 ? 1f : (float)p);
        }

        /// <summary>Số việc chăm kỹ đã làm trong lượt hiện tại (0..3).</summary>
        public static int CarefulMarksDone(PetHealth h, DateTime now, HealthConfig c)
        {
            CarefulCareMarks m = h?.Marks;
            if (m == null) return 0;
            int n = 0;
            if (Fresh(m.PorridgeUtc, now, c.CarefulRoundWindowHours) != null) n++;
            if (Fresh(m.BlanketUtc, now, c.CarefulRoundWindowHours) != null) n++;
            if (Fresh(m.WipeUtc, now, c.CarefulRoundWindowHours) != null) n++;
            return n;
        }

        private static HealthChange BeginOrExtend(PetHealth h, TreatmentKind method, DateTime now, HealthConfig c)
        {
            if (h.Recovery == null)
            {
                h.Recovery = new RecoveryState
                {
                    Method = method,
                    RequiredHours = h.Stage == HealthStage.Critical ? c.CriticalRecoveryHours : c.SickRecoveryHours,
                    StartedUtc = now,
                    LastCareUtc = now,
                    CoveredUntilUtc = now.AddHours(c.CareCoverHours),
                };
                return HealthChange.RecoveryStarted;
            }
            h.Recovery.LastCareUtc = now;
            if (method == TreatmentKind.Medicine) h.Recovery.Method = TreatmentKind.Medicine;
            Extend(h.Recovery, now, c);
            return HealthChange.None;
        }

        private static void Extend(RecoveryState r, DateTime now, HealthConfig c)
        {
            DateTime cover = now.AddHours(c.CareCoverHours);
            if (cover > r.CoveredUntilUtc) r.CoveredUntilUtc = cover;
        }

        private static DateTime? Fresh(DateTime? mark, DateTime now, double windowHours) =>
            mark.HasValue && (now - mark.Value).TotalHours <= windowHours && mark.Value <= now ? mark : null;
    }
}
