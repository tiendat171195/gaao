using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using PawFit.Garden.View;
using PawFit.Pet;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>Dữ liệu App điền cho 1 lần đi săn: màn nào + chữ trên thẻ kết quả + các nút sau trận.</summary>
    public sealed class HuntOptions
    {
        public int Stage;

        /// <summary>Mã màn ("1-3"), tên màn, tên vùng (hiện lúc vào trận).</summary>
        public string Code;
        public string Title;
        public string RegionName;

        /// <summary>Hết trận: App trả các dòng cho thẻ kết quả (sao, Coin, mở màn / vùng mới...).</summary>
        public Func<HuntSession, List<string>> Lines;

        /// <summary>Nút "Đánh lại" (rời màn rồi gọi), null = không có.</summary>
        public Action Again;

        /// <summary>Nút "Đánh tiếp" (App quyết theo kết quả: có màn sau đã mở + bé đủ sức). null / nhãn rỗng = không có.</summary>
        public Func<HuntSession, KeyValuePair<string, Action>> Next;
    }

    /// <summary>
    /// Màn Săn quái (màn NGANG, Phase 9.3) — Thám hiểm kiểu mới, khác trận 1-1 PvP: bé đứng giữa dải đường của vùng, quái rừng
    /// kéo tới từ 2 bên theo đợt. Chạm = đánh (chạm trúng con nào đánh con đó, chạm chỗ trống = con gần nhất), vuốt lên = đập đất,
    /// vuốt xuống = đỡ, vuốt ngang = lướt, đầy Nộ = nút TUYỆT CHIÊU. Luật ở HuntSession / HuntSimulation (C# thuần);
    /// đây chỉ đọc chạm, chạy hoạt cảnh + tiếng theo sự kiện, camera đi theo bé.
    /// </summary>
    public sealed class HuntScene : MonoBehaviour
    {
        private static readonly Vector3 ArenaPos = new Vector3(300f, 0f, 0f);   // xa vườn, camera không thấy vườn

        private HuntSession _session;
        private HuntOptions _opt;
        private PetService _pets;
        private ILog _log;
        private Action _onExit;
        private Camera _cam;
        private Vector3 _camPos;
        private Quaternion _camRot;
        private float _camFov, _camX, _shake, _slow, _endT = -1f;
        private HuntHud _hud;
        private HunterView _hunter;
        private IPointerInput _input;
        private HuntGestures _gestures;
        private float _dpi = 1f;
        private bool _blocked, _resultShown;
        private readonly Dictionary<int, MonsterView> _monsters = new Dictionary<int, MonsterView>();
        private readonly Dictionary<int, ShotView> _shots = new Dictionary<int, ShotView>();
        private readonly List<HuntEvent> _events = new List<HuntEvent>();
        private readonly List<int> _drop = new List<int>();
        private readonly HashSet<int> _alive = new HashSet<int>();

        public static HuntScene Enter(ServiceLocator s, HuntOptions options, Action onExit)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var scene = new GameObject("PawFit_Hunt").AddComponent<HuntScene>();
            scene.Build(s, options, onExit);
            return scene;
        }

        private void Build(ServiceLocator s, HuntOptions o, Action onExit)
        {
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.LandscapeLeft;   // đi săn: màn ngang
            _opt = o;
            _pets = s.Get<PetService>();
            _log = s.Get<ILog>();
            _onExit = onExit;
            var adv = s.Get<AdventureService>();
            _session = adv.StartHunt(o.Stage, (ulong)DateTime.UtcNow.Ticks);
            _session.Events = s.TryGet(out EventBus bus) ? bus : null;   // hết trận báo BattleEnded (nhiệm vụ ngày)
            transform.position = ArenaPos;
            ArenaBuilder.Build(transform, _session.Plan.Region, lane: true);
            _hunter = HunterView.Create(transform, _pets, s.Get<EvolutionService>());
            _cam = Camera.main;
            if (_cam != null)
            {
                _camPos = _cam.transform.position;
                _camRot = _cam.transform.rotation;
                _camFov = _cam.fieldOfView;
                _cam.fieldOfView = 40f;
            }
            _hud = HuntHud.Create(transform, _pets.Current.Name, OnQuit, () => _session.Ultimate());
            _input = new LegacyPointerInput();
            _dpi = Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
            _gestures = new HuntGestures(null, _dpi);
            _hud.Big(o.Code + " · " + o.Title, 2f);
            _hud.Hint("Chạm để đánh · quái hiện dấu ! thì vuốt xuống để đỡ, vuốt ngang để lướt né", 4f);
            Sfx.Music(MusicId.Battle, 0.36f);
            Sfx.Play(SoundId.Fight, 0.5f);
            _log.Info("[PawFit] Đi săn: " + o.Code + " " + o.Title + " · " + _session.Plan.TotalMonsters + " quái");
        }

        private void OnQuit()
        {
            if (_session.Finished) return;
            _session.Quit();
        }

        // ------------------------------------------------------------------ mỗi frame

        private void Update()
        {
            if (_session == null) return;
            float dt = Time.deltaTime;
            if (_slow > 0f)   // phản đòn: chậm lại 1 nhịp cho đã mắt
            {
                _slow -= dt;
                dt *= 0.35f;
            }
            ReadInput();
            _session.Update(dt);
            _events.Clear();
            _session.TakeEvents(_events);
            foreach (HuntEvent e in _events) OnEvent(e);
            SyncViews(dt);
            UpdateHud();
            if (_session.Finished && _endT < 0f) _endT = 1.2f;
            if (_endT > 0f && !_resultShown)
            {
                _endT -= Time.deltaTime;
                if (_endT <= 0f) ShowResult();
            }
        }

        private void ReadInput()
        {
            PointerSample p = _input.Read();
            if (_session.Finished || _hud.ResultShown) return;
            switch (p.Phase)
            {
                case PointerPhase.Began:
                    _blocked = _hud.IsOverButton(p.Position);   // chạm nút (Bỏ cuộc / Tuyệt chiêu) không tính là đánh
                    if (!_blocked) _gestures.Down(p.Position.ToNumerics(), p.Time);
                    break;
                case PointerPhase.Moved:
                    if (!_blocked) Do(_gestures.Move(p.Position.ToNumerics(), p.Time));
                    break;
                case PointerPhase.Ended:
                    if (!_blocked) Do(_gestures.Up(p.Position.ToNumerics(), p.Time));
                    _blocked = false;
                    break;
            }
        }

        private void Do(HuntGestureKind k)
        {
            switch (k)
            {
                case HuntGestureKind.Tap: _session.Tap(PickMonster(_gestures.Start.ToUnity())); break;
                case HuntGestureKind.Up: _session.Slam(); break;
                case HuntGestureKind.Down: _session.Guard(); break;
                case HuntGestureKind.Left: _session.Dash(-1); break;
                case HuntGestureKind.Right: _session.Dash(1); break;
            }
        }

        /// <summary>Chạm trúng (gần) con quái nào trên màn hình thì đánh con đó; chạm chỗ trống = -1 (con gần bé nhất).</summary>
        private int PickMonster(Vector2 screen)
        {
            if (_cam == null) return -1;
            int best = -1;
            float bd = 130f * _dpi;
            foreach (MonsterView v in _monsters.Values)
            {
                if (v == null || !v.Monster.IsAlive) continue;
                Vector3 sp = _cam.WorldToScreenPoint(v.Center);
                if (sp.z <= 0f) continue;
                float d = Vector2.Distance(screen, new Vector2(sp.x, sp.y)) - v.Monster.Def.Size * 60f * _dpi;
                if (d < bd)
                {
                    bd = d;
                    best = v.Monster.Id;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ sự kiện -> hình + tiếng

        private void OnEvent(HuntEvent e)
        {
            HuntSimulation sim = _session.Sim;
            MonsterView mv = e.Monster >= 0 && _monsters.TryGetValue(e.Monster, out MonsterView found) ? found : null;
            Vector3 at = ArenaPos + new Vector3(e.X, 0.6f, 0f);
            switch (e.Kind)
            {
                case HuntEventKind.WaveStart:
                    bool bossWave = _session.Plan.Boss && e.Value == sim.WaveCount - 1;
                    _hud.Big(bossWave ? "TRÙM XUẤT HIỆN!" : "Đợt " + (e.Value + 1) + "/" + sim.WaveCount, 1.4f);
                    Sfx.Play(bossWave ? SoundId.RageFull : SoundId.Chime, bossWave ? 0.45f : 0.3f, bossWave ? 0.8f : 1f);
                    break;
                case HuntEventKind.Spawn:
                    EnsureView(sim.Find(e.Monster));
                    break;
                case HuntEventKind.Windup:
                    HuntMonster wm = sim.Find(e.Monster);
                    Sfx.Play(SoundId.UltCharge, wm != null && wm.Def.Boss ? 0.32f : 0.16f, wm != null && wm.Def.Boss ? 0.7f : 1.25f);
                    break;
                case HuntEventKind.Interrupted:
                    if (mv != null) FxBurst.Popup(transform, mv.Center + Vector3.up * 0.4f, "Cắt đòn!", new Color(0.7f, 0.9f, 1f), 0.16f);
                    break;
                case HuntEventKind.Hit:
                    if (mv != null)
                    {
                        mv.Hit();
                        at = mv.Center;
                    }
                    FxBurst.Spark(transform, at, e.Crit ? new Color(1f, 0.85f, 0.3f) : Color.white, e.Crit ? 9 : 5, e.Crit ? 3f : 2f);
                    FxBurst.Popup(transform, at + Vector3.up * 0.35f, Mathf.RoundToInt(e.Amount).ToString(),
                                  e.Crit ? new Color(1f, 0.8f, 0.2f) : Color.white, e.Crit ? 0.26f : 0.19f);
                    Sfx.PlayVaried(e.Crit ? SoundId.HitCrit : SoundId.HitLight, e.Crit ? 0.5f : 0.38f);
                    if (e.Crit) Shake(0.12f);
                    break;
                case HuntEventKind.Kill:
                    if (mv != null)
                    {
                        mv.Die();
                        at = mv.Center;
                    }
                    FxBurst.Spark(transform, at, new Color(1f, 1f, 0.85f), 12, 3f);
                    Sfx.PlayVaried(SoundId.Pop, 0.45f);
                    Sfx.Play(SoundId.KO, 0.22f, 1.3f);
                    break;
                case HuntEventKind.Drop:   // quái rơi hạt giống cho vườn rau (Phase 10.3): túi hạt bật lên rồi bay vào bé
                    Vector3 seedAt = (mv != null ? mv.Center : at) + Vector3.up * 0.15f;
                    SeedPickup.Spawn(transform, seedAt, () => _hunter.Center);
                    FxBurst.Popup(transform, seedAt + Vector3.up * 0.55f, "+" + Mathf.RoundToInt(e.Amount) + " hạt " + CropNames.Of((CropKind)e.Value),
                                  new Color(0.62f, 1f, 0.55f), 0.2f);
                    Sfx.Play(SoundId.Chime, 0.22f, 1.45f);
                    break;
                case HuntEventKind.PetHurt:
                    _hunter.OnHurt();
                    FxBurst.Spark(transform, _hunter.Center, new Color(1f, 0.35f, 0.3f), 7, 2.4f);
                    FxBurst.Popup(transform, _hunter.Center + Vector3.up * 0.5f, "-" + Mathf.RoundToInt(e.Amount), new Color(1f, 0.4f, 0.35f), 0.2f);
                    Sfx.PlayVaried(SoundId.HitHeavy, 0.45f);
                    Shake(0.16f);
                    break;
                case HuntEventKind.Guarded:
                    Sfx.PlayVaried(SoundId.Block, 0.45f);
                    break;
                case HuntEventKind.PerfectGuard:
                    _hunter.OnPerfectGuard();
                    _hud.Big("PHẢN ĐÒN!", 0.9f);
                    FxBurst.Spark(transform, _hunter.Front, new Color(1f, 0.9f, 0.4f), 10, 3f);
                    Sfx.Play(SoundId.Counter, 0.55f);
                    _slow = 0.2f;
                    Shake(0.14f);
                    break;
                case HuntEventKind.Dodged:
                    FxBurst.Popup(transform, _hunter.Center + Vector3.up * 0.5f, "NÉ!", new Color(0.6f, 0.95f, 1f), 0.2f);
                    Sfx.PlayVaried(SoundId.WhooshQuick, 0.4f);
                    break;
                case HuntEventKind.Whiff:
                    Sfx.PlayVaried(SoundId.Swish, 0.2f);
                    break;
                case HuntEventKind.Strike:
                    _hunter.OnStrike();
                    FxBurst.Slash(transform, _hunter.Front, Color.white, sim.Hunter.Facing > 0 ? -25f : 25f, 0.8f, 0.06f);
                    Sfx.PlayVaried(SoundId.WhooshQuick, 0.3f);
                    break;
                case HuntEventKind.Slam:
                    _hunter.OnSlam();
                    FxBurst.Ring(transform, ArenaPos + new Vector3(e.X, 0.05f, 0f), new Color(1f, 0.85f, 0.5f));
                    FxBurst.Spark(transform, ArenaPos + new Vector3(e.X, 0.1f, 0f), new Color(0.65f, 0.5f, 0.35f), 14, 3.2f);
                    Sfx.Play(SoundId.WhooshHeavy, 0.5f);
                    Sfx.Play(SoundId.HitHeavy, 0.4f, 0.8f);
                    Shake(0.3f);
                    break;
                case HuntEventKind.Dash:
                    FxBurst.Spark(transform, _hunter.Center, new Color(0.75f, 0.9f, 1f), 6, 1.5f);
                    Sfx.PlayVaried(SoundId.WhooshSpecial, 0.35f);
                    break;
                case HuntEventKind.Ultimate:
                    _hunter.OnUltimate();
                    _hud.Big("TUYỆT CHIÊU!", 1.2f);
                    FxBurst.Ring(transform, ArenaPos + new Vector3(e.X, 0.05f, 0f), new Color(1f, 0.75f, 0.3f));
                    foreach (MonsterView v in _monsters.Values)
                        if (v != null && v.Monster.IsAlive) FxBurst.Spark(transform, v.Center, new Color(1f, 0.8f, 0.3f), 10, 3f);
                    Sfx.Play(SoundId.UltCharge, 0.5f, 0.8f);
                    Sfx.Play(SoundId.WhooshSpecial, 0.55f, 0.8f);
                    Shake(0.45f);
                    break;
                case HuntEventKind.Shot:
                    HuntProjectile shot = null;
                    foreach (HuntProjectile p in sim.Projectiles)
                        if (p.Id == e.Value) shot = p;
                    if (shot != null && !_shots.ContainsKey(shot.Id)) _shots[shot.Id] = ShotView.Create(transform, shot);
                    Sfx.PlayVaried(SoundId.Pop, 0.22f, 0.2f);
                    break;
                case HuntEventKind.ShotGone:
                    if (_shots.TryGetValue(e.Value, out ShotView sv))
                    {
                        if (sv != null)
                        {
                            FxBurst.Spark(transform, sv.transform.position, new Color(0.8f, 1f, 0.6f), 4, 1.2f);
                            Destroy(sv.gameObject);
                        }
                        _shots.Remove(e.Value);
                    }
                    break;
                case HuntEventKind.Smash:
                    FxBurst.Ring(transform, ArenaPos + new Vector3(e.X, 0.05f, 0f), new Color(0.9f, 0.4f, 0.3f));
                    Sfx.Play(SoundId.HitHeavy, 0.35f, 0.7f);
                    if (Mathf.Abs(e.X - sim.Hunter.X) < 4f) Shake(0.2f);
                    break;
                case HuntEventKind.Charge:
                    Sfx.Play(SoundId.WhooshHeavy, 0.4f, 0.9f);
                    break;
                case HuntEventKind.Summon:
                    HuntMonster boss = sim.Find(e.Monster);
                    if (boss != null) _hud.Hint(boss.Def.Name + " gọi thêm quái!", 1.6f);
                    Sfx.Play(SoundId.Boing, 0.4f, 0.8f);
                    break;
                case HuntEventKind.Enrage:
                    HuntMonster angry = sim.Find(e.Monster);
                    if (angry != null) _hud.Big(angry.Def.Name + " nổi giận!", 1.4f);
                    Sfx.Play(SoundId.RageFull, 0.45f, 0.75f);
                    break;
                case HuntEventKind.Combo:
                    _hud.Combo(e.Value);
                    if (e.Value == 3 || e.Value == 6 || e.Value == 10 || e.Value == 15)
                        Sfx.Play(e.Value >= 15 ? SoundId.Combo5 : e.Value >= 10 ? SoundId.Combo4 : e.Value >= 6 ? SoundId.Combo3 : SoundId.Combo2, 0.4f);
                    break;
                case HuntEventKind.RageFull:
                    _hud.Hint("Đầy Nộ! Chạm nút TUYỆT CHIÊU", 2f);
                    Sfx.Play(SoundId.RageFull, 0.4f);
                    break;
                case HuntEventKind.Won:
                    _hunter.OnWin();
                    _hud.Big("THẮNG!", 1.5f);
                    Sfx.Music(MusicId.None);
                    Sfx.Play(SoundId.Win, 0.7f);
                    break;
                case HuntEventKind.Lost:
                    _hunter.OnDead();
                    _hud.Big("Bé kiệt sức rồi...", 1.5f);
                    Sfx.Music(MusicId.None);
                    Sfx.Play(SoundId.Lose, 0.7f);
                    break;
            }
        }

        private MonsterView EnsureView(HuntMonster m)
        {
            if (m == null) return null;
            if (_monsters.TryGetValue(m.Id, out MonsterView v) && v != null) return v;
            v = MonsterView.Create(transform, m, _session.Plan.Region);
            _monsters[m.Id] = v;
            return v;
        }

        private void SyncViews(float dt)
        {
            HuntSimulation sim = _session.Sim;
            _hunter.Tick(sim.Hunter, dt, sim.RageFull);
            _alive.Clear();
            foreach (HuntMonster m in sim.Monsters)
            {
                _alive.Add(m.Id);
                EnsureView(m);
            }
            _drop.Clear();
            foreach (KeyValuePair<int, MonsterView> kv in _monsters)
            {
                MonsterView v = kv.Value;
                if (v == null)
                {
                    _drop.Add(kv.Key);
                    continue;
                }
                if (!_alive.Contains(kv.Key) || !v.Monster.IsAlive) v.Die();   // quái đã biến mất khỏi trận: chạy nốt cảnh chết
                v.Tick(dt);
                if (v.Gone)
                {
                    Destroy(v.gameObject);
                    _drop.Add(kv.Key);
                }
            }
            foreach (int id in _drop) _monsters.Remove(id);

            _drop.Clear();
            foreach (KeyValuePair<int, ShotView> kv in _shots)
            {
                if (kv.Value == null || !kv.Value.Shot.Alive)
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    _drop.Add(kv.Key);
                    continue;
                }
                kv.Value.Tick(dt);
            }
            foreach (int id in _drop) _shots.Remove(id);
        }

        private void UpdateHud()
        {
            HuntSimulation sim = _session.Sim;
            Hunter h = sim.Hunter;
            _hud.SetHp(h.Hp01);
            _hud.SetRage(h.Rage / Mathf.Max(1f, sim.Config.RageMax), sim.RageFull && !_session.Finished);
            _hud.SetWave(sim.Wave, sim.WaveCount, _session.Plan.Boss);
            HuntMonster boss = null;
            foreach (HuntMonster m in sim.Monsters)
                if (m.Def.Boss && m.IsAlive) boss = m;
            _hud.SetBoss(boss != null ? boss.Def.Name : null, boss != null ? boss.Hp01 : 0f);
            _hud.SetCooldowns(h.SlamCd <= 0f, h.DashCd <= 0f, h.GuardCd <= 0f);
        }

        private void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        /// <summary>Camera ngang đi theo bé (giữ cả dải đường trong khung), lùi xa hơn khi màn hình hẹp; rung khi đòn mạnh.</summary>
        private void LateUpdate()
        {
            if (_cam == null || _session == null) return;
            float dt = Time.deltaTime;
            float target = Mathf.Clamp(_session.Sim.Hunter.X * 0.55f, -3.2f, 3.2f);
            _camX = Mathf.Lerp(_camX, target, 1f - Mathf.Exp(-dt * 4f));
            float aspect = Mathf.Max(0.5f, _cam.aspect);
            float dist = 10.5f * Mathf.Clamp(1.9f / aspect, 1f, 2.2f);
            _shake = Mathf.Max(0f, _shake - dt * 1.8f);
            Vector3 jitter = _shake > 0f ? new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), 0f) * _shake * 0.25f : Vector3.zero;
            Vector3 eye = ArenaPos + new Vector3(_camX, 2.7f + dist * 0.05f, -dist) + jitter;
            Vector3 look = ArenaPos + new Vector3(_camX, 0.85f, 0f);
            _cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
        }

        // ------------------------------------------------------------------ hết trận

        private void ShowResult()
        {
            if (_resultShown) return;
            _resultShown = true;
            var lines = new List<string>();
            if (_opt.Lines != null) lines.AddRange(_opt.Lines(_session));
            if (_session.StrengthGained > 0.01f) lines.Add("+" + _session.StrengthGained.ToString("0.#") + " Sức mạnh");
            if (_session.SpeedGained > 0.01f) lines.Add("+" + _session.SpeedGained.ToString("0.#") + " Tốc độ");
            lines.Add("Hạ " + _session.Sim.Kills + "/" + _session.Plan.TotalMonsters + " quái");
            var buttons = new List<KeyValuePair<string, Action>> { new KeyValuePair<string, Action>("Về vườn", Exit) };
            Action again = _opt.Again;
            if (again != null) buttons.Add(new KeyValuePair<string, Action>("Đánh lại", () =>
            {
                Exit();
                again();
            }));
            if (_opt.Next != null)
            {
                KeyValuePair<string, Action> next = _opt.Next(_session);
                if (!string.IsNullOrEmpty(next.Key) && next.Value != null)
                {
                    Action go = next.Value;
                    buttons.Add(new KeyValuePair<string, Action>(next.Key, () =>
                    {
                        Exit();
                        go();
                    }));
                }
            }
            _hud.ShowResult(_session.Won ? "Thắng!" : "Thua", string.Join("\n", lines), buttons);
        }

        private void Exit()
        {
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
