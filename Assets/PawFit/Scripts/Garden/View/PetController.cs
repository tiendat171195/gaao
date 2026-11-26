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
    /// Nối "bộ não" PetBrain (C# thuần) với Unity: đặt đích cho NavMeshAgent và chạy hiệu ứng của IPetAvatar.
    /// Không chứa luật game — luật nằm ở PetBrain / CareActionService.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PetController : MonoBehaviour
    {
        private static readonly NVec2 NoTarget = new NVec2(float.NaN, float.NaN);

        private PetBrain _brain;
        private CareActionService _care;
        private GardenConfig _cfg;
        private NavMeshAgent _agent;
        private IPetAvatar _avatar;
        private Camera _cam;
        private IDisposable _careSub;
        private NVec2 _sentTarget = NoTarget;
        private PetActivity _lastActivity = (PetActivity)(-1);

        public static PetController Spawn(Vector3 position, PetDna dna, PetBrain brain, CareActionService care,
                                          GardenConfig cfg, EventBus bus, Transform parent)
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
            var avatar = go.AddComponent<PlaceholderPetAvatar>();
            avatar.Build(dna);
            var pc = go.AddComponent<PetController>();
            pc._brain = brain;
            pc._care = care;
            pc._cfg = cfg;
            pc._agent = agent;
            pc._avatar = avatar;
            pc._cam = Camera.main;
            // bị từ chối (no rồi, chưa buồn ngủ...) thì lắc đầu
            pc._careSub = bus.Subscribe<CareDone>(e =>
            {
                if (e.Result == CareResult.Refused) pc._avatar.PlayRefuse();
            });
            return pc;
        }

        private void OnDestroy() => _careSub?.Dispose();

        private void Update()
        {
            if (_brain == null || !_agent.isOnNavMesh) return;
            _brain.Update(Time.deltaTime, transform.position.ToGround());

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

            if (_brain.Activity == PetActivity.Reacting) FaceCamera();
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
        }

        private void FaceCamera()
        {
            if (_cam == null) return;
            Vector3 d = _cam.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 8f);
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
