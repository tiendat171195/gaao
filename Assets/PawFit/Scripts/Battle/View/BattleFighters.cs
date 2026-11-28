using System.Collections.Generic;
using PawFit.Core.Unity;
using PawFit.Pet.View;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Thân 1 bên trong trận (thú hoặc hình nộm). Vuốt xong là chuyển động NGAY: nhún lấy đà rồi lao / nhảy / xoay tới,
    /// tới sát đối thủ đúng lúc đòn trúng (hết thời gian vung đòn), rồi lùi về. Giật lùi khi trúng đòn, khiên khi đỡ,
    /// choáng khi bị phản đòn / cắt đòn, vầng Nộ dưới chân khi đầy Nộ, ngã khi bị hạ. Chỉ là hình — luật nằm ở BattleSimulation.
    /// </summary>
    public sealed class FighterRig : MonoBehaviour
    {
        private const float RecoverSeconds = 0.22f;

        private Transform _body;
        private Vector3 _bodyScale = Vector3.one;
        private Transform _shield;
        private Transform _aura;
        private Transform[] _arms;
        private IPetAvatar _avatar;
        private Vector3 _home;
        private float _facing;

        // ra đòn: chạy theo thời gian vung đòn, tới sát đối thủ đúng lúc đòn trúng
        private MoveKind _atkMove;
        private float _atkT = -1f, _atkDur = 0.3f, _holdT;
        private Vector3 _atkOffset;
        private float _atkLean, _atkYaw;

        // lùi về sau khi đòn trúng / bị cắt
        private Vector3 _recFrom;
        private float _recT = -1f, _recLean, _recYaw;

        private float _lungeT = -1f, _flinchT = -1f, _flinchPower, _guardT = -1f, _dizzyT = -1f;

        /// <summary>&gt; 0: bẹp xuống (lấy đà / trúng đòn), &lt; 0: vươn dài (lao tới); tự về 0.</summary>
        private float _squash;

        private bool _down, _rage;
        private float _downT, _auraK;

        public IPetAvatar Avatar => _avatar;

        /// <summary>Điểm giữa người (để đặt hiệu ứng).</summary>
        public Vector3 Center => transform.position + Vector3.up * 0.6f;

        /// <summary>
        /// Tốc độ trận so với giờ thật (Mode Huấn luyện chạy 0,75×, slow-mo 0,3×): động tác ra đòn chạy theo
        /// để tới sát đối thủ đúng lúc luật tính đòn trúng.
        /// </summary>
        public float Speed { get; set; } = 1f;

        public void Init(Transform body, float facing, IPetAvatar avatar, Transform[] arms = null)
        {
            _body = body;
            _bodyScale = body.localScale;   // cỡ gốc (trùm Thám hiểm to hơn): nhún / vươn nhân trên cỡ này
            _facing = facing;
            _avatar = avatar;
            _arms = arms;
            _home = transform.localPosition;
            _shield = Prim.Make(PrimitiveType.Sphere, transform, new Vector3(0.45f * facing, 0.6f, 0f), Vector3.zero,
                                new Color(0.55f, 0.8f, 1f), smoothness: 0.9f, name: "Shield").transform;
            _aura = Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.015f, 0f), Vector3.zero,
                              new Color(1f, 0.62f, 0.2f), smoothness: 0.9f, name: "RageAura").transform;
        }

        /// <summary>Bắt đầu ra đòn: thấy chuyển động ngay frame này, chạm đối thủ sau `windup` giây.</summary>
        public void Attack(MoveKind move, float windup)
        {
            _atkMove = move;
            _atkDur = Mathf.Max(0.08f, windup);
            _atkT = 0f;
            _holdT = 0f;
            _recT = -1f;
            _squash = move == MoveKind.Quick ? 0.14f : 0.22f;   // nhún lấy đà
            if (move == MoveKind.Ultimate) _avatar?.PlayCry();
        }

        /// <summary>PvP qua máy chủ: vừa vuốt xong là nhún ngay trên máy mình, trong lúc chờ máy chủ chốt nét (đỡ cảm giác trễ mạng).</summary>
        public void Anticipate()
        {
            if (_atkT < 0f) _squash = Mathf.Max(_squash, 0.1f);
        }

        /// <summary>Đòn vừa chạm (trúng / bị đỡ): lùi về. Không đang ra đòn (phản đòn) thì lao nhanh tới rồi về.</summary>
        public void Strike(bool heavy)
        {
            if (_atkT >= 0f)
            {
                BeginRecover();
                _squash = -0.1f;
                return;
            }
            _lungeT = 0f;
            if (heavy) _avatar?.PlayHop();
        }

        /// <summary>Đòn bị cắt ngang / bị phản đòn: thôi lao, lùi về.</summary>
        public void CancelAttack()
        {
            if (_atkT >= 0f) BeginRecover();
        }

        /// <summary>Giật lùi khi trúng đòn. Không hủy đòn đang ra (đòn Nhanh không cắt được đòn của mình — luật quyết định qua Interrupted).</summary>
        public void Flinch(float power)
        {
            _flinchT = 0f;
            _flinchPower = Mathf.Clamp(power, 0.3f, 1.5f);
            _squash = 0.12f * _flinchPower;
            _avatar?.PlayRefuse();
        }

        public void Guard() => _guardT = 0f;

        public void Dizzy() => _dizzyT = 0f;

        /// <summary>Đầy Nộ: vầng cam dưới chân phập phồng.</summary>
        public void SetRageAura(bool on) => _rage = on;

        public void Knockout()
        {
            _down = true;
            _downT = 0f;
            _rage = false;
            CancelAttack();
            if (_avatar != null)
            {
                _avatar.SetPose(AvatarPose.Sleep);
                _avatar.SetLook(new AvatarLook { EyeOpen = 0f, Droop = 1f });
            }
        }

        public void Cheer() => _avatar?.PlayCry();

        private void BeginRecover()
        {
            _recFrom = _atkOffset;
            _recLean = _atkLean;
            _recYaw = _atkYaw;
            _recT = 0f;
            _atkT = -1f;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);

        /// <summary>Dáng ra đòn ở tiến độ p (0..1) của thời gian vung đòn; p = 1 là lúc chạm đối thủ.</summary>
        private void AttackPose(float p, out Vector3 off, out float lean, out float yaw)
        {
            float f = _facing;
            yaw = 0f;
            switch (_atkMove)
            {
                case MoveKind.Quick:   // phóng thẳng tới
                    off = new Vector3(0.75f * EaseOut(p) * f, 0f, 0f);
                    lean = 16f * p;
                    break;
                case MoveKind.Heavy:   // nhún xuống rồi bật nhảy tới
                    if (p < 0.4f)
                    {
                        float c = p / 0.4f;
                        off = new Vector3(-0.1f * c * f, -0.06f * c, 0f);
                        lean = -10f * c;
                    }
                    else
                    {
                        float q = (p - 0.4f) / 0.6f;
                        off = new Vector3((-0.1f + 0.95f * EaseOut(q)) * f, Mathf.Sin(q * Mathf.PI) * 0.38f, 0f);
                        lean = 22f * q;
                    }
                    break;
                case MoveKind.Special:   // vừa xoay 1 vòng vừa lao tới
                    off = new Vector3(0.8f * EaseOut(p) * f, Mathf.Sin(p * Mathf.PI) * 0.12f, 0f);
                    lean = 10f * p;
                    yaw = 360f * EaseInOut(p);
                    break;
                default:   // Tuyệt chiêu: bay lên tụ lực (rung) rồi lao bổ xuống đối thủ
                    if (p < 0.6f)
                    {
                        float c = p / 0.6f;
                        off = new Vector3((Mathf.Sin(Time.time * 70f) * 0.025f - 0.08f * c) * f, 0.32f * EaseOut(c), 0f);
                        lean = -12f * c;
                    }
                    else
                    {
                        float q = (p - 0.6f) / 0.4f;
                        off = new Vector3((-0.08f + 1.03f * q * q) * f, 0.32f * (1f - q), 0f);
                        lean = 25f * q;
                    }
                    break;
            }
        }

        private void Update()
        {
            if (_body == null) return;
            float dt = Time.deltaTime;
            Vector3 offset = Vector3.zero;
            float lean = 0f, yaw = 0f;
            if (_atkT >= 0f)
            {
                _atkT += dt * Mathf.Max(0.05f, Speed) / _atkDur;
                AttackPose(Mathf.Clamp01(_atkT), out _atkOffset, out _atkLean, out _atkYaw);
                if (_atkT >= 1f && (_holdT += dt) > 0.12f) BeginRecover();   // không có sự kiện trúng (hết trận...): tự lùi về
                else
                {
                    offset += _atkOffset;
                    lean += _atkLean;
                    yaw += _atkYaw;
                }
            }
            if (_recT >= 0f)
            {
                _recT += dt / RecoverSeconds;
                float r = EaseOut(Mathf.Clamp01(_recT));
                offset += Vector3.Lerp(_recFrom, Vector3.zero, r);
                lean += Mathf.Lerp(_recLean, 0f, r);
                yaw += Mathf.LerpAngle(_recYaw, 0f, r);
                if (_recT >= 1f) _recT = -1f;
            }
            if (_lungeT >= 0f)  // phản đòn: lao tới rồi lùi về
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
            _auraK = Mathf.MoveTowards(_auraK, _rage ? 1f : 0f, dt * 4f);   // vầng Nộ hiện / tắt dần
            float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.06f;
            _aura.localScale = new Vector3(1.15f * pulse, 0.008f, 0.85f * pulse) * _auraK;
            if (_down)
            {
                _downT = Mathf.Min(1f, _downT + dt / 0.6f);
                lean = -80f * _downT;
                offset.y -= 0.15f * _downT;
            }
            _squash *= Mathf.Exp(-dt * 12f);
            if (_atkT >= 0f && _atkT > 0.3f) _squash = Mathf.Min(_squash, -0.06f);   // đang lao: vươn dài ra
            _body.localScale = Vector3.Scale(_bodyScale, new Vector3(1f + _squash * 0.5f, 1f - _squash, 1f + _squash * 0.5f));
            transform.localPosition = _home + offset;
            _body.localRotation = Quaternion.Euler(wobble, yaw, lean * _facing * -1f);
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

    /// <summary>Hiệu ứng nhỏ trong trận: tia văng, vòng sáng, vệt chém, chữ nổi lên — tự hủy.</summary>
    public sealed class FxBurst : MonoBehaviour
    {
        private readonly List<Transform> _bits = new List<Transform>();
        private readonly List<Vector3> _vel = new List<Vector3>();
        private float _t, _life = 0.6f;
        private Transform _ring;
        private Transform _slash;
        private float _slashLen, _slashWidth;
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

        /// <summary>Vệt chém: vạch sáng vụt dài ra rồi mảnh dần (nghiêng `angleDeg` độ trên mặt màn hình).</summary>
        public static void Slash(Transform parent, Vector3 at, Color color, float angleDeg, float length = 0.9f, float width = 0.07f)
        {
            var fx = new GameObject("Slash").AddComponent<FxBurst>();
            fx.transform.SetParent(parent, true);
            fx.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angleDeg));
            fx._slash = Prim.Make(PrimitiveType.Cube, fx.transform, Vector3.zero, Vector3.zero, color, smoothness: 0.9f).transform;
            fx._slashLen = length;
            fx._slashWidth = width;
            fx._life = 0.24f;
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
            if (_slash != null)
            {
                float grow = Mathf.Clamp01(k / 0.35f), thin = 1f - Mathf.Clamp01((k - 0.35f) / 0.65f);
                _slash.localScale = new Vector3(_slashLen * grow, _slashWidth * thin, _slashWidth * thin);
            }
            if (_text != null)
            {
                _text.transform.localPosition = _textStart + Vector3.up * (0.4f * k);
                _text.transform.localScale = Vector3.one * (k < 0.15f ? k / 0.15f : 1f);
            }
            if (_t >= _life) Destroy(gameObject);
        }
    }
}
