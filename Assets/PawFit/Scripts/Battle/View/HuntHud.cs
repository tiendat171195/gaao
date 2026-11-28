using System;
using System.Collections.Generic;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Giao diện màn Săn quái (màn NGANG, Phase 9.3): máu + Nộ của bé (trên trái), đợt quái + vạch máu trùm (giữa trên),
    /// nút Bỏ cuộc (trên phải), nút TUYỆT CHIÊU khi đầy Nộ (dưới phải), dòng nhắc cách đánh (dưới trái), chữ lớn / gợi ý giữa màn,
    /// thẻ kết quả có tới 3 nút (Về vườn / Đánh lại / Đánh tiếp). Chỉ hiển thị, không có luật.
    /// </summary>
    public sealed class HuntHud : MonoBehaviour
    {
        private static readonly Color Back = new Color(0f, 0f, 0f, 0.3f);
        private static readonly Color RageColor = new Color(1f, 0.62f, 0.2f, 0.85f);

        private Font _font;
        private RectTransform _hpFill, _rageFill, _bossFill, _bossRoot, _quit, _ult;
        private Image _rageImg, _ultImg;
        private Text _wave, _bossName, _hint, _big, _combo, _slamText, _dashText, _guardText;
        private GameObject _result;
        private float _hintT, _bigT, _comboT;
        private bool _rageFull;

        public bool ResultShown => _result != null;

        public static HuntHud Create(Transform parent, string petName, Action onQuit, Action onUltimate)
        {
            var go = new GameObject("HuntUI");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<HuntHud>();
            hud.Build(petName, onQuit, onUltimate);
            return hud;
        }

        private void Build(string petName, Action onQuit, Action onUltimate)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            Transform root = transform;

            Label(root, petName, 30, new Vector2(0f, 1f), new Vector2(40f, -34f), new Vector2(600f, 40f), TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
            _hpFill = Bar(root, new Vector2(0f, 1f), new Vector2(40f, -64f), new Vector2(560f, 22f), new Color(0.55f, 1f, 0.6f, 0.85f));
            _rageFill = Bar(root, new Vector2(0f, 1f), new Vector2(40f, -94f), new Vector2(420f, 12f), RageColor);
            _rageImg = _rageFill.GetComponent<Image>();
            Label(root, "NỘ", 22, new Vector2(0f, 1f), new Vector2(472f, -100f), new Vector2(80f, 30f), TextAnchor.UpperLeft, RageColor);

            _wave = Label(root, "", 36, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 50f), TextAnchor.MiddleCenter, Color.white);
            _wave.fontStyle = FontStyle.Bold;
            _bossRoot = Panel(root, "Boss", new Color(0f, 0f, 0f, 0f));
            Place(_bossRoot, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(760f, 70f));
            _bossName = Label(_bossRoot, "", 28, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(760f, 34f), TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.75f));
            _bossFill = Bar(_bossRoot, new Vector2(0f, 1f), new Vector2(0f, -36f), new Vector2(760f, 20f), new Color(1f, 0.35f, 0.3f, 0.9f));
            _bossRoot.gameObject.SetActive(false);

            _quit = Button(root, "Bỏ cuộc", new Vector2(1f, 1f), new Vector2(-130f, -60f), new Vector2(210f, 76f), onQuit);
            _ult = Button(root, "TUYỆT CHIÊU", new Vector2(1f, 0f), new Vector2(-210f, 130f), new Vector2(340f, 150f), onUltimate);
            _ultImg = _ult.GetComponent<Image>();
            _ult.gameObject.SetActive(false);

            Label(root, "Chạm: đánh", 28, new Vector2(0f, 0f), new Vector2(40f, 160f), new Vector2(600f, 36f), TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));
            _slamText = Label(root, "Vuốt lên: đập đất", 28, new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(600f, 36f), TextAnchor.UpperLeft, Color.white);
            _guardText = Label(root, "Vuốt xuống: đỡ (sát lúc = phản đòn)", 28, new Vector2(0f, 0f), new Vector2(40f, 80f), new Vector2(700f, 36f), TextAnchor.UpperLeft, Color.white);
            _dashText = Label(root, "Vuốt ngang: lướt né", 28, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(600f, 36f), TextAnchor.UpperLeft, Color.white);

            _hint = Label(root, "", 38, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1500f, 60f), TextAnchor.MiddleCenter, Color.white);
            _big = Label(root, "", 76, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1600f, 110f), TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.45f));
            _big.fontStyle = FontStyle.Bold;
            _combo = Label(root, "", 56, new Vector2(1f, 0.5f), new Vector2(-80f, 120f), new Vector2(520f, 80f), TextAnchor.UpperRight, new Color(1f, 0.85f, 0.3f));
            _combo.fontStyle = FontStyle.Bold;
        }

        /// <summary>Chạm này rơi vào nút / thẻ kết quả (để không tính là chạm đánh quái).</summary>
        public bool IsOverButton(Vector2 screen) =>
            _result != null || RectTransformUtility.RectangleContainsScreenPoint(_quit, screen, null) ||
            (_ult.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_ult, screen, null));

        public void SetHp(float hp01) => SetFill(_hpFill, hp01);

        public void SetRage(float rage01, bool full)
        {
            SetFill(_rageFill, rage01);
            _rageFull = full;
            if (_ult.gameObject.activeSelf != full) _ult.gameObject.SetActive(full);
        }

        public void SetWave(int wave, int count, bool boss) =>
            _wave.text = wave < 0 ? "" : boss && wave == count - 1 ? "Đợt " + (wave + 1) + "/" + count + " · TRÙM" : "Đợt " + (wave + 1) + "/" + count;

        /// <summary>Vạch máu trùm (name = null: ẩn).</summary>
        public void SetBoss(string name, float hp01)
        {
            bool on = name != null;
            if (_bossRoot.gameObject.activeSelf != on) _bossRoot.gameObject.SetActive(on);
            if (!on) return;
            _bossName.text = name;
            SetFill(_bossFill, hp01);
        }

        /// <summary>Chiêu đang hồi thì dòng nhắc mờ đi (0 = sẵn sàng).</summary>
        public void SetCooldowns(bool slamReady, bool dashReady, bool guardReady)
        {
            _slamText.color = slamReady ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            _dashText.color = dashReady ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            _guardText.color = guardReady ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        public void Combo(int n)
        {
            if (n < 3) return;
            _combo.text = n + " COMBO";
            _combo.transform.localScale = Vector3.one * 1.3f;
            _comboT = 1.2f;
        }

        public void Hint(string text, float seconds = 1.8f)
        {
            _hint.text = text;
            _hintT = seconds;
        }

        public void Big(string text, float seconds = 1.4f)
        {
            _big.text = text;
            _bigT = seconds;
            _big.transform.localScale = Vector3.one * 1.25f;
        }

        /// <summary>Thẻ kết quả: tiêu đề, các dòng, 1–3 nút (nhãn + việc làm khi chạm).</summary>
        public void ShowResult(string title, string body, IList<KeyValuePair<string, Action>> buttons)
        {
            if (_result != null) return;
            RectTransform dim = Panel(transform, "Result", new Color(0f, 0f, 0f, 0.5f));
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            RectTransform card = Panel(dim, "Card", new Color(0.98f, 0.97f, 0.94f, 1f));
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 640f));
            Text t = Label(card, title, 72, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1100f, 110f), TextAnchor.MiddleCenter, new Color(0.25f, 0.2f, 0.2f));
            t.fontStyle = FontStyle.Bold;
            Label(card, body, 34, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1080f, 260f), TextAnchor.MiddleCenter, new Color(0.3f, 0.28f, 0.3f));
            int n = Mathf.Clamp(buttons.Count, 1, 3);
            float w = n == 3 ? 340f : 380f, gap = n == 3 ? 370f : 420f;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * gap;
                Action act = buttons[i].Value;
                Button(card, buttons[i].Key, new Vector2(0.5f, 0.5f), new Vector2(x, -220f), new Vector2(w, 110f), act);
            }
            _result = dim.gameObject;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_hintT > 0f && (_hintT -= dt) <= 0f) _hint.text = "";
            if (_bigT > 0f && (_bigT -= dt) <= 0f) _big.text = "";
            if (_comboT > 0f && (_comboT -= dt) <= 0f) _combo.text = "";
            _big.transform.localScale = Vector3.MoveTowards(_big.transform.localScale, Vector3.one, dt * 2f);
            _combo.transform.localScale = Vector3.MoveTowards(_combo.transform.localScale, Vector3.one, dt * 3f);
            float glow = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 9f);   // đầy Nộ: vạch + nút nhấp nháy
            _rageImg.color = _rageFull ? new Color(1f, 0.75f + 0.2f * glow, 0.3f, glow) : RageColor;
            if (_ult.gameObject.activeSelf)
            {
                _ultImg.color = new Color(1f, 0.8f + 0.15f * glow, 0.35f, 0.95f);
                _ult.localScale = Vector3.one * (1f + 0.05f * glow);
            }
        }

        // ------------------------------------------------------------------ dựng UI

        private static void SetFill(RectTransform fill, float v)
        {
            if (fill == null) return;
            Vector3 s = fill.localScale;
            s.x = Mathf.Clamp01(v);
            fill.localScale = s;
        }

        private RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
        {
            RectTransform bg = Panel(parent, "Bar", Back);
            Place(bg, anchor, pos, size);
            bg.pivot = new Vector2(anchor.x >= 0.5f && anchor.x < 1f ? 0.5f : anchor.x, 1f);
            bg.anchoredPosition = pos;
            RectTransform f = Panel(bg, "Fill", fill);
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = f.offsetMax = Vector2.zero;
            f.pivot = new Vector2(0f, 0.5f);
            return f;
        }

        private RectTransform Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            RectTransform rt = Panel(parent, "Button", new Color(1f, 1f, 1f, 0.88f));
            Place(rt, anchor, pos, size);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            b.onClick.AddListener(() =>
            {
                Sfx.Play(SoundId.UiTap, 0.3f);
                onClick?.Invoke();
            });
            Label(rt, label, 36, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, new Color(0.2f, 0.2f, 0.25f));
            return rt;
        }

        private Text Label(Transform parent, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, anchor, pos, box);
            rt.pivot = new Vector2(align == TextAnchor.UpperLeft ? 0f : align == TextAnchor.UpperRight ? 1f : 0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
