using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Unity;
using PawFit.Garden.View;
using PawFit.Pet;
using PawFit.Pet.View;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Dữ liệu 1 trận với đối thủ cho trước để màn đấu dựng (App điền): PvP (từ kết quả ghép trận) hoặc Thám hiểm (sinh vật hoang).
    /// Gồm đối thủ trong trận + thú của họ trông thế nào, và hàm App gọi lúc hết trận để tính Elo / sao / Coin
    /// rồi trả về các dòng chữ cho thẻ kết quả.
    /// </summary>
    public sealed class RankedMatchView
    {
        /// <summary>Ranked (PvP) hoặc Adventure (Thám hiểm, Phase 7.2).</summary>
        public BattleMode Mode = BattleMode.Ranked;

        /// <summary>Sân đấu: -1 = đấu trường thường; 0..4 = vùng Thám hiểm (đồi cỏ, rừng nấm, hồ sương, núi lửa, hang pha lê).</summary>
        public int Theme = -1;

        /// <summary>Cỡ thú đối thủ (trùm Thám hiểm to hơn).</summary>
        public float OpponentScale = 1f;

        /// <summary>Thẻ kết quả có thêm nút này (vd "Đánh lại"): rời màn đấu rồi gọi Again.</summary>
        public string AgainLabel;
        public Action Again;

        public RankedOpponent Opponent;
        public ulong Seed;

        /// <summary>Trận qua máy chủ riêng: nhịp trận máy chủ gửi về (null = người chơi ảo chạy ngay trên máy).</summary>
        public IMatchFeed Feed;

        /// <summary>Tên thú đối thủ, biệt danh người chơi, dòng phụ (vd "Silver · Elo 1180").</summary>
        public string PetName;
        public string PlayerName;
        public string Title;

        public PetDna Dna;
        public GrowthLook Look;
        public AccessoryKind Accessory;

        /// <summary>Hết trận: App ghi Elo / Coin và trả các dòng thêm cho thẻ kết quả (vd "Elo 1000 → 1016 (+16)").</summary>
        public Func<BattleResult, List<string>> OnResult;
    }

    /// <summary>
    /// Màn đấu (màn NGANG): đấu trường dựng bằng code, thú của người chơi (bên trái) vs hình nộm (bên phải),
    /// hoặc vs thú của người chơi khác / người chơi ảo khi đấu PvP (Ranked).
    /// Đọc nét vuốt -> SwipeRecognizer -> BattleSession (C# thuần); sự kiện trận -> hoạt cảnh + hiệu ứng.
    /// Không chứa luật đấu.
    /// </summary>
    public sealed class BattleScene : MonoBehaviour
    {
        private static readonly Vector3 ArenaPos = new Vector3(300f, 0f, 0f);   // xa vườn, camera không thấy vườn
        private static readonly string[] LevelNames = { "Hình nộm Dễ", "Hình nộm Thường", "Hình nộm Khó" };

        private BattleSession _session;
        private PetService _pets;
        private EvolutionService _evo;
        private ILog _log;
        private Action _onExit;
        private Camera _cam;
        private Vector3 _camPos;
        private Quaternion _camRot;
        private float _camFov;
        private FighterRig _player, _dummy;
        private PetMoodFx _playerFx;
        private BattleHud _hud;
        private IPointerInput _input;
        private SwipeRecognizer _recognizer;
        private readonly List<SwipePoint> _stroke = new List<SwipePoint>();
        private bool _stroking;
        private float _dpi = 1f;
        private float _endT = -1f;
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private readonly List<SessionNote> _notes = new List<SessionNote>();
        private RankedMatchView _match;
        private string _lastFail;

        // ---- cảm giác đánh (chỉ là hình): khựng hình, chậm lại, rung camera — tính theo giờ thật ----
        private float _stopLeft, _slowLeft, _shake;
        private Vector3 _camBase;

        public static BattleScene Enter(ServiceLocator s, BattleMode mode, BotLevel level, Action onExit)
        {
            var scene = new GameObject("PawFit_Battle").AddComponent<BattleScene>();
            scene.Build(s, mode, level, onExit, null);
            return scene;
        }

        /// <summary>Vào trận PvP 90 giây với đối thủ đã ghép (người thật sau này / người chơi ảo bây giờ).</summary>
        public static BattleScene EnterRanked(ServiceLocator s, RankedMatchView match, Action onExit)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            var scene = new GameObject("PawFit_Battle").AddComponent<BattleScene>();
            scene.Build(s, match.Mode, BotLevel.Normal, onExit, match);
            return scene;
        }

        private void Build(ServiceLocator s, BattleMode mode, BotLevel level, Action onExit, RankedMatchView match)
        {
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.LandscapeLeft;   // đấu: màn ngang
            _pets = s.Get<PetService>();
            _evo = s.Get<EvolutionService>();
            _log = s.Get<ILog>();
            _onExit = onExit;
            _match = match;
            if (match != null)   // PvP: Elo do App (mạng giả lập) / máy chủ tính sau trận; Thám hiểm: sao + Coin do AdventureService
                _session = match.Feed != null ? new BattleSession(_pets, _evo, match.Opponent, match.Feed)
                                              : new BattleSession(_pets, _evo, match.Opponent, match.Seed,
                                                                  match.Mode == BattleMode.Adventure ? BattleMode.Adventure : BattleMode.Ranked);
            else
            {
                ICoinWallet wallet = s.TryGet(out ICoinWallet w) ? w : null;   // thắng Bot có Coin (Phase 5.1)
                _session = new BattleSession(mode, level, _pets, _evo, (ulong)DateTime.UtcNow.Ticks, wallet: wallet);
            }
            transform.position = ArenaPos;
            BuildArena(match != null ? match.Theme : -1);

            // thú của người chơi (bên trái, nhìn sang phải + hơi về camera)
            Transform p = Prim.Group(transform, "Player", new Vector3(-1.3f, 0f, 0f));
            Transform pBody = Prim.Group(p, "Body");
            var host = new GameObject("Pet");
            host.transform.SetParent(pBody, false);
            host.transform.localRotation = Quaternion.Euler(0f, 65f, 0f);
            IPetAvatar avatar = PetAvatarFactory.Create(host, _pets.Current.Dna, PetController.UsePlaceholder);
            avatar.SetGrowth(_evo.Look);
            AccessoryView.Attach(avatar, _pets.Cosmetics?.Equipped ?? AccessoryKind.None);   // ra trận vẫn đeo phụ kiện
            _playerFx = host.AddComponent<PetMoodFx>();
            _playerFx.Init(avatar);
            _player = p.gameObject.AddComponent<FighterRig>();
            _player.Init(pBody, 1f, avatar);

            if (match != null) BuildOpponentPet(match);
            else
            {
                // hình nộm (bên phải, nhìn sang trái)
                Transform d = Prim.Group(transform, "Dummy", new Vector3(1.3f, 0f, 0f));
                Transform dBody = DummyBuilder.Build(d, level, out Transform[] arms);
                dBody.localRotation = Quaternion.Euler(0f, -25f, 0f);
                _dummy = d.gameObject.AddComponent<FighterRig>();
                _dummy.Init(dBody, -1f, null, arms);
            }

            _cam = Camera.main;
            if (_cam != null)
            {
                _camPos = _cam.transform.position;
                _camRot = _cam.transform.rotation;
                _camFov = _cam.fieldOfView;
                _cam.fieldOfView = 38f;
                Vector3 eye = ArenaPos + new Vector3(0f, 1.25f, -5f);
                _cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(ArenaPos + new Vector3(0f, 0.75f, 0f) - eye));
                _camBase = eye;
            }
            bool training = mode == BattleMode.Training;
            string rightName = match != null ? match.PetName + " · " + match.Title : training ? "Hình nộm tập" : LevelNames[(int)level];
            _hud = BattleHud.Create(transform, training, _pets.Current.Name, rightName, OnButton);
            _input = new LegacyPointerInput();
            _dpi = Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
            _recognizer = new SwipeRecognizer(new SwipeConfig(), _dpi);
            if (training) _hud.Hint("Vuốt theo bóng mờ để học chiêu. Vuốt xuống để đỡ.", 3f);
            else if (match == null) _hud.Hint("Đánh trúng liên tục để nối combo · đầy Nộ thì vuốt Z", 3f);
            if (match != null)
            {
                _hud.Big(_pets.Current.Name + "   VS   " + match.PetName, 2.2f);
                _hud.Hint(match.Mode == BattleMode.Adventure ? match.PlayerName + " · " + match.Title + " · thắng còn nhiều máu = nhiều sao"
                                                             : "Đối thủ: " + match.PlayerName + " · " + match.Title, 3f);
            }
            _log.Info("[PawFit] Vào trận: " + mode + (match != null ? " · gặp " + match.PlayerName : training ? "" : " · " + level));
        }

        /// <summary>Thú của đối thủ (bên phải, nhìn sang trái): dựng từ DNA + dáng tiến hóa + phụ kiện của họ.</summary>
        private void BuildOpponentPet(RankedMatchView match)
        {
            Transform d = Prim.Group(transform, "Opponent", new Vector3(1.3f, 0f, 0f));
            Transform dBody = Prim.Group(d, "Body");
            dBody.localScale = Vector3.one * Mathf.Clamp(match.OpponentScale, 0.5f, 2f);   // trùm Thám hiểm to hơn
            var host = new GameObject("Pet");
            host.transform.SetParent(dBody, false);
            host.transform.localRotation = Quaternion.Euler(0f, -65f, 0f);   // đối xứng với thú mình (65°)
            IPetAvatar avatar = PetAvatarFactory.Create(host, match.Dna, PetController.UsePlaceholder);
            avatar.SetGrowth(match.Look);
            AccessoryView.Attach(avatar, match.Accessory);
            _dummy = d.gameObject.AddComponent<FighterRig>();
            _dummy.Init(dBody, -1f, avatar);
        }

        // màu sân theo vùng: sân ngoài, sân trong, nền đất, trời / vách
        private static readonly string[][] ThemeColors =
        {
            new[] { "#B9DB8A", "#A7CF77", "#8CC36A", "#CDEBF7" },   // 0 Đồi Cỏ Gió
            new[] { "#7D9A5E", "#6E8C52", "#4F6B3E", "#8E7BB5" },   // 1 Rừng Nấm
            new[] { "#CFE0E2", "#BCD3D7", "#9FC3C9", "#DDE8EE" },   // 2 Hồ Sương
            new[] { "#6B5751", "#5C4A45", "#4A3B37", "#E9967A" },   // 3 Núi Lửa
            new[] { "#5A4E7D", "#4C416B", "#3E3557", "#2B2547" },   // 4 Hang Pha Lê
        };

        /// <summary>Sân đấu: theme -1 = đấu trường (cát + hàng rào gỗ); 0..4 = vùng Thám hiểm, mỗi vùng màu + đồ vật riêng.</summary>
        private void BuildArena(int theme)
        {
            bool region = theme >= 0 && theme < ThemeColors.Length;
            string[] c = region ? ThemeColors[theme] : new[] { "#E3C88F", "#D9B877", "#8CC36A", "#BFE0F2" };
            Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.05f, 0f), new Vector3(6.5f, 0.05f, 4f), Prim.Hex(c[0]), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.02f, 0f), new Vector3(5.6f, 0.03f, 3.3f), Prim.Hex(c[1]), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, -0.3f, 6f), new Vector3(40f, 0.5f, 20f), Prim.Hex(c[2]), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, 3f, 7f), new Vector3(40f, 8f, 0.2f), Prim.Hex(c[3]), smoothness: 0f);
            if (!region)
            {
                for (int i = -4; i <= 4; i++)   // hàng rào gỗ phía sau
                {
                    Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(i * 1.2f, 0.45f, 2.6f), new Vector3(0.12f, 0.45f, 0.12f), Prim.Hex("#B9865A"));
                    if (i < 4) Prim.Make(PrimitiveType.Cube, transform, new Vector3(i * 1.2f + 0.6f, 0.65f, 2.6f), new Vector3(1.2f, 0.08f, 0.06f), Prim.Hex("#7A5236"));
                }
                return;
            }
            var rng = new System.Random(17 + theme);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            switch (theme)
            {
                case 0:   // đồi cỏ: 3 quả đồi xa + bụi cây + hoa
                    for (int i = 0; i < 3; i++)
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(-7f + i * 7f, -0.5f, 6f), new Vector3(9f, 4.5f, 4f),
                                  Prim.Hex(i == 1 ? "#7FBF5A" : "#93CC6B"), smoothness: 0.05f);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = R(-6f, 6f);
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(x, 0.2f, R(2.6f, 4f)), Vector3.one * R(0.5f, 0.9f), Prim.Hex("#5FA84F"), smoothness: 0.1f);
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(R(-5f, 5f), 0.05f, R(1.8f, 3f)), Vector3.one * 0.14f,
                                  Prim.Hex(i % 2 == 0 ? "#F7C6D9" : "#FFF1A8"));
                    }
                    break;
                case 1:   // rừng nấm: thân cây tối + nấm đỏ chấm trắng
                    for (int i = 0; i < 6; i++)
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(-7.5f + i * 3f, 2f, R(4.5f, 6f)), new Vector3(0.6f, 2.5f, 0.6f),
                                  Prim.Hex("#5B4636"));
                    for (int i = 0; i < 6; i++)
                    {
                        float x = (i < 3 ? -1f : 1f) * R(3.4f, 6f), z = R(2.4f, 4f), h = R(0.5f, 1.1f);
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(x, h * 0.5f, z), new Vector3(0.18f, h * 0.5f, 0.18f), Prim.Hex("#F3E6C4"));
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(x, h, z), new Vector3(0.9f, 0.45f, 0.9f) * (0.6f + h * 0.5f),
                                  Prim.Hex(i % 2 == 0 ? "#D9443B" : "#B04BC0"), smoothness: 0.4f);
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(x + 0.15f, h + 0.18f, z - 0.25f), Vector3.one * 0.12f, Color.white);
                    }
                    break;
                case 2:   // hồ sương: mặt hồ phía sau, cỏ lau, sương mờ
                    Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.04f, 5f), new Vector3(18f, 0.02f, 5f), Prim.Hex("#7FB7D6"), smoothness: 0.9f);
                    for (int i = 0; i < 12; i++)
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(R(-7f, 7f), 0.45f, R(2.4f, 3f)), new Vector3(0.04f, R(0.35f, 0.6f), 0.04f),
                                  Prim.Hex("#8FA86E"));
                    for (int i = 0; i < 5; i++)
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(-8f + i * 4f, R(0.6f, 1.2f), R(5.5f, 6.5f)), new Vector3(4f, 1.2f, 1.5f),
                                  Prim.Hex("#EEF3F6"), smoothness: 0f);
                    break;
                case 3:   // núi lửa: núi xa đỉnh lửa, vũng dung nham, đá
                    Prim.Make(PrimitiveType.Sphere, transform, new Vector3(2f, 1f, 6.5f), new Vector3(10f, 7f, 3f), Prim.Hex("#4A3B37"), smoothness: 0.05f);
                    Prim.Make(PrimitiveType.Sphere, transform, new Vector3(2f, 4.3f, 6.2f), new Vector3(2.2f, 0.8f, 1f), Prim.Hex("#FF7A2F"), smoothness: 0.9f);
                    for (int i = 0; i < 4; i++)
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3((i % 2 == 0 ? -1f : 1f) * R(3.8f, 6f), -0.01f, R(2f, 3.6f)),
                                  new Vector3(R(0.9f, 1.6f), 0.01f, R(0.6f, 1f)), Prim.Hex("#FF8C2E"), smoothness: 0.9f);
                    for (int i = 0; i < 6; i++)
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(R(-6.5f, 6.5f), 0.15f, R(2.6f, 4.2f)), new Vector3(R(0.5f, 1f), R(0.4f, 0.7f), R(0.5f, 0.9f)),
                                  Prim.Hex("#3A2E2B"), smoothness: 0.1f);
                    break;
                default:   // hang pha lê: tinh thể xanh / tím lấp lánh, nhũ đá
                    for (int i = 0; i < 9; i++)
                    {
                        float x = (i % 2 == 0 ? -1f : 1f) * R(3.2f, 7f), h = R(0.8f, 2f);
                        Prim.Make(PrimitiveType.Cube, transform, new Vector3(x, h * 0.45f, R(2.4f, 4.5f)), new Vector3(0.35f, h, 0.35f),
                                  Prim.Hex(i % 3 == 0 ? "#7FE3F0" : i % 3 == 1 ? "#B58CF2" : "#E3A6F5"), smoothness: 0.95f,
                                  rotation: Quaternion.Euler(R(-12f, 12f), R(0f, 90f), R(-15f, 15f)));
                    }
                    for (int i = 0; i < 6; i++)
                        Prim.Make(PrimitiveType.Capsule, transform, new Vector3(-7.5f + i * 3f, 6f, R(5f, 6.5f)), new Vector3(0.5f, R(1f, 1.8f), 0.5f),
                                  Prim.Hex("#463C66"));
                    break;
            }
        }

        private void OnButton()
        {
            if (_session.IsOver) return;
            _session.Quit(_notes);   // huấn luyện: xong buổi; đấu: bỏ cuộc (thua)
        }

        private void Update()
        {
            if (_session == null) return;
            // xóa TRƯỚC khi đọc nét vuốt: sự kiện của chính nét vừa vuốt (ra đòn / đỡ / vuốt sai) phải được vẽ ngay frame này
            _events.Clear();
            _notes.Clear();
            UpdateJuice();
            ReadSwipes();
            _session.Advance(Time.deltaTime, _events, _notes);
            foreach (BattleEvent e in _events) Show(e);
            foreach (SessionNote n in _notes) Show(n);

            BattleSimulation sim = _session.Sim;
            BattleConfig cfg = sim.Config;
            Fighter me = sim.Fighters[BattleSession.PlayerSide], foe = sim.Fighters[BattleSession.BotSide];
            _hud.SetHp(me.HpPercent, foe.HpPercent);
            _hud.SetTime(sim.TimeLeft / cfg.DurationSeconds);
            if (cfg.UltimateNeedsRage)
            {
                _hud.SetRage(me.Rage / cfg.RageMax, foe.Rage / cfg.RageMax);
                _player.SetRageAura(sim.UltimateReady(BattleSession.PlayerSide));
                _dummy.SetRageAura(sim.UltimateReady(BattleSession.BotSide));
            }
            _hud.SetReady(1f - Mathf.Clamp01(sim.AttackReadyIn(BattleSession.PlayerSide) / Mathf.Max(0.01f, cfg.CooldownSeconds)));
            _player.Speed = _dummy.Speed = _session.TimeScale;
            if (_session.Mode == BattleMode.Training)
                _hud.SetLesson(_session.Lesson, _session.LessonProgress, _pets.Config.Evolution.PracticeToLearn, _session.SlowMo);
            if (_session.SlowMo) _hud.Hint("Hình nộm sắp đánh — vuốt XUỐNG để đỡ!", 0.3f);
            if (_session.WaitingForServer) _hud.Hint("Chờ đối thủ vào trận...", 0.3f);   // PvP qua máy chủ: đối thủ chưa vào màn đấu
            _player.Avatar?.LookAt(_dummy.Center);
            _player.Avatar?.SetMoveSpeed(0f);
            _dummy.Avatar?.LookAt(_player.Center);   // PvP: thú đối thủ cũng nhìn sang mình
            _dummy.Avatar?.SetMoveSpeed(0f);

            if (_session.IsOver && _endT < 0f) _endT = 0f;
            if (_endT >= 0f && !_hud.ResultShown)
            {
                _endT += Time.deltaTime;
                if (_endT > 1.3f) ShowResult();
            }
        }

        /// <summary>Gom điểm chạm thành nét vuốt; nhấc tay thì nhận dạng rồi gửi vào trận.</summary>
        private void ReadSwipes()
        {
            if (_session.IsOver) return;
            PointerSample s = _input.Read();
            switch (s.Phase)
            {
                case PointerPhase.Began:
                    _stroking = !_hud.IsOverButton(s.Position);
                    _stroke.Clear();
                    if (_stroking) _stroke.Add(new SwipePoint(s.Position.x, s.Position.y, s.Time));
                    break;
                case PointerPhase.Moved:
                    if (!_stroking) break;
                    _stroke.Add(new SwipePoint(s.Position.x, s.Position.y, s.Time));
                    _hud.TrailAt(s.Position);
                    break;
                case PointerPhase.Ended:
                    if (!_stroking) break;
                    _stroking = false;
                    _stroke.Add(new SwipePoint(s.Position.x, s.Position.y, s.Time));
                    if (StrokeLength() < 25f * _dpi) break;   // chạm nhẹ: không tính là vuốt sai
                    OnStroke(_recognizer.Recognize(_stroke));
                    break;
            }
        }

        /// <summary>
        /// 1 nét vuốt xong: quá ngắn / quá chậm thì chỉ nhắc (không phạt); còn lại gửi vào trận.
        /// Vết vuốt đổi màu ngay: vàng = nhận chiêu, đỏ = vuốt sai, xám = không tính / đang bận.
        /// </summary>
        private void OnStroke(SwipeResult r)
        {
            if (!r.IsValid && r.Fail != SwipeFail.Unclear && _session.Mode != BattleMode.Training)
            {
                _hud.TintTrail(StrokeTint.Ignored);
                _hud.Hint(r.Fail == SwipeFail.TooShort ? "Vuốt dài hơn chút nhé" : "Vuốt nhanh, dứt khoát hơn nhé", 1.2f);
                return;
            }
            _lastFail = r.Reason;
            bool ok = _session.PlayerSwipe(r, _events, _notes);
            _hud.TintTrail(!r.IsValid ? StrokeTint.Wrong : ok ? StrokeTint.Ok : StrokeTint.Ignored);
            if (ok && r.IsValid && _session.Networked) _player.Anticipate();   // qua máy chủ: nhún ngay trong lúc chờ máy chủ chốt
        }

        private float StrokeLength()
        {
            float len = 0f;
            for (int i = 1; i < _stroke.Count; i++)
                len += Mathf.Sqrt((_stroke[i].X - _stroke[i - 1].X) * (_stroke[i].X - _stroke[i - 1].X) +
                                  (_stroke[i].Y - _stroke[i - 1].Y) * (_stroke[i].Y - _stroke[i - 1].Y));
            return len;
        }

        private FighterRig Rig(int side) => side == BattleSession.PlayerSide ? _player : _dummy;

        /// <summary>Khựng hình (gần như dừng) bấy nhiêu giây thật — cảm giác đòn có lực.</summary>
        private void HitStop(float seconds) => _stopLeft = Mathf.Max(_stopLeft, seconds);

        private void SlowMo(float seconds) => _slowLeft = Mathf.Max(_slowLeft, seconds);

        private void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        /// <summary>Chạy bằng giờ thật nên vẫn chạy trong lúc khựng hình. Trận + hoạt cảnh chạy theo Time.timeScale.</summary>
        private void UpdateJuice()
        {
            float dt = Time.unscaledDeltaTime;
            if (_stopLeft > 0f)
            {
                _stopLeft -= dt;
                Time.timeScale = 0.03f;
            }
            else if (_slowLeft > 0f)
            {
                _slowLeft -= dt;
                Time.timeScale = 0.3f;
            }
            else Time.timeScale = 1f;
            if (_cam == null) return;
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 0.5f);
            Vector3 offset = _shake > 0f ? new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f) * _shake
                                         : Vector3.zero;
            _cam.transform.position = _camBase + _cam.transform.rotation * offset;
        }

        private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
        private static readonly Color GuardBlue = new Color(0.55f, 0.8f, 1f);
        private static readonly Color RageOrange = new Color(1f, 0.62f, 0.2f);

        private static Color SlashColor(MoveKind m, bool crit) =>
            crit || m == MoveKind.Ultimate ? Gold : m == MoveKind.Heavy ? new Color(1f, 0.6f, 0.25f)
          : m == MoveKind.Special ? new Color(0.5f, 0.85f, 1f) : Color.white;

        /// <summary>Góc vệt chém theo chiêu (bên phải đánh sang thì lật ngược).</summary>
        private static float SlashAngle(MoveKind m, int side)
        {
            float a = m == MoveKind.Quick ? -12f : m == MoveKind.Heavy ? 55f : m == MoveKind.Special ? -40f : 30f;
            return side == BattleSession.PlayerSide ? a : -a;
        }

        private static Color ComboColor(int n) =>
            n >= 5 ? new Color(1f, 0.35f, 0.25f) : n == 4 ? new Color(1f, 0.55f, 0.2f) : n == 3 ? Gold : new Color(1f, 0.95f, 0.7f);

        private void Show(BattleEvent e)
        {
            FighterRig self = Rig(e.Side), other = Rig(1 - e.Side);
            bool mine = e.Side == BattleSession.PlayerSide;
            switch (e.Kind)
            {
                case BattleEventKind.AttackStarted:   // thấy chuyển động ngay lúc vuốt xong
                    self.Attack(e.Move, _session.Sim.Config.WindupSeconds[(int)e.Move]);
                    if (e.Move == MoveKind.Special)
                        FxBurst.Popup(transform, self.Center + Vector3.up * 0.75f, "Chiêu riêng!", new Color(0.75f, 0.9f, 1f), 0.2f);
                    else if (e.Move == MoveKind.Ultimate)
                    {
                        _hud.Big((mine ? "" : "Đối thủ: ") + "TUYỆT CHIÊU!", 1f);
                        FxBurst.Ring(transform, self.Center, RageOrange);
                        FxBurst.Spark(transform, self.Center, RageOrange, 14, 1.6f);
                        SlowMo(0.3f);
                        Shake(0.04f);
                    }
                    break;
                case BattleEventKind.Hit:
                    self.Strike(e.Move != MoveKind.Quick);
                    other.Flinch(e.Move == MoveKind.Quick ? 0.5f : e.Move == MoveKind.Ultimate ? 1.5f : 1f);
                    HitStop(e.Crit || e.Move == MoveKind.Ultimate ? 0.1f : e.Move == MoveKind.Quick ? 0.035f : 0.07f);
                    if (e.Crit || e.Move == MoveKind.Ultimate) Shake(e.Move == MoveKind.Ultimate ? 0.14f : 0.07f);
                    FxBurst.Slash(transform, other.Center, SlashColor(e.Move, e.Crit), SlashAngle(e.Move, e.Side),
                                  e.Move == MoveKind.Ultimate ? 1.5f : 0.9f, e.Move == MoveKind.Quick ? 0.06f : 0.1f);
                    FxBurst.Spark(transform, other.Center, e.Crit ? Gold : Color.white, e.Crit || e.Move == MoveKind.Ultimate ? 16 : 9);
                    if (e.Crit) FxBurst.Popup(transform, other.Center + Vector3.up * 0.6f, "Chí mạng!", new Color(1f, 0.8f, 0.2f));
                    if (e.Combo >= 2)
                    {
                        _hud.Combo(e.Side, e.Combo);
                        FxBurst.Popup(transform, other.Center + Vector3.up * 0.4f, "x" + e.Combo, ComboColor(e.Combo),
                                      0.16f + 0.02f * Mathf.Min(e.Combo, 5));
                    }
                    break;
                case BattleEventKind.Blocked:
                    self.Strike(false);
                    FxBurst.Ring(transform, other.Center, GuardBlue);
                    FxBurst.Popup(transform, other.Center + Vector3.up * 0.6f, "Đỡ!", GuardBlue, 0.18f);
                    break;
                case BattleEventKind.Countered:
                    self.Strike(true);
                    other.CancelAttack();   // đòn đâm vào thế đỡ: thôi lao, bị hất lùi
                    other.Flinch(1.3f);
                    other.Dizzy();
                    FxBurst.Spark(transform, other.Center, Gold, 18, 3f);
                    FxBurst.Popup(transform, self.Center + Vector3.up * 0.7f, "Phản đòn!", Gold, 0.3f);
                    if (e.Combo >= 2) _hud.Combo(e.Side, e.Combo);
                    HitStop(0.12f);
                    Shake(0.09f);
                    break;
                case BattleEventKind.Interrupted:
                    self.CancelAttack();
                    self.Dizzy();
                    FxBurst.Popup(transform, self.Center + Vector3.up * 0.7f, "Bị cắt đòn!", new Color(1f, 0.6f, 0.5f), 0.18f);
                    break;
                case BattleEventKind.WrongSwipe:
                    self.Dizzy();
                    FxBurst.Popup(transform, self.Center + Vector3.up * 0.7f, "Vuốt sai!", new Color(1f, 0.45f, 0.4f));
                    if (mine)
                        _hud.Hint("Vuốt sai" + (string.IsNullOrEmpty(_lastFail) ? "" : " (" + _lastFail + ")") +
                                  " — hở 0,5 giây. Vuốt thẳng, hoặc V / Z rõ góc", 1.8f);
                    break;
                case BattleEventKind.Guard:
                    self.Guard();
                    break;
                case BattleEventKind.KO:
                    other.Knockout();
                    self.Cheer();
                    HitStop(0.15f);
                    SlowMo(1f);   // hạ gục: chậm lại 1 giây cho thấy rõ
                    Shake(0.15f);
                    break;
                case BattleEventKind.RageFull:
                    FxBurst.Spark(transform, self.Center, RageOrange, 12, 1.4f);
                    if (mine) _hud.Hint("Nộ đầy! Vuốt Z để ra Tuyệt chiêu", 2f);
                    break;
                case BattleEventKind.RageLow:
                    if (!mine) break;
                    _hud.FlashRage(BattleSession.PlayerSide);
                    _hud.Hint("Nộ chưa đầy — đánh trúng, đỡ, phản đòn để tích Nộ", 1.6f);
                    break;
            }
        }

        private void Show(SessionNote n)
        {
            switch (n.Kind)
            {
                case SessionNoteKind.Practiced:
                    FxBurst.Spark(transform, _player.Center + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.5f), 5, 1.2f);
                    break;
                case SessionNoteKind.Learned:
                    _hud.Big("Thuộc chiêu " + BattleHud.SkillNames[n.Skill] + "!", 2.2f);
                    _playerFx.Evolve(false);   // quầng sáng + tiếng chuông
                    if (n.Skill == 3) _hud.Hint("Đấu thật: thanh Nộ đầy mới ra được Tuyệt chiêu", 3f);
                    break;
                case SessionNoteKind.AllLearned:
                    _hud.Hint("Đã thuộc cả 4 chiêu!", 3f);
                    break;
                case SessionNoteKind.Retry:
                    _hud.Hint("Vuốt lại nhé" + (string.IsNullOrEmpty(n.Text) ? "" : " (" + n.Text + ")"), 1.2f);
                    break;
                case SessionNoteKind.GuardHint:
                    _hud.Hint("Hình nộm sắp đánh — vuốt XUỐNG để đỡ!", 1.5f);
                    break;
            }
        }

        private void ShowResult()
        {
            BattleResult r = _session.Result;
            string title = _session.Mode == BattleMode.Training ? "Xong buổi tập"
                         : r.PlayerWon ? "Thắng!" : r.Outcome == BattleOutcome.Draw ? "Hòa" : "Thua";
            var lines = new List<string>();
            if (r.Learned.Count > 0)
            {
                var names = new List<string>();
                foreach (int k in r.Learned) names.Add(BattleHud.SkillNames[k]);
                lines.Add("Thuộc chiêu: " + string.Join(", ", names));
            }
            if (r.Gained.Strength > 0.01f) lines.Add("+" + r.Gained.Strength.ToString("0.#") + " Sức mạnh");
            if (r.Gained.Speed > 0.01f) lines.Add("+" + r.Gained.Speed.ToString("0.#") + " Tốc độ");
            if (r.Coins > 0) lines.Add("+" + r.Coins + " Coin");
            if (_match?.OnResult != null) lines.AddRange(_match.OnResult(r));   // PvP: Elo / bậc / Coin do App tính
            if (lines.Count == 0) lines.Add("Hôm nay tập đủ rồi, mai tập tiếp nhé");
            Action again = _match?.Again;
            if (again != null && !string.IsNullOrEmpty(_match.AgainLabel))   // Thám hiểm: "Đánh lại" = rời màn đấu rồi vào lại
                _hud.ShowResult(title, string.Join("\n", lines), Exit, _match.AgainLabel, () =>
                {
                    Exit();
                    again();
                });
            else _hud.ShowResult(title, string.Join("\n", lines), Exit);
        }

        private void OnDestroy() => Time.timeScale = 1f;   // rời màn đấu bằng đường nào cũng trả tốc độ game về bình thường

        private void Exit()
        {
            Time.timeScale = 1f;
            if (_cam != null)
            {
                _cam.transform.SetPositionAndRotation(_camPos, _camRot);
                _cam.fieldOfView = _camFov;
            }
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;
            _pets.Save();
            Action done = _onExit;
            _onExit = null;
            Destroy(gameObject);
            done?.Invoke();
        }
    }
}
