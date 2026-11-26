using System.Collections.Generic;
using PawFit.Core.Unity;
using PawFit.Pet.View;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Thân 1 bên trong trận (thú hoặc hình nộm): ngả người vung đòn, lao tới khi đòn ra, giật lùi khi trúng đòn,
    /// khiên khi đỡ, choáng khi bị phản đòn, ngã khi bị hạ. Chỉ là hình — luật nằm ở BattleSimulation.
    /// </summary>
    public sealed class FighterRig : MonoBehaviour
    {
        private Transform _body;
        private Transform _shield;
        private Transform[] _arms;
        private IPetAvatar _avatar;
        private Vector3 _home;
        private float _facing;
        private float _windT = -1f, _windDur = 0.3f, _lungeT = -1f, _flinchT = -1f, _flinchPower, _guardT = -1f, _dizzyT = -1f;
        private bool _down;
        private float _downT;

        public IPetAvatar Avatar => _avatar;

        /// <summary>Điểm giữa người (để đặt hiệu ứng).</summary>
        public Vector3 Center => transform.position + Vector3.up * 0.6f;

        public void Init(Transform body, float facing, IPetAvatar avatar, Transform[] arms = null)
        {
            _body = body;
            _facing = facing;
            _avatar = avatar;
            _arms = arms;
            _home = transform.localPosition;
            _shield = Prim.Make(PrimitiveType.Sphere, transform, new Vector3(0.45f * facing, 0.6f, 0f), Vector3.zero,
                                new Color(0.55f, 0.8f, 1f), smoothness: 0.9f, name: "Shield").transform;
        }

        public void WindUp(float seconds)
        {
            _windT = 0f;
            _windDur = Mathf.Max(0.1f, seconds);
        }

        public void Strike(bool heavy)
        {
            _windT = -1f;
            _lungeT = 0f;
            if (heavy) _avatar?.PlayHop();
        }

        public void Flinch(float power)
        {
            _flinchT = 0f;
            _flinchPower = Mathf.Clamp(power, 0.3f, 1.5f);
            _windT = -1f;
            _avatar?.PlayRefuse();
        }

        public void Guard() => _guardT = 0f;

        public void Dizzy() => _dizzyT = 0f;

        public void Knockout()
        {
            _down = true;
            _downT = 0f;
            if (_avatar != null)
            {
                _avatar.SetPose(AvatarPose.Sleep);
                _avatar.SetLook(new AvatarLook { EyeOpen = 0f, Droop = 1f });
            }
        }

        public void Cheer() => _avatar?.PlayCry();

        private void Update()
        {
            if (_body == null) return;
            float dt = Time.deltaTime;
            Vector3 offset = Vector3.zero;
            float lean = 0f;
            if (_windT >= 0f)   // ngả ra sau lấy đà
            {
                _windT += dt / _windDur;
                lean = -Mathf.Clamp01(_windT) * 10f;
                offset.x -= 0.08f * Mathf.Clamp01(_windT) * _facing;
                if (_windT >= 1f) _windT = -1f;
            }
            if (_lungeT >= 0f)  // lao tới rồi lùi về
            {
                _lungeT += dt / 0.28f;
                float t = Mathf.Clamp01(_lungeT);
                offset.x += Mathf.Sin(t * Mathf.PI) * 0.55f * _facing;
                lean += Mathf.Sin(t * Mathf.PI) * 14f;
                if (_lungeT >= 1f) _lungeT = -1f;
            }
            if (_flinchT >= 0f) // giật lùi
            {
                _flinchT += dt / 0.35f;
                float t = Mathf.Clamp01(_flinchT);
                offset.x -= Mathf.Sin(t * Mathf.PI) * 0.25f * _flinchPower * _facing;
                offset.y += Mathf.Sin(t * Mathf.PI) * 0.06f * _flinchPower;
                lean -= Mathf.Sin(t * Mathf.PI) * 12f * _flinchPower;
                if (_flinchT >= 1f) _flinchT = -1f;
            }
            float wobble = 0f;
            if (_dizzyT >= 0f)  // choáng: lắc lư
            {
                _dizzyT += dt / 1f;
                wobble = Mathf.Sin(_dizzyT * 30f) * 8f * (1f - _dizzyT);
                if (_dizzyT >= 1f) _dizzyT = -1f;
            }
            float shield = 0f;
            if (_guardT >= 0f)  // khiên xanh khi đỡ
            {
                _guardT += dt / 0.45f;
                shield = Mathf.Sin(Mathf.Clamp01(_guardT) * Mathf.PI);
                if (_guardT >= 1f) _guardT = -1f;
            }
            _shield.localScale = new Vector3(0.18f, 0.9f, 0.9f) * shield;
            if (_down)
            {
                _downT = Mathf.Min(1f, _downT + dt / 0.6f);
                lean = -80f * _downT;
                offset.y -= 0.15f * _downT;
            }
            transform.localPosition = _home + offset;
            _body.localRotation = Quaternion.Euler(wobble, 0f, lean * _facing * -1f);
            if (_arms != null)   // hình nộm vung tay khi đánh
                for (int i = 0; i < _arms.Length; i++)
                    _arms[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * (30f + Mathf.Max(0f, lean) * 3f));
        }
    }

    /// <summary>Hình nộm bằng gỗ + rơm: màu theo mức (Dễ xanh, Thường vàng, Khó đỏ) giống 3 hình nộm ở sân tập.</summary>
    public static class DummyBuilder
    {
        private static readonly string[] Colors = { "#7BC67B", "#F2C84B", "#E06A5A" };

        public static Transform Build(Transform parent, BotLevel level, out Transform[] arms)
        {
            Transform body = Prim.Group(parent, "DummyBody");
            Color wood = Prim.Hex("#B9865A");
            Color cloth = Prim.Hex(Colors[Mathf.Clamp((int)level, 0, 2)]);
            Prim.Make(PrimitiveType.Cylinder, body, new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.08f, 0.5f), Prim.Hex("#7A5236"));
            Prim.Make(PrimitiveType.Cylinder, body, new Vector3(0f, 0.45f, 0f), new Vector3(0.09f, 0.4f, 0.09f), wood);
            Prim.Make(PrimitiveType.Capsule, body, new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.38f, 0.42f), cloth);
            Prim.Make(PrimitiveType.Sphere, body, new Vector3(0f, 1.5f, 0f), Vector3.one * 0.34f, Prim.Hex("#E8D9C4"));
            // mặt bia: vòng đỏ trắng
            Prim.Make(PrimitiveType.Cylinder, body, new Vector3(0f, 1.5f, -0.16f), new Vector3(0.24f, 0.01f, 0.24f), Prim.Hex("#E2463F"),
                      rotation: Quaternion.Euler(90f, 0f, 0f));
            Prim.Make(PrimitiveType.Cylinder, body, new Vector3(0f, 1.5f, -0.17f), new Vector3(0.14f, 0.01f, 0.14f), Color.white,
                      rotation: Quaternion.Euler(90f, 0f, 0f));
            var list = new List<Transform>();
            foreach (float side in new[] { -1f, 1f })
            {
                Transform arm = Prim.Group(body, "Arm", new Vector3(0.24f * side, 1.1f, 0f));
                Prim.Make(PrimitiveType.Cylinder, arm, new Vector3(0.18f * side, 0f, 0f), new Vector3(0.07f, 0.2f, 0.07f), wood,
                          rotation: Quaternion.Euler(0f, 0f, 90f));
                list.Add(arm);
            }
            arms = list.ToArray();
            return body;
        }
    }

    /// <summary>Hiệu ứng nhỏ trong trận: tia văng, vòng sáng, chữ nổi lên — tự hủy.</summary>
    public sealed class FxBurst : MonoBehaviour
    {
        private readonly List<Transform> _bits = new List<Transform>();
        private readonly List<Vector3> _vel = new List<Vector3>();
        private float _t, _life = 0.6f;
        private Transform _ring;
        private TextMesh _text;
        private Vector3 _textStart;

        public static void Spark(Transform parent, Vector3 at, Color color, int count, float speed = 2.2f)
        {
            var fx = new GameObject("Spark").AddComponent<FxBurst>();
            fx.transform.SetParent(parent, true);
            fx.transform.position = at;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Transform b = Prim.Make(PrimitiveType.Sphere, fx.transform, Vector3.zero, Vector3.one * 0.06f, color, smoothness: 0.9f).transform;
                fx._bits.Add(b);
                fx._vel.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.4f, Mathf.Sin(a * 3f) * 0.4f) * speed);
            }
        }

        public static void Ring(Transform parent, Vector3 at, Color color)
        {
            var fx = new GameObject("Ring").AddComponent<FxBurst>();
            fx.transform.SetParent(parent, true);
            fx.transform.position = at;
            fx._ring = Prim.Make(PrimitiveType.Sphere, fx.transform, Vector3.zero, Vector3.zero, color, smoothness: 0.9f).transform;
            fx._life = 0.4f;
        }

        public static void Popup(Transform parent, Vector3 at, string text, Color color, float height = 0.22f)
        {
            var fx = new GameObject("Popup").AddComponent<FxBurst>();
            fx.transform.SetParent(parent, true);
            fx.transform.position = at;
            fx._text = Garden.View.MemorialView.Text3D(fx.transform, Vector3.zero, text, height, color, FontStyle.Bold);
            fx._textStart = fx._text.transform.localPosition;
            fx._life = 0.9f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            float k = Mathf.Clamp01(_t / _life);
            for (int i = 0; i < _bits.Count; i++)
            {
                _vel[i] += Vector3.up * (-5f * dt);
                _bits[i].localPosition += _vel[i] * dt;
                _bits[i].localScale = Vector3.one * 0.06f * (1f - k);
            }
            if (_ring != null) _ring.localScale = new Vector3(1f, 1f, 0.2f) * (0.3f + 1.2f * k) * (1f - k);
            if (_text != null)
            {
                _text.transform.localPosition = _textStart + Vector3.up * (0.4f * k);
                _text.transform.localScale = Vector3.one * (k < 0.15f ? k / 0.15f : 1f);
            }
            if (_t >= _life) Destroy(gameObject);
        }
    }
}
