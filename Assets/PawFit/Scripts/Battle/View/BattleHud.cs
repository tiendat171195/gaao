using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Giao diện màn đấu (màn ngang, uGUI dựng bằng code). Trận thật chỉ có 2 vạch HP mờ + vạch thời gian mảnh (GDD);
    /// Mode Huấn luyện thêm: tên chiêu đang học + 10 chấm tiến độ, bóng mờ nét vuốt, gợi ý.
    /// Thêm vết ngón tay khi vuốt và thẻ kết quả cuối trận.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        public static readonly string[] SkillNames = { "Nhanh (vuốt ngang)", "Mạnh (vuốt lên)", "Chiêu riêng (vuốt V)", "Ultimate (vuốt Z)" };

        // đường bóng mờ của từng chiêu trong khung 0..1 (y hướng lên); 4 = đỡ (vuốt xuống)
        private static readonly Vector2[][] Ghosts =
        {
            new[] { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f) },
            new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f) },
            new[] { new Vector2(0f, 1f), new Vector2(0.5f, 0f), new Vector2(1f, 1f) },
            new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) },
            new[] { new Vector2(0.5f, 1f), new Vector2(0.5f, 0f) },
        };

        private Canvas _canvas;
        private Font _font;
        private RectTransform _hpFill0, _hpFill1, _timeFill;
        private Text _lessonText, _hint, _big;
        private Image[] _dots;
        private RectTransform _ghostRoot, _ghostRunner;
        private readonly List<RectTransform> _ghostDots = new List<RectTransform>();
        private int _ghostSkill = -1;
        private float _hintT, _bigT;
        private readonly List<(RectTransform rt, float t)> _trail = new List<(RectTransform, float)>();
        private RectTransform _button;
        private GameObject _result;

        public bool ResultShown => _result != null;

        public static BattleHud Create(Transform parent, bool training, string leftName, string rightName, Action onButton)
        {
            var go = new GameObject("BattleUI");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<BattleHud>();
            hud.Build(training, leftName, rightName, onButton);
            return hud;
        }

        private void Build(bool training, string leftName, string rightName, Action onButton)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            Transform root = transform;

            // 2 vạch HP mờ trên cùng (trái: thú, phải: hình nộm) + vạch thời gian mảnh ở giữa
            _hpFill0 = Bar(root, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(620f, 18f), new Color(0.6f, 1f, 0.7f, 0.55f), true);
            _hpFill1 = Bar(root, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(620f, 18f), new Color(1f, 0.7f, 0.6f, 0.55f), false);
            Label(root, leftName, 26, new Vector2(0f, 1f), new Vector2(40f, -70f), new Vector2(620f, 36f), TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f));
            Label(root, rightName, 26, new Vector2(1f, 1f), new Vector2(-40f, -70f), new Vector2(620f, 36f), TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.7f));
            if (!training)
                _timeFill = Bar(root, new Vector2(0.5f, 1f), new Vector2(-200f, -20f), new Vector2(400f, 6f), new Color(1f, 1f, 1f, 0.4f), true);

            _hint = Label(root, "", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1400f, 60f), TextAnchor.MiddleCenter, Color.white);
            _big = Label(root, "", 72, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1600f, 110f), TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.45f));
            _big.fontStyle = FontStyle.Bold;

            if (training)
            {
                _lessonText = Label(root, "", 34, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1400f, 50f), TextAnchor.MiddleCenter, Color.white);
                _dots = new Image[10];
                for (int i = 0; i < _dots.Length; i++)
                    _dots[i] = Box(root, new Vector2(0.5f, 0f), new Vector2(-225f + i * 50f, 95f), new Vector2(30f, 30f), new Color(1f, 1f, 1f, 0.25f));
                _ghostRoot = new GameObject("Ghost", typeof(RectTransform)).GetComponent<RectTransform>();
                _ghostRoot.SetParent(root, false);
                Place(_ghostRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(360f, 260f));
            }
            _button = Button(root, training ? "Xong" : "Bỏ cuộc", new Vector2(1f, 0f), new Vector2(-150f, 70f), new Vector2(220f, 80f), onButton);
        }

        /// <summary>Chạm này rơi vào nút (để không tính là vuốt).</summary>
        public bool IsOverButton(Vector2 screen) =>
            (_button != null && RectTransformUtility.RectangleContainsScreenPoint(_button, screen, null)) || _result != null;

        public void SetHp(float left01, float right01)
        {
            SetFill(_hpFill0, left01);
            SetFill(_hpFill1, right01);
        }

        public void SetTime(float left01)
        {
            if (_timeFill != null) SetFill(_timeFill, left01);
        }

        /// <summary>Huấn luyện: chiêu đang học + tiến độ. skill = -1: đã thuộc cả 4 (tập tự do).</summary>
        public void SetLesson(int skill, int progress, int needed, bool slowMo)
        {
            if (_lessonText == null) return;
            _lessonText.text = skill >= 0 ? "Đang học: " + SkillNames[skill] + "  " + Mathf.Min(progress, needed) + "/" + needed
                                          : "Đã thuộc cả 4 chiêu — tập tự do";
            for (int i = 0; i < _dots.Length; i++)
                _dots[i].color = skill >= 0 && i < progress ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(1f, 1f, 1f, 0.25f);
            ShowGhost(slowMo ? 4 : skill);   // hình nộm đang vung đòn: chỉ cách vuốt xuống để đỡ
        }

        public void Hint(string text, float seconds = 1.6f)
        {
            _hint.text = text;
            _hintT = seconds;
        }

        public void Big(string text, float seconds = 1.6f)
        {
            _big.text = text;
            _bigT = seconds;
        }

        /// <summary>Vết ngón tay khi đang vuốt.</summary>
        public void TrailAt(Vector2 screen)
        {
            if (_canvas == null) return;
            Image dot = Box(transform, Vector2.zero, Vector2.zero, new Vector2(18f, 18f), new Color(1f, 1f, 1f, 0.8f));
            var rt = (RectTransform)dot.transform;
            float k = _canvas.scaleFactor > 0f ? 1f / _canvas.scaleFactor : 1f;
            rt.anchoredPosition = screen * k;
            _trail.Add((rt, 0.35f));
        }

        public void ShowResult(string title, string body, Action onClose)
        {
            if (_result != null) return;
            RectTransform dim = Panel(transform, "Result", new Color(0f, 0f, 0f, 0.5f));
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            RectTransform card = Panel(dim, "Card", new Color(0.98f, 0.97f, 0.94f, 1f));
            Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
            Text t = Label(card, title, 72, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(860f, 110f), TextAnchor.MiddleCenter, new Color(0.25f, 0.2f, 0.2f));
            t.fontStyle = FontStyle.Bold;
            Label(card, body, 36, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(820f, 220f), TextAnchor.MiddleCenter, new Color(0.3f, 0.28f, 0.3f));
            Button(card, "Về vườn", new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(380f, 110f), onClose);
            _result = dim.gameObject;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_hintT > 0f && (_hintT -= dt) <= 0f) _hint.text = "";
            if (_bigT > 0f && (_bigT -= dt) <= 0f) _big.text = "";
            for (int i = _trail.Count - 1; i >= 0; i--)
            {
                (RectTransform rt, float t) = _trail[i];
                t -= dt;
                if (t <= 0f)
                {
                    Destroy(rt.gameObject);
                    _trail.RemoveAt(i);
                    continue;
                }
                rt.localScale = Vector3.one * (t / 0.35f);
                _trail[i] = (rt, t);
            }
            if (_ghostRunner != null && _ghostSkill >= 0)   // chấm sáng chạy theo đường bóng mờ
            {
                Vector2[] path = Ghosts[_ghostSkill];
                float u = (Time.unscaledTime * 0.7f) % 1f;
                _ghostRunner.anchoredPosition = PointOnPath(path, u);
            }
        }

        private void ShowGhost(int skill)
        {
            if (_ghostRoot == null || skill == _ghostSkill) return;
            _ghostSkill = skill;
            foreach (RectTransform d in _ghostDots) Destroy(d.gameObject);
            _ghostDots.Clear();
            if (_ghostRunner != null) Destroy(_ghostRunner.gameObject);
            _ghostRunner = null;
            if (skill < 0) return;
            Vector2[] path = Ghosts[skill];
            Color c = skill == 4 ? new Color(0.6f, 0.85f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.35f);
            for (int i = 0; i <= 24; i++)
            {
                Image dot = Box(_ghostRoot, new Vector2(0.5f, 0.5f), PointOnPath(path, i / 24f), new Vector2(16f, 16f), c);
                _ghostDots.Add((RectTransform)dot.transform);
            }
            _ghostRunner = (RectTransform)Box(_ghostRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f),
                                              skill == 4 ? new Color(0.6f, 0.85f, 1f, 0.95f) : new Color(1f, 0.9f, 0.4f, 0.95f)).transform;
        }

        /// <summary>Điểm ở vị trí u (0..1) theo chiều dài đường gấp khúc, đổi ra tọa độ trong khung bóng mờ.</summary>
        private Vector2 PointOnPath(Vector2[] path, float u)
        {
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
            float want = u * total;
            for (int i = 1; i < path.Length; i++)
            {
                float seg = Vector2.Distance(path[i - 1], path[i]);
                if (want <= seg || i == path.Length - 1)
                {
                    Vector2 p = Vector2.Lerp(path[i - 1], path[i], seg > 0f ? Mathf.Clamp01(want / seg) : 0f);
                    Vector2 size = _ghostRoot.sizeDelta;
                    return new Vector2((p.x - 0.5f) * size.x, (p.y - 0.5f) * size.y);
                }
                want -= seg;
            }
            return Vector2.zero;
        }

        // ------------------------------------------------------------------ dựng UI

        private static void SetFill(RectTransform fill, float v)
        {
            if (fill == null) return;
            Vector3 s = fill.localScale;
            s.x = Mathf.Clamp01(v);
            fill.localScale = s;
        }

        private RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, bool leftToRight)
        {
            RectTransform bg = Panel(parent, "Bar", new Color(0f, 0f, 0f, 0.3f));
            Place(bg, anchor, pos, size);
            bg.pivot = new Vector2(leftToRight ? 0f : 1f, 1f);
            bg.anchoredPosition = pos;
            RectTransform f = Panel(bg, "Fill", fill);
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = f.offsetMax = Vector2.zero;
            f.pivot = new Vector2(leftToRight ? 0f : 1f, 0.5f);   // co về phía đầu vạch
            return f;
        }

        private Image Box(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
        {
            RectTransform rt = Panel(parent, "Dot", c);
            Place(rt, anchor, pos, size);
            Image img = rt.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private RectTransform Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick)
        {
            RectTransform rt = Panel(parent, "Button", new Color(1f, 1f, 1f, 0.85f));
            Place(rt, anchor, pos, size);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            b.onClick.AddListener(() => onClick?.Invoke());
            Label(rt, label, 38, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, new Color(0.2f, 0.2f, 0.25f));
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
