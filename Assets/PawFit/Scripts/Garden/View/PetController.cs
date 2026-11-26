using System;
using PawFit.Core;
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
        private GardenConfig _cfg;
        private NavMeshAgent _agent;
        private IPetAvatar _avatar;
        private PetMoodFx _fx;
        private Camera _cam;
        private Vector3 _foodPoint;
        private IDisposable _careSub, _createdSub;
        private NVec2 _sentTarget = NoTarget;
        private PetActivity _lastActivity = (PetActivity)(-1);

        public IPetAvatar Avatar => _avatar;

        public static PetController Spawn(Vector3 position, PetDna dna, PetBrain brain, CareActionService care,
                                          PetExpressionService expr, GardenLayout layout, GardenConfig cfg, EventBus bus,
                                          Transform parent)
        {
            var go = new GameObject("Pet");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));   // nhìn về phía camera
            var col = go.AddComponent<CapsuleCollider>();   // to hơn hình 1 chút cho dễ chạm trúng
            col.direction = 2;
            col.center = new Vector3(0f, 0.5f, 0.1f);
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
            pc._cfg = cfg;
            pc._agent = agent;
            pc._cam = Camera.main;
            GardenZone hut = layout.Get(ZoneType.FoodHut);
            // điểm nhìn khi đòi ăn = khay đồ ăn (cùng chỗ GardenBuilder đặt khay)
            pc._foodPoint = hut != null ? NVec2.Lerp(hut.UsePoint, hut.Center, 0.55f).ToWorld(0.5f) : position;
            pc._fx = go.AddComponent<PetMoodFx>();
            pc.RebuildAvatar(dna);
            // bị từ chối (no rồi, chưa buồn ngủ...) thì lắc đầu
            pc._careSub = bus.Subscribe<CareDone>(e =>
            {
                if (e.Result == CareResult.Refused) pc._avatar.PlayRefuse();
            });
            // có thú mới (Debug overlay / sau này: trứng nở) thì dựng lại hình
            pc._createdSub = bus.Subscribe<PetCreated>(e => pc.RebuildAvatar(e.Pet.Dna));
            return pc;
        }

        /// <summary>Dựng lại hình thú theo DNA: ưu tiên model Gen 1, không có thì dùng thú tạm.</summary>
        public void RebuildAvatar(PetDna dna)
        {
            if (_avatar is MonoBehaviour old) Destroy(old);   // OnDestroy của avatar tự xóa phần hình
            _avatar = null;
            if (!UsePlaceholder && Gen1PetAvatar.KitAvailable)
            {
                var gen1 = gameObject.AddComponent<Gen1PetAvatar>();
                gen1.Build(dna);
                if (gen1.IsBuilt) _avatar = gen1;
                else Destroy(gen1);
            }
            if (_avatar == null)
            {
                var placeholder = gameObject.AddComponent<PlaceholderPetAvatar>();
                placeholder.Build(dna);
                _avatar = placeholder;
            }
            _fx.Init(_avatar);
            _lastActivity = (PetActivity)(-1);
        }

        private void OnDestroy()
        {
            _careSub?.Dispose();
            _createdSub?.Dispose();
        }

        private void Update()
        {
            if (_brain == null || _avatar == null || !_agent.isOnNavMesh) return;
            float dt = Time.deltaTime;
            _brain.Update(dt, transform.position.ToGround());

            if (_brain.IsMoving)
            {
                _agent.isStopped = false;
                _agent.speed = _brain.Activity == PetActivity.Led ? _cfg.LeadSpeed : (_brain.Run ? _cfg.RunSpeed : _cfg.WalkSpeed);
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

            if (_brain.Activity == PetActivity.Reacting) FaceTowards(_cam != null ? _cam.transform.position : transform.position);
            if (_brain.Activity != _lastActivity)
            {
                if (_brain.Activity == PetActivity.Eating) _avatar.PlayEat();
                else if (_brain.Activity == PetActivity.Bathing) _avatar.PlayBathe();
                _lastActivity = _brain.Activity;
            }
            float v = _agent.velocity.magnitude;
            _avatar.SetMoveSpeed(v);
            _avatar.SetPose(_brain.Activity == PetActivity.Sleeping ? AvatarPose.Sleep
                            : v > 2.4f ? AvatarPose.Run : v > 0.15f ? AvatarPose.Walk : AvatarPose.Idle);
            UpdateExpression(dt, v);
        }

        /// <summary>Biểu cảm theo nhu cầu: mắt díp, bẩn có ruồi, đói thì nhìn chòi ăn + kêu, ngủ có Zzz.</summary>
        private void UpdateExpression(float dt, float speed)
        {
            if (_expr == null) return;
            ExpressionCue cue = _expr.Update(dt);
            PetExpression e = _expr.Current;
            _avatar.SetLook(new AvatarLook { EyeOpen = e.EyeOpen, Dirt = e.Dirt, Droop = e.Droop });
            if (e.LookAtFood)
            {
                _avatar.LookAt(_foodPoint);
                if (speed < 0.15f) FaceTowards(_foodPoint);   // đứng yên thì quay cả người về phía chòi ăn
            }
            else if (_brain.Activity == PetActivity.Reacting && _cam != null) _avatar.LookAt(_cam.transform.position);
            else _avatar.LookAt(null);
            _fx.SetFlies(e.Flies);
            _fx.SetSleeping(e.Has(PetCondition.Sleeping));
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

        private void FaceTowards(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
        }

        // ---- Lệnh từ input (chỉ chuyển tiếp sang bộ não) ----

        public void Tap()
        {
            _brain.OnTapped();
            if (_brain.Activity == PetActivity.Reacting) _avatar.PlayHop();
        }

        public void MoveTo(Vector3 ground) => _brain.CommandMoveTo(ground.ToGround(), transform.position.ToGround());
        public void BeginLead(Vector3 ground) => _brain.BeginLead(ground.ToGround());
        public void LeadTo(Vector3 ground) => _brain.Lead(ground.ToGround());
        public void EndLead(Vector3 ground) => _brain.EndLead(ground.ToGround(), transform.position.ToGround());
        public void GoToZone(ZoneType zone) => _brain.CommandUseZone(zone, transform.position.ToGround());

        /// <summary>Thả đồ ăn trúng thú. Trả true nếu thú chịu ăn.</summary>
        public bool TryFeed()
        {
            if (_care.Feed() != CareResult.Ok) return false;
            _brain.OnFed();
            return true;
        }

        public Vector3 MouthPosition => _avatar.MouthPosition;
    }
}
