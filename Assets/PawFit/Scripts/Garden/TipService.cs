using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>Các mẹo hướng dẫn (vườn không có nút nên người mới cần được chỉ đúng lúc).</summary>
    public enum TipId { Move = 0, Pet = 1, Lead = 2, Feed = 3, Bathe = 4, Sleep = 5, Sick = 6, Shop = 7, Training = 8, Arena = 9 }

    public sealed class TipConfig
    {
        /// <summary>Vào vườn bấy nhiêu giây thì hiện mẹo đầu tiên.</summary>
        public float FirstTipAfterSeconds = 2.5f;

        /// <summary>2 mẹo cách nhau tối thiểu bấy nhiêu giây.</summary>
        public float MinGapSeconds = 25f;

        /// <summary>Chuỗi mở đầu (chạm đất → vuốt ve → dắt): chưa làm theo thì chờ bấy nhiêu giây mới hiện mẹo kế.</summary>
        public float IntroWaitSeconds = 40f;

        /// <summary>Chơi bấy nhiêu giây thì giới thiệu quầy hàng / sân tập / đấu trường.</summary>
        public float ShopAfterSeconds = 150f;
        public float TrainingAfterSeconds = 300f;
        public float ArenaAfterSeconds = 480f;

        /// <summary>Vắng từ bấy nhiêu giờ trở lên thì vào game có dòng "trong lúc bạn vắng".</summary>
        public float WelcomeBackHours = 1f;
    }

    /// <summary>
    /// Mẹo hướng dẫn theo tình huống (C# thuần). Mỗi mẹo hiện đúng 1 lần (lưu ở cài đặt của máy).
    /// Người chơi tự làm trước thì mẹo đó bỏ qua luôn.
    ///  * Mở đầu theo thứ tự: chạm đất → chạm thú (vuốt ve) → kéo thú (dắt).
    ///  * Theo tình huống: đói, bẩn, buồn ngủ, ốm (ốm được ưu tiên).
    ///  * Khám phá theo thời gian chơi: quầy hàng, sân tập, đấu trường.
    /// View gọi <see cref="Update"/> khi đang ở vườn và không có thẻ nào mở; báo việc người chơi đã làm qua <see cref="Done"/>.
    /// </summary>
    public sealed class TipService : IDisposable
    {
        private readonly SettingsService _settings;
        private readonly PetService _pets;
        private readonly HealthService _health;
        private readonly EvolutionService _evo;
        private readonly ExpressionConfig _expr;
        private readonly TipConfig _cfg;
        private readonly HashSet<TipId> _doneNow = new HashSet<TipId>();
        private readonly IDisposable _careSub;
        private float _played, _sinceTip;
        private bool _shownAny;
        private TipId? _lastIntro;

        public TipService(SettingsService settings, PetService pets, HealthService health, EvolutionService evo,
                          ExpressionConfig expr, EventBus bus, TipConfig cfg = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _expr = expr ?? new ExpressionConfig();
            _cfg = cfg ?? new TipConfig();
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            _careSub = bus.Subscribe<CareDone>(e =>
            {
                if (e.Result != CareResult.Ok) return;
                if (e.Kind == CareKind.Feed) Done(TipId.Feed);
                else if (e.Kind == CareKind.Bathe) Done(TipId.Bathe);
                else if (e.Kind == CareKind.Pet || e.Kind == CareKind.Wipe) Done(TipId.Pet);
                else if (e.Kind == CareKind.Sleep) Done(TipId.Sleep);
            });
        }

        public TipConfig Config => _cfg;

        public void Dispose() => _careSub?.Dispose();

        public bool Seen(TipId id) => _settings.HasSeenTip(Key(id));

        /// <summary>Người chơi đã làm việc mà mẹo này chỉ (mẹo đó không hiện nữa).</summary>
        public void Done(TipId id)
        {
            _doneNow.Add(id);
            _settings.MarkTipSeen(Key(id));
        }

        /// <summary>Hòm thư ▸ Xem lại hướng dẫn: hiện lại từ đầu.</summary>
        public void Restart()
        {
            _settings.ResetTips();
            _doneNow.Clear();
            _played = 0f;
            _sinceTip = 0f;
            _shownAny = false;
            _lastIntro = null;
        }

        /// <summary>Cho thời gian chơi trôi; trả mẹo cần hiện lúc này (đã đánh dấu là đã xem) hoặc null.</summary>
        public TipId? Update(float seconds)
        {
            _played += Math.Max(0f, seconds);
            _sinceTip += Math.Max(0f, seconds);
            if (!_pets.HasPet) return null;   // đang chờ trứng nở: cảnh trứng đã tự giải thích
            if (_shownAny && _sinceTip < _cfg.MinGapSeconds) return null;
            TipId? tip = Pick();
            if (tip == null) return null;
            _settings.MarkTipSeen(Key(tip.Value));
            _shownAny = true;
            _sinceTip = 0f;
            if (tip.Value <= TipId.Lead) _lastIntro = tip;
            return tip;
        }

        private TipId? Pick()
        {
            PetData pet = _pets.Current;
            PetNeeds n = pet.Needs;
            if (_health.IsIll && !Seen(TipId.Sick)) return TipId.Sick;
            if (_played < _cfg.FirstTipAfterSeconds) return null;
            // chuỗi mở đầu: mẹo sau chỉ hiện khi đã làm theo mẹo trước (hoặc chờ quá lâu)
            if (!Seen(TipId.Move)) return TipId.Move;
            if (!Seen(TipId.Pet) && IntroReady(TipId.Move)) return TipId.Pet;
            if (!Seen(TipId.Lead) && Seen(TipId.Pet) && IntroReady(TipId.Pet)) return TipId.Lead;
            // theo tình huống
            if (!Seen(TipId.Feed) && n.Satiety < _expr.HungryBelowSatiety && !pet.IsSleeping) return TipId.Feed;
            if (!Seen(TipId.Bathe) && n.Hygiene < _expr.DirtyBelowHygiene && !_health.IsIll && !pet.IsSleeping) return TipId.Bathe;
            if (!Seen(TipId.Sleep) && n.Energy < _expr.SleepyBelowEnergy && !pet.IsSleeping) return TipId.Sleep;
            // khám phá
            if (!Seen(TipId.Shop) && _played >= _cfg.ShopAfterSeconds) return TipId.Shop;
            if (!Seen(TipId.Training) && _health.CanTrain && !pet.IsSleeping &&
                (_played >= _cfg.TrainingAfterSeconds || _evo.Stage >= EvolutionStage.Child)) return TipId.Training;
            if (!Seen(TipId.Arena) && Seen(TipId.Training) && _health.CanTrain && _played >= _cfg.ArenaAfterSeconds) return TipId.Arena;
            return null;
        }

        /// <summary>Mẹo mở đầu `previous` đã được làm theo, hoặc đã hiện quá lâu mà người chơi chưa làm.</summary>
        private bool IntroReady(TipId previous) =>
            _doneNow.Contains(previous) || _lastIntro != previous || _sinceTip >= _cfg.IntroWaitSeconds;

        private static string Key(TipId id) => "tip." + id;

        public static string Text(TipId id)
        {
            switch (id)
            {
                case TipId.Move: return "Chạm xuống đất để bé chạy tới chỗ đó. Kéo trên mặt đất để xem chỗ khác trong vườn.";
                case TipId.Pet: return "Chạm vào bé để vuốt ve, bé sẽ vui hơn.";
                case TipId.Lead: return "Giữ và kéo bé để dắt đi. Thả vào hồ = tắm, thả vào ổ = đi ngủ.";
                case TipId.Feed: return "Bé đói rồi! Kéo quả táo ở khay chòi ăn (bên trái) thả lên bé.";
                case TipId.Bathe: return "Bé bẩn rồi! Dắt bé thả xuống hồ (bên phải) để tắm.";
                case TipId.Sleep: return "Bé buồn ngủ! Dắt bé vào ổ giữa vườn.";
                case TipId.Sick:
                    return "Bé ốm rồi! Cho ăn cháo, dắt vào ổ đắp chăn, chạm để lau người. Hoặc mua thuốc ở quầy hàng.";
                case TipId.Shop: return "Quầy hàng ở phía trước bên phải: thuốc, bánh, phụ kiện, trang trí, xem quảng cáo lấy Coin.";
                case TipId.Training: return "Chạm hình nộm ở sân tập (phía sau) để học chiêu bằng cách vuốt, hoặc đấu thử.";
                case TipId.Arena: return "Cổng đấu trường sau sân tập: đấu PvP 90 giây, thắng được Elo và Coin.";
                default: return "";
            }
        }

        /// <summary>Dòng "trong lúc bạn vắng" khi vào game (null nếu vắng chưa lâu / không có thú).</summary>
        public string WelcomeBack(double hoursAway)
        {
            if (!_pets.HasPet || hoursAway < _cfg.WelcomeBackHours) return null;
            PetNeeds n = _pets.Current.Needs;
            var states = new List<string>();
            if (_health.IsIll) states.Add("đang ốm");
            if (n.Satiety < _expr.HungryBelowSatiety) states.Add("đói");
            if (n.Hygiene < _expr.DirtyBelowHygiene) states.Add("bẩn");
            if (n.Energy < _expr.SleepyBelowEnergy) states.Add("buồn ngủ");
            string away = hoursAway >= 48 ? ((int)(hoursAway / 24)) + " ngày" : ((int)Math.Round(hoursAway)) + " giờ";
            string pet = _pets.Current.Name;
            return states.Count == 0
                ? "Bạn vắng " + away + ". " + pet + " vẫn ổn và rất nhớ bạn!"
                : "Bạn vắng " + away + ". " + pet + " đang " + string.Join(", ", states) + ".";
        }
    }
}
