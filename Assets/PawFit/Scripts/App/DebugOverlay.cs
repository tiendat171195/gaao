#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Garden.View;
using PawFit.Pet;
using PawFit.Pet.View;
using UnityEngine;

namespace PawFit.App
{
    /// <summary>
    /// Bảng công cụ test — CHỈ có trong Editor / bản Development, bản phát hành không có (vườn thật không có số, không có nút):
    ///  * xem 4 nhu cầu, DNA, việc thú đang làm, biểu cảm, sức khỏe (bỏ bê bao lâu, hồi phục bao nhiêu)
    ///  * tua giờ, đặt nhanh trạng thái (đói / bẩn / buồn ngủ / khỏe / ngủ), chỉnh từng nhu cầu
    ///  * bệnh: Mệt / Ốm / Nguy kịch / Chết ngay, uống thuốc, trứng nở ngay
    ///  * tiến hóa: giai đoạn, dạng đang nghiêng, CareScore, điểm tập, chỉ số, lực chiến; thêm tuổi / điểm tập / chiêu
    ///  * thú mới DNA ngẫu nhiên, đổi model Gen 1 / thú tạm, LOD0 / LOD1, shader riêng / URP Lit
    /// Mở / đóng: nút "DEV" góc trên trái, hoặc phím F1. Phím tắt: 1 / 2 / 3 tua +1 / +6 / +24 giờ, N in nhu cầu.
    /// Chỉ GỌI service (logic nằm ở PetService / DebugTimeProvider), không tự sửa luật game.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private static readonly NeedType[] Needs = { NeedType.Satiety, NeedType.Hygiene, NeedType.Energy, NeedType.Mood };
        private static readonly string[] NeedNames = { "No", "Sạch", "Năng lượng", "Vui" };

        private bool _open;
        private float _scale = 1f;
        private Rect _toggleRect, _panelRect;
        private Vector2 _scroll;
        private GUIStyle _label, _small, _button, _title;
        private float _fps = 60f;

        private static ServiceLocator S => GameBootstrap.Services;

        private void OnEnable() => GardenInputRouter.PointerBlocker = BlocksPointer;

