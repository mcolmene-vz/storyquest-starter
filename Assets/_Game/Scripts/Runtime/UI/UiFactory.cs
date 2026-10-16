using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime
{
    /// The only place that creates uGUI objects (ARCHITECTURE §14.5): flat colours, the TMP default font, and the
    /// 170 reference-px minimum for anything tappable (64 pt on the worst case, an iPhone 11 at canvas scale 0.846).
    public static class UiFactory
    {
        public const float MinTapSize = 170f;

        public static readonly Color PanelColour = new Color(0.08f, 0.10f, 0.16f, 0.82f);
        public static readonly Color ButtonColour = new Color(0.16f, 0.36f, 0.62f, 1f);
        public static readonly Color HighlightColour = new Color(0.95f, 0.72f, 0.12f, 1f);
        public static readonly Color BarBackColour = new Color(0.05f, 0.05f, 0.08f, 1f);
        public static readonly Color BarFillColour = new Color(0.28f, 0.78f, 0.32f, 1f);
        public static readonly Color TextColour = Color.white;

        /// Equal anchors make a fixed-size panel pivoted on that corner; different anchors stretch that axis and
        /// ignore the size on it.
        public static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color colour)
        {
            var rect = Rect("Panel", parent, anchorMin, anchorMax, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            // A clear panel is a layout container; it must not swallow taps meant for the world or for siblings.
            image.raycastTarget = colour.a > 0f;
            return rect;
        }

        public static UiButton Button(Transform parent, string label, Vector2 size, Action onClick)
        {
            if (size.x < MinTapSize || size.y < MinTapSize)
                throw new ArgumentOutOfRangeException(nameof(size), size, "tappable controls are at least " + MinTapSize + " x " + MinTapSize + " reference px (§14.5)");

            var rect = Rect("Button", parent, Vector2.zero, Vector2.zero, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColour;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = Color.white;
            colours.selectedColor = Color.white;
            colours.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colours.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colours.fadeDuration = 0.05f;
            button.colors = colours;
            button.onClick.AddListener(() => onClick());

            var text = Label(rect, label, 40f, TextAlignmentOptions.Center);
            return new UiButton(rect, image, button, text);
        }

        /// Stretched over its parent; callers shrink the offsets for margins.
        public static TMP_Text Label(Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var rect = Rect("Label", parent, Vector2.zero, Vector2.one, Vector2.zero);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = TextColour;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.text = text;
            return tmp;
        }

        public static UiBar Bar(Transform parent, Vector2 size)
        {
            var back = Rect("Bar", parent, Vector2.zero, Vector2.zero, size);
            var backImage = back.gameObject.AddComponent<Image>();
            backImage.color = BarBackColour;
            backImage.raycastTarget = false;

            // No sprites in M1, and a sprite-less Image ignores its fill type, so the fill is an anchored child.
            var fill = Rect("Fill", back, Vector2.zero, Vector2.one, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = BarFillColour;
            fillImage.raycastTarget = false;
            return new UiBar(back, fill, fillImage);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                Mathf.Approximately(anchorMin.x, anchorMax.x) ? size.x : 0f,
                Mathf.Approximately(anchorMin.y, anchorMax.y) ? size.y : 0f);
            return rect;
        }
    }

    public sealed class UiButton
    {
        public UiButton(RectTransform rect, Image image, Button button, TMP_Text label)
        {
            Rect = rect;
            Image = image;
            Button = button;
            Label = label;
        }

        public RectTransform Rect { get; }
        public Image Image { get; }
        public Button Button { get; }
        public TMP_Text Label { get; }

        public bool Interactable
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }

        public void SetText(string text) => Label.text = text;

        /// Anchors and pivot together, so anchoredPosition is always an inset from that corner.
        public void Place(Vector2 anchor, Vector2 inset)
        {
            Rect.anchorMin = anchor;
            Rect.anchorMax = anchor;
            Rect.pivot = anchor;
            Rect.anchoredPosition = inset;
        }
    }

    public sealed class UiBar
    {
        float value = 1f;

        public UiBar(RectTransform rect, RectTransform fill, Image fillImage)
        {
            Rect = rect;
            FillRect = fill;
            Fill = fillImage;
        }

        public RectTransform Rect { get; }
        public RectTransform FillRect { get; }
        public Image Fill { get; }

        /// 0..1 of the bar's width.
        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp01(value);
                FillRect.anchorMax = new Vector2(this.value, 1f);
            }
        }
    }
}
