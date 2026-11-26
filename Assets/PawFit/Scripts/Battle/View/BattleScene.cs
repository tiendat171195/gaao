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
    /// Màn đấu (màn NGANG): đấu trường dựng bằng code, thú của người chơi (bên trái) vs hình nộm (bên phải).
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

        public static BattleScene Enter(ServiceLocator s, BattleMode mode, BotLevel level, Action onExit)
        {
            var scene = new GameObject("PawFit_Battle").AddComponent<BattleScene>();
            scene.Build(s, mode, level, onExit);
            return scene;
        }

        private void Build(ServiceLocator s, BattleMode mode, BotLevel level, Action onExit)
        {
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.LandscapeLeft;   // đấu: màn ngang
            _pets = s.Get<PetService>();
            _evo = s.Get<EvolutionService>();
            _log = s.Get<ILog>();
            _onExit = onExit;
            _session = new BattleSession(mode, level, _pets, _evo, (ulong)DateTime.UtcNow.Ticks);
            transform.position = ArenaPos;
            BuildArena();

            // thú của người chơi (bên trái, nhìn sang phải + hơi về camera)
            Transform p = Prim.Group(transform, "Player", new Vector3(-1.3f, 0f, 0f));
            Transform pBody = Prim.Group(p, "Body");
            var host = new GameObject("Pet");
            host.transform.SetParent(pBody, false);
            host.transform.localRotation = Quaternion.Euler(0f, 65f, 0f);
            IPetAvatar avatar = PetAvatarFactory.Create(host, _pets.Current.Dna, PetController.UsePlaceholder);
            avatar.SetGrowth(_evo.Look);
            _playerFx = host.AddComponent<PetMoodFx>();
            _playerFx.Init(avatar);
            _player = p.gameObject.AddComponent<FighterRig>();
            _player.Init(pBody, 1f, avatar);

            // hình nộm (bên phải, nhìn sang trái)
            Transform d = Prim.Group(transform, "Dummy", new Vector3(1.3f, 0f, 0f));
            Transform dBody = DummyBuilder.Build(d, level, out Transform[] arms);
            dBody.localRotation = Quaternion.Euler(0f, -25f, 0f);
            _dummy = d.gameObject.AddComponent<FighterRig>();
            _dummy.Init(dBody, -1f, null, arms);

            _cam = Camera.main;
            if (_cam != null)
            {
                _camPos = _cam.transform.position;
                _camRot = _cam.transform.rotation;
                _camFov = _cam.fieldOfView;
                _cam.fieldOfView = 38f;
                Vector3 eye = ArenaPos + new Vector3(0f, 1.25f, -5f);
                _cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(ArenaPos + new Vector3(0f, 0.75f, 0f) - eye));
            }
            bool training = mode == BattleMode.Training;
            _hud = BattleHud.Create(transform, training, _pets.Current.Name, training ? "Hình nộm tập" : LevelNames[(int)level],
                                    OnButton);
            _input = new LegacyPointerInput();
            _dpi = Screen.dpi > 0 ? Mathf.Max(1f, Screen.dpi / 160f) : 1f;
            _recognizer = new SwipeRecognizer(new SwipeConfig(), _dpi);
            if (training) _hud.Hint("Vuốt theo bóng mờ để học chiêu. Vuốt xuống để đỡ.", 3f);
            _log.Info("[PawFit] Vào sân tập: " + mode + (training ? "" : " · " + level));
        }

        private void BuildArena()
        {
            Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.05f, 0f), new Vector3(6.5f, 0.05f, 4f), Prim.Hex("#E3C88F"), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.02f, 0f), new Vector3(5.6f, 0.03f, 3.3f), Prim.Hex("#D9B877"), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, -0.3f, 6f), new Vector3(40f, 0.5f, 20f), Prim.Hex("#8CC36A"), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, 3f, 7f), new Vector3(40f, 8f, 0.2f), Prim.Hex("#BFE0F2"), smoothness: 0f);
            for (int i = -4; i <= 4; i++)   // hàng rào gỗ phía sau
            {
                Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(i * 1.2f, 0.45f, 2.6f), new Vector3(0.12f, 0.45f, 0.12f), Prim.Hex("#B9865A"));
                if (i < 4) Prim.Make(PrimitiveType.Cube, transform, new Vector3(i * 1.2f + 0.6f, 0.65f, 2.6f), new Vector3(1.2f, 0.08f, 0.06f), Prim.Hex("#7A5236"));
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
            ReadSwipes();
            _events.Clear();
            _notes.Clear();
            _session.Advance(Time.deltaTime, _events, _notes);
            foreach (BattleEvent e in _events) Show(e);
            foreach (SessionNote n in _notes) Show(n);

            Fighter me = _session.Sim.Fighters[BattleSession.PlayerSide], foe = _session.Sim.Fighters[BattleSession.BotSide];
            _hud.SetHp(me.HpPercent, foe.HpPercent);
            _hud.SetTime(_session.Sim.TimeLeft / _session.Sim.Config.DurationSeconds);
            if (_session.Mode == BattleMode.Training)
                _hud.SetLesson(_session.Lesson, _session.LessonProgress, _pets.Config.Evolution.PracticeToLearn, _session.SlowMo);
            if (_session.SlowMo) _hud.Hint("Hình nộm sắp đánh — vuốt XUỐNG để đỡ!", 0.3f);
            _player.Avatar?.LookAt(_dummy.Center);
            _player.Avatar?.SetMoveSpeed(0f);

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
                    SwipeResult r = _recognizer.Recognize(_stroke);
                    _session.PlayerSwipe(r, _events, _notes);
                    break;
            }
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

        private void Show(BattleEvent e)
        {
            FighterRig self = Rig(e.Side), other = Rig(1 - e.Side);
            switch (e.Kind)
            {
                case BattleEventKind.AttackStarted:
                    self.WindUp(_session.Sim.Config.WindupSeconds[(int)e.Move]);
                    break;
                case BattleEventKind.Hit:
                    self.Strike(e.Move != MoveKind.Quick);
                    other.Flinch(e.Move == MoveKind.Quick ? 0.5f : 1f);
                    FxBurst.Spark(transform, other.Center, e.Crit ? new Color(1f, 0.85f, 0.3f) : Color.white, e.Crit ? 16 : 9);
                    if (e.Crit) FxBurst.Popup(transform, other.Center + Vector3.up * 0.6f, "Chí mạng!", new Color(1f, 0.8f, 0.2f));
                    break;
                case BattleEventKind.Blocked:
                    self.Strike(false);
                    FxBurst.Ring(transform, other.Center, new Color(0.55f, 0.8f, 1f));
                    break;
                case BattleEventKind.Countered:
                    self.Strike(true);
                    other.Flinch(1.3f);
                    other.Dizzy();
                    FxBurst.Spark(transform, other.Center, new Color(1f, 0.85f, 0.3f), 18, 3f);
                    FxBurst.Popup(transform, self.Center + Vector3.up * 0.7f, "Phản đòn!", new Color(1f, 0.85f, 0.3f), 0.3f);
                    break;
                case BattleEventKind.Interrupted:
                    self.Dizzy();
                    break;
                case BattleEventKind.WrongSwipe:
                    self.Dizzy();
                    FxBurst.Popup(transform, self.Center + Vector3.up * 0.7f, "Vuốt sai!", new Color(1f, 0.45f, 0.4f));
                    break;
                case BattleEventKind.Guard:
                    self.Guard();
                    break;
                case BattleEventKind.KO:
                    other.Knockout();
                    self.Cheer();
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
            if (lines.Count == 0) lines.Add("Hôm nay tập đủ rồi, mai tập tiếp nhé");
            _hud.ShowResult(title, string.Join("\n", lines), Exit);
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
