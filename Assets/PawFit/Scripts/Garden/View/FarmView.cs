using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Garden.View
{
    /// <summary>Vùng chạm của vườn rau (router đọc Plot): số luống 0.., -1 = giỏ quả cạnh chòi ăn.</summary>
    public sealed class FarmHandle : MonoBehaviour
    {
        public int Plot;
    }

    /// <summary>
    /// Vườn rau trong vườn (Phase 10.2) — chỉ có hình + đưa lệnh, luật nằm ở <see cref="FarmService"/> (C# thuần):
    ///  * 3 luống đất có viền gỗ trước chòi ăn: chạm luống trống = chọn hạt để gieo; đang lớn + đất khô = tưới (đất sẫm lại);
    ///    chín (có ngôi sao lấp lánh) = hái, quả bay vào giỏ cạnh khay táo;
    ///  * cây lớn dần theo giờ thật: ụ hạt → mầm 2 lá → cây lá xum xuê → ra quả (dâu tây đỏ, cà rốt cam, việt quất xanh, bí ngô to);
    ///  * giỏ quả: chạm để chọn quả cho bé ăn — bé chạy ra giỏ ăn (mỗi loại quả 1 tác dụng);
    ///  * bé đang thức thì chạy ra luống cúi xuống tưới / gieo; bé đang ngủ thì để bé ngủ, việc vẫn xong.
    /// Luống + giỏ có collider trong nhóm vật cản (dựng trước khi nướng NavMesh) nên bé đi vòng, không giẫm lên cây.
    /// </summary>
    public sealed class FarmView : MonoBehaviour
    {
        private enum Trip { None, Tend, Eat }

        private sealed class PlotView
        {
            public Transform Root, Crop, Sparkle;
            public Renderer Soil;
            public PlotStage Stage = (PlotStage)(-1);
            public CropKind Kind;
            public bool Wet;
        }

        private static readonly Color DrySoil = Prim.Hex("#8A5A3A");
        private static readonly Color WetSoil = Prim.Hex("#5B3A27");
        private static readonly Color Wood = Prim.Hex("#B9865A");
        private static readonly Color Leaf = Prim.Hex("#5FA84F");
        private static readonly Color LeafLight = Prim.Hex("#7CC36B");

        /// <summary>Màu quả theo loại (chỉ số = CropKind).</summary>
        public static readonly Color[] FruitColors = { Prim.Hex("#E8453C"), Prim.Hex("#F29A2E"), Prim.Hex("#4A5FC1"), Prim.Hex("#F28C28") };

        private FarmService _farm;
        private GardenLayout _layout;
        private Func<PetController> _pet;
        private readonly List<PlotView> _plots = new List<PlotView>();
        private Transform _basket, _basketFruit;
        private int _basketShown = -1;
        private float _refreshIn;
        private Trip _trip;
        private Vector3 _tripStand;
        private CropKind _tripCrop;
        private float _tripLeft;

        /// <summary>Mở quầy hạt giống (SceneFlow đặt qua GardenScene).</summary>
        public Action OpenSeedShop { get; set; }

        public bool Paused { get; set; }

        /// <summary>Dựng luống + giỏ (gọi TRƯỚC khi nướng NavMesh: collider nằm trong nhóm vật cản `obstacles`).</summary>
        public static FarmView Build(Transform parent, Transform obstacles, GardenLayout layout, FarmService farm, Func<PetController> pet)
        {
            var go = new GameObject("Farm");
            go.transform.SetParent(parent, false);
            FarmView v = go.AddComponent<FarmView>();
            v._farm = farm;
            v._layout = layout;
            v._pet = pet;
            for (int i = 0; i < layout.FarmPlots.Count && i < farm.PlotCount; i++) v._plots.Add(v.BuildPlot(obstacles, i, layout.FarmPlots[i].ToWorld()));
            v.BuildBasket(obstacles, layout.FarmBasket.ToWorld());
            v.Refresh(true);
            return v;
        }

        // ------------------------------------------------------------------ dựng hình

        private PlotView BuildPlot(Transform obstacles, int index, Vector3 at)
        {
            Transform root = Prim.Group(obstacles, "FarmPlot" + index, at);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.05f, 0.4f, 1.35f);
            col.center = new Vector3(0f, 0.2f, 0f);
            root.gameObject.AddComponent<FarmHandle>().Plot = index;
            var pv = new PlotView { Root = root };
            pv.Soil = Prim.Make(PrimitiveType.Cube, root, new Vector3(0f, 0.07f, 0f), new Vector3(0.9f, 0.1f, 1.2f), DrySoil, smoothness: 0.05f,
                                name: "Soil").GetComponent<Renderer>();
            foreach (float s in new[] { -1f, 1f })   // viền gỗ
            {
                Prim.Make(PrimitiveType.Cube, root, new Vector3(0.5f * s, 0.08f, 0f), new Vector3(0.08f, 0.16f, 1.32f), Wood);
                Prim.Make(PrimitiveType.Cube, root, new Vector3(0f, 0.08f, 0.64f * s), new Vector3(1.08f, 0.16f, 0.08f), Wood);
            }
            pv.Crop = Prim.Group(root, "Crop", new Vector3(0f, 0.12f, 0f));
            pv.Sparkle = Prim.Group(root, "Sparkle", new Vector3(0f, 0.95f, 0f));
            Prim.Make(PrimitiveType.Cube, pv.Sparkle, Vector3.zero, new Vector3(0.12f, 0.12f, 0.025f), Prim.Hex("#FFE066"), smoothness: 0.8f,
                      rotation: Quaternion.Euler(0f, 0f, 45f));
            Prim.Make(PrimitiveType.Cube, pv.Sparkle, Vector3.zero, new Vector3(0.07f, 0.07f, 0.03f), Color.white, smoothness: 0.9f);
            pv.Sparkle.gameObject.SetActive(false);
            return pv;
        }

        private void BuildBasket(Transform obstacles, Vector3 at)
        {
            _basket = Prim.Group(obstacles, "FruitBasket", at);
            var col = _basket.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.7f, 0.6f, 0.7f);
            col.center = new Vector3(0f, 0.3f, 0f);
            _basket.gameObject.AddComponent<FarmHandle>().Plot = -1;
            Prim.Make(PrimitiveType.Cylinder, _basket, new Vector3(0f, 0.14f, 0f), new Vector3(0.56f, 0.14f, 0.56f), Prim.Hex("#C08A4A"), smoothness: 0.1f);
            Prim.Make(PrimitiveType.Cylinder, _basket, new Vector3(0f, 0.285f, 0f), new Vector3(0.6f, 0.02f, 0.6f), Prim.Hex("#8A5A2B"));
            Prim.Make(PrimitiveType.Cube, _basket, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.03f, 0.03f), Prim.Hex("#8A5A2B"));   // quai
            foreach (float s in new[] { -1f, 1f })
                Prim.Make(PrimitiveType.Cube, _basket, new Vector3(0.25f * s, 0.4f, 0f), new Vector3(0.03f, 0.22f, 0.03f), Prim.Hex("#8A5A2B"));
            _basketFruit = Prim.Group(_basket, "Fruit", new Vector3(0f, 0.3f, 0f));
        }

        /// <summary>Dựng lại cây của 1 luống theo loại + giai đoạn (3 cây / luống, bí ngô 2 quả to).</summary>
        private static void BuildCrop(Transform crop, CropKind kind, PlotStage stage)
        {
            for (int i = crop.childCount - 1; i >= 0; i--) Destroy(crop.GetChild(i).gameObject);
            if (stage == PlotStage.Empty) return;
            bool pumpkin = kind == CropKind.Pumpkin;
            float[] spots = pumpkin ? new[] { -0.28f, 0.28f } : new[] { -0.38f, 0f, 0.38f };
            foreach (float z in spots)
            {
                Transform p = Prim.Group(crop, "Plant", new Vector3(0f, 0f, z));
                switch (stage)
                {
                    case PlotStage.Seeded:   // ụ đất nhỏ vừa gieo
                        Prim.Make(PrimitiveType.Sphere, p, Vector3.zero, new Vector3(0.18f, 0.05f, 0.18f), Prim.Hex("#6E4630"));
                        break;
                    case PlotStage.Sprout:   // mầm 2 lá
                        Prim.Make(PrimitiveType.Cylinder, p, new Vector3(0f, 0.05f, 0f), new Vector3(0.02f, 0.05f, 0.02f), LeafLight);
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3(-0.05f, 0.1f, 0f), new Vector3(0.1f, 0.025f, 0.06f), LeafLight,
                                  rotation: Quaternion.Euler(0f, 0f, 25f));
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3(0.05f, 0.1f, 0f), new Vector3(0.1f, 0.025f, 0.06f), LeafLight,
                                  rotation: Quaternion.Euler(0f, 0f, -25f));
                        break;
                    default:   // đang lớn / chín: lá xum xuê (+ quả khi chín)
                        BuildLeaves(p, kind);
                        if (stage == PlotStage.Ripe) BuildFruit(p, kind);
                        break;
                }
            }
        }

        private static void BuildLeaves(Transform p, CropKind kind)
        {
            switch (kind)
            {
                case CropKind.Carrot:   // lá cà rốt: nhánh cao mảnh
                    for (int k = 0; k < 3; k++)
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3((k - 1) * 0.05f, 0.18f, 0f), new Vector3(0.05f, 0.2f, 0.05f), Leaf,
                                  rotation: Quaternion.Euler(0f, k * 60f, (k - 1) * 18f));
                    break;
                case CropKind.Blueberry:   // bụi nhỏ tròn
                    Prim.Make(PrimitiveType.Sphere, p, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.26f, 0.3f), Prim.Hex("#4E9444"), smoothness: 0.1f);
                    break;
                case CropKind.Pumpkin:   // dây bí: lá to bè ra sát đất
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.1f;
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3(Mathf.Cos(a) * 0.16f, 0.05f, Mathf.Sin(a) * 0.12f), new Vector3(0.2f, 0.04f, 0.16f), Leaf,
                                  rotation: Quaternion.Euler(0f, a * 57f, 0f));
                    }
                    break;
                default:   // dâu tây: 3 lá thấp
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.1f;
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3(Mathf.Cos(a) * 0.07f, 0.08f, Mathf.Sin(a) * 0.07f), new Vector3(0.13f, 0.035f, 0.09f), Leaf,
                                  rotation: Quaternion.Euler(0f, a * 57f, 15f));
                    }
                    break;
            }
        }

        private static void BuildFruit(Transform p, CropKind kind)
        {
            Color c = FruitColors[(int)kind];
            switch (kind)
            {
                case CropKind.Carrot:   // đầu cà rốt cam nhô khỏi đất
                    Prim.Make(PrimitiveType.Cylinder, p, new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.04f, 0.1f), c, smoothness: 0.3f);
                    break;
                case CropKind.Blueberry:   // chùm quả xanh tím trên bụi
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 1.05f;
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3(Mathf.Cos(a) * 0.13f, 0.17f + (k % 2) * 0.06f, Mathf.Sin(a) * 0.13f), Vector3.one * 0.055f,
                                  c, smoothness: 0.5f);
                    }
                    break;
                case CropKind.Pumpkin:   // quả bí to có múi
                    Prim.Make(PrimitiveType.Sphere, p, new Vector3(0f, 0.13f, 0f), new Vector3(0.36f, 0.26f, 0.36f), c, smoothness: 0.35f);
                    Prim.Make(PrimitiveType.Sphere, p, new Vector3(0f, 0.13f, 0f), new Vector3(0.24f, 0.27f, 0.38f), Prim.Hex("#E07A1F"), smoothness: 0.35f);
                    Prim.Make(PrimitiveType.Cylinder, p, new Vector3(0f, 0.28f, 0f), new Vector3(0.03f, 0.04f, 0.03f), Prim.Hex("#6B8E23"));
                    break;
                default:   // dâu tây đỏ treo quanh gốc
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.1f + 1f;
                        Prim.Make(PrimitiveType.Sphere, p, new Vector3(Mathf.Cos(a) * 0.1f, 0.07f, Mathf.Sin(a) * 0.1f), new Vector3(0.08f, 0.1f, 0.08f), c,
                                  smoothness: 0.6f);
                    }
                    break;
            }
        }

        /// <summary>Quả trong giỏ: tối đa 9 quả bày trên miệng giỏ, theo loại đang có.</summary>
        private void BuildBasketFruit()
        {
            for (int i = _basketFruit.childCount - 1; i >= 0; i--) Destroy(_basketFruit.GetChild(i).gameObject);
            int shown = 0;
            foreach (CropKind k in FarmService.Kinds)
            {
                int n = Mathf.Min(_farm.InBasket(k), 3);
                for (int j = 0; j < n && shown < 9; j++, shown++)
                {
                    float a = shown * 2.4f, r = 0.07f + (shown % 3) * 0.06f;
                    float size = k == CropKind.Pumpkin ? 0.16f : k == CropKind.Blueberry ? 0.07f : 0.1f;
                    Prim.Make(PrimitiveType.Sphere, _basketFruit, new Vector3(Mathf.Cos(a) * r, 0.02f + (shown / 5) * 0.04f, Mathf.Sin(a) * r),
                              Vector3.one * size, FruitColors[(int)k], smoothness: 0.5f);
                }
            }
        }

        // ------------------------------------------------------------------ cập nhật

        private void Update()
        {
            float dt = Time.deltaTime;
            _refreshIn -= dt;
            if (_refreshIn <= 0f)
            {
                _refreshIn = 0.5f;
                Refresh(false);
            }
            float t = Time.time;
            foreach (PlotView pv in _plots)
            {
                if (!pv.Sparkle.gameObject.activeSelf) continue;
                pv.Sparkle.localRotation = Quaternion.Euler(0f, t * 90f, 0f);
                pv.Sparkle.localPosition = new Vector3(0f, 0.95f + Mathf.Sin(t * 2.4f) * 0.05f, 0f);
            }
            UpdateTrip(dt);
        }

        /// <summary>Đọc lại vườn rau: luống nào đổi giai đoạn thì dựng lại cây (bật lên), đất ướt / khô, ngôi sao khi chín, quả trong giỏ.</summary>
        private void Refresh(bool first)
        {
            for (int i = 0; i < _plots.Count; i++)
            {
                PlotView pv = _plots[i];
                PlotStage stage = _farm.Stage(i);
                PlotState state = _farm.Plot(i);
                CropKind kind = state != null ? state.Crop : CropKind.Strawberry;
                if (stage != pv.Stage || (stage != PlotStage.Empty && kind != pv.Kind))
                {
                    bool grew = !first && stage > pv.Stage && stage != PlotStage.Empty;
                    pv.Stage = stage;
                    pv.Kind = kind;
                    BuildCrop(pv.Crop, kind, stage);
                    if (grew) pv.Crop.gameObject.AddComponent<PopIn>();   // vừa lớn thêm 1 bậc: cây bật lên
                    pv.Sparkle.gameObject.SetActive(stage == PlotStage.Ripe);
                }
                if (stage == PlotStage.Growing && pv.Crop.GetComponent<PopIn>() == null)   // đang lớn: to dần theo tiến độ
                {
                    float s = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(0.5f, 1f, _farm.Progress01(i)));
                    pv.Crop.localScale = Vector3.one * s;
                }
                else if (stage != PlotStage.Growing && pv.Crop.GetComponent<PopIn>() == null) pv.Crop.localScale = Vector3.one;
                bool wet = _farm.IsWet(i);
                if (wet != pv.Wet || first)
                {
                    pv.Wet = wet;
                    pv.Soil.sharedMaterial = MaterialFactory.Lit(wet ? WetSoil : DrySoil, wet ? 0.35f : 0.05f);
                }
            }
            int total = 0;
            foreach (CropKind k in FarmService.Kinds) total = total * 31 + _farm.InBasket(k);
            if (total != _basketShown)
            {
                _basketShown = total;
                BuildBasketFruit();
            }
        }

        // ------------------------------------------------------------------ chạm

        /// <summary>Chạm luống `plot` (0..) hoặc giỏ quả (-1).</summary>
        public void Tap(int plot)
        {
            if (Paused) return;
            if (plot < 0)
            {
                TapBasket();
                return;
            }
            if (plot >= _plots.Count) return;
            Sfx.Play(SoundId.UiTap, 0.35f);
            PlotState p = _farm.Plot(plot);
            if (p == null) return;
            if (!p.Planted)
            {
                AskPlant(plot);
                return;
            }
            CropDef def = _farm.Def(p.Crop);
            if (_farm.IsRipe(plot))
            {
                Harvest(plot, def);
                return;
            }
            if (_farm.IsWet(plot))
            {
                GardenUi.Toast(def.Name + " đang lớn · chín sau " + Span(_farm.TimeToRipe(plot)) + " · đất còn ẩm, khô rồi tưới tiếp nhé", 3f);
                return;
            }
            if (_farm.Water(plot) != FarmResult.Ok) return;
            Tend(plot);
            Sfx.Play(SoundId.Splash, 0.25f, 1.25f);
            Refresh(false);
            GardenUi.Toast("Đã tưới luống " + (plot + 1) + ": " + def.Name + " lớn nhanh gấp đôi trong " + _farm.Config.WaterHours.ToString("0.#") +
                           " giờ tới · chín sau " + Span(_farm.TimeToRipe(plot)), 3f);
        }

        private void AskPlant(int plot)
        {
            if (_farm.SeedTotal <= 0)
            {
                GardenUi.ShowChoice("Hết hạt giống", "Mua hạt ở quầy hàng (trước bên phải), hoặc đi săn quái rừng để nhặt hạt.",
                                    new[] { "Mua hạt giống", "Thôi" }, i => { if (i == 0) OpenSeedShop?.Invoke(); });
                return;
            }
            var labels = new List<string>();
            var enabled = new List<bool>();
            var kinds = new List<CropKind>();
            foreach (CropKind k in FarmService.Kinds)
            {
                CropDef d = _farm.Def(k);
                int n = _farm.Seeds(k);
                labels.Add(n > 0 ? "Gieo " + d.Name + " · còn " + n + " hạt · chín sau " + GrowText(d, _farm.Config) + " · " + d.Effect
                                 : d.Name + " · hết hạt");
                enabled.Add(n > 0);
                kinds.Add(k);
            }
            labels.Add("Mua thêm hạt giống...");
            enabled.Add(true);
            labels.Add("Thôi");
            enabled.Add(true);
            GardenUi.ShowChoice("Luống " + (plot + 1) + " đang trống",
                                "Chọn hạt để gieo (gieo xong là tưới luôn). Tưới đều thì cây lớn nhanh gấp đôi.",
                                labels.ToArray(), i =>
                                {
                                    if (i < kinds.Count) Plant(plot, kinds[i]);
                                    else if (i == kinds.Count) OpenSeedShop?.Invoke();
                                }, enabled.ToArray());
        }

        private void Plant(int plot, CropKind kind)
        {
            if (_farm.Plant(plot, kind) != FarmResult.Ok)
            {
                GardenUi.Toast("Chưa gieo được");
                return;
            }
            Tend(plot);
            Sfx.Play(SoundId.Dig, 0.45f);
            Refresh(false);
            CropDef d = _farm.Def(kind);
            GardenUi.Toast("Đã gieo " + d.Name + " ở luống " + (plot + 1) + " · chín sau " + Span(_farm.TimeToRipe(plot)) + " · đất khô thì chạm để tưới", 3.5f);
        }

        private void Harvest(int plot, CropDef def)
        {
            FarmResult r = _farm.Harvest(plot, out int count);
            if (r == FarmResult.BasketFull)
            {
                GardenUi.Toast("Giỏ đầy " + def.Name + " rồi, cho bé ăn bớt nhé (chạm giỏ cạnh chòi ăn)");
                return;
            }
            if (r != FarmResult.Ok) return;
            Vector3 from = _plots[plot].Root.position + Vector3.up * 0.3f;
            for (int i = 0; i < count; i++) FlyingFruit.Launch(transform, from, _basket.position + Vector3.up * 0.4f, FruitColors[(int)def.Kind], i * 0.12f);
            Sfx.Play(SoundId.Pop, 0.45f);
            Sfx.Play(SoundId.Treasure, 0.35f, 1.1f);
            Refresh(false);
            PetController pet = _pet();
            if (pet != null && pet.IsFree && Vector3.Distance(pet.transform.position, _plots[plot].Root.position) < 10f) pet.Cheer(1);
            GardenUi.Toast("+" + count + " " + def.Name + " vào giỏ quả cạnh chòi ăn · chạm giỏ để cho bé ăn", 3f);
        }

        /// <summary>Bé đang thức thì chạy ra đứng sau luống (mặt nhìn về camera) cúi xuống chăm cây; đang ngủ thì để bé ngủ.</summary>
        private void Tend(int plot)
        {
            PetController pet = _pet();
            if (pet == null || pet.IsSleeping || plot >= _layout.FarmStands.Count) return;
            _trip = Trip.Tend;
            _tripStand = _layout.FarmStands[plot].ToWorld();
            _tripLeft = 12f;
            pet.MoveTo(_tripStand);
        }

        private void TapBasket()
        {
            Sfx.Play(SoundId.UiTap, 0.35f);
            if (_farm.BasketTotal <= 0)
            {
                GardenUi.Toast("Giỏ quả trống · hái quả ở vườn rau trước chòi ăn (chạm luống có ngôi sao lấp lánh)", 3f);
                return;
            }
            var labels = new List<string>();
            var enabled = new List<bool>();
            var kinds = new List<CropKind>();
            foreach (CropKind k in FarmService.Kinds)
            {
                int n = _farm.InBasket(k);
                if (n <= 0) continue;
                CropDef d = _farm.Def(k);
                bool ok = _farm.CanEat(k, out string why);
                labels.Add(d.Name + " ×" + n + " · " + d.Effect + (ok ? "" : " (" + why + ")"));
                enabled.Add(ok);
                kinds.Add(k);
            }
            labels.Add("Thôi");
            enabled.Add(true);
            GardenUi.ShowChoice("Giỏ quả", "Chọn quả cho bé ăn: bé sẽ chạy ra giỏ ăn ngay.", labels.ToArray(), i =>
            {
                if (i < kinds.Count) StartEat(kinds[i]);
            }, enabled.ToArray());
        }

        private void StartEat(CropKind kind)
        {
            PetController pet = _pet();
            bool can = _farm.CanEat(kind, out string why);
            if (pet == null || !can)
            {
                GardenUi.Toast("Bé chưa ăn được: " + (pet == null ? "chưa có bé" : why));
                return;
            }
            _trip = Trip.Eat;
            _tripCrop = kind;
            _tripStand = _layout.FarmBasketStand.ToWorld();
            _tripLeft = 15f;
            pet.MoveTo(_tripStand);
        }

        /// <summary>Bé chạy tới chỗ đứng: tới nơi thì cúi chăm cây / ăn quả; bạn bảo bé đi chỗ khác thì thôi.</summary>
        private void UpdateTrip(float dt)
        {
            if (_trip == Trip.None) return;
            PetController pet = _pet();
            _tripLeft -= dt;
            if (pet == null || _tripLeft <= 0f)
            {
                _trip = Trip.None;
                return;
            }
            float d = Vector3.Distance(Flat(pet.transform.position), Flat(_tripStand));
            if (d > 0.9f && pet.IsGoingTo(_tripStand, 0.5f)) return;   // còn đang chạy tới
            Trip trip = _trip;
            _trip = Trip.None;
            if (d > 1.6f) return;   // bé được bảo đi chỗ khác
            if (trip == Trip.Tend)
            {
                pet.Avatar?.PlayEat();   // cúi xuống luống
                return;
            }
            CareResult r = _farm.Eat(_tripCrop);
            CropDef def = _farm.Def(_tripCrop);
            if (r == CareResult.Ok)
            {
                pet.EatSnack(_basket.position + Vector3.up * 0.35f);
                Sfx.PlayVaried(SoundId.Munch, 0.5f);
                Refresh(false);
                GardenUi.Toast("Bé ăn " + def.Name + ": " + def.Effect + "!", 2.5f);
            }
            else
            {
                pet.Refuse();
                _farm.CanEat(_tripCrop, out string why);
                GardenUi.Toast("Bé chưa ăn được: " + (why ?? "bé không muốn ăn"));
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>"45 phút", "1 giờ 20 phút", "chưa tới 1 phút" (thời gian còn lại).</summary>
        private static string Span(TimeSpan? left) => left.HasValue ? Minutes(left.Value.TotalMinutes) : "?";

        public static string Hours(float hours) => Minutes(hours * 60.0);

        /// <summary>"3 giờ (tưới đều: 1 giờ 30 phút)".</summary>
        public static string GrowText(CropDef d, FarmConfig c) =>
            Hours(d.GrowHours) + " (tưới đều: " + Hours(d.GrowHours / Mathf.Max(1f, c.WaterSpeed)) + ")";

        private static string Minutes(double minutes)
        {
            if (minutes < 1.5) return "chưa tới 1 phút";
            int m = (int)Math.Round(minutes);
            if (m < 60) return m + " phút";
            int h = m / 60, rest = m % 60;
            return h + " giờ" + (rest > 0 ? " " + rest + " phút" : "");
        }
    }

    /// <summary>1 quả vừa hái bay vòng cung từ luống vào giỏ rồi biến mất.</summary>
    public sealed class FlyingFruit : MonoBehaviour
    {
        private Vector3 _from, _to;
        private float _t, _delay;

        public static void Launch(Transform parent, Vector3 from, Vector3 to, Color color, float delay)
        {
            GameObject go = Prim.Make(PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * 0.12f, color, smoothness: 0.55f, name: "FlyingFruit");
            go.transform.position = from;
            FlyingFruit f = go.AddComponent<FlyingFruit>();
            f._from = from;
            f._to = to;
            f._delay = delay;
        }

        private void Update()
        {
            if (_delay > 0f)
            {
                _delay -= Time.deltaTime;
                return;
            }
            _t += Time.deltaTime / 0.8f;
            float k = Mathf.Clamp01(_t);
            Vector3 p = Vector3.Lerp(_from, _to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * 1.6f;   // bay vòng cung
            transform.position = p;
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
