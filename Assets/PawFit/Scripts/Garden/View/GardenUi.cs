using System;
using PawFit.Pet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Giao diện tối thiểu dựng bằng code (uGUI, không cần prefab): bảng đặt tên khi trứng nở, thẻ chữ khi chạm bia mộ.
    /// Vườn vẫn không có nút thường trực — bảng chỉ hiện đúng lúc cần rồi tắt. Đang mở thì vườn bỏ qua chạm.
    /// </summary>
    public static class GardenUi
    {
        private static Canvas _canvas;
        private static Font _font;
        private static GameObject _open;

        /// <summary>Đang có bảng mở (vườn không nhận chạm).</summary>
        public static bool IsOpen => _open != null;

        /// <summary>Hỏi tên thú mới (trứng vừa nở). `done` nhận tên người chơi gõ (logic tự chuẩn hóa).</summary>
        public static void AskName(string title, Action<string> done)
        {
            Close();
            RectTransform dim = Panel(Root, "NamePrompt", new Color(0f, 0f, 0f, 0.45f));
            Stretch(dim);
            RectTransform card = Panel(dim, "Card", new Color(1f, 0.98f, 0.95f, 1f));
            Place(card, Vector2.zero, new Vector2(880f, 560f));
            Label(card, title, 50, FontStyle.Bold, new Color(0.25f, 0.2f, 0.2f), new Vector2(0f, 170f), new Vector2(820f, 140f));
            InputField input = Input(card, "Tên bé...", new Vector2(0f, 10f), new Vector2(720f, 120f));
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

        /// <summary>Thẻ có vài lựa chọn (vd chạm hình nộm: Huấn luyện / Đấu / Thôi). `chosen` nhận chỉ số nút.</summary>
        public static void ShowChoice(string title, string body, string[] options, Action<int> chosen)
        {
            Close();
            RectTransform dim = Panel(Root, "Choice", new Color(0f, 0f, 0f, 0.35f));
            Stretch(dim);
            RectTransform card = Panel(dim, "Box", new Color(0.97f, 0.97f, 0.95f, 1f));
            Place(card, Vector2.zero, new Vector2(900f, 640f));
            Label(card, title, 58, FontStyle.Bold, new Color(0.22f, 0.2f, 0.22f), new Vector2(0f, 220f), new Vector2(840f, 110f));
            Label(card, body, 38, FontStyle.Normal, new Color(0.3f, 0.28f, 0.3f), new Vector2(0f, 90f), new Vector2(820f, 150f));
            Color[] colors = { new Color(0.36f, 0.6f, 0.85f), new Color(0.85f, 0.45f, 0.36f), new Color(0.6f, 0.6f, 0.62f) };
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button b = Button(card, options[i], colors[i % colors.Length], new Vector2(0f, -60f - i * 120f), new Vector2(560f, 100f));
                b.onClick.AddListener(() =>
                {
                    Close();
                    chosen?.Invoke(index);
                });
            }
            card.sizeDelta = new Vector2(900f, 380f + options.Length * 120f);
            _open = dim.gameObject;
        }

        /// <summary>Thẻ chữ to (vd thông tin bia mộ). Chạm bất kỳ đâu để đóng.</summary>
        public static void ShowCard(string title, string body)
        {
            Close();
            RectTransform dim = Panel(Root, "Card", new Color(0f, 0f, 0f, 0.35f));
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

        private static Button Button(Transform parent, string label, Color color, Vector2 pos, Vector2 size)
        {
            RectTransform rt = Panel(parent, "Button", color);
            Place(rt, pos, size);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = rt.GetComponent<Image>();
            Label(rt, label, 48, FontStyle.Bold, Color.white, Vector2.zero, size);
            return b;
        }
    }
}
