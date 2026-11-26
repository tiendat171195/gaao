using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Unity;
using PawFit.Pet;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Màn vườn (màn dọc): dựng vườn bằng code, ánh sáng mềm + hậu kỳ URP, NavMesh lúc chạy,
    /// thả thú, camera, input. Lấy service qua ServiceLocator (do App đăng ký).
    /// </summary>
    public sealed class GardenScene : MonoBehaviour
    {
        private NavMeshDataInstance _navMesh;

        public PetController Pet { get; private set; }

        public static GardenScene Enter(ServiceLocator services)
        {
            var scene = new GameObject("PawFit_Garden").AddComponent<GardenScene>();
            scene.Build(services);
            return scene;
        }

        private void Build(ServiceLocator s)
        {
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;   // vườn: màn dọc
            var layout = s.Get<GardenLayout>();
            var cfg = s.Get<GardenConfig>();
            var pets = s.Get<PetService>();
            var log = s.Get<ILog>();

            SetupLighting();
            Camera cam = SetupCamera();
            SetupPostFx(cam);
            GardenBuildResult built = GardenBuilder.Build(layout, transform);
            BakeNavMesh(built, layout);

            NVec2 spawn = pets.Current.IsSleeping ? layout.Get(ZoneType.Bed).UsePoint : layout.PetSpawn;
            Pet = PetController.Spawn(spawn.ToWorld(), pets.Current.Dna, s.Get<PetBrain>(), s.Get<CareActionService>(),
                                      s.Get<PetExpressionService>(), layout, cfg, s.Get<EventBus>(), transform);
            GardenCamera camCtl = cam.GetComponent<GardenCamera>();
            if (camCtl == null) camCtl = cam.gameObject.AddComponent<GardenCamera>();
            camCtl.Init(cam, Pet.transform, layout);
            gameObject.AddComponent<GardenInputRouter>().Init(cam, new LegacyPointerInput(), camCtl, Pet, cfg, log);

            log.Info("[PawFit] Vườn sẵn sàng · chạm đất = thú chạy tới · kéo đất = kéo camera · chạm thú = vuốt ve · " +
                     "kéo thú = dắt (thả vào hồ = tắm, vào ổ = ngủ) · kéo táo từ khay ở chòi ăn thả lên thú = cho ăn");
        }

        private static void SetupLighting()
        {
            Light sun = null;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;

            // ánh sáng môi trường 3 tầng (trời / chân trời / đất) cho bóng mềm
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.80f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.40f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.88f, 0.94f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 70f;
        }

        private static Camera SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.88f, 0.94f);
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();   // nghe tiếng thú kêu
            return cam;
        }

        private void SetupPostFx(Camera cam)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var vol = gameObject.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(8f);
            color.postExposure.Override(0.1f);
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vol.sharedProfile = profile;
        }

        /// <summary>Nướng NavMesh lúc chạy từ collider: mặt đất đi được, nhóm Obstacles không đi được.</summary>
        private void BakeNavMesh(GardenBuildResult built, GardenLayout layout)
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>
            {
                new NavMeshBuildMarkup { root = built.Obstacles, overrideArea = true, area = 1 },   // 1 = Not Walkable
            };
            NavMeshBuilder.CollectSources(built.Root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1f;
            settings.agentClimb = 0.25f;
            settings.agentSlope = 35f;
            NVec2 size = layout.Max - layout.Min, c = (layout.Min + layout.Max) * 0.5f;
            var bounds = new Bounds(new Vector3(c.X, 0.5f, c.Y), new Vector3(size.X + 2f, 4f, size.Y + 2f));
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            _navMesh = NavMesh.AddNavMeshData(data);
        }

        private void OnDestroy()
        {
            if (_navMesh.valid) _navMesh.Remove();
        }
    }
}
