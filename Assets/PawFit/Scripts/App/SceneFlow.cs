using PawFit.Battle;
using PawFit.Battle.View;
using PawFit.Core;
using PawFit.Garden.View;
using PawFit.Pet;

namespace PawFit.App
{
    /// <summary>Chuyển màn: Vườn (dọc) ⇄ Sân tập / Màn đấu (ngang). Phase 4.2 thêm PvP ở cổng đấu trường.</summary>
    public static class SceneFlow
    {
        private static readonly string[] DummyNames = { "Hình nộm Dễ", "Hình nộm Thường", "Hình nộm Khó" };
        private static ServiceLocator _services;

        public static GardenScene Garden { get; private set; }
        public static BattleScene Battle { get; private set; }

        public static void EnterGarden(ServiceLocator services)
        {
            if (Garden != null) return;
            _services = services;
            Garden = GardenScene.Enter(services);
            Garden.DummyTapped = OnDummyTapped;
        }

        /// <summary>Chạm hình nộm ở sân tập: chọn Huấn luyện (học chiêu) hoặc Đấu (90 giây) với Bot mức đó.</summary>
        private static void OnDummyTapped(int level)
        {
            var pets = _services.Get<PetService>();
            if (!BattleSession.CanStart(pets, _services.Get<HealthService>(), out string reason))
            {
                GardenUi.ShowCard("Chưa vào sân tập được", "Thú " + reason + ".");
                return;
            }
            GardenUi.ShowChoice(DummyNames[level], "Học chiêu với hình nộm (vuốt đúng 10 lần = thuộc chiêu), hoặc đấu thử 90 giây.",
                                new[] { "Huấn luyện", "Đấu", "Thôi" }, i =>
                                {
                                    if (i == 0) EnterBattle(BattleMode.Training, (BotLevel)level);
                                    else if (i == 1) EnterBattle(BattleMode.BotFight, (BotLevel)level);
                                });
        }

        /// <summary>Vào màn đấu (vườn tạm dừng), xong thì quay về vườn.</summary>
        public static void EnterBattle(BattleMode mode, BotLevel level)
        {
            if (Battle != null || Garden == null) return;
            var pets = _services.Get<PetService>();
            if (!BattleSession.CanStart(pets, _services.Get<HealthService>(), out string reason))
            {
                GardenUi.ShowCard("Chưa vào sân tập được", "Thú " + reason + ".");
                return;
            }
            GardenUi.Close();
            Garden.SetPaused(true);
            Battle = BattleScene.Enter(_services, mode, level, () =>
            {
                Battle = null;
                if (Garden != null) Garden.SetPaused(false);
            });
        }
    }
}
