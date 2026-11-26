using PawFit.Core;
using PawFit.Garden.View;

namespace PawFit.App
{
    /// <summary>Chuyển màn: hiện chỉ có Vườn (dọc). Phase 3.2 thêm màn Đấu (ngang).</summary>
    public static class SceneFlow
    {
        public static GardenScene Garden { get; private set; }

        public static void EnterGarden(ServiceLocator services)
        {
            if (Garden != null) return;
            Garden = GardenScene.Enter(services);
        }
    }
}
