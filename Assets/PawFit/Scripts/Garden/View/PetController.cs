using System;
using PawFit.Core;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using PawFit.Pet;
using PawFit.Pet.View;
using UnityEngine;
using UnityEngine.AI;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Nối "bộ não" PetBrain + biểu cảm PetExpressionService (C# thuần) với Unity:
    /// đặt đích cho NavMeshAgent, chạy hoạt cảnh của IPetAvatar và hiệu ứng PetMoodFx.
    /// Không chứa luật game — luật nằm ở PetBrain / CareActionService / PetExpressionService.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PetController : MonoBehaviour
    {
        private static readonly NVec2 NoTarget = new NVec2(float.NaN, float.NaN);

        /// <summary>Ép dùng thú tạm bằng khối (Debug overlay bật / tắt được).</summary>
        public static bool UsePlaceholder { get; set; }

        private PetBrain _brain;
        private CareActionService _care;
        private PetExpressionService _expr;
        private EvolutionService _evo;
        private GardenConfig _cfg;
        private NavMeshAgent _agent;
        private IPetAvatar _avatar;
        private PetMoodFx _fx;
        private Camera _cam;
        private Vector3 _foodPoint, _trainPoint;
        private IDisposable _careSub, _createdSub, _evolvedSub, _dnaSub;
        private NVec2 _sentTarget = NoTarget;
        private PetActivity _lastActivity = (PetActivity)(-1);
        private AccessoryKind _accessory;
        private bool _dead;

        // ---- tiếng (Phase 8.1): bước chân theo tốc độ, nhai lúc ăn, té nước + sủi bọt lúc tắm, thở lúc ngủ ----
        private float _stepT, _activityT;
        private int _step;

        // ---- vuốt ve + chạm (Phase 8.2) ----
        private bool _stroking;
        private Vector3 _hand;
        private float _heartT, _tapGlow, _lastTapTime = -10f;
        private object _playOwner;
        private Vector3? _benchSeat;
        private Vector3 _benchFront = Vector3.back, _admirePoint;
        private float _admireT;
        private bool _perched;
        private Vector3 _perchWorld;
        private CapsuleCollider _col;
        private static readonly Vector3 ColCenter = new Vector3(0f, 0.5f, 0.1f);
        private int _tapCount;

        public IPetAvatar Avatar => _avatar;

        /// <summary>Thú đang ốm / nguy kịch (khay đồ ăn đưa bát cháo thay quả táo).</summary>
        public bool IsIll => _expr != null && (_expr.Current.Has(PetCondition.Sick) || _expr.Current.Has(PetCondition.Critical));

        public static PetController Spawn(Vector3 position, PetDna dna, PetBrain brain, CareActionService care,
                                          PetExpressionService expr, EvolutionService evo, GardenLayout layout, GardenConfig cfg,
                                          EventBus bus, Transform parent)
        {
            var go = new GameObject("Pet");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));   // nhìn về phía camera
            var col = go.AddComponent<CapsuleCollider>();   // to hơn hình 1 chút cho dễ chạm trúng
            col.direction = 2;
            col.center = ColCenter;
            col.radius = 0.45f;
            col.height = 1.5f;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1f;
            agent.speed = cfg.WalkSpeed;
            agent.acceleration = 14f;
            agent.angularSpeed = 600f;
            agent.stoppingDistance = 0.05f;
            var pc = go.AddComponent<PetController>();
            pc._brain = brain;
            pc._care = care;
            pc._expr = expr;
            pc._evo = evo;
            pc._cfg = cfg;
            pc._agent = agent;
            pc._col = col;
            pc._cam = Camera.main;
            GardenZone hut = layout.Get(ZoneType.FoodHut);
            // điểm nhìn khi đòi ăn = khay đồ ăn (cùng chỗ GardenBuilder đặt khay)
            pc._foodPoint = hut != null ? NVec2.Lerp(hut.UsePoint, hut.Center, 0.55f).ToWorld(0.5f) : position;
            GardenZone train = layout.Get(ZoneType.TrainingGround);
            pc._trainPoint = train != null ? train.Center.ToWorld(0.6f) : position;   // nhìn về phía hình nộm khi tập
            pc._fx = go.AddComponent<PetMoodFx>();
            pc.RebuildAvatar(dna);
            // bị từ chối (no rồi, chưa buồn ngủ, ốm không chịu tắm...) thì lắc đầu; lau người thì có khăn
            pc._careSub = bus.Subscribe<CareDone>(e =>
            {
                if (pc._dead || pc._avatar == null) return;
                if (e.Result == CareResult.Refused) pc._avatar.PlayRefuse();
                else if (e.Kind == CareKind.Wipe) pc._fx.Wipe();
            });
            // có thú mới khi con này còn sống (công cụ test "Thú mới") thì dựng lại hình
            pc._createdSub = bus.Subscribe<PetCreated>(e =>
            {
                if (!pc._dead) pc.RebuildAvatar(e.Pet.Dna);
            });
            // tiến hóa: quầng sáng + tiếng chuông (dáng tự đổi mượt theo SetGrowth); DNA trượt: dựng lại hình
            pc._evolvedSub = bus.Subscribe<PetEvolved>(e =>
            {
                if (!pc._dead) pc._fx.Evolve(e.To == EvolutionStage.Ultimate);
            });
            pc._dnaSub = bus.Subscribe<PetDnaChanged>(e =>
            {
                if (!pc._dead) pc.RebuildAvatar(e.Pet.Dna);
            });
            return pc;
        }

        /// <summary>Dựng lại hình thú theo DNA: ưu tiên model Gen 1, không có thì dùng thú tạm.</summary>
        public void RebuildAvatar(PetDna dna)
        {
            if (_avatar is MonoBehaviour old) Destroy(old);   // OnDestroy của avatar tự xóa phần hình
            _avatar = PetAvatarFactory.Create(gameObject, dna, UsePlaceholder);
            _fx.Init(_avatar);
            AccessoryView.Attach(_avatar, _accessory);
            _lastActivity = (PetActivity)(-1);
        }

        /// <summary>Đeo / tháo phụ kiện (Phase 5.2). `celebrate` = vừa đổi trong quầy: thú nhảy lên khoe.</summary>
        public void SetAccessory(AccessoryKind kind, bool celebrate = false)
        {
            _accessory = kind;
            if (_dead || _avatar == null) return;
            AccessoryView.Attach(_avatar, kind);
            if (celebrate && kind != AccessoryKind.None && !IsIll) _avatar.PlayHop();
        }

        private void OnDestroy()
        {
            _careSub?.Dispose();
            _createdSub?.Dispose();
            _evolvedSub?.Dispose();
            _dnaSub?.Dispose();
        }

        /// <summary>Đang ở màn đấu: thú đứng yên trong vườn (bộ não không chạy).</summary>
        public void SetPaused(bool paused)
        {
            if (_dead) return;
            enabled = !paused;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = paused || !_brain.IsMoving;
                if (paused) _agent.ResetPath();
                _sentTarget = NoTarget;
            }
            if (paused) StopLoops();
            if (paused && _avatar != null)
            {
                _avatar.SetMoveSpeed(0f);
                _avatar.SetPose(AvatarPose.Idle);
            }
        }

        /// <summary>Thú đã mất: đứng lại, chạy cảnh chia tay rồi tự biến mất (gọi `done` khi xong).</summary>
        public void PlayDeath(Action done)
        {
            _dead = true;
            StopLoops();
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            _fx.SetFlies(0);
            _fx.SetSleeping(false);
            _fx.SetHealth(0f, false, false);
            PetDeathFx.Play(gameObject, _avatar, done);
        }

        private void Update()
        {
            if (_dead || _brain == null || _avatar == null || !_agent.isOnNavMesh) return;
            float dt = Time.deltaTime;
            _brain.Update(dt, transform.position.ToGround());
            if (!IsPlaying) _playOwner = null;
            _admireT -= dt;
            UpdatePerch();

            if (_brain.IsMoving)
            {
                _agent.isStopped = false;
                float baseSpeed = _brain.Activity == PetActivity.Led ? _cfg.LeadSpeed : (_brain.Run ? _cfg.RunSpeed : _cfg.WalkSpeed);
                _agent.speed = baseSpeed * _brain.SpeedFactor;   // mệt / ốm đi chậm
                NVec2 t = _brain.Target;
                if (float.IsNaN(_sentTarget.X) || NVec2.Distance(t, _sentTarget) > 0.05f)
                {
                    _agent.SetDestination(t.ToWorld());
                    _sentTarget = t;
                }
            }
            else if (!_agent.isStopped)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _sentTarget = NoTarget;
            }

            if (_brain.Activity == PetActivity.Reacting || _brain.Activity == PetActivity.Petted)
                FaceTowards(_cam != null ? _cam.transform.position : transform.position);
            UpdatePetting(dt);
            if (_brain.Activity == PetActivity.Training) FaceTowards(_trainPoint);
            if (_brain.Activity != _lastActivity)
            {
                if (_brain.Activity == PetActivity.Eating)
                {
                    _avatar.PlayEat();
                    Sfx.PlayVaried(SoundId.Munch, 0.55f);
                }
                else if (_brain.Activity == PetActivity.Bathing)
                {
                    _avatar.PlayBathe();
                    Sfx.Play(SoundId.Splash, 0.6f);
                }
                else if (_brain.Activity == PetActivity.Training) _avatar.PlayTrain(_cfg.WorkoutSeconds);
                else if (_brain.Activity == PetActivity.Resting)   // lên ghế ngồi: thở phào, tim bay
                {
                    Sfx.PlayVaried(SoundId.Happy, 0.3f, 0.1f);
                    _fx.Hearts(1);
                }
                _lastActivity = _brain.Activity;
                _activityT = 0f;
            }
            ActivitySounds(dt);
            if (_evo != null)   // lớn lên: dáng theo giai đoạn tiến hóa
            {
                GrowthLook look = _evo.Look;
                _avatar.SetGrowth(look);
                _fx.SetAura(look.Ultimate);
            }
            float v = _agent.velocity.magnitude;
            Footsteps(dt, v);
            _avatar.SetMoveSpeed(v);
            _avatar.SetPose(_brain.Activity == PetActivity.Sleeping ? AvatarPose.Sleep
                            : v > 2.4f ? AvatarPose.Run : v > 0.15f ? AvatarPose.Walk : AvatarPose.Idle);
            UpdateExpression(dt, v);
        }

        /// <summary>
        /// Ngồi ghế gỗ (Phase 8.4): hình bé nhảy lên mặt ghế (bé vẫn đứng ở chỗ trước ghế trên NavMesh), quay mặt ra trước;
        /// vùng chạm của bé theo lên ghế để vẫn vuốt ve được.
        /// </summary>
        private void UpdatePerch()
        {
            if (_avatar == null) return;
            PetActivity a = _brain.Activity;
            if (a == PetActivity.Resting && _benchSeat.HasValue && !_dead) _perched = true;
            else if (a != PetActivity.Petted && a != PetActivity.Reacting) _perched = false;   // được vuốt / chạm trên ghế: vẫn ngồi đó
            Vector3 local = Vector3.zero;
            _perchWorld = Vector3.zero;
            if (_perched && _benchSeat.HasValue)
            {
                if (a == PetActivity.Resting) FaceTowards(transform.position + _benchFront);
                local = transform.InverseTransformPoint(_benchSeat.Value);
                if (local.magnitude > 1.5f) local = local.normalized * 1.5f;   // an toàn: bé bị đẩy xa ghế thì không bay theo
                else _perchWorld = _benchSeat.Value - transform.position;
            }
            _avatar.SetPerch(local);
            if (_col != null) _col.center = ColCenter + local;
        }

        /// <summary>Giữa thân bé đang thấy trên màn hình (ngồi ghế thì ở trên mặt ghế) — để nhận vuốt ve cho đúng chỗ.</summary>
        public Vector3 BodyCenter => transform.position + _perchWorld + Vector3.up * 0.5f;

        /// <summary>Biểu cảm theo nhu cầu: mắt díp, bẩn có ruồi, đói thì nhìn chòi ăn + kêu, ngủ có Zzz.</summary>
        private void UpdateExpression(float dt, float speed)
        {
            if (_expr == null) return;
            ExpressionCue cue = _expr.Update(dt);
            PetExpression e = _expr.Current;
            _avatar.SetLook(new AvatarLook { EyeOpen = e.EyeOpen, Dirt = e.Dirt, Droop = e.Droop, Sick = e.Sickness });
            if (e.LookAtFood)
            {
                _avatar.LookAt(_foodPoint);
                if (speed < 0.15f) FaceTowards(_foodPoint);   // đứng yên thì quay cả người về phía chòi ăn
            }
            else if (_stroking) _avatar.LookAt(_hand);   // được vuốt: ngước nhìn theo tay
            else if (_admireT > 0f) _avatar.LookAt(_admirePoint);   // ngước nhìn đèn lồng / nhìn món ăn vặt
            else if (_brain.Activity == PetActivity.Reacting && _cam != null) _avatar.LookAt(_cam.transform.position);
            else _avatar.LookAt(null);
            _fx.SetFlies(e.Flies);
            _fx.SetSleeping(e.Has(PetCondition.Sleeping));
            _fx.SetHealth(e.Sickness, e.Has(PetCondition.Critical), e.Has(PetCondition.Recovering));
            switch (cue)
            {
                case ExpressionCue.Cry:
                    _avatar.PlayCry();
                    _fx.Cry();
                    break;
                case ExpressionCue.Yawn:
                    _avatar.PlayYawn();
                    _fx.Yawn();
                    break;
            }
        }

        /// <summary>Bước chân trên cỏ: đi nhanh thì bước dồn hơn + to hơn; 3 tiếng bước xoay vòng cho khỏi nhàm.</summary>
        private void Footsteps(float dt, float speed)
        {
            if (speed < 0.25f || _brain.Activity == PetActivity.Sleeping)
            {
                _stepT = 0.05f;
                return;
            }
            _stepT -= dt;
            if (_stepT > 0f) return;
            _stepT = Mathf.Clamp(0.55f / speed, 0.15f, 0.42f);
            SoundId s = _step++ % 3 == 0 ? SoundId.Step0 : _step % 3 == 1 ? SoundId.Step1 : SoundId.Step2;
            Sfx.Play(s, Mathf.Lerp(0.14f, 0.3f, speed / 4f), UnityEngine.Random.Range(0.9f, 1.1f));
        }

        /// <summary>Tiếng trong lúc làm việc: nhai tiếp khi đang ăn, sủi nước khi tắm, thở khi ngủ, đấm hình nộm khi tập.</summary>
        private void ActivitySounds(float dt)
        {
            PetActivity a = _brain.Activity;
            _activityT += dt;
            if (a == PetActivity.Eating && _activityT >= 0.8f)
            {
                _activityT = 0f;
                Sfx.PlayVaried(SoundId.Munch, 0.45f);
            }
            if (a == PetActivity.Training && _activityT >= 0.45f)
            {
                _activityT = 0f;
                Sfx.PlayVaried(SoundId.HitLight, 0.3f);
            }
            Sfx.Loop(LoopId.Bath, a == PetActivity.Bathing, 0.45f);
            Sfx.Loop(LoopId.Sleep, a == PetActivity.Sleeping, 0.32f);
        }

        /// <summary>Đang được vuốt: lim dim + dụi đầu, tim bay đều, gừ gừ. Vừa được chạm: mắt híp vui 1 chút.</summary>
        private void UpdatePetting(float dt)
        {
            _tapGlow = Mathf.Max(0f, _tapGlow - dt);
            bool asleep = _brain.Activity == PetActivity.Sleeping;
            _avatar.SetPetted(_stroking ? (asleep ? 0.45f : 1f) : (_tapGlow > 0f ? 0.7f : (IsResting ? 0.3f : 0f)));   // ngồi ghế: lim dim thư thái
            Sfx.Loop(LoopId.Purr, _stroking, asleep ? 0.3f : 0.5f);
            if (!_stroking) return;
            _heartT -= dt;
            if (_heartT > 0f) return;
            _heartT = 0.75f;
            _fx.Hearts(1);
        }

        private static void StopLoops()
        {
            Sfx.Loop(LoopId.Bath, false);
            Sfx.Loop(LoopId.Sleep, false);
            Sfx.Loop(LoopId.Purr, false);
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
        }

        // ---- Lệnh từ input (chỉ chuyển tiếp sang bộ não) ----

        /// <summary>
        /// Chạm vào bé: bé quay lại nhìn, nhảy lên mừng, mắt híp, tim bay + tiếng kêu vui.
        /// Chạm 3 lần liền = cù: cười khúc khích, lắc mình. Ốm: chỉ được lau, không nhảy.
        /// </summary>
        public void Tap()
        {
            _brain.OnTapped();
            if (_brain.Activity != PetActivity.Reacting || IsIll) return;
            float now = Time.time;
            _tapCount = now - _lastTapTime < 1.2f ? _tapCount + 1 : 1;
            _lastTapTime = now;
            _tapGlow = 0.9f;
            if (_tapCount >= 3)
            {
                _tapCount = 0;
                _avatar.PlayBathe();   // lắc mình cười
                _fx.Hearts(3);
                Sfx.PlayVaried(SoundId.Giggle, 0.55f, 0.08f);
                return;
            }
            _avatar.PlayHop();
            _fx.Hearts(1);
            Sfx.PlayVaried(SoundId.Happy, 0.55f, 0.1f);
        }

        /// <summary>Bắt đầu xoa trên người bé (vuốt ve): bé đứng yên tận hưởng.</summary>
        public void BeginStroke()
        {
            if (_stroking) return;
            _stroking = true;
            _heartT = 0.2f;
            _brain.BeginPetting();
        }

        /// <summary>Đang xoa ở điểm `handWorld` (trên người bé), vừa xoa thêm `passes` lượt (mỗi lượt cộng vui).</summary>
        public void StrokeAt(Vector3 handWorld, int passes)
        {
            if (!_stroking) return;
            _hand = handWorld;
            for (int i = 0; i < passes; i++) _brain.Stroke();
        }

        /// <summary>Thả tay sau khi vuốt: bé nhảy lên vui, tim bay, kêu "mi!".</summary>
        public void EndStroke()
        {
            if (!_stroking) return;
            _stroking = false;
            _brain.EndPetting();
            if (_brain.Activity == PetActivity.Sleeping || IsIll) return;
            _avatar.PlayHop();
            _fx.Hearts(2);
            Sfx.PlayVaried(SoundId.Happy, 0.5f, 0.1f);
        }

        public void MoveTo(Vector3 ground) => _brain.CommandMoveTo(ground.ToGround(), transform.position.ToGround());
        public void BeginLead(Vector3 ground) => _brain.BeginLead(ground.ToGround());
        public void LeadTo(Vector3 ground) => _brain.Lead(ground.ToGround());
        public void EndLead(Vector3 ground) => _brain.EndLead(ground.ToGround(), transform.position.ToGround());
        public void GoToZone(ZoneType zone) => _brain.CommandUseZone(zone, transform.position.ToGround());

        /// <summary>Thả đồ ăn trúng thú. Trả true nếu thú chịu ăn.</summary>
        public bool TryFeed()
        {
            if (_care.Feed() != CareResult.Ok)
            {
                Sfx.PlayVaried(SoundId.Wrong, 0.25f);   // lắc đầu "hông ăn đâu"
                return false;
            }
            _brain.OnFed();
            return true;
        }

        public Vector3 MouthPosition => _avatar.MouthPosition;

        // ---- chơi đồ chơi (Phase 8.3): ToyPlayground điều khiển, luật ở PlayService ----

        public bool IsPlaying => _brain.Activity == PetActivity.Playing;
        public bool SelfPlaying => _brain.SelfPlaying;

        /// <summary>
        /// Chạy theo đồ chơi ở `ground` (gọi liên tục khi đồ chơi di chuyển). `owner` = ai đang cho bé chơi
        /// (null = đồ chơi / bộ não; DecorLife = đuổi bướm) để 2 bên không giành nhau điều khiển bé.
        /// </summary>
        public void PlayTo(Vector3 ground, bool selfPlay = false, object owner = null)
        {
            _playOwner = owner;
            _brain.Play(ground.ToGround(), selfPlay);
        }

        /// <summary>Ai đang cho bé chơi (xem PlayTo). null khi bé không chơi hoặc đồ chơi / bộ não cho chơi.</summary>
        public object PlayOwner => IsPlaying ? _playOwner : null;

        public void StopPlay() => _brain.EndPlay();

        /// <summary>Vồ / nhảy đớp: nhảy lên + tiếng "vụt".</summary>
        public void Pounce()
        {
            _avatar.PlayHop();
            Sfx.PlayVaried(SoundId.Pounce, 0.45f);
        }

        /// <summary>Chơi trúng: tim bay + kêu vui.</summary>
        public void Cheer(int hearts)
        {
            _fx.Hearts(hearts);
            _tapGlow = 0.6f;
            Sfx.PlayVaried(SoundId.Happy, 0.45f, 0.12f);
        }

        /// <summary>Không chịu (mệt / ốm...): lắc đầu.</summary>
        public void Refuse() => _avatar.PlayRefuse();

        // ---- đồ trang trí (Phase 8.4): DecorLife điều khiển ----

        public bool IsResting => _brain.Activity == PetActivity.Resting;

        /// <summary>Bé đang rảnh (đứng / đi lang thang, không ngủ, không ốm): bướm ở vòm hoa rủ bé chơi được.</summary>
        public bool IsFree => !_dead && !IsIll && (_brain.Activity == PetActivity.Idle || _brain.Activity == PetActivity.Wander);

        /// <summary>Bé đang đi tới gần `ground` (trong vòng `tolerance` mét) không.</summary>
        public bool IsGoingTo(Vector3 ground, float tolerance) =>
            _brain.IsMoving && NVec2.Distance(_brain.Target, ground.ToGround()) <= tolerance;

        /// <summary>Chỗ ghế gỗ: tâm mặt ghế (thế giới) + hướng ghế nhìn ra. Bé ngồi ghế thì nhảy lên đây, quay mặt ra trước.</summary>
        public void SetBench(Vector3 seat, Vector3 front)
        {
            _benchSeat = seat;
            _benchFront = front;
        }

        /// <summary>Bé ăn món ăn vặt (túi bánh cối xay gió): đứng nhìn món, cúi ăn 1 lúc, tim bay.</summary>
        public void EatSnack(Vector3 food)
        {
            if (_dead) return;
            _admirePoint = food;
            _admireT = 0.8f;
            _brain.OnFed();   // PetController thấy đổi sang Eating: cúi ăn + tiếng nhai
            _fx.Hearts(2);
        }

        /// <summary>Ngước nhìn 1 thứ đẹp (đèn lồng lấp lánh): quay đầu nhìn, nhảy lên, 1 tim. Đang ngủ thì thôi.</summary>
        public void Admire(Vector3 point)
        {
            if (_dead || _brain.Activity == PetActivity.Sleeping) return;
            _admirePoint = point;
            _admireT = 1.8f;
            _avatar.PlayHop();
            _fx.Hearts(1);
            Sfx.PlayVaried(SoundId.Happy, 0.35f, 0.1f);
        }

        /// <summary>Có bóng trong vườn: bé rảnh + chán thì tự tới đẩy bóng chơi (luật `wantsSelfPlay` của PlayService).</summary>
        public void SetSelfPlay(Func<NVec2?> ballRest, Func<double, bool> wantsSelfPlay)
        {
            _brain.BallPosition = ballRest;
            _brain.WantsSelfPlay = wantsSelfPlay;
        }
    }
}
