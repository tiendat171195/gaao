using PawFit.Core;
using PawFit.Core.Unity;
using PawFit.Garden.View;
using PawFit.Pet;
using PawFit.Pet.View;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Bé khi đi săn (Phase 9.3): hình thú của người chơi (DNA + dáng tiến hóa + phụ kiện) chạy ngang theo Hunter (C# thuần).
    /// Lao tới đánh (nhoài người + vệt chém), khiên xanh khi đỡ, lướt (ngả người + vệt sáng), nhảy đập đất, hào quang khi đầy Nộ.
    /// </summary>
    public sealed class HunterView : MonoBehaviour
    {
        private IPetAvatar _avatar;
        private PetMoodFx _fx;
        private Transform _host, _body, _shield, _aura;
        private Material _shieldMat;
        private float _x, _lunge, _hurt, _t, _perfect;
        private int _facing = 1;
        private bool _dead;

        public Vector3 Center => transform.position + Vector3.up * 0.6f;
        public Vector3 Front => transform.position + new Vector3(_facing * 0.6f, 0.55f, 0f);

        public static HunterView Create(Transform parent, PetService pets, EvolutionService evo)
        {
            var go = new GameObject("Hunter");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<HunterView>();
            v._body = Prim.Group(go.transform, "Body");
            var host = new GameObject("Pet");
            host.transform.SetParent(v._body, false);
            v._host = host.transform;
            v._avatar = PetAvatarFactory.Create(host, pets.Current.Dna, PetController.UsePlaceholder);
            v._avatar.SetGrowth(evo.Look);
            AccessoryView.Attach(v._avatar, pets.Cosmetics?.Equipped ?? AccessoryKind.None);   // đi săn vẫn đeo phụ kiện
            v._fx = host.AddComponent<PetMoodFx>();
            v._fx.Init(v._avatar);
            // khiên khi đỡ: đĩa xanh dựng đứng trước mặt
            v._shield = Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0.55f, 0.6f, 0f), new Vector3(1.1f, 0.03f, 1.1f), Prim.Hex("#7FD3FF"),
                                  smoothness: 0.95f, rotation: Quaternion.Euler(0f, 0f, 90f)).transform;
            Renderer sr = v._shield.GetComponent<Renderer>();
            v._shieldMat = new Material(sr.sharedMaterial);
            sr.sharedMaterial = v._shieldMat;
            v._shield.gameObject.SetActive(false);
            // hào quang đầy Nộ: vòng vàng dưới chân
            v._aura = Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.005f, 0.8f), Prim.Hex("#FFB347"),
                                smoothness: 0.9f).transform;
            v._aura.gameObject.SetActive(false);
            return v;
        }

        public void OnStrike() => _lunge = 0.16f;
        public void OnHurt() => _hurt = 0.25f;
        public void OnSlam() => _avatar.PlayHop();
        public void OnPerfectGuard() => _perfect = 0.3f;

        public void OnUltimate()
        {
            _avatar.PlayHop();
            _fx.Hearts(2);
        }

        public void OnWin()
        {
            _avatar.PlayHop();
            _avatar.PlayCry();
            _fx.Hearts(3);
        }

        public void OnDead()
        {
            _dead = true;
            _avatar.SetPose(AvatarPose.Sleep);
            _avatar.SetMoveSpeed(0f);
        }

        public void Tick(Hunter h, float dt, bool rageFull)
        {
            _t += dt;
            float prev = _x;
            _x = Mathf.Lerp(_x, h.X, 1f - Mathf.Exp(-dt * 22f));
            float v = dt > 0f ? Mathf.Abs(_x - prev) / dt : 0f;
            transform.localPosition = new Vector3(_x, 0f, 0f);
            if (h.Facing != 0) _facing = h.Facing;
            Quaternion want = Quaternion.Euler(0f, _facing > 0 ? 65f : -65f, 0f);   // nhìn ngang, hơi quay về camera như màn đấu
            _host.localRotation = Quaternion.Slerp(_host.localRotation, want, 1f - Mathf.Exp(-dt * 14f));
            if (_dead) return;

            // nhoài người khi đánh, ngả người khi lướt, giật lùi khi bị đánh
            _lunge = Mathf.Max(0f, _lunge - dt);
            _hurt = Mathf.Max(0f, _hurt - dt);
            float lunge = _lunge > 0f ? Mathf.Sin(_lunge / 0.16f * Mathf.PI) * 0.28f : 0f;
            float knock = _hurt > 0f ? -Mathf.Sin(_hurt / 0.25f * Mathf.PI) * 0.2f : 0f;
            _body.localPosition = new Vector3(_facing * (lunge + knock), 0f, 0f);
            float lean = h.Action == HunterAction.Dash ? -18f : 0f;
            _body.localRotation = Quaternion.Euler(0f, 0f, _facing * lean);

            bool guard = h.Action == HunterAction.Guard;
            _shield.gameObject.SetActive(guard || _perfect > 0f);
            if (_shield.gameObject.activeSelf)
            {
                _perfect = Mathf.Max(0f, _perfect - dt);
                _shield.localPosition = new Vector3(_facing * 0.6f, 0.6f, 0f);
                float s = _perfect > 0f ? 1.25f : 1f + 0.05f * Mathf.Sin(_t * 20f);
                _shield.localScale = new Vector3(1.1f, 0.03f, 1.1f) * s;
                _shieldMat.color = _perfect > 0f ? Prim.Hex("#FFE066") : Prim.Hex("#7FD3FF");
            }

            _aura.gameObject.SetActive(rageFull);
            if (rageFull) _aura.localScale = new Vector3(1.4f, 0.005f, 0.8f) * (1f + 0.08f * Mathf.Sin(_t * 8f));

            float speed = h.Action == HunterAction.Dash ? 6f : v;
            _avatar.SetMoveSpeed(speed);
            _avatar.SetPose(speed > 2.4f ? AvatarPose.Run : speed > 0.15f ? AvatarPose.Walk : AvatarPose.Idle);
        }

        private void OnDestroy()
        {
            if (_shieldMat != null) Destroy(_shieldMat);
        }
    }
}
