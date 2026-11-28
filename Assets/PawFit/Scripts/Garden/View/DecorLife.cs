using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Garden.View
{
    /// <summary>Vùng chạm của 1 đồ trang trí (router đọc Kind). Là trigger nằm ngoài nhóm Obstacles: không làm đổi NavMesh, bóng bay xuyên qua.</summary>
    public sealed class DecorHandle : MonoBehaviour
    {
        public DecorKind Kind;
    }

    /// <summary>
    /// Đồ trang trí "sống" (Phase 8.4) — bé dùng được đồ mua ở quầy, không chỉ để ngắm:
    ///  * Ghế gỗ: bé hơi mệt tự ra ghế nhảy lên ngồi nghỉ (bộ não quyết); chạm ghế / thả bé lên ghế = bé ra ngồi.
    ///  * Dây đèn lồng: tối (18:00–6:00 giờ trên máy) đèn sáng ấm; chạm = đèn lấp lánh lần lượt + chuông, bé ngước nhìn, nhảy lên.
    ///  * Cối xay gió: mỗi ngày 1 túi bánh ở chân cối; chạm = bé chạy tới ăn (no + vui — DailyService quyết).
    ///  * Vòm hoa: 3 con bướm bay quanh vòm; thỉnh thoảng 1 con bay tới rủ bé đuổi (PlayService thưởng); chạm vòm = gọi bướm.
    /// Luật (tác dụng, thưởng, mỗi ngày 1 túi bánh) nằm ở C# thuần; ở đây chỉ có hình + đưa lệnh.
    /// </summary>
    public sealed class DecorLife : MonoBehaviour
    {
        private sealed class Butterfly
        {
            public Transform Body, WingL, WingR;
            public float Phase, Radius, Height;
            public Vector3 Pos;
        }

        private PetBrain _brain;
        private PlayService _play;
        private DailyService _daily;
        private Func<PetController> _pet;
        private readonly Dictionary<DecorKind, Collider> _touch = new Dictionary<DecorKind, Collider>();

        // ghế gỗ
        private bool _hasBench;
        private Vector3 _seat, _benchFront;
        private PetController _benchPet;

        // dây đèn lồng
        private readonly List<Material> _bulbMats = new List<Material>();
        private readonly List<Color> _bulbBase = new List<Color>();
        private Light _lanternLight;
        private Vector3 _lanternPoint;
        private float _night, _nightGoal, _nightCheck, _twinkleT = -1f, _admireCd;

        // cối xay gió
        private GameObject _basket;
        private Transform _sparkle;
        private Vector3 _basketStand;
        private bool _treatTrip, _treatHinted;
        private float _treatLeft, _hintIn = 6f;

        // vòm hoa
        private readonly List<Butterfly> _flies = new List<Butterfly>();
        private Vector3 _archCenter;
        private int _chaser = -1;
        private Vector3 _chaseGoal;
        private float _chaseIn, _chaseLeft, _pounceCd, _dartIn;
        private int _pounces;

        public bool Paused { get; set; }

        public void Init(PetBrain brain, PlayService play, DailyService daily, Func<PetController> pet)
        {
            _brain = brain;
            _play = play;
            _daily = daily;
            _pet = pet;
            _chaseIn = UnityEngine.Random.Range(25f, 45f);
        }

        /// <summary>Gắn "sự sống" cho 1 món vừa dựng (lúc vào vườn hoặc vừa mua). `root` = gốc món do DecorBuilder dựng.</summary>
        public void Add(DecorKind kind, Transform root)
        {
            if (root == null || _touch.ContainsKey(kind)) return;
            switch (kind)
            {
                case DecorKind.Bench:
                    AddTouch(kind, root, new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1.0f, 0.8f));
                    _seat = At(root, new Vector3(0f, 0.47f, 0.02f));
                    _benchFront = root.rotation * Vector3.back;   // mặt trước ghế nhìn ra (về phía camera)
                    if (_brain != null)
                    {
                        _brain.BenchStand = At(root, new Vector3(0f, 0f, -0.68f)).ToGround();
                        _brain.BenchCenter = root.position.ToGround();
                    }
                    _hasBench = true;
                    _benchPet = null;
                    break;
                case DecorKind.Lanterns:
                    AddTouch(kind, root, new Vector3(0f, 1.5f, 0f), new Vector3(3f, 2.4f, 0.9f));
                    for (int i = 0; i < 3; i++)
                    {
                        Transform bulb = root.Find("Lantern" + i + "/Bulb");
                        Renderer r = bulb != null ? bulb.GetComponent<Renderer>() : null;
                        if (r == null || r.sharedMaterial == null) continue;
                        var m = new Material(r.sharedMaterial);   // bản riêng: sáng lên không làm đổi màu đồ khác cùng màu
                        r.sharedMaterial = m;
                        _bulbMats.Add(m);
                        _bulbBase.Add(m.color);
                    }
                    var lg = new GameObject("LanternLight");
                    lg.transform.SetParent(root, false);
                    lg.transform.localPosition = new Vector3(0f, 1.5f, -0.4f);
                    _lanternLight = lg.AddComponent<Light>();
                    _lanternLight.type = LightType.Point;
                    _lanternLight.color = new Color(1f, 0.78f, 0.45f);
                    _lanternLight.range = 5.5f;
                    _lanternLight.shadows = LightShadows.None;
                    _lanternLight.intensity = 0f;
                    _lanternLight.enabled = false;
                    _lanternPoint = At(root, new Vector3(0f, 1.7f, 0f));
                    _nightCheck = 0f;
                    break;
                case DecorKind.Windmill:
                    AddTouch(kind, root, new Vector3(0f, 1.6f, 0f), new Vector3(1.6f, 3.4f, 1.6f));
                    BuildBasket(root);
                    _basketStand = At(root, new Vector3(0.85f, 0f, -1.35f));
                    break;
                case DecorKind.FlowerArch:
                    AddTouch(kind, root, new Vector3(0f, 1.4f, 0f), new Vector3(2.4f, 3f, 0.9f));
                    _archCenter = At(root, new Vector3(0f, 1.3f, 0f));
                    string[] colors = { "#FFB3D1", "#FFE066", "#9AD8FF" };
                    for (int i = 0; i < 3; i++) _flies.Add(MakeButterfly(Prim.Hex(colors[i]), i));
                    break;
            }
        }

        /// <summary>Chạm vào đồ trang trí (router gọi).</summary>
        public void Tap(DecorKind kind)
        {
            PetController pet = _pet();
            switch (kind)
            {
                case DecorKind.Bench:
                    if (pet == null) return;
                    pet.GoToZone(ZoneType.Bench);
                    Sfx.Play(SoundId.UiTap, 0.4f);
                    break;
                case DecorKind.Lanterns:
                    _twinkleT = 0f;
                    Sfx.Play(SoundId.Chime, 0.45f, 1.15f);
                    if (pet != null && _admireCd <= 0f && Vector3.Distance(pet.transform.position, _lanternPoint) < 12f)
                    {
                        _admireCd = 2.5f;
                        pet.Admire(_lanternPoint);
                    }
                    break;
                case DecorKind.Windmill:
                    TapWindmill(pet);
                    break;
                case DecorKind.FlowerArch:
                    if (_chaser >= 0 || _flies.Count == 0 || pet == null || _play == null) return;
                    bool canPlay = _play.CanPlay(out string why);
                    if (pet.IsIll || !canPlay)
                    {
                        GardenUi.Toast("Bé chưa đuổi bướm được: " + (pet.IsIll ? "bé đang ốm" : why));
                        Sfx.PlayVaried(SoundId.Swish, 0.25f);
                        return;
                    }
                    StartChase(pet);
                    break;
            }
        }

        // ------------------------------------------------------------------ mỗi frame

        private void Update()
        {
            float dt = Time.deltaTime;
            _admireCd -= dt;
            PetController pet = _pet();
            UpdateBench(pet);
            UpdateLanterns(dt);
            UpdateWindmill(pet, dt);
            UpdateButterflies(pet, dt);
        }

        private void UpdateBench(PetController pet)
        {
            if (!_hasBench) return;
            if (pet != null && pet != _benchPet)   // bé mới nở: cho biết chỗ ghế
            {
                pet.SetBench(_seat, _benchFront);
                _benchPet = pet;
            }
            // bé đang ngồi ghế: tắt vùng chạm ghế để chạm trúng bé (vuốt ve bé trên ghế)
            if (_touch.TryGetValue(DecorKind.Bench, out Collider c)) c.enabled = pet == null || !pet.IsResting;
        }

        /// <summary>Tối thì đèn sáng ấm (đèn điểm + lồng sáng màu); vừa chạm thì từng lồng lóe sáng lần lượt.</summary>
        private void UpdateLanterns(float dt)
        {
            if (_lanternLight == null) return;
            _nightCheck -= dt;
            if (_nightCheck <= 0f)
            {
                _nightCheck = 10f;
                int h = DateTime.Now.Hour;
                _nightGoal = h >= 18 || h < 6 ? 1f : 0f;
            }
            _night = Mathf.MoveTowards(_night, _nightGoal, dt * 0.5f);
            float flashSum = 0f;
            if (_twinkleT >= 0f)
            {
                _twinkleT += dt;
                if (_twinkleT > 1.6f) _twinkleT = -1f;
            }
            for (int i = 0; i < _bulbMats.Count; i++)
            {
                float flash = _twinkleT >= 0f ? Mathf.Clamp01(1f - Mathf.Abs(_twinkleT * 3f - i * 0.7f - 0.4f) * 2f) : 0f;
                flashSum += flash;
                float glow = _night * (0.5f + 0.08f * Mathf.Sin(Time.time * 2.3f + i * 1.7f)) + flash * 0.65f;
                _bulbMats[i].color = Color.Lerp(_bulbBase[i], new Color(1f, 0.93f, 0.7f), Mathf.Clamp01(glow));
            }
            float intensity = _night * 1.5f + flashSum * 0.8f;
            _lanternLight.intensity = intensity;
            _lanternLight.enabled = intensity > 0.02f;
        }

        // ------------------------------------------------------------------ cối xay gió

        /// <summary>Túi bánh mây tre ở chân cối + ngôi sao nhỏ lấp lánh phía trên (có bánh thì mới hiện).</summary>
        private void BuildBasket(Transform root)
        {
            Transform b = Prim.Group(root, "TreatBasket", new Vector3(0.85f, 0f, -0.75f));
            Prim.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.09f, 0.36f), Prim.Hex("#B07A3E"), smoothness: 0.1f);
            Prim.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 0.185f, 0f), new Vector3(0.38f, 0.015f, 0.38f), Prim.Hex("#8A5A2B"));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                Prim.Make(PrimitiveType.Cylinder, b, new Vector3(Mathf.Cos(a) * 0.07f, 0.2f + i * 0.025f, Mathf.Sin(a) * 0.07f),
                          new Vector3(0.13f, 0.015f, 0.13f), Prim.Hex("#E0A458"), smoothness: 0.15f, rotation: Quaternion.Euler(12f, a * 40f, 0f));
            }
            _sparkle = Prim.Group(b, "Sparkle", new Vector3(0f, 0.55f, 0f));
            Prim.Make(PrimitiveType.Cube, _sparkle, Vector3.zero, new Vector3(0.09f, 0.09f, 0.02f), Prim.Hex("#FFE066"), smoothness: 0.8f,
                      rotation: Quaternion.Euler(0f, 0f, 45f));
            Prim.Make(PrimitiveType.Cube, _sparkle, Vector3.zero, new Vector3(0.05f, 0.05f, 0.025f), Color.white, smoothness: 0.9f);
            _basket = b.gameObject;
            _basket.SetActive(_daily != null && _daily.WindmillReady);
        }

        private void TapWindmill(PetController pet)
        {
            Sfx.Play(SoundId.UiTap, 0.4f);
            if (_daily == null || pet == null) return;
            if (!_daily.WindmillReady)
            {
                GardenUi.Toast("Hôm nay bé ăn bánh của cối xay gió rồi, mai lại có túi mới nhé");
                return;
            }
            pet.MoveTo(_basketStand);
            _treatTrip = true;
            _treatLeft = 15f;
        }

        private void UpdateWindmill(PetController pet, float dt)
        {
            if (_basket == null || _daily == null) return;
            bool ready = _daily.WindmillReady;
            if (_basket.activeSelf != ready) _basket.SetActive(ready);
            if (ready && _sparkle != null)
            {
                _sparkle.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                _sparkle.localPosition = new Vector3(0f, 0.55f + Mathf.Sin(Time.time * 2.4f) * 0.05f, 0f);
            }
            if (ready && !_treatHinted && pet != null && !Paused)   // mới vào vườn: nhắc 1 lần
            {
                _hintIn -= dt;
                if (_hintIn <= 0f && !GardenUi.IsOpen)
                {
                    _treatHinted = true;
                    GardenUi.Toast("Cối xay gió vừa xay xong túi bánh! Chạm cối xay gió để bé ra ăn", 3.5f);
                }
            }
            if (!_treatTrip) return;
            _treatLeft -= dt;
            if (pet == null || _treatLeft <= 0f)
            {
                _treatTrip = false;
                return;
            }
            float d = Vector3.Distance(Flat(pet.transform.position), Flat(_basketStand));
            if (d > 0.9f && pet.IsGoingTo(_basketStand, 0.5f)) return;   // còn đang chạy tới
            _treatTrip = false;
            if (d > 1.6f) return;   // bạn bảo bé đi chỗ khác
            if (_daily.TakeWindmillTreat(out string why))
            {
                pet.EatSnack(_basket.transform.position + Vector3.up * 0.2f);
                Sfx.Play(SoundId.Treasure, 0.4f);
                GardenUi.Toast("Bé ăn túi bánh của cối xay gió: no bụng + vui hẳn lên!");
            }
            else
            {
                pet.Refuse();
                GardenUi.Toast("Chưa ăn được: " + why);
            }
        }

        // ------------------------------------------------------------------ vòm hoa: bướm

        private Butterfly MakeButterfly(Color color, int i)
        {
            Transform body = Prim.Group(transform, "Butterfly" + i);
            Prim.Make(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.025f, 0.025f, 0.09f), Prim.Hex("#3A3A3A"));
            var f = new Butterfly
            {
                Body = body,
                WingL = Prim.Group(body, "WingL"),
                WingR = Prim.Group(body, "WingR"),
                Phase = i * 2.1f,
                Radius = 0.9f + i * 0.2f,
                Height = 0.4f * i,
            };
            Prim.Make(PrimitiveType.Cube, f.WingL, new Vector3(-0.055f, 0f, 0f), new Vector3(0.1f, 0.004f, 0.085f), color, smoothness: 0.4f);
            Prim.Make(PrimitiveType.Cube, f.WingR, new Vector3(0.055f, 0f, 0f), new Vector3(0.1f, 0.004f, 0.085f), color, smoothness: 0.4f);
            f.Pos = IdlePos(f, 0f);
            body.position = f.Pos;
            return f;
        }

        /// <summary>Bay lượn quanh vòm hoa (vòng lệch + lên xuống).</summary>
        private Vector3 IdlePos(Butterfly f, float t) =>
            _archCenter + new Vector3(Mathf.Cos(t * 0.6f + f.Phase) * f.Radius,
                                      f.Height + Mathf.Sin(t * 1.3f + f.Phase) * 0.35f,
                                      Mathf.Sin(t * 0.45f + f.Phase) * f.Radius * 0.7f);

        private void StartChase(PetController pet)
        {
            _chaser = 0;
            _pounces = 0;
            _chaseLeft = 10f;
            _pounceCd = 0.8f;
            _dartIn = 1.2f;
            Vector3 p = pet.transform.position;
            _chaseGoal = ClampGarden(p + pet.transform.forward * 1.3f + Vector3.up * 0.8f);
            pet.PlayTo(Flat(_flies[0].Pos), true, this);
            Sfx.PlayVaried(SoundId.Swish, 0.3f);
        }

        private void EndChase(PetController pet, bool reward)
        {
            _chaser = -1;
            _chaseIn = UnityEngine.Random.Range(45f, 90f);
            if (pet == null) return;
            if (reward && _play.Reward(PlayKind.Butterfly)) pet.Cheer(2);
            if (pet.PlayOwner == (object)this) pet.StopPlay();
        }

        private void UpdateButterflies(PetController pet, float dt)
        {
            if (_flies.Count == 0) return;
            float t = Time.time;
            for (int i = 0; i < _flies.Count; i++)
            {
                Butterfly f = _flies[i];
                Vector3 goal = i == _chaser ? _chaseGoal : IdlePos(f, t);
                float speed = i == _chaser ? 1.7f : 2.5f;
                Vector3 prev = f.Pos;
                f.Pos = Vector3.MoveTowards(f.Pos, goal, speed * dt) + new Vector3(0f, Mathf.Sin(t * 9f + f.Phase) * 0.004f, 0f);
                f.Body.position = f.Pos;
                Vector3 v = f.Pos - prev;
                v.y = 0f;
                if (v.sqrMagnitude > 1e-6f) f.Body.rotation = Quaternion.Slerp(f.Body.rotation, Quaternion.LookRotation(v), dt * 6f);
                float flap = Mathf.Sin(t * 20f + f.Phase) * 55f;   // vỗ cánh
                f.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                f.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            if (Paused || pet == null || _play == null) return;
            if (_chaser < 0)
            {
                _chaseIn -= dt;
                if (_chaseIn > 0f) return;
                _chaseIn = UnityEngine.Random.Range(30f, 60f);
                // bé rảnh + còn sức + ở không quá xa vòm hoa: 1 con bướm bay tới rủ bé đuổi
                if (pet.IsFree && _play.CanPlay() && Vector3.Distance(Flat(pet.transform.position), Flat(_archCenter)) < 16f) StartChase(pet);
                return;
            }
            UpdateChase(pet, dt);
        }

        /// <summary>Bé đuổi bướm: bướm bay chậm hơn bé, bé tới gần thì vồ, bướm vụt lên né sang chỗ khác; vồ 2 lần = 1 lượt chơi.</summary>
        private void UpdateChase(PetController pet, float dt)
        {
            Butterfly f = _flies[_chaser];
            _chaseLeft -= dt;
            _pounceCd -= dt;
            _dartIn -= dt;
            if (pet.PlayOwner != (object)this || !_play.CanPlay())   // bạn ném bóng / dắt bé đi / bé mệt
            {
                EndChase(pet, false);
                return;
            }
            if (_chaseLeft <= 0f)
            {
                EndChase(pet, _pounces > 0);
                return;
            }
            Vector3 pp = Flat(pet.transform.position);
            pet.PlayTo(Flat(f.Pos), true, this);
            if (_dartIn <= 0f)   // thỉnh thoảng tự đổi hướng bay
            {
                _dartIn = UnityEngine.Random.Range(1f, 1.8f);
                _chaseGoal = ClampGarden(f.Pos + new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), 0f, UnityEngine.Random.Range(-1.6f, 1.6f)));
                _chaseGoal.y = UnityEngine.Random.Range(0.6f, 1.1f);
            }
            if (Vector3.Distance(pp, Flat(f.Pos)) >= 0.8f || _pounceCd > 0f) return;
            _pounceCd = 1.1f;
            _pounces++;
            pet.Pounce();
            Vector3 away = Flat(f.Pos) - pp;
            if (away.sqrMagnitude < 0.01f) away = pet.transform.forward;
            away.y = 0f;
            _chaseGoal = ClampGarden(f.Pos + away.normalized * UnityEngine.Random.Range(1.6f, 2.3f));
            _chaseGoal.y = UnityEngine.Random.Range(1.1f, 1.5f);   // vụt lên né
            _dartIn = 1.4f;
            if (_pounces >= 2) EndChase(pet, true);
        }

        // ------------------------------------------------------------------

        private void AddTouch(DecorKind kind, Transform root, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Touch_" + kind);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(root.position, root.rotation);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.isTrigger = true;
            go.AddComponent<DecorHandle>().Kind = kind;
            _touch[kind] = box;
        }

        /// <summary>Điểm local của món → thế giới (chỉ xoay + dời; món vừa mua đang phóng to dần nên không nhân tỉ lệ).</summary>
        private static Vector3 At(Transform root, Vector3 local) => root.position + root.rotation * local;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 ClampGarden(Vector3 p) => new Vector3(Mathf.Clamp(p.x, -9f, 9f), p.y, Mathf.Clamp(p.z, -12f, 13f));

        private void OnDestroy()
        {
            foreach (Material m in _bulbMats)
                if (m != null) Destroy(m);
        }
    }
}
