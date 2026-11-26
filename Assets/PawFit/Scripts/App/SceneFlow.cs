using PawFit.Core;
using PawFit.Garden.View;
using PawFit.Pet;
using UnityEngine;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Phím tắt để test (chỉ có trong Editor / bản Development; Phase 1.3 thay bằng Debug overlay):
    /// [1] tua +1 giờ · [2] +6 giờ · [3] +24 giờ · [N] in 4 nhu cầu ra Console.
    /// </summary>
    public sealed class DevHotkeys : MonoBehaviour
    {
        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) Skip(1);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Skip(6);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Skip(24);
                if (Input.GetKeyDown(KeyCode.N)) LogNeeds("");
            }
            catch (System.InvalidOperationException)
            {
                enabled = false;   // Input cũ đang tắt
            }
        }

        private static void Skip(int hours)
        {
            var time = GameBootstrap.Services.Get<DebugTimeProvider>();
            time.Advance(System.TimeSpan.FromHours(hours));
            GameBootstrap.Services.Get<PetService>().Tick();
            LogNeeds("Tua +" + hours + " giờ · ");
        }

        private static void LogNeeds(string prefix)
        {
            PetData p = GameBootstrap.Services.Get<PetService>().Current;
            PetNeeds n = p.Needs;
            Debug.Log($"[PawFit] {prefix}no {n.Satiety:0} / sạch {n.Hygiene:0} / năng lượng {n.Energy:0} / vui {n.Mood:0}" +
                      (p.IsSleeping ? " · đang ngủ" : ""));
        }
    }
#endif
}
