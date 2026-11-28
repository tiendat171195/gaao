using System;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using PawFit.Pet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Giao diện tối thiểu dựng bằng code (uGUI, không cần prefab): bảng đặt tên khi trứng nở, thẻ chữ khi chạm bia mộ,
    /// thẻ lựa chọn (hình nộm, quầy hàng), thẻ tự cập nhật (quảng cáo giả lập), dòng báo nhanh (toast).
    /// Vườn vẫn không có nút thường trực — bảng chỉ hiện đúng lúc cần rồi tắt. Đang mở thì vườn bỏ qua chạm.
    /// </summary>
    public static class GardenUi
    {
        private static readonly Color[] ChoiceColors =
        {
            new Color(0.36f, 0.6f, 0.85f), new Color(0.85f, 0.45f, 0.36f), new Color(0.38f, 0.7f, 0.45f),
            new Color(0.62f, 0.45f, 0.8f), new Color(0.3f, 0.68f, 0.7f),
        };
        private static readonly Color CancelColor = new Color(0.6f, 0.6f, 0.62f);
        private static readonly Color DisabledColor = new Color(0.8f, 0.8f, 0.8f);

        private static Canvas _canvas;
        private static Font _font;
        private static GameObject _open;
        private static GameObject _toast;
        private static GameObject _tip;

        /// <summary>Đang có bảng mở (vườn không nhận chạm).</summary>
        public static bool IsOpen => _open != null;

        /// <summary>
        /// Hỏi tên thú mới (trứng vừa nở) — cũng dùng để gõ biệt danh / mã chuyển máy (đổi `placeholder`).
        /// `done` nhận chữ người chơi gõ (logic tự chuẩn hóa).
        /// </summary>
        public static void AskName(string title, Action<string> done, string placeholder = "Tên bé...")
        {
            Close();
            RectTransform dim = Panel(Root, "NamePrompt", new Color(0f, 0f, 0f, 0.45f));
            Sfx.Play(SoundId.UiOpen, 0.3f);
            Stretch(dim);
            RectTransform card = Panel(dim, "Card", new Color(1f, 0.98f, 0.95f, 1f));
            Place(card, Vector2.zero, new Vector2(880f, 560f));
            Label(card, title, 50, FontStyle.Bold, new Color(0.25f, 0.2f, 0.2f), new Vector2(0f, 170f), new Vector2(820f, 140f));
            InputField input = Input(card, placeholder, new Vector2(0f, 10f), new Vector2(720f, 120f));
            Button ok = Button(card, "Xong", new Color(0.36f, 0.73f, 0.45f), new Vector2(0f, -165f), new Vector2(380f, 120f));
            ok.onClick.AddListener(() =>
            {
                string name = input.text;
                Close();
                done?.Invoke(name);
            });
            _open = dim.gameObject;
            input.ActivateInputField();   // điện thoại: bật bàn phím luôn
        }

        /// <summary>
        /// Thẻ có vài lựa chọn (vd chạm hình nộm: Huấn luyện / Đấu / Thôi; quầy hàng: các món + quảng cáo + Thôi).
        /// Nút cuối luôn màu xám (Thôi). `enabled[i] == false` thì nút đó xám nhạt, không bấm được (vd thiếu Coin).
        /// Bấm nút nào thì thẻ đóng rồi `chosen` nhận chỉ số nút đó.
        /// </summary>
        public static void ShowChoice(string title, string body, string[] options, Action<int> chosen, bool[] enabled = null)
        {
            Close();
            int n = options.Length;
            bool many = n > 3;   // nhiều nút: nút rộng hơn, chữ nhỏ hơn cho vừa
            float step = many ? 112f : 120f, head = 330f, h = head + n * step + 30f, top = h * 0.5f;
            var buttonSize = new Vector2(many ? 780f : 560f, many ? 96f : 100f);
            RectTransform dim = Panel(Root, "Choice", new Color(0f, 0f, 0f, 0.35f));
            Sfx.Play(SoundId.UiOpen, 0.3f);
            Stretch(dim);
            RectTransform card = Panel(dim, "Box", new Color(0.97f, 0.97f, 0.95f, 1f));
            Place(card, Vector2.zero, new Vector2(900f, h));
            Label(card, title, 58, FontStyle.Bold, new Color(0.22f, 0.2f, 0.22f), new Vector2(0f, top - 85f), new Vector2(840f, 110f));
            Label(card, body, 36, FontStyle.Normal, new Color(0.3f, 0.28f, 0.3f), new Vector2(0f, top - 220f), new Vector2(820f, 150f));
            for (int i = 0; i < n; i++)
            {
                int index = i;
                bool on = enabled == null || i >= enabled.Length || enabled[i];
                Color color = !on ? DisabledColor : (i == n - 1 && n > 1 ? CancelColor : ChoiceColors[i % ChoiceColors.Length]);
                Button b = Button(card, options[i], color, new Vector2(0f, top - head - step * 0.5f - i * step), buttonSize, many ? 38 : 48);
                b.interactable = on;
                if (!on)
                {
                    b.GetComponentInChildren<Text>().color = new Color(0.42f, 0.42f, 0.44f);   // chữ xám đậm cho dễ đọc trên nền xám
                    continue;
                }
                b.onClick.AddListener(() =>
                {
                    Close();
                    chosen?.Invoke(index);
                });
            }
            _open = dim.gameObject;
        }

        /// <summary>
        /// Thẻ phủ kín màn hình, chữ tự cập nhật mỗi frame (vd quảng cáo giả lập đếm ngược) + 1 nút (null = không có nút).
        /// Không tự đóng khi bấm nút: bên gọi tự <see cref="Close"/> khi xong việc.
        /// </summary>
        public static void ShowLive(string title, Func<string> body, string button, Action pressed) =>
            ShowLive(title, body, button != null ? new[] { button } : null, i => pressed?.Invoke());

        /// <summary>Như trên nhưng nhiều nút (xếp dọc, nút cuối màu xám). `pressed` nhận chỉ số nút; không tự đóng thẻ.</summary>
        public static void ShowLive(string title, Func<string> body, string[] buttons, Action<int> pressed)
        {
            Close();
            RectTransform dim = Panel(Root, "Live", new Color(0.06f, 0.06f, 0.09f, 0.94f));
            Sfx.Play(SoundId.UiOpen, 0.3f);
            Stretch(dim);
            Label(dim, title, 64, FontStyle.Bold, Color.white, new Vector2(0f, 320f), new Vector2(940f, 140f));
            Text text = Label(dim, body != null ? body() : "", 44, FontStyle.Normal, new Color(0.88f, 0.88f, 0.9f),
                              new Vector2(0f, 60f), new Vector2(940f, 300f));
            GameObject self = dim.gameObject;
            if (body != null)
                self.AddComponent<UiTicker>().Tick = () =>
                {
                    if (_open == self) text.text = body();   // body() có thể tự mở thẻ khác (vd vừa tìm được trận)
                };
            int n = buttons?.Length ?? 0;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                Color color = i == n - 1 ? new Color(0.42f, 0.42f, 0.48f) : ChoiceColors[i % ChoiceColors.Length];
                Button b = Button(dim, buttons[i], color, new Vector2(0f, -320f + (n - 1 - i) * 130f), new Vector2(760f, 110f), 40);
                b.onClick.AddListener(() => pressed?.Invoke(index));
            }
            _open = self;
        }

        /// <summary>Mẹo hướng dẫn ở phía dưới màn hình (nền vàng nhạt), hiện dần rồi tự tắt sau `seconds`. Không chặn chạm.</summary>
        public static void Tip(string text, float seconds = 7f)
        {
            if (_tip != null) UnityEngine.Object.Destroy(_tip);
            RectTransform box = Panel(Root, "Tip", new Color(1f, 0.96f, 0.82f, 0.96f));
            box.GetComponent<Image>().raycastTarget = false;
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0f);
            box.anchoredPosition = new Vector2(0f, 140f);
            box.sizeDelta = new Vector2(960f, 180f);
            Label(box, "Mẹo", 30, FontStyle.Bold, new Color(0.6f, 0.42f, 0.1f), new Vector2(0f, 58f), new Vector2(900f, 40f));
            Label(box, text, 36, FontStyle.Normal, new Color(0.25f, 0.2f, 0.15f), new Vector2(0f, -16f), new Vector2(900f, 120f));
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            float start = Time.unscaledTime, life = Mathf.Max(1.5f, seconds);
            GameObject go = box.gameObject;
            go.AddComponent<UiTicker>().Tick = () =>
            {
                float t = Time.unscaledTime - start;
                group.alpha = Mathf.Min(Mathf.Clamp01(t / 0.35f), Mathf.Clamp01((life - t) / 0.6f));
                if (t >= life) UnityEngine.Object.Destroy(go);
            };
            _tip = go;
        }

        /// <summary>Dòng báo nhanh ở phía trên màn hình (vd "-25 Coin"), tự mờ dần rồi tắt. Không chặn chạm.</summary>
        public static void Toast(string text, float seconds = 2.4f)
        {
            if (_toast != null) UnityEngine.Object.Destroy(_toast);
            RectTransform box = Panel(Root, "Toast", new Color(0.16f, 0.15f, 0.18f, 0.88f));
            box.GetComponent<Image>().raycastTarget = false;
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -170f);
            box.sizeDelta = new Vector2(920f, 120f);
            Label(box, text, 40, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(880f, 110f));
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            float start = Time.unscaledTime, life = Mathf.Max(0.5f, seconds);
            GameObject go = box.gameObject;
            go.AddComponent<UiTicker>().Tick = () =>
            {
                float k = (Time.unscaledTime - start) / life;
                group.alpha = k < 0.75f ? 1f : Mathf.Clamp01((1f - k) / 0.25f);
                box.SetAsLastSibling();   // luôn nằm trên thẻ mở sau nó
                if (k >= 1f) UnityEngine.Object.Destroy(go);
            };
            _toast = go;
        }

        /// <summary>Thẻ chữ to (vd thông tin bia mộ). Chạm bất kỳ đâu để đóng.</summary>
        public static void ShowCard(string title, string body)
        {
            Close();
            RectTransform dim = Panel(Root, "Card", new Color(0f, 0f, 0f, 0.35f));
            Sfx.Play(SoundId.UiOpen, 0.3f);
            Stretch(dim);
            Button closer = dim.gameObject.AddComponent<Button>();
            closer.targetGraphic = dim.GetComponent<Image>();
            closer.onClick.AddListener(Close);
            RectTransform card = Panel(dim, "Box", new Color(0.97f, 0.97f, 0.95f, 1f));
            Place(card, Vector2.zero, new Vector2(880f, 620f));
            Label(card, title, 62, FontStyle.Bold, new Color(0.22f, 0.2f, 0.22f), new Vector2(0f, 205f), new Vector2(820f, 120f));
            Label(card, body, 40, FontStyle.Normal, new Color(0.3f, 0.28f, 0.3f), new Vector2(0f, -10f), new Vector2(820f, 300f));
            Label(card, "(chạm để đóng)", 30, FontStyle.Italic, new Color(0.55f, 0.55f, 0.55f), new Vector2(0f, -250f), new Vector2(820f, 60f));
            _open = dim.gameObject;
        }

        public static void Close()
        {
            if (_open != null) UnityEngine.Object.Destroy(_open);
            _open = null;
        }

        // ------------------------------------------------------------------

        private static Transform Root
        {
            get
            {
                if (_canvas == null)
                {
                    var go = new GameObject("PawFit_UI");
                    _canvas = go.AddComponent<Canvas>();
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    _canvas.sortingOrder = 50;
                    var scaler = go.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080f, 1920f);
                    scaler.matchWidthOrHeight = 0.5f;
                    go.AddComponent<GraphicRaycaster>();
                }
                if (EventSystem.current == null && UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                {
                    var es = new GameObject("EventSystem");
                    es.AddComponent<EventSystem>();
                    es.AddComponent<StandaloneInputModule>();
                }
                return _canvas.transform;
            }
        }

        private static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        private static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Text Label(Transform parent, string text, int size, FontStyle style, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, pos, box);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static InputField Input(Transform parent, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("NameInput", typeof(RectTransform));
            go.SetActive(false);   // gắn đủ thành phần rồi mới bật (InputField cần textComponent lúc bật)
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, pos, size);
            Image bg = go.AddComponent<Image>();
            bg.color = Color.white;
            Text text = Label(go.transform, "", 52, FontStyle.Normal, new Color(0.15f, 0.15f, 0.15f), Vector2.zero, size - new Vector2(40f, 10f));
            text.supportRichText = false;
            Text hint = Label(go.transform, placeholder, 48, FontStyle.Italic, new Color(0.6f, 0.6f, 0.6f), Vector2.zero, size - new Vector2(40f, 10f));
            var input = go.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = PetNames.MaxLength;
            input.lineType = InputField.LineType.SingleLine;
            go.SetActive(true);
            return input;
        }

        private static Button Button(Transform parent, string label, Color color, Vector2 pos, Vector2 size, int fontSize = 48)
        {
            RectTransform rt = Panel(parent, "Button", color);
            Place(rt, pos, size);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            b.onClick.AddListener(() => Sfx.Play(SoundId.UiTap, 0.3f));   // mọi nút có tiếng "tách"
            Label(rt, label, fontSize, FontStyle.Bold, Color.white, Vector2.zero, size - new Vector2(30f, 0f));
            return b;
        }
    }

    /// <summary>Gọi 1 hàm mỗi frame (chữ tự cập nhật / toast mờ dần của GardenUi). Tự hết khi GameObject bị hủy.</summary>
    public sealed class UiTicker : MonoBehaviour
    {
        public Action Tick;

        private void Update() => Tick?.Invoke();
    }
}