        private void OnDisable()
        {
            if (GardenInputRouter.PointerBlocker == BlocksPointer) GardenInputRouter.PointerBlocker = null;
        }

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            try
            {
                if (Input.GetKeyDown(KeyCode.F1)) _open = !_open;
                if (Input.GetKeyDown(KeyCode.Alpha1)) Skip(1);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Skip(6);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Skip(24);
                if (Input.GetKeyDown(KeyCode.N)) LogNeeds("");
            }
            catch (InvalidOperationException)
            {
                // Input cũ đang tắt: vẫn dùng được nút DEV trên màn hình
            }
        }

        /// <summary>Chạm rơi vào nút DEV / bảng đang mở thì vườn bỏ qua (tọa độ Input: gốc dưới trái).</summary>
        private bool BlocksPointer(Vector2 screen)
        {
            var gui = new Vector2(screen.x / _scale, (Screen.height - screen.y) / _scale);
            return _toggleRect.Contains(gui) || (_open && _panelRect.Contains(gui));
        }

        private void OnGUI()
        {
            if (S == null) return;
            _scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            EnsureStyles();
            float vw = Screen.width / _scale, vh = Screen.height / _scale;
            _toggleRect = new Rect(8f, 8f, 64f, 32f);
            if (GUI.Button(_toggleRect, _open ? "x DEV" : "DEV", _button)) _open = !_open;
            if (!_open) return;

            _panelRect = new Rect(8f, 46f, Mathf.Min(380f, vw - 16f), Mathf.Min(640f, vh - 56f));
            GUI.Box(_panelRect, GUIContent.none);
            GUILayout.BeginArea(new Rect(_panelRect.x + 8f, _panelRect.y + 6f, _panelRect.width - 16f, _panelRect.height - 12f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("PawFit · công cụ test (bản dev)", _title);
            if (S.Get<PetService>().HasPet)
            {
                DrawPet();
                DrawNeeds();
                DrawQuickStates();
                DrawHealth();
                DrawEvolution();
            }
            else DrawEgg();
            DrawTime();
            DrawAvatar();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPet()
        {
            var pets = S.Get<PetService>();
            PetData p = pets.Current;
            if (p == null) return;
            var time = S.Get<ITimeProvider>();
            GUILayout.Label($"'{p.Name}' · {pets.DnaCode} · Gen {p.Dna.Gen} · {p.AgeDays(time.UtcNow):0.00} ngày tuổi", _label);
            GUILayout.Label(p.Dna.ToString(), _small);
            PetBrain brain = S.Get<PetBrain>();
            PetExpression e = S.Get<PetExpressionService>().Current;
            GUILayout.Label($"Đang: {brain.Activity}{(p.IsSleeping ? " (ngủ)" : "")} · biểu cảm: {e.Conditions} · mắt {e.EyeOpen:0.00} · " +
                            $"bẩn {e.Dirt:0.00} · ruồi {e.Flies}{(e.LookAtFood ? " · nhìn chòi ăn" : "")}", _small);
        }

        private void DrawNeeds()
        {
            var pets = S.Get<PetService>();
            PetNeeds n = pets.Current.Needs;
            for (int i = 0; i < Needs.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{NeedNames[i]}: {n.Get(Needs[i]):0}", _label, GUILayout.Width(150f));
                if (GUILayout.Button("-25", _button)) pets.SetNeedForDebug(Needs[i], n.Get(Needs[i]) - 25f);
                if (GUILayout.Button("+25", _button)) pets.SetNeedForDebug(Needs[i], n.Get(Needs[i]) + 25f);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawQuickStates()
        {
            var pets = S.Get<PetService>();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Đói", _button)) pets.SetNeedForDebug(NeedType.Satiety, 12f);
            if (GUILayout.Button("Bẩn", _button)) pets.SetNeedForDebug(NeedType.Hygiene, 8f);
            if (GUILayout.Button("Buồn ngủ", _button)) pets.SetNeedForDebug(NeedType.Energy, 14f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Khỏe hết", _button))
                foreach (NeedType t in Needs) pets.SetNeedForDebug(t, 85f);
            if (GUILayout.Button(pets.Current.IsSleeping ? "Đánh thức" : "Cho ngủ", _button))
                pets.SetSleeping(!pets.Current.IsSleeping);
            if (GUILayout.Button("Lưu", _button)) pets.Save();
            GUILayout.EndHorizontal();
        }

        private void DrawHealth()
        {
            var pets = S.Get<PetService>();
            var health = S.Get<HealthService>();
            HealthConfig c = pets.Config.Health;
            GUILayout.Space(4f);
            string rec = health.IsRecovering ? $" · hồi phục {health.RecoveryProgress01 * 100f:0}%" : "";
            GUILayout.Label($"Sức khỏe: {health.Stage} · bỏ bê {health.NeglectHours:0.#} giờ · chăm kỹ {health.CarefulMarksDone}/3{rec}", _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Mệt", _button)) pets.SetNeglectForDebug(c.TiredAfterHours + 1);
            if (GUILayout.Button("Ốm", _button)) pets.SetNeglectForDebug(c.SickAfterHours + 1);
            if (GUILayout.Button("Nguy kịch", _button)) pets.SetNeglectForDebug(c.CriticalAfterHours + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Chết", _button)) pets.SetNeglectForDebug(c.DeathAfterHours + 1);
            if (GUILayout.Button("Uống thuốc", _button)) health.GiveMedicine();
            if (GUILayout.Button("Khỏi bệnh", _button)) pets.SetNeglectForDebug(0);
            GUILayout.EndHorizontal();
        }

        private void DrawEvolution()
        {
            var pets = S.Get<PetService>();
            var evo = S.Get<EvolutionService>();
            var time = S.Get<ITimeProvider>();
            EvolutionConfig c = pets.Config.Evolution;
            double age = pets.Current.AgeDays(time.UtcNow);
            GUILayout.Space(4f);
            string form = evo.Form != AdultForm.None ? "dạng " + evo.Form : "đang nghiêng " + evo.LeaningForm;
            GUILayout.Label($"Tiến hóa: {evo.Stage} · {age:0.0} ngày · {form} · CareScore {evo.CareScore:0} · chiêu {evo.LearnedSkills}/4" +
                            (evo.CanUltimate ? " · đủ điều kiện Ultimate" : ""), _label);
            GUILayout.Label("Điểm tập: " + evo.Training, _small);
            GUILayout.Label(evo.Stats.ToString(), _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 ngày tuổi", _button)) pets.AgeForDebug(1);
            if (GUILayout.Button("Lên giai đoạn", _button))
            {
                double next = evo.Stage == EvolutionStage.Baby ? c.ChildAtDays : evo.Stage == EvolutionStage.Child ? c.TeenAtDays
                            : evo.Stage == EvolutionStage.Teen ? c.AdultAtDays : c.UltimateAtDays;
                if (next > age) pets.AgeForDebug(next - age + 0.01);
            }
            if (GUILayout.Button("Thuộc 1 chiêu", _button)) evo.LearnSkill();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();   // vào sân tập nhanh (giống chạm hình nộm)
            if (GUILayout.Button("Huấn luyện", _button)) SceneFlow.EnterBattle(PawFit.Battle.BattleMode.Training, PawFit.Battle.BotLevel.Easy);
            if (GUILayout.Button("Đấu Bot Thường", _button)) SceneFlow.EnterBattle(PawFit.Battle.BattleMode.BotFight, PawFit.Battle.BotLevel.Normal);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 Sức", _button)) evo.Training.Add(TrainingAxis.Strength, 10f);
            if (GUILayout.Button("+10 Tốc", _button)) evo.Training.Add(TrainingAxis.Speed, 10f);
            if (GUILayout.Button("+10 Chăm", _button)) evo.Training.Add(TrainingAxis.Care, 10f);
            if (GUILayout.Button("+10 Tối", _button)) evo.Training.Add(TrainingAxis.Shadow, 10f);
            GUILayout.EndHorizontal();
        }

        private void DrawEgg()
        {
            var pets = S.Get<PetService>();
            var time = S.Get<ITimeProvider>();
            EggData egg = pets.Egg;
            GUILayout.Label($"Thú đã mất · {pets.Memorials.Count} bia mộ", _label);
            if (egg == null) return;
            double left = (egg.ReadyAtUtc - time.UtcNow).TotalHours;
            GUILayout.Label(left > 0 ? $"Trứng nở sau {left:0.0} giờ · thừa hưởng {egg.Trait} từ '{egg.ParentName}'"
                                     : $"Trứng đã sẵn sàng — chạm vào trứng trong ổ · thừa hưởng {egg.Trait}", _label);
            GUILayout.Label(DnaCodec.Encode(egg.Dna) + " · " + egg.Dna, _small);
            if (GUILayout.Button("Trứng nở được ngay", _button)) pets.MakeEggReadyForDebug();
        }

        private void DrawTime()
        {
            var dbg = S.Get<DebugTimeProvider>();
            GUILayout.Space(4f);
            GUILayout.Label($"Đồng hồ game lệch: +{dbg.Offset.TotalHours:0.#} giờ", _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 giờ", _button)) Skip(1);
            if (GUILayout.Button("+6 giờ", _button)) Skip(6);
            if (GUILayout.Button("+24 giờ", _button)) Skip(24);
            GUILayout.EndHorizontal();
        }

        private void DrawAvatar()
        {
            PetController pc = SceneFlow.Garden != null ? SceneFlow.Garden.Pet : null;
            var pets = S.Get<PetService>();
            GUILayout.Space(4f);
            string kind = pc == null ? "—" : pc.Avatar is Gen1PetAvatar g
                ? $"Gen 1 · {(Gen1PetAvatar.LowDetail ? "LOD1" : "LOD0")} · {g.BodyVertexCount} đỉnh · " +
                  (Gen1PetAvatar.SimpleShaders ? "URP Lit" : "shader riêng")
                : "thú tạm (khối)";
            GUILayout.Label($"Hình: {kind} · {_fps:0} fps", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Thú mới", _button)) pets.CreateNewPet("Bé " + UnityEngine.Random.Range(1, 99));
            if (GUILayout.Button(PetController.UsePlaceholder ? "Dùng Gen 1" : "Dùng thú tạm", _button))
            {
                PetController.UsePlaceholder = !PetController.UsePlaceholder;
                Rebuild(pc, pets);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Gen1PetAvatar.LowDetail ? "Đổi LOD0" : "Đổi LOD1", _button))
            {
                Gen1PetAvatar.LowDetail = !Gen1PetAvatar.LowDetail;
                Rebuild(pc, pets);
            }
            if (GUILayout.Button(Gen1PetAvatar.SimpleShaders ? "Shader riêng" : "URP Lit", _button))
            {
                Gen1PetAvatar.SimpleShaders = !Gen1PetAvatar.SimpleShaders;
                Rebuild(pc, pets);
            }
            GUILayout.EndHorizontal();
        }

        private static void Rebuild(PetController pc, PetService pets)
        {
            if (pc != null && pets.HasPet) pc.RebuildAvatar(pets.Current.Dna);
        }

        private static void Skip(int hours)
        {
            S.Get<DebugTimeProvider>().Advance(TimeSpan.FromHours(hours));
            S.Get<PetService>().Tick();
            LogNeeds("Tua +" + hours + " giờ · ");
        }

        private static void LogNeeds(string prefix)
        {
            PetData p = S.Get<PetService>().Current;
            if (p == null)
            {
                Debug.Log("[PawFit] " + prefix + "thú đã mất, đang chờ trứng nở");
                return;
            }
            PetNeeds n = p.Needs;
            Debug.Log($"[PawFit] {prefix}no {n.Satiety:0} / sạch {n.Hygiene:0} / năng lượng {n.Energy:0} / vui {n.Mood:0}" +
                      (p.IsSleeping ? " · đang ngủ" : ""));
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 30f };
        }
    }
}
#endif
