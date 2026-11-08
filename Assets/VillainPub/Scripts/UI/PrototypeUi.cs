using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VillainPub.UI
{
    /// <summary>
    /// 프로토타입 화면(영업·전투)이 같이 쓰는 코드 UI 생성 도구. 사각형과 텍스트만 만든다.
    /// 정식 UI로 넘어가면 프리팹으로 대체하고 지운다.
    /// </summary>
    public sealed class PrototypeUi
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public static readonly Color BackgroundColor = new Color(0.12f, 0.1f, 0.14f);
        public static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.85f);
        public static readonly Color ButtonColor = new Color(0.3f, 0.3f, 0.36f);
        public static readonly Color AcceptColor = new Color(0.2f, 0.5f, 0.3f);

        private readonly TMP_FontAsset _font;

        public PrototypeUi(TMP_FontAsset font)
        {
            _font = font;
        }

        // 씬에 EventSystem이 없으면 새 Input System용으로 만든다(없으면 버튼이 클릭되지 않는다).
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static Canvas CreateCanvas(Transform parent, string objectName, int sortingOrder = 0)
        {
            var canvasObject = new GameObject(objectName, typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, string objectName, Color color)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform rect, RectOffset padding, float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static RectTransform CreateRow(Transform parent, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public TextMeshProUGUI CreateText(Transform parent, float fontSize)
        {
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        public Button CreateButton(Transform parent, string label, Action onClick, Color color,
            float width = 200f, float height = 72f, float fontSize = 30f)
        {
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            TextMeshProUGUI text = CreateText(buttonObject.transform, fontSize);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            return button;
        }

        public GameObject CreateOverlay(Transform parent, string objectName, out TextMeshProUGUI body,
            string buttonLabel, Action onButton, float fontSize = 34f)
        {
            RectTransform panel = CreatePanel(parent, objectName, OverlayColor);
            VerticalLayoutGroup layout = AddVerticalLayout(panel, new RectOffset(120, 120, 80, 80), 32f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;

            body = CreateText(panel, fontSize);
            body.alignment = TextAlignmentOptions.Center;
            CreateButton(panel, buttonLabel, onButton, ButtonColor);

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }
    }
}
