using System;
using System.Numerics;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>Thú đang làm gì trong vườn.</summary>
    /// <summary>Petted = đang được vuốt ve (Phase 8.2). Playing = đang chơi đồ chơi: chạy theo bóng / lông vũ / bong bóng (Phase 8.3).</summary>
    public enum PetActivity { Idle, Wander, MoveTo, Led, GoingToZone, Eating, Bathing, Reacting, Sleeping, Training, Petted, Playing }

    /// <summary>
    /// "Bộ não" của thú trong vườn (C# thuần): nhận lệnh người chơi + tự quyết khi rảnh
    /// (đi lang thang, đói thì tới chòi ăn chờ, buồn ngủ thì về ổ; ốm thì đi chậm, không chạy, hay về ổ nằm nghỉ).
    /// View chỉ đọc Activity / Target / Run / SpeedFactor để điều khiển NavMeshAgent và chạy hiệu ứng.
    /// </summary>
    public sealed class PetBrain
    {
        private readonly GardenLayout _layout;
        private readonly GardenConfig _cfg;
        private readonly IRandom _rng;
        private readonly PetService _pets;
        private readonly CareActionService _care;
        private float _timer;
        private float _playerHold;
        private float _moveTimeout;
        private ZoneType _zoneGoal;
        private Vector2 _lastLedPos;
        private bool _hasLedPos;
        private float _ledMeters;

        public PetBrain(GardenLayout layout, GardenConfig cfg, IRandom rng, PetService pets, CareActionService care)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _care = care ?? throw new ArgumentNullException(nameof(care));
            Activity = PetActivity.Idle;
            _timer = 1f;
        }

        public PetActivity Activity { get; private set; }

        /// <summary>Đích đang đi tới (tọa độ mặt đất).</summary>
        public Vector2 Target { get; private set; }

        public bool Run { get; private set; }

        public ZoneType ZoneGoal => _zoneGoal;

        /// <summary>Hệ số tốc độ theo sức khỏe (mệt / ốm / nguy kịch đi chậm hơn).</summary>
        public float SpeedFactor
        {
            get
            {
                switch (Stage)
                {
                    case HealthStage.Tired: return _cfg.TiredSpeedFactor;
                    case HealthStage.Sick: return _cfg.SickSpeedFactor;
                    case HealthStage.Critical: return _cfg.CriticalSpeedFactor;
                    default: return 1f;
                }
            }
        }

        private HealthStage Stage => _pets.HasPet ? _pets.Current.Health.Stage : HealthStage.Dead;
        private bool Ill => HealthRules.IsIll(Stage);

        /// <summary>Có thú mới (nở từ trứng / thú mới): quên hết việc đang làm của con cũ.</summary>
        public void Reset()
        {
            Activity = PetActivity.Idle;
            _timer = 1f;
            _playerHold = 0f;
            _moveTimeout = 0f;
            _zoneGoal = ZoneType.None;
            Run = false;
        }

        /// <summary>Thú có đang cần di chuyển tới Target không.</summary>
        public bool IsMoving =>
            Activity == PetActivity.Wander || Activity == PetActivity.MoveTo ||
            Activity == PetActivity.Led || Activity == PetActivity.GoingToZone || Activity == PetActivity.Playing;

        /// <summary>Đang tự chơi bóng (bé tự quyết lúc rảnh), khác với người chơi ném bóng cho bé.</summary>
        public bool SelfPlaying { get; private set; }

        /// <summary>
        /// Có bóng trong vườn thì View đặt hàm này (trả vị trí bóng). Bé rảnh + chán + còn khỏe thì thỉnh thoảng tự chạy tới đẩy bóng chơi.
        /// `WantsSelfPlay(roll)` = luật của PlayService.
        /// </summary>
        public Func<Vector2?> BallPosition { get; set; }
        public Func<double, bool> WantsSelfPlay { get; set; }

        // ------------------------------------------------------------------
        // Lệnh từ người chơi
        // ------------------------------------------------------------------

        /// <summary>Chạm xuống đất: thú chạy tới đó.</summary>
        public void CommandMoveTo(Vector2 point, Vector2 petPos)
        {
            WakeIfSleeping();
            StartMove(PetActivity.MoveTo, _layout.Clamp(point, 0.6f), true, petPos);
            _playerHold = _cfg.PlayerHoldSeconds;
        }

        /// <summary>Bắt đầu kéo thú: thú chạy theo ngón tay.</summary>
        public void BeginLead(Vector2 point)
        {
            WakeIfSleeping();
            _hasLedPos = false;
            Activity = PetActivity.Led;
            Target = _layout.Clamp(point, 0.6f);
            Run = true;
            _playerHold = _cfg.PlayerHoldSeconds;
        }

        public void Lead(Vector2 point)
        {
            if (Activity == PetActivity.Led) Target = _layout.Clamp(point, 0.6f);
        }

        /// <summary>Thả tay khi đang kéo: thả vào hồ = đi tắm, vào ổ = đi ngủ, vào sân tập = tập, chỗ khác = đứng đó.</summary>
        public void EndLead(Vector2 dropPoint, Vector2 petPos)
        {
            if (Activity != PetActivity.Led) return;
            FlushRun();
            ZoneType zone = _layout.ZoneAt(dropPoint);
            if (zone == ZoneType.Pond || zone == ZoneType.Bed || zone == ZoneType.TrainingGround) GoUse(zone, petPos);
            else StartMove(PetActivity.MoveTo, _layout.Clamp(dropPoint, 0.6f), false, petPos);
            _playerHold = _cfg.PlayerHoldSeconds;
        }

        /// <summary>Đi tới 1 khu và dùng nó (hồ: tắm, ổ: ngủ, chòi ăn: đứng chờ).</summary>
        public void CommandUseZone(ZoneType zone, Vector2 petPos)
        {
            WakeIfSleeping();
            GoUse(zone, petPos);
            _playerHold = _cfg.PlayerHoldSeconds;
        }

        /// <summary>Chạm vào thú: đang ngủ thì dậy, đang thức thì phản ứng (vuốt ve). Thú ốm: lau người, không đánh thức.</summary>
        public void OnTapped()
        {
            if (!_pets.HasPet) return;
            if (Ill)
            {
                _care.Pet();   // = lau người
                if (_pets.Current.IsSleeping) return;
                Activity = PetActivity.Reacting;
                _timer = _cfg.ReactSeconds;
                _playerHold = Math.Max(_playerHold, 2f);
                return;
            }
            if (_pets.Current.IsSleeping)
            {
                _care.Wake();
                SetIdle(1.5f);
                return;
            }
            _care.Pet();
            Activity = PetActivity.Reacting;
            _timer = _cfg.ReactSeconds;
            _playerHold = Math.Max(_playerHold, 2f);
        }

        /// <summary>Bắt đầu được vuốt ve: đứng yên tận hưởng. Đang ngủ thì vẫn ngủ (vuốt nhẹ không đánh thức).</summary>
        public void BeginPetting()
        {
            if (!_pets.HasPet || _pets.Current.IsSleeping) return;
            Activity = PetActivity.Petted;
            _timer = 30f;   // an toàn: View quên báo thả tay thì tự thôi
            Run = false;
            _playerHold = Math.Max(_playerHold, 3f);
        }

        /// <summary>1 lượt xoa trên người thú (mỗi lượt cộng vui, có thời gian chờ ở CareActionService).</summary>
        public void Stroke()
        {
            if (!_pets.HasPet) return;
            _care.Stroke();
            if (Activity == PetActivity.Petted) _timer = Math.Max(_timer, 5f);
        }

        /// <summary>Thả tay sau khi vuốt: vui vẻ phản ứng 1 chút rồi làm việc khác.</summary>
        public void EndPetting()
        {
            if (Activity != PetActivity.Petted) return;
            Activity = PetActivity.Reacting;
            _timer = _cfg.ReactSeconds;
            _playerHold = Math.Max(_playerHold, 2f);
        }

        /// <summary>
        /// Chơi đồ chơi: chạy theo `target` (View gọi liên tục khi bóng / lông vũ / bong bóng di chuyển).
        /// View thôi gọi quá 4 giây thì bé tự thôi chơi.
        /// </summary>
        public void Play(Vector2 target, bool selfPlay = false)
        {
            if (!_pets.HasPet || _pets.Current.IsSleeping) return;
            Activity = PetActivity.Playing;
            SelfPlaying = selfPlay;
            Target = _layout.Clamp(target, 0.6f);
            Run = !Ill;
            _timer = Math.Max(_timer, 4f);
            if (!selfPlay) _playerHold = Math.Max(_playerHold, 4f);
        }

        /// <summary>Thôi chơi (bé nhặt bóng về rồi / người chơi thả lông vũ): vui vẻ phản ứng 1 chút.</summary>
        public void EndPlay()
        {
            if (Activity != PetActivity.Playing) return;
            SelfPlaying = false;
            Activity = PetActivity.Reacting;
            _timer = _cfg.ReactSeconds;
        }

        /// <summary>Đã cho ăn thành công: đứng ăn 1 lúc.</summary>
        public void OnFed()
        {
            Activity = PetActivity.Eating;
            _timer = _cfg.EatSeconds;
            _playerHold = Math.Max(_playerHold, 3f);
        }

        // ------------------------------------------------------------------
        // Mỗi frame
        // ------------------------------------------------------------------

        public void Update(float dt, Vector2 petPos)
        {
            if (!_pets.HasPet) return;
            _playerHold = Math.Max(0f, _playerHold - dt);
            PetData pet = _pets.Current;
            if (pet.IsSleeping && Activity != PetActivity.Sleeping) Activity = PetActivity.Sleeping;

            switch (Activity)
            {
                case PetActivity.Sleeping:
                    if (!pet.IsSleeping) SetIdle(1f);
                    else if (pet.Needs.Energy >= _cfg.WakeAboveEnergy)
                    {
                        _care.Wake();
                        SetIdle(1f);
                    }
                    break;

                case PetActivity.Eating:
                case PetActivity.Bathing:
                case PetActivity.Reacting:
                case PetActivity.Training:
                case PetActivity.Petted:
                    _timer -= dt;
                    if (_timer <= 0f) SetIdle(RandomIdle());
                    break;

                case PetActivity.MoveTo:
                case PetActivity.Wander:
                    _moveTimeout -= dt;
                    if (Arrived(petPos) || _moveTimeout <= 0f) SetIdle(RandomIdle());
                    break;

                case PetActivity.GoingToZone:
                    _moveTimeout -= dt;
                    if (Arrived(petPos) || _moveTimeout <= 0f) UseZone(_zoneGoal);
                    break;

                case PetActivity.Playing:   // View cập nhật Target theo đồ chơi; thôi cập nhật thì tự thôi chơi
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        SelfPlaying = false;
                        SetIdle(RandomIdle());
                    }
                    break;

                case PetActivity.Led:   // View cập nhật Target theo ngón tay; chạy theo tay = tập Tốc độ
                    if (_hasLedPos) _ledMeters += Vector2.Distance(petPos, _lastLedPos);
                    _lastLedPos = petPos;
                    _hasLedPos = true;
                    if (_ledMeters >= 1f) FlushRun();
                    break;

                default:     // Idle
                    _timer -= dt;
                    if (_timer <= 0f && _playerHold <= 0f) Decide(petPos);
                    break;
            }
        }

        private void Decide(Vector2 petPos)
        {
            PetNeeds n = _pets.Current.Needs;
            if (n.Energy < _cfg.SleepyBelowEnergy)
            {
                GoUse(ZoneType.Bed, petPos);
                return;
            }
            if (n.Satiety < _cfg.HungryBelowSatiety)
            {
                GardenZone hut = _layout.Get(ZoneType.FoodHut);
                if (hut != null && Vector2.Distance(petPos, hut.UsePoint) > 1.2f) GoUse(ZoneType.FoodHut, petPos);
                else SetIdle(RandomIdle() * 1.5f);   // đói: đứng chờ ở chòi ăn (nhìn chòi + kêu)
                return;
            }
            if (Ill)   // ốm: không lang thang, mệt chút là về ổ nằm nghỉ
            {
                float rest = Stage == HealthStage.Critical ? _cfg.CriticalRestBelowEnergy : _cfg.SickRestBelowEnergy;
                if (n.Energy < rest) GoUse(ZoneType.Bed, petPos);
                else SetIdle(RandomIdle() * 2f);
                return;
            }
            if (Stage == HealthStage.Tired && _rng.NextDouble() < 0.5)   // mệt: lười đi hơn
            {
                SetIdle(RandomIdle() * 1.5f);
                return;
            }
            Vector2? ball = BallPosition?.Invoke();   // rảnh + chán: tự chạy tới đẩy bóng chơi (Phase 8.3)
            if (ball.HasValue && WantsSelfPlay != null && WantsSelfPlay(_rng.NextDouble()))
            {
                Play(ball.Value, true);
                _timer = 10f;
                return;
            }
            StartMove(PetActivity.Wander, RandomWanderPoint(petPos), _rng.NextDouble() < 0.15, petPos);
        }

        private void UseZone(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Pond:
                    if (_care.Bathe() == CareResult.Ok)
                    {
                        Activity = PetActivity.Bathing;
                        _timer = _cfg.BatheSeconds;
                    }
                    else SetIdle(RandomIdle());   // ốm: lắc đầu không chịu xuống nước
                    break;
                case ZoneType.Bed:
                    if (_care.Sleep() == CareResult.Ok) Activity = PetActivity.Sleeping;
                    else SetIdle(RandomIdle());
                    break;
                case ZoneType.TrainingGround:   // đấm hình nộm: Sức mạnh
                    if (_care.Workout() == CareResult.Ok)
                    {
                        Activity = PetActivity.Training;
                        _timer = _cfg.WorkoutSeconds;
                    }
                    else SetIdle(RandomIdle());
                    break;
                default:
                    SetIdle(RandomIdle());
                    break;
            }
            _zoneGoal = ZoneType.None;
        }

        private void GoUse(ZoneType zone, Vector2 petPos)
        {
            GardenZone z = _layout.Get(zone);
            if (z == null)
            {
                SetIdle(RandomIdle());
                return;
            }
            _zoneGoal = zone;
            StartMove(PetActivity.GoingToZone, z.UsePoint, zone != ZoneType.Bed, petPos);
        }

        private void StartMove(PetActivity activity, Vector2 target, bool run, Vector2 petPos)
        {
            Activity = activity;
            Target = target;
            Run = run && !Ill;   // ốm thì không chạy
            float speed = run ? _cfg.RunSpeed : _cfg.WalkSpeed;
            _moveTimeout = Vector2.Distance(petPos, target) / speed * 2f + _cfg.MoveTimeoutExtraSeconds;
        }

        private void SetIdle(float seconds)
        {
            Activity = PetActivity.Idle;
            _timer = seconds;
            Run = false;
        }

        private void FlushRun()
        {
            if (_ledMeters > 0f) _care.Run(_ledMeters);
            _ledMeters = 0f;
        }

        private void WakeIfSleeping()
        {
            if (_pets.Current.IsSleeping) _care.Wake();
        }

        private bool Arrived(Vector2 petPos) => Vector2.Distance(petPos, Target) <= _cfg.ArriveDistance;

        private float RandomIdle() => _cfg.IdleMinSeconds + (float)_rng.NextDouble() * (_cfg.IdleMaxSeconds - _cfg.IdleMinSeconds);

        /// <summary>Điểm lang thang ngẫu nhiên: trong vườn, không rơi vào giữa các khu.</summary>
        private Vector2 RandomWanderPoint(Vector2 from)
        {
            for (int i = 0; i < 10; i++)
            {
                double a = _rng.NextDouble() * Math.PI * 2.0;
                float d = 1.5f + (float)_rng.NextDouble() * (_cfg.WanderRadius - 1.5f);
                var p = _layout.Clamp(from + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * d, 1f);
                if (_layout.ZoneAt(p) == ZoneType.None) return p;
            }
            return _layout.PetSpawn;
        }
    }
}
