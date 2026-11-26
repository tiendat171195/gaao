using System;
using System.Collections.Generic;
using PawFit.Core.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace PawFit.Pet.View
{
    /// <summary>Mốc neo xuất kèm FBX (tọa độ Unity, mét, trong không gian gốc của model).</summary>
    [Serializable]
    public sealed class Gen1Anchors
    {
        public int version;
        public float[] headCenter;
        public float headRadius;
        public float[] tailOrigin;
        public float[] mouth;
        public Gen1AnchorKey[] keys;

        public static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }

    /// <summary>Tâm đầu / gốc đuôi / đầu mỗi xương dời bao nhiêu khi 1 blendshape thân = 100%.</summary>
    [Serializable]
    public sealed class Gen1AnchorKey
    {
        public string name;
        public float[] head;
        public float headScale = 1f;
        public float[] tail;
        public Gen1BoneDelta[] bones;
    }

    [Serializable]
    public sealed class Gen1BoneDelta
    {
        public string name;
        public float[] d;
    }

    /// <summary>
    /// Thú Gen 1 (FBX dựng bằng script Blender, xem Tools/Blender/export_kit.py) qua IPetAvatar:
    ///  * DNA -> blendshape thân (đầu to, mũm mĩm, chân dài), dạng mắt, kiểu tai / sừng / đuôi, màu lông + mắt.
    ///  * Bộ phận là khối cứng gắn vào xương head / hips, tự dời theo blendshape thân nhờ file mốc neo.
    ///  * Chuyển động bằng code: bước chân, thở, vẫy đuôi, quay đầu nhìn, chớp mắt, ăn, kêu, ngáp, ngủ.
    ///  * Điện thoại dùng thân LOD1 (~13k đỉnh), PC / Editor dùng LOD0 (~38k đỉnh).
    /// Mặt thú nhìn về +Z.
    /// </summary>
    public sealed class Gen1PetAvatar : MonoBehaviour, IPetAvatar
    {
        public const string KitPath = "Pets/Gen1/PawFit_Gen1_PetKit";
        public const string AnchorsPath = "Pets/Gen1/PawFit_Gen1_Anchors";
        private const float ModelScale = 0.85f;
        private const float UltimateScale = 1.08f;
        private const float GrowthPerSecond = 0.35f;   // dáng đổi mượt khi lớn / tiến hóa
        private static readonly string[] FormKeys = { "to_adult_fire", "to_adult_water", "to_adult_light", "to_adult_steel", "to_adult_shadow" };

        // thứ tự = thứ tự enum trong PetDna (EyeShape / EarType / HornShape / TailType); kiểu đầu tiên là hình gốc
        private static readonly string[] EyeKinds = { "tron", "hanh_nhan", "meo", "lim_dim", "cun", "sac_hep" };
        private static readonly string[] EarKinds = { "nhon", "tron", "cup", "vay" };
        private static readonly string[] HornKinds = { "thang", "dai", "cong" };
        private static readonly string[] TailKinds = { "cao", "tua", "bong", "vay", "xoan" };
        private static readonly string[] HornSlots = { "C", "PR", "PL", "WR", "WL" };
        private static readonly string[][] HornLayout =
        {
            new string[0], new[] { "C" }, new[] { "PR", "PL" }, new[] { "C", "WR", "WL" },
        };
        private static readonly string[] HeadParts = { "PF_Eye_R", "PF_Eye_L", "PF_Liner_R", "PF_Liner_L", "PF_Nose", "PF_Mouth" };

        private static GameObject _kit;
        private static Gen1Anchors _anchors;
        private static bool _loadTried;
        private static bool? _lowDetail;

        /// <summary>Dùng thân nhẹ LOD1. Mặc định bật trên điện thoại. Đổi xong phải dựng lại thú.</summary>
        public static bool LowDetail
        {
            get => _lowDetail ?? Application.isMobilePlatform;
            set => _lowDetail = value;
        }

        /// <summary>Bỏ shader riêng, dùng URP Lit (thử khi shader lỗi trên 1 máy nào đó).</summary>
        public static bool SimpleShaders { get; set; }

        /// <summary>Model + file mốc neo đã nạp được từ Resources chưa.</summary>
        public static bool KitAvailable
        {
            get
            {
                LoadKit();
                return _kit != null && _anchors != null;
            }
        }

        // ---- dựng ----
        private Transform _visual;
        private Transform _model;
        private SkinnedMeshRenderer _body;
        private readonly List<SkinnedMeshRenderer> _eyeRenderers = new List<SkinnedMeshRenderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Dictionary<string, int>> _shapeIndex =
            new Dictionary<SkinnedMeshRenderer, Dictionary<string, int>>();
        private readonly List<Material> _furMaterials = new List<Material>();
        private readonly List<Color> _furBaseColors = new List<Color>();
        private readonly List<Material> _ownedMaterials = new List<Material>();
        private int _eyeKind;
        private Joint _spine, _neck, _head, _tail;
        private Joint[] _legs;                       // FR, FL, BR, BL
        private readonly List<Joint> _ears = new List<Joint>();
        private readonly List<float> _earSide = new List<float>();
        private Transform _headAnchor, _mouth, _mouthOpen;
        private Vector3 _mouthOpenScale;
        private Vector3 _headCenterModel, _headCenterRest;
        private float _headRadius;
        private RestMap _headMap;
        private readonly Dictionary<string, float> _dnaWeights = new Dictionary<string, float>();
        private readonly float[] _form = new float[5];
        private GrowthLook _targetLook;
        private bool _growthSet, _ultimate;
        private float _trainT = -1f, _trainSeconds = 4f;

        // ---- trạng thái hoạt cảnh ----
        private AvatarPose _pose;
        private AvatarLook _look = AvatarLook.Normal;
        private Vector3? _lookAt;
        private float _speed, _phase, _tailPhase, _sleepBlend;
        private float _lookYaw, _lookPitch, _appliedClosed = -1f, _appliedDirt = -1f, _appliedPale = -1f;
        private float _hopT = -1f, _eatT = -1f, _batheT = -1f, _refuseT = -1f, _cryT = -1f, _yawnT = -1f;
        private float _blinkTimer = 2f, _blinkT = -1f, _earFlickTimer = 5f, _earFlickT = -1f;
        private int _earFlickIndex;
        private System.Random _rnd = new System.Random(1);

        /// <summary>Dựng thành công (có thân) — nếu false thì PetController dùng thú tạm.</summary>
        public bool IsBuilt => _body != null;

        /// <summary>Số đỉnh của thân đang dùng (để Debug overlay hiện LOD0 / LOD1).</summary>
        public int BodyVertexCount => _body != null && _body.sharedMesh != null ? _body.sharedMesh.vertexCount : 0;

        public Vector3 MouthPosition => _mouth != null ? _mouth.position : transform.position + Vector3.up * 0.7f;

        public Vector3 HeadTop => _headAnchor != null
            ? _headAnchor.position + Vector3.up * (_headRadius * _headAnchor.lossyScale.y * 1.45f)
            : transform.position + Vector3.up * 1.2f;

        /// <summary>Tâm đầu (con của xương head, tự dời / co giãn theo dáng tiến hóa) — phụ kiện gắn vào đây.</summary>
        public Transform HeadAnchor => _headAnchor;
        public float HeadRadius => _headRadius;

        private static void LoadKit()
        {
            if (_loadTried) return;
            _loadTried = true;
            _kit = Resources.Load<GameObject>(KitPath);
            TextAsset json = Resources.Load<TextAsset>(AnchorsPath);
            if (json != null) _anchors = JsonUtility.FromJson<Gen1Anchors>(json.text);
            if (_kit == null || _anchors == null || _anchors.keys == null)
            {
                _anchors = null;
                Debug.LogWarning("[PawFit] Không nạp được model Gen 1 (Resources/" + KitPath + " + " + AnchorsPath +
                                 ".json) — dùng thú tạm.");
            }
        }

        // ==================================================================
        // Dựng thú theo DNA
        // ==================================================================

        public void Build(PetDna dna)
        {
            if (dna == null) throw new ArgumentNullException(nameof(dna));
            ClearBuilt();
            LoadKit();
            if (_kit == null || _anchors == null) return;
            _rnd = new System.Random(dna.GetHashCode());

            _visual = Prim.Group(transform, "Visual");
            GameObject inst = Instantiate(_kit, _visual);
            inst.name = "Gen1Model";
            _model = inst.transform;
            _model.localPosition = Vector3.zero;
            _model.localRotation = Quaternion.identity;
            _model.localScale = Vector3.one * ModelScale;
            Animator anim = inst.GetComponent<Animator>();    // import kiểu Generic có Animator: tắt, xương xoay bằng code
            if (anim != null) anim.enabled = false;

            // thân: giữ đúng 1 bản theo mức chi tiết
            Transform lod0 = FindDeep(_model, "PF_Body"), lod1 = FindDeep(_model, "PF_Body_LOD1");
            Transform keep = LowDetail && lod1 != null ? lod1 : (lod0 != null ? lod0 : lod1);
            if (keep == null)
            {
                Debug.LogError("[PawFit] Model Gen 1 thiếu PF_Body — kiểm tra lại file FBX.");
                ClearBuilt();
                return;
            }
            if (lod0 != null && lod0 != keep) Destroy(lod0.gameObject);
            if (lod1 != null && lod1 != keep) Destroy(lod1.gameObject);
            _body = keep.GetComponent<SkinnedMeshRenderer>();
            if (_body == null)
            {
                ClearBuilt();
                return;
            }
            Bounds b = _body.localBounds;        // blendshape + cử động làm thân to ra khỏi khung bao gốc
            b.Expand(0.6f);
            _body.localBounds = b;

            // xương
            _spine = MakeJoint("spine");
            _neck = MakeJoint("neck");
            _head = MakeJoint("head");
            _legs = new[] { MakeJoint("legFR"), MakeJoint("legFL"), MakeJoint("legBR"), MakeJoint("legBL") };
            Transform headBone = FindDeep(_model, "head"), hipsBone = FindDeep(_model, "hips");

            // tâm đầu: mọi bộ phận trên đầu gắn vào đây (là con của xương head)
            _headCenterRest = _headCenterModel = Gen1Anchors.V(_anchors.headCenter);
            _headRadius = _anchors.headRadius > 0f ? _anchors.headRadius : 0.29f;
            _headAnchor = Prim.Group(headBone != null ? headBone : _model, "HeadParts");
            _headAnchor.SetPositionAndRotation(_model.TransformPoint(_headCenterModel), _model.rotation);
            _headMap = new RestMap(_headAnchor, _model);
            foreach (string n in HeadParts) Attach(n, _headAnchor);
            _mouth = FindDeep(_headAnchor, "PF_Mouth");

            // tai: giữ đúng 1 cặp theo DNA
            string ear = EarKinds[Index((int)dna.Ear, EarKinds.Length)];
            foreach (string k in EarKinds)
                foreach (string side in new[] { "R", "L" })
                {
                    Transform t = FindDeep(_model, "PF_Ear_" + k + "_" + side);
                    if (t == null) continue;
                    if (k != ear)
                    {
                        Destroy(t.gameObject);
                        continue;
                    }
                    t.SetParent(_headAnchor, true);
                    _ears.Add(new Joint(t, _model));
                    _earSide.Add(Mathf.Sign(_model.InverseTransformPoint(t.position).x - _headCenterModel.x));
                }

            // sừng: số sừng + dạng theo DNA
            string[] slots = HornLayout[Index(dna.HornCount, HornLayout.Length)];
            string hornKind = HornKinds[Index((int)dna.Horn, HornKinds.Length)];
            foreach (string slot in HornSlots)
            {
                Transform t = FindDeep(_model, "PF_Horn_" + slot);
                if (t == null) continue;
                if (Array.IndexOf(slots, slot) < 0)
                {
                    Destroy(t.gameObject);
                    continue;
                }
                t.SetParent(_headAnchor, true);
                if (hornKind != HornKinds[0]) SetShape(t.GetComponent<SkinnedMeshRenderer>(), hornKind, 100f);
            }

            // đuôi: gốc đuôi là con của xương hông, vẫy bằng cách xoay gốc
            Transform tailGeo = FindDeep(_model, "PF_Tail");
            if (tailGeo != null)
            {
                // tự kiểm tra: pivot đuôi trong FBX phải trùng gốc đuôi trong file mốc neo (cùng hệ tọa độ)
                Vector3 got = _model.InverseTransformPoint(tailGeo.position), want = Gen1Anchors.V(_anchors.tailOrigin);
                if (Vector3.Distance(got, want) > 0.03f)
                    Debug.LogWarning($"[PawFit] Gen 1: gốc đuôi trong FBX {got} lệch file mốc neo {want} — " +
                                     "có thể Unity import trục khác, gửi dòng này cho dev.");
                Transform tailAnchor = Prim.Group(hipsBone != null ? hipsBone : _model, "TailRoot");
                tailAnchor.SetPositionAndRotation(_model.TransformPoint(Gen1Anchors.V(_anchors.tailOrigin)), _model.rotation);
                tailGeo.SetParent(tailAnchor, true);
                string tailKind = TailKinds[Index((int)dna.Tail, TailKinds.Length)];
                if (tailKind != TailKinds[0]) SetShape(tailGeo.GetComponent<SkinnedMeshRenderer>(), tailKind, 100f);
                _tail = new Joint(tailAnchor, _model);   // vẫy đuôi = xoay gốc đuôi quanh chính nó
            }

            _dnaWeights.Clear();
            _dnaWeights["head_big"] = dna.HeadSize / 15f;
            _dnaWeights["body_chubby"] = dna.Chubby / 15f;
            _dnaWeights["legs_long"] = dna.LegLength / 15f;
            _growthSet = false;
            ApplyShape();
            ApplyMaterials(dna);

            // mắt: dạng theo DNA
            _eyeKind = Index((int)dna.EyeShape, EyeKinds.Length);
            foreach (string n in new[] { "PF_Eye_R", "PF_Eye_L", "PF_Liner_R", "PF_Liner_L" })
            {
                Transform t = FindDeep(_headAnchor, n);
                SkinnedMeshRenderer r = t != null ? t.GetComponent<SkinnedMeshRenderer>() : null;
                if (r != null) _eyeRenderers.Add(r);
            }
            _appliedClosed = -1f;
            ApplyEyes(0f);
            BuildOpenMouth();
        }

        /// <summary>
        /// Áp dáng thân: 3 blendshape tỉ lệ theo DNA + 5 blendshape tiến hóa theo giai đoạn. Dời tâm đầu / gốc đuôi
        /// và tâm xoay của từng khớp xương theo đúng lượng đó (file mốc neo), nên thú đã lớn vẫn xoay đầu / chân đúng chỗ.
        /// Tính bằng bản đồ chụp lúc nghỉ => gọi được cả khi xương đang xoay.
        /// </summary>
        private void ApplyShape()
        {
            if (_body == null) return;
            foreach (KeyValuePair<string, float> kv in _dnaWeights) SetShape(_body, kv.Key, kv.Value * 100f);
            for (int i = 0; i < FormKeys.Length; i++) SetShape(_body, FormKeys[i], _form[i] * 100f);
            Vector3 dHead = Vector3.zero, dTail = Vector3.zero;
            float headScale = 1f;
            var bones = new Dictionary<string, Vector3>();
            foreach (Gen1AnchorKey k in _anchors.keys)
            {
                if (k == null) continue;
                float w = Weight(k.name);
                if (w == 0f) continue;
                dHead += Gen1Anchors.V(k.head) * w;
                dTail += Gen1Anchors.V(k.tail) * w;
                headScale += (k.headScale - 1f) * w;
                if (k.bones == null) continue;
                foreach (Gen1BoneDelta b in k.bones)
                {
                    if (b == null || b.name == null) continue;
                    bones.TryGetValue(b.name, out Vector3 acc);
                    bones[b.name] = acc + Gen1Anchors.V(b.d) * w;
                }
            }
            _headCenterModel = _headCenterRest + dHead;
            if (_headMap != null)
            {
                _headAnchor.localPosition = _headMap.At(dHead);
                _headAnchor.localScale = Vector3.one * headScale;
            }
            _tail?.MoveRest(dTail);
            Pivot(_spine, "spine", bones);
            Pivot(_neck, "neck", bones);
            Pivot(_head, "head", bones);
            if (_legs != null)
            {
                string[] names = { "legFR", "legFL", "legBR", "legBL" };
                for (int i = 0; i < _legs.Length; i++) Pivot(_legs[i], names[i], bones);
            }
            _model.localScale = Vector3.one * (ModelScale * (_ultimate ? UltimateScale : 1f));
        }

        private float Weight(string key)
        {
            if (_dnaWeights.TryGetValue(key, out float w)) return w;
            int i = Array.IndexOf(FormKeys, key);
            return i >= 0 ? _form[i] : 0f;
        }

        private static void Pivot(Joint j, string bone, Dictionary<string, Vector3> deltas)
        {
            if (j == null) return;
            j.MovePivot(deltas.TryGetValue(bone, out Vector3 d) ? d : Vector3.zero);
        }

        /// <summary>Hình dáng theo giai đoạn (gọi mỗi frame). Lần đầu sau khi dựng thì đặt ngay, sau đó đổi mượt.</summary>
        public void SetGrowth(GrowthLook look)
        {
            _targetLook = look;
            if (_growthSet) return;
            _growthSet = true;
            for (int i = 0; i < 5; i++) _form[i] = look.Get(EvolutionRules.Forms[i]);
            _ultimate = look.Ultimate;
            ApplyShape();
        }

        private void UpdateGrowth(float dt)
        {
            if (!_growthSet) return;
            bool changed = false;
            for (int i = 0; i < 5; i++)
            {
                float target = _targetLook.Get(EvolutionRules.Forms[i]);
                if (Mathf.Abs(_form[i] - target) < 1e-4f) continue;
                _form[i] = Mathf.MoveTowards(_form[i], target, GrowthPerSecond * dt);
                changed = true;
            }
            if (_ultimate != _targetLook.Ultimate)
            {
                _ultimate = _targetLook.Ultimate;
                changed = true;
            }
            if (changed) ApplyShape();
        }

        private void ApplyMaterials(PetDna dna)
        {
            Color fur = DnaVisuals.Fur(dna), belly = DnaVisuals.Belly(dna), points = DnaVisuals.Points(dna);
            Color pattern = DnaVisuals.Pattern(dna);
            float stripes = dna.Pattern == FurPattern.Stripes ? 0.9f : 0f;
            float spots = dna.Pattern == FurPattern.Spots ? 0.9f : 0f;
            SetMaterial(_body, FurMaterial(fur, belly, points, pattern, stripes, spots, 0.3f, 1f, 0.06f), true);
            foreach (Joint e in _ears)
                SetMaterial(e.T.GetComponent<Renderer>(),
                            FurMaterial(fur, DnaVisuals.EarInner(dna), DnaVisuals.EarTip(dna), pattern, 0f, 0f, 0f, 1f, 0.06f), true);
            Transform tail = FindDeep(_model, "PF_Tail");
            if (tail != null)
                SetMaterial(tail.GetComponent<Renderer>(),
                            FurMaterial(fur, belly, Color.Lerp(fur, points, 0.65f), pattern, stripes, 0f, 0f, 1f, 0.06f), true);
            Material horn = FurMaterial(Prim.Hex("#EFE6D6"), Prim.Hex("#EFE6D6"), Prim.Hex("#FFFFFF"), Color.white, 0f, 0f, 0f, 0.15f, 0.5f);
            foreach (string slot in HornSlots)
            {
                Transform t = FindDeep(_headAnchor, "PF_Horn_" + slot);
                if (t != null) SetMaterial(t.GetComponent<Renderer>(), horn, false);
            }
            Color iris = DnaVisuals.Eye(dna);
            SetMaterial(Part("PF_Eye_R"), EyeMaterial(iris, 1f), false, false);
            SetMaterial(Part("PF_Eye_L"), EyeMaterial(iris, -1f), false, false);
            Material liner = Owned(new Material(MaterialFactory.Lit(Prim.Hex("#1C1416"), 0.35f)));
            SetMaterial(Part("PF_Liner_R"), liner, false, false);
            SetMaterial(Part("PF_Liner_L"), liner, false, false);
            SetMaterial(Part("PF_Nose"), MaterialFactory.Lit(Prim.Hex("#2E2026"), 0.65f), false, false);
            SetMaterial(Part("PF_Mouth"), MaterialFactory.Lit(Prim.Hex("#3A2428"), 0.3f), false, false);
        }

        private Material FurMaterial(Color fur, Color light, Color points, Color pattern, float stripes, float spots,
                                     float dorsal, float fuzz, float gloss)
        {
            Shader sh = SimpleShaders ? null : Shader.Find("PawFit/Gen1Fur");
            if (sh == null || !sh.isSupported) return Owned(new Material(MaterialFactory.Lit(fur, 0.15f)));
            var m = new Material(sh);
            m.SetColor("_FurColor", fur);
            m.SetColor("_LightColor", light);
            m.SetColor("_PointsColor", points);
            m.SetColor("_PatternColor", pattern);
            m.SetFloat("_StripeAmount", stripes);
            m.SetFloat("_SpotAmount", spots);
            m.SetFloat("_DorsalAmount", dorsal);
            m.SetFloat("_Fuzz", fuzz);
            m.SetFloat("_Gloss", gloss);
            return Owned(m);
        }

        private Material EyeMaterial(Color iris, float side)
        {
            Shader sh = SimpleShaders ? null : Shader.Find("PawFit/Gen1Eye");
            if (sh == null || !sh.isSupported) return Owned(new Material(MaterialFactory.Lit(iris * 0.7f, 0.9f)));
            var m = new Material(sh);
            m.SetColor("_IrisColor", iris);
            m.SetFloat("_Side", side);
            return Owned(m);
        }

        private Material Owned(Material m)
        {
            _ownedMaterials.Add(m);
            return m;
        }

        private void SetMaterial(Renderer r, Material m, bool dirtable, bool shadows = true)
        {
            if (r == null || m == null) return;
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            if (dirtable && !_furMaterials.Contains(m))
            {
                _furMaterials.Add(m);
                // shader riêng có _Dirt; URP Lit dự phòng thì nhớ màu gốc để pha bẩn
                _furBaseColors.Add(m.HasProperty("_Dirt") ? Color.white : m.color);
            }
        }

        private Renderer Part(string name)
        {
            Transform t = FindDeep(_headAnchor, name);
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        /// <summary>Miệng há (khi ăn / kêu / ngáp): 1 khối dẹt màu tối đặt ngay dưới nét miệng.</summary>
        private void BuildOpenMouth()
        {
            if (_mouth == null) return;
            Vector3 world = _mouth.position + _model.forward * (0.006f * ModelScale) - _model.up * (0.022f * ModelScale);
            float k = 1f / Mathf.Max(0.0001f, _headAnchor.lossyScale.x);
            _mouthOpenScale = new Vector3(0.075f, 0.062f, 0.03f) * (ModelScale * k);
            GameObject go = Prim.Make(PrimitiveType.Sphere, _headAnchor, _headAnchor.InverseTransformPoint(world), Vector3.zero,
                                      Prim.Hex("#4A1E28"), smoothness: 0.4f, name: "MouthOpen");
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            _mouthOpen = go.transform;
        }

        private Joint MakeJoint(string bone)
        {
            Transform t = FindDeep(_model, bone);
            return t != null && t.parent != null ? new Joint(t, _model) : null;
        }

        private void Attach(string name, Transform parent)
        {
            Transform t = FindDeep(_model, name);
            if (t != null) t.SetParent(parent, true);
        }

        private void ClearBuilt()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            foreach (Material m in _ownedMaterials)
                if (m != null) Destroy(m);
            _ownedMaterials.Clear();
            _visual = _model = _headAnchor = _mouth = _mouthOpen = null;
            _body = null;
            _eyeRenderers.Clear();
            _shapeIndex.Clear();
            _furMaterials.Clear();
            _furBaseColors.Clear();
            _ears.Clear();
            _earSide.Clear();
            _spine = _neck = _head = _tail = null;
            _legs = null;
            _headMap = null;
            _growthSet = false;
            _ultimate = false;
            Array.Clear(_form, 0, _form.Length);
        }

        private void OnDestroy() => ClearBuilt();

        // ==================================================================
        // Lệnh từ PetController
        // ==================================================================

        public void SetPose(AvatarPose pose) => _pose = pose;
        public void SetMoveSpeed(float metersPerSecond) => _speed = metersPerSecond;
        public void PlayHop() => _hopT = 0f;
        public void PlayEat() => _eatT = 0f;
        public void PlayBathe() => _batheT = 0f;
        public void PlayRefuse() => _refuseT = 0f;
        public void PlayCry() => _cryT = 0f;
        public void PlayYawn() => _yawnT = 0f;

        public void PlayTrain(float seconds)
        {
            _trainSeconds = Mathf.Max(0.5f, seconds);
            _trainT = 0f;
        }
        public void SetLook(AvatarLook look) => _look = look;
        public void LookAt(Vector3? worldPoint) => _lookAt = worldPoint;

        // ==================================================================
        // Hoạt cảnh bằng code (mỗi frame)
        // ==================================================================

        private void Update()
        {
            if (_model == null) return;
            float dt = Time.deltaTime, time = Time.time;
            bool sleep = _pose == AvatarPose.Sleep;
            UpdateGrowth(dt);
            // tập ở sân tập: lao người tới đấm hình nộm liên tục
            float punch = 0f;
            if (Step(ref _trainT, _trainSeconds, out float tt))
                punch = Mathf.Max(0f, Mathf.Sin(tt * _trainSeconds * Mathf.PI * 2.2f)) * (1f - Mathf.Pow(tt, 6f));
            // thú đã lớn: xoay khớp nhỏ lại 1 chút cho tự nhiên
            float grown = Mathf.Clamp01(_form[0] + _form[1] + _form[2] + _form[3] + _form[4]);
            _sleepBlend = Mathf.MoveTowards(_sleepBlend, sleep ? 1f : 0f, dt * 1.6f);
            float moving = sleep ? 0f : Mathf.Clamp01(_speed / 1.4f);
            _phase += dt * (3f + _speed * 4.2f);

            // ---- thân: nhún bước, nhảy, lắc khi tắm, thở, nằm xuống khi ngủ ----
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * moving * 0.045f;
            float hop = 0f;
            Vector3 squash = Vector3.one;
            if (Step(ref _hopT, 0.45f, out float th))
            {
                hop = 4f * 0.26f * th * (1f - th);
                float s = Mathf.Sin(th * Mathf.PI);
                squash = new Vector3(1f - 0.06f * s, 1f + 0.1f * s, 1f - 0.06f * s);
            }
            float wiggle = Step(ref _batheT, 2.4f, out float tb) ? Mathf.Sin(tb * 45f) * 14f * (1f - tb) : 0f;
            float breathe = Mathf.Sin(time * Mathf.Lerp(2.2f, 1.3f, _sleepBlend)) * Mathf.Lerp(0.012f, 0.025f, _sleepBlend);
            _visual.localPosition = new Vector3(0f, bob + hop + punch * 0.04f - _sleepBlend * 0.11f, punch * 0.12f);
            _visual.localScale = Vector3.Scale(squash, new Vector3(1f + 0.04f * _sleepBlend, 1f + breathe - 0.06f * _sleepBlend, 1f));
            float shiver = _look.Sick >= 0.5f ? Mathf.Sin(time * 55f) * 2.2f * _look.Sick * (1f - 0.5f * _sleepBlend) : 0f;
            _visual.localRotation = Quaternion.Euler(0f, wiggle, shiver);   // ốm: run lẩy bẩy

            // ---- chân: bước chéo (trước phải + sau trái cùng nhịp), gập lại khi ngủ ----
            float swing = Mathf.Sin(_phase) * moving * Mathf.Lerp(28f, 20f, grown) + punch * 22f;
            float fold = _sleepBlend * 55f;
            if (_legs != null)
            {
                _legs[0]?.Set(swing + fold, 0f);
                _legs[1]?.Set(-swing + fold, 0f);
                _legs[2]?.Set(-swing - fold, 0f);
                _legs[3]?.Set(swing - fold, 0f);
            }
            _spine?.Set(0f, Mathf.Sin(_phase) * moving * 4f);

            // ---- đầu: nhìn theo điểm + cúi ăn + lắc từ chối + ngẩng kêu / ngáp ----
            float tYaw = 0f, tPitch = 0f;
            if (_lookAt.HasValue)
            {
                Vector3 l = _model.InverseTransformPoint(_lookAt.Value) - _headCenterModel;
                float flat = Mathf.Max(0.01f, new Vector2(l.x, l.z).magnitude);
                tYaw = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -70f, 70f);
                tPitch = Mathf.Clamp(Mathf.Atan2(-l.y, flat) * Mathf.Rad2Deg, -25f, 30f);
            }
            else if (!sleep)
            {
                tYaw = (Mathf.Sin(time * 0.37f) * 9f + Mathf.Sin(time * 0.13f + 1.7f) * 7f) * (1f - moving);   // ngó nghiêng
            }
            float k = 1f - Mathf.Exp(-dt * 5f);
            _lookYaw = Mathf.Lerp(_lookYaw, tYaw, k);
            _lookPitch = Mathf.Lerp(_lookPitch, tPitch, k);

            float eat = Step(ref _eatT, 1.5f, out float te) ? Mathf.Abs(Mathf.Sin(te * Mathf.PI * 4f)) : 0f;
            float refuse = Step(ref _refuseT, 0.7f, out float tr) ? Mathf.Sin(tr * Mathf.PI * 6f) * 28f * (1f - tr) : 0f;
            float cry = Step(ref _cryT, 0.8f, out float tc) ? Mathf.Sin(tc * Mathf.PI) : 0f;
            float yawn = Step(ref _yawnT, 1.4f, out float ty) ? Mathf.Sin(ty * Mathf.PI) : 0f;
            float pitch = _lookPitch + eat * 26f - cry * 22f - yawn * 16f + _sleepBlend * 16f + _look.Droop * 5f + punch * 14f;
            float yaw = _lookYaw * (1f - _sleepBlend) + refuse;
            float roll = Mathf.Sin(time * 0.5f) * 3f * (1f - moving) * (1f - _sleepBlend);
            _neck?.Set(pitch * 0.4f, yaw * 0.35f);
            _head?.Set(pitch * 0.6f, yaw * 0.65f, roll);

            // ---- đuôi: vẫy (buồn ngủ thì chậm + cụp), chạy thì vểnh lên ----
            float droop = Mathf.Max(_look.Droop, _sleepBlend);
            _tailPhase += dt * Mathf.Lerp(7f, 2.5f, droop) * (1f + moving * 0.6f);
            float wag = Mathf.Sin(_tailPhase) * Mathf.Lerp(16f, 4f, droop) * (1f - _sleepBlend);
            _tail?.Set(-(droop * 24f) + moving * 8f, wag);

            // ---- tai: cụp ra ngoài khi buồn ngủ / ngủ, thỉnh thoảng giật nhẹ ----
            _earFlickTimer -= dt;
            if (_earFlickTimer <= 0f && _ears.Count > 0 && !sleep)
            {
                _earFlickT = 0f;
                _earFlickIndex = _rnd.Next(_ears.Count);
                _earFlickTimer = 3.5f + (float)_rnd.NextDouble() * 5f;
            }
            float flick = Step(ref _earFlickT, 0.25f, out float tf) ? Mathf.Sin(tf * Mathf.PI) * 14f : 0f;
            for (int i = 0; i < _ears.Count; i++)
            {
                float side = _earSide[i];
                float r = -side * (droop * 32f + (i == _earFlickIndex ? flick : 0f));
                _ears[i].Set(0f, 0f, r);
            }

            // ---- mắt: độ mở theo biểu cảm + chớp mắt + nhắm khi ngủ / ngáp ----
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinkT = 0f;
                _blinkTimer = 2.5f + (float)_rnd.NextDouble() * 3.5f;
            }
            float blink = Step(ref _blinkT, 0.16f, out float tbl) ? Mathf.Sin(tbl * Mathf.PI) : 0f;
            float closed = Mathf.Max(1f - Mathf.Clamp01(_look.EyeOpen), Mathf.Max(blink, yawn * 0.85f));
            ApplyEyes(Mathf.Lerp(closed, 1f, _sleepBlend));

            // ---- miệng há ----
            if (_mouthOpen != null)
            {
                float open = Mathf.Max(eat * 0.7f, Mathf.Max(cry, yawn * 1.25f));
                _mouthOpen.localScale = _mouthOpenScale * Mathf.Clamp01(open);
            }

            ApplyDirt(_look.Dirt);
            ApplyPale(_look.Sick);
        }

        /// <summary>Ốm: lông nhợt đi (shader riêng có _Pale; URP Lit dự phòng thì pha xám).</summary>
        private void ApplyPale(float sick)
        {
            float pale = Mathf.Clamp01((sick - 0.2f) / 0.8f);
            if (Mathf.Abs(pale - _appliedPale) < 0.01f) return;
            _appliedPale = pale;
            for (int i = 0; i < _furMaterials.Count; i++)
            {
                Material m = _furMaterials[i];
                if (m.HasProperty("_Pale")) m.SetFloat("_Pale", pale);
            }
            if (_furMaterials.Count > 0 && !_furMaterials[0].HasProperty("_Pale")) _appliedDirt = -1f;   // để ApplyDirt pha lại màu
        }

        private void ApplyEyes(float closed)
        {
            closed = Mathf.Clamp01(closed);
            if (Mathf.Abs(closed - _appliedClosed) < 0.002f) return;
            _appliedClosed = closed;
            string kind = EyeKinds[_eyeKind];
            foreach (SkinnedMeshRenderer r in _eyeRenderers)
            {
                if (_eyeKind != 0) SetShape(r, kind, (1f - closed) * 100f);
                SetShape(r, "closed_" + kind, closed * 100f);
            }
        }

        private void ApplyDirt(float dirt)
        {
            dirt = Mathf.Clamp01(dirt);
            if (Mathf.Abs(dirt - _appliedDirt) < 0.01f) return;
            _appliedDirt = dirt;
            Color mud = Prim.Hex("#6B5642");
            for (int i = 0; i < _furMaterials.Count; i++)
            {
                Material m = _furMaterials[i];
                if (m.HasProperty("_Dirt")) m.SetFloat("_Dirt", dirt);
                else   // URP Lit dự phòng: chỉ xỉn màu + nhợt khi ốm
                {
                    Color c = Color.Lerp(_furBaseColors[i], mud, dirt * 0.35f);
                    float pale = Mathf.Max(0f, _appliedPale);
                    float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    m.color = Color.Lerp(c, new Color(g * 0.9f + 0.06f, g + 0.06f, g * 0.92f + 0.06f), pale * 0.6f);
                }
            }
        }

        /// <summary>Chạy 1 đồng hồ hoạt cảnh: trả true + t (0..1) khi đang chạy, tự tắt khi hết.</summary>
        private static bool Step(ref float timer, float duration, out float t)
        {
            t = 0f;
            if (timer < 0f) return false;
            timer += Time.deltaTime / duration;
            t = Mathf.Clamp01(timer);
            if (timer >= 1f) timer = -1f;
            return true;
        }

        // ==================================================================
        // Tiện ích
        // ==================================================================

        private void SetShape(SkinnedMeshRenderer r, string shape, float weight)
        {
            int i = ShapeIndex(r, shape);
            if (i >= 0) r.SetBlendShapeWeight(i, weight);
        }

        private int ShapeIndex(SkinnedMeshRenderer r, string shape)
        {
            if (r == null || r.sharedMesh == null) return -1;
            if (!_shapeIndex.TryGetValue(r, out Dictionary<string, int> map))
            {
                map = new Dictionary<string, int>();
                Mesh mesh = r.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string n = mesh.GetBlendShapeName(i);
                    int dot = n.LastIndexOf('.');          // có bản import đặt tên "Mesh.shape"
                    map[dot >= 0 ? n.Substring(dot + 1) : n] = i;
                }
                _shapeIndex[r] = map;
            }
            return map.TryGetValue(shape, out int idx) ? idx : -1;
        }

        private static int Index(int value, int count) => count <= 0 ? 0 : Mathf.Clamp(value, 0, count - 1);

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// 1 khớp xoay bằng code. Trục xoay là trục của GỐC MODEL (ngang / đứng / dọc) đổi sang không gian của cha
        /// lúc nghỉ, nên không phụ thuộc hướng xương trong file FBX.
        /// </summary>
        private sealed class Joint
        {
            public readonly Transform T;
            private readonly Quaternion _rest;
            private readonly Vector3 _right, _up, _fwd;
            private readonly RestMap _map;
            private Vector3 _restPos, _pivot;

            public Joint(Transform t, Transform model)
            {
                T = t;
                _rest = t.localRotation;
                Transform p = t.parent;
                _right = p.InverseTransformDirection(model.right).normalized;
                _up = p.InverseTransformDirection(model.up).normalized;
                _fwd = p.InverseTransformDirection(model.forward).normalized;
                _map = new RestMap(t, model);
                _restPos = _pivot = t.localPosition;
            }

            /// <summary>Dời tâm xoay (đầu khớp) theo dáng thân, `modelDelta` tính trong hệ tọa độ gốc model.</summary>
            public void MovePivot(Vector3 modelDelta) => _pivot = _map.At(modelDelta);

            /// <summary>Dời cả vị trí nghỉ lẫn tâm xoay (vd gốc đuôi đi theo mông khi thân đổi dáng).</summary>
            public void MoveRest(Vector3 modelDelta)
            {
                _restPos = _pivot = _map.At(modelDelta);
                T.localPosition = _restPos;
            }

            /// <summary>pitch + = cúi xuống / vung ra sau · yaw + = quay sang phải · roll quanh trục dọc thân.</summary>
            public void Set(float pitch, float yaw, float roll = 0f)
            {
                Quaternion q = Quaternion.AngleAxis(yaw, _up) * Quaternion.AngleAxis(pitch, _right) * Quaternion.AngleAxis(roll, _fwd);
                T.localRotation = q * _rest;
                T.localPosition = _pivot + q * (_restPos - _pivot);   // xoay quanh tâm khớp (đã dời theo dáng thân)
            }
        }

        /// <summary>
        /// Chụp lúc nghỉ: vị trí cục bộ + cách đổi 1 độ dời trong hệ gốc model sang hệ của cha.
        /// Nhờ vậy dời được vị trí theo dáng thân kể cả khi cha đang xoay.
        /// </summary>
        private sealed class RestMap
        {
            private readonly Vector3 _rest, _x, _y, _z;

            public RestMap(Transform t, Transform model)
            {
                _rest = t.localPosition;
                Transform p = t.parent;
                _x = p.InverseTransformVector(model.TransformVector(Vector3.right));
                _y = p.InverseTransformVector(model.TransformVector(Vector3.up));
                _z = p.InverseTransformVector(model.TransformVector(Vector3.forward));
            }

            public Vector3 At(Vector3 modelDelta) => _rest + _x * modelDelta.x + _y * modelDelta.y + _z * modelDelta.z;
        }
    }
}
