using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Pet.View
{
    /// <summary>Tư thế lặp lại của thú.</summary>
    public enum AvatarPose { Idle, Walk, Run, Sleep }

    /// <summary>Biểu cảm theo nhu cầu (Phase 1.3) — View tự vẽ, gameplay chỉ đưa số.</summary>
    public struct AvatarLook
    {
        /// <summary>1 = mắt mở to, 0 = nhắm.</summary>
        public float EyeOpen;

        /// <summary>0 = sạch, 1 = rất bẩn.</summary>
        public float Dirt;

        /// <summary>0 = tai / đuôi bình thường, 1 = cụp hẳn.</summary>
        public float Droop;

        public static AvatarLook Normal => new AvatarLook { EyeOpen = 1f };
    }

    /// <summary>
    /// Phần HÌNH của thú. Gameplay chỉ gọi interface này nên đổi model (thú tạm → Gen 1 → Gen 2)
    /// chỉ cần viết 1 lớp mới implement IPetAvatar, không sửa gameplay.
    /// </summary>
    public interface IPetAvatar
    {
        void Build(PetDna dna);
        void SetPose(AvatarPose pose);
        void SetMoveSpeed(float metersPerSecond);
        void PlayHop();
        void PlayEat();
        void PlayBathe();
        void PlayRefuse();

        /// <summary>Kêu (đòi ăn): ngẩng đầu, há miệng.</summary>
        void PlayCry();

        /// <summary>Ngáp (buồn ngủ).</summary>
        void PlayYawn();

        void SetLook(AvatarLook look);

        /// <summary>Quay đầu nhìn 1 điểm trong thế giới; null = nhìn thẳng.</summary>
        void LookAt(Vector3? worldPoint);

        /// <summary>Vị trí miệng (đồ ăn bay vào đây).</summary>
        Vector3 MouthPosition { get; }

        /// <summary>Đỉnh đầu (chỗ hiện bong bóng / Zzz).</summary>
        Vector3 HeadTop { get; }
    }

    /// <summary>Màu thú suy ra từ DNA — dùng chung cho thú tạm và model Gen 1.</summary>
    public static class DnaVisuals
    {
        // bảng 12 màu mắt (chỉ số = gene EyeColor)
        private static readonly string[] EyeHex =
        {
            "#2FB3C2", "#E9B23B", "#58A6E6", "#EA4450", "#4CC38A", "#A66BFF",
            "#F28C28", "#F06AA8", "#2E6BD8", "#F4C430", "#8FD3C0", "#8A5A3B",
        };

        public static Color Fur(PetDna d) =>
            Color.HSVToRGB(d.FurHue / 64f, 0.12f + d.FurSat / 7f * 0.68f, 0.35f + d.FurVal / 7f * 0.6f);

        public static Color Belly(PetDna d) => Color.Lerp(Fur(d), Color.white, 0.15f + d.BellyTone / 3f * 0.65f);

        public static Color Points(PetDna d) =>
            Color.Lerp(Fur(d), new Color(0.12f, 0.08f, 0.07f), 0.3f + d.PawsDark / 3f * 0.5f);

        public static Color Eye(PetDna d) => Prim.Hex(EyeHex[d.EyeColor % EyeHex.Length]);

        /// <summary>Màu hoa văn (sọc / đốm): sẫm hơn lông, lệch màu theo gene PatternHue.</summary>
        public static Color Pattern(PetDna d)
        {
            float h = d.FurHue / 64f + (d.PatternHue - 3.5f) / 7f * 0.12f;
            h -= Mathf.Floor(h);
            float s = Mathf.Clamp01(0.25f + d.FurSat / 7f * 0.6f);
            float v = 0.12f + d.FurVal / 7f * 0.3f;
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>Chóp tai: lông pha màu chân theo gene EarTipDark.</summary>
        public static Color EarTip(PetDna d) => Color.Lerp(Fur(d), Points(d), d.EarTipDark / 3f);

        /// <summary>Mặt trong tai: hồng nhạt pha 1 chút màu lông.</summary>
        public static Color EarInner(PetDna d) => Color.Lerp(Prim.Hex("#F2B3AC"), Fur(d), 0.18f);
    }

    /// <summary>
    /// Thú TẠM dựng bằng khối cơ bản (Phase 1.2): thân, đầu, mắt, tai / sừng / đuôi theo DNA, màu theo DNA,
    /// hiệu ứng bằng code (nhún bước chân, nhảy, ăn, tắm, lắc đầu, ngủ). Phase 1.3 thay bằng model Gen 1.
    /// Mặt thú nhìn về +Z (hướng tiến của Unity).
    /// </summary>
    public sealed class PlaceholderPetAvatar : MonoBehaviour, IPetAvatar
    {
        private Transform _visual;
        private Transform _head;
        private Transform[] _legs;
        private Transform[] _eyes;
        private Vector3 _eyeScale;
        private AvatarPose _pose;
        private float _speed;
        private float _phase;
        private float _hopT = -1f, _eatT = -1f, _batheT = -1f, _refuseT = -1f, _cryT = -1f, _yawnT = -1f;
        private AvatarLook _look = AvatarLook.Normal;
        private Vector3? _lookAt;
        private float _lookYaw;

        public Vector3 MouthPosition => _head != null ? _head.TransformPoint(new Vector3(0f, -0.15f, 0.45f)) : transform.position;
        public Vector3 HeadTop => _head != null ? _head.position + Vector3.up * 0.45f : transform.position + Vector3.up * 1.2f;

        public void Build(PetDna dna)
        {
            if (_visual != null) Destroy(_visual.gameObject);
            Color fur = DnaVisuals.Fur(dna), belly = DnaVisuals.Belly(dna), points = DnaVisuals.Points(dna);
            Color dark = new Color(0.08f, 0.06f, 0.07f);
            _visual = Prim.Group(transform, "Visual");

            // thân nằm dọc trục Z, to / nhỏ theo gene Chubby
            float chubby = 1f + dna.Chubby / 15f * 0.25f;
            float legLen = 0.14f + dna.LegLength / 15f * 0.1f;
            float bodyY = legLen + 0.24f;
            Prim.Make(PrimitiveType.Capsule, _visual, new Vector3(0f, bodyY, 0f), new Vector3(0.5f * chubby, 0.42f, 0.5f * chubby),
                      fur, rotation: Quaternion.Euler(90f, 0f, 0f), name: "Body");
            Prim.Make(PrimitiveType.Sphere, _visual, new Vector3(0f, bodyY - 0.06f, 0.12f), new Vector3(0.36f, 0.3f, 0.4f) * chubby, belly, name: "Belly");

            // 4 chân (xoay quanh khớp hông khi bước)
            _legs = new Transform[4];
            int i = 0;
            foreach (float z in new[] { 0.22f, -0.22f })
            foreach (float x in new[] { 0.13f, -0.13f })
            {
                Transform hip = Prim.Group(_visual, "Leg", new Vector3(x * chubby, legLen + 0.05f, z));
                Prim.Make(PrimitiveType.Cylinder, hip, new Vector3(0f, -legLen * 0.5f - 0.02f, 0f), new Vector3(0.11f, legLen * 0.5f + 0.03f, 0.11f), points);
                _legs[i++] = hip;
            }

            // đầu (to / nhỏ theo gene HeadSize)
            float hs = 0.5f + dna.HeadSize / 15f * 0.14f;
            _head = Prim.Group(_visual, "Head", new Vector3(0f, bodyY + 0.3f, 0.34f));
            Prim.Make(PrimitiveType.Sphere, _head, Vector3.zero, Vector3.one * hs, fur);
            Prim.Make(PrimitiveType.Sphere, _head, new Vector3(0f, -0.07f, 0.2f), new Vector3(0.22f, 0.17f, 0.17f), belly, name: "Muzzle");
            Prim.Make(PrimitiveType.Sphere, _head, new Vector3(0f, -0.03f, 0.29f), Vector3.one * 0.055f, dark, smoothness: 0.6f, name: "Nose");
            _eyeScale = new Vector3(0.1f, 0.1f, 0.06f);
            _eyes = new Transform[2];
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? 1f : -1f;
                Transform eye = Prim.Make(PrimitiveType.Sphere, _head, new Vector3(0.1f * side, 0.06f, 0.21f), _eyeScale,
                                          DnaVisuals.Eye(dna), smoothness: 0.85f, name: "Eye").transform;
                Prim.Make(PrimitiveType.Sphere, eye, new Vector3(0f, 0f, 0.45f), new Vector3(0.55f, 0.6f, 0.5f), dark, smoothness: 0.9f, name: "Pupil");
                _eyes[s] = eye;
                BuildEar(dna, side, hs, fur, points);
            }
            BuildHorns(dna, hs);
            BuildTail(dna, bodyY, fur, points);
        }

        private void BuildEar(PetDna dna, float side, float hs, Color fur, Color points)
        {
            Color tip = Color.Lerp(fur, points, dna.EarTipDark / 3f);
            Vector3 pos = new Vector3(0.14f * side, hs * 0.42f, -0.02f);
            switch (dna.Ear)
            {
                case EarType.Pointed:
                    Prim.Make(PrimitiveType.Cube, _head, pos, new Vector3(0.13f, 0.13f, 0.04f), tip, rotation: Quaternion.Euler(0f, 0f, 45f + 10f * side));
                    break;
                case EarType.Round:
                    Prim.Make(PrimitiveType.Sphere, _head, pos, new Vector3(0.15f, 0.15f, 0.05f), tip);
                    break;
                case EarType.Folded:
                    Prim.Make(PrimitiveType.Cube, _head, pos + new Vector3(0f, -0.03f, 0.05f), new Vector3(0.13f, 0.1f, 0.04f), tip,
                              rotation: Quaternion.Euler(-55f, 0f, 45f));
                    break;
                default: // Fin
                    Prim.Make(PrimitiveType.Cube, _head, pos + new Vector3(0.04f * side, -0.04f, -0.04f), new Vector3(0.05f, 0.12f, 0.2f), tip,
                              rotation: Quaternion.Euler(-30f, 0f, -20f * side));
                    break;
            }
        }

        private void BuildHorns(PetDna dna, float hs)
        {
            if (dna.HornCount <= 0) return;
            Color horn = new Color(0.93f, 0.88f, 0.78f);
            float len = dna.Horn == HornShape.Long ? 0.16f : 0.1f;
            float tilt = dna.Horn == HornShape.Curved ? -35f : -8f;
            float[] xs = dna.HornCount == 1 ? new[] { 0f } : dna.HornCount == 2 ? new[] { -0.08f, 0.08f } : new[] { -0.1f, 0f, 0.1f };
            foreach (float x in xs)
                Prim.Make(PrimitiveType.Capsule, _head, new Vector3(x, hs * 0.48f, 0.08f), new Vector3(0.05f, len * 0.5f, 0.05f), horn,
                          smoothness: 0.5f, rotation: Quaternion.Euler(tilt, 0f, x * -150f));
        }

        private void BuildTail(PetDna dna, float bodyY, Color fur, Color points)
        {
            Transform tail = Prim.Group(_visual, "Tail", new Vector3(0f, bodyY + 0.05f, -0.42f));
            switch (dna.Tail)
            {
                case TailType.Puff:
                    Prim.Make(PrimitiveType.Sphere, tail, Vector3.zero, Vector3.one * 0.2f, Color.Lerp(fur, Color.white, 0.4f));
                    break;
                case TailType.Fox:
                    Prim.Make(PrimitiveType.Capsule, tail, new Vector3(0f, 0.15f, -0.12f), new Vector3(0.2f, 0.22f, 0.2f), fur,
                              rotation: Quaternion.Euler(-40f, 0f, 0f));
                    break;
                case TailType.Fin:
                    Prim.Make(PrimitiveType.Cube, tail, new Vector3(0f, 0.08f, -0.14f), new Vector3(0.04f, 0.22f, 0.26f), points,
                              rotation: Quaternion.Euler(-25f, 0f, 0f));
                    break;
                case TailType.Curl:
                    Prim.Make(PrimitiveType.Capsule, tail, new Vector3(0f, 0.16f, -0.06f), new Vector3(0.08f, 0.14f, 0.08f), fur,
                              rotation: Quaternion.Euler(-10f, 0f, 0f));
                    Prim.Make(PrimitiveType.Sphere, tail, new Vector3(0f, 0.3f, 0.02f), Vector3.one * 0.12f, fur);
                    break;
                default: // Tuft
                    Prim.Make(PrimitiveType.Capsule, tail, new Vector3(0f, 0.08f, -0.2f), new Vector3(0.06f, 0.2f, 0.06f), fur,
                              rotation: Quaternion.Euler(-60f, 0f, 0f));
                    Prim.Make(PrimitiveType.Sphere, tail, new Vector3(0f, 0.2f, -0.36f), Vector3.one * 0.12f, points);
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_visual != null) Destroy(_visual.gameObject);
        }

        public void SetPose(AvatarPose pose) => _pose = pose;
        public void SetMoveSpeed(float metersPerSecond) => _speed = metersPerSecond;
        public void PlayHop() => _hopT = 0f;
        public void PlayEat() => _eatT = 0f;
        public void PlayBathe() => _batheT = 0f;
        public void PlayRefuse() => _refuseT = 0f;
        public void PlayCry() => _cryT = 0f;
        public void PlayYawn() => _yawnT = 0f;
        public void SetLook(AvatarLook look) => _look = look;
        public void LookAt(Vector3? worldPoint) => _lookAt = worldPoint;

        private void Update()
        {
            if (_visual == null) return;
            float dt = Time.deltaTime;
            bool sleep = _pose == AvatarPose.Sleep;

            // bước chân: nhún thân + vung chân theo tốc độ
            _phase += dt * (3f + _speed * 5f);
            float moving = Mathf.Clamp01(_speed / 1.5f);
            float bob = sleep ? 0f : Mathf.Abs(Mathf.Sin(_phase)) * moving * 0.06f;
            float swing = Mathf.Sin(_phase) * moving * 35f;
            for (int i = 0; i < _legs.Length; i++)
                _legs[i].localRotation = Quaternion.Euler(i == 0 || i == 3 ? swing : -swing, 0f, 0f);

            float hop = 0f;
            Vector3 squash = Vector3.one;
            if (_hopT >= 0f)   // nhảy lên khi được chạm
            {
                _hopT += dt / 0.45f;
                float t = Mathf.Clamp01(_hopT);
                hop = 4f * 0.28f * t * (1f - t);
                float s = Mathf.Sin(t * Mathf.PI);
                squash = new Vector3(1f - 0.06f * s, 1f + 0.1f * s, 1f - 0.06f * s);
                if (_hopT >= 1f) _hopT = -1f;
            }
            float wiggle = 0f;
            if (_batheT >= 0f)  // lắc mình khi tắm
            {
                _batheT += dt / 2.4f;
                wiggle = Mathf.Sin(_batheT * 45f) * 14f * (1f - _batheT);
                if (_batheT >= 1f) _batheT = -1f;
            }
            float headPitch = 0f;
            if (_eatT >= 0f)    // cúi đầu ăn
            {
                _eatT += dt / 1.5f;
                headPitch = Mathf.Abs(Mathf.Sin(_eatT * Mathf.PI * 4f)) * 28f;
                if (_eatT >= 1f) _eatT = -1f;
            }
            float headYaw = 0f;
            if (_refuseT >= 0f) // lắc đầu từ chối
            {
                _refuseT += dt / 0.7f;
                headYaw = Mathf.Sin(_refuseT * Mathf.PI * 6f) * 28f * (1f - _refuseT);
                if (_refuseT >= 1f) _refuseT = -1f;
            }
            if (_cryT >= 0f)    // kêu: ngẩng đầu
            {
                _cryT += dt / 0.8f;
                headPitch -= Mathf.Sin(Mathf.Clamp01(_cryT) * Mathf.PI) * 25f;
                if (_cryT >= 1f) _cryT = -1f;
            }
            if (_yawnT >= 0f)   // ngáp: ngửa đầu chậm
            {
                _yawnT += dt / 1.3f;
                headPitch -= Mathf.Sin(Mathf.Clamp01(_yawnT) * Mathf.PI) * 18f;
                if (_yawnT >= 1f) _yawnT = -1f;
            }
            float targetYaw = 0f;   // quay đầu nhìn theo điểm (vd. chòi ăn khi đói)
            if (_lookAt.HasValue)
            {
                Vector3 local = transform.InverseTransformPoint(_lookAt.Value);
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
            }
            _lookYaw = Mathf.Lerp(_lookYaw, targetYaw, 1f - Mathf.Exp(-dt * 6f));
            headYaw += _lookYaw;

            float breathe = sleep ? Mathf.Sin(Time.time * 2f) * 0.02f : 0f;
            _visual.localPosition = new Vector3(0f, bob + hop + (sleep ? -0.1f : 0f), 0f);
            _visual.localScale = Vector3.Scale(squash, sleep ? new Vector3(1.08f, 0.8f + breathe, 1.05f) : Vector3.one);
            _visual.localRotation = Quaternion.Euler(0f, wiggle, 0f);
            _head.localRotation = Quaternion.Euler(headPitch + (sleep ? 20f : 0f), headYaw, 0f);
            float open = sleep ? 0f : Mathf.Clamp01(_look.EyeOpen);
            Vector3 eye = new Vector3(_eyeScale.x, _eyeScale.y * Mathf.Lerp(0.15f, 1f, open), _eyeScale.z);   // ngủ: nhắm mắt
            foreach (Transform e in _eyes) e.localScale = eye;
        }
    }
}
