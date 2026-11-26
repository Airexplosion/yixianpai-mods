using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk.Unity;

namespace YxCanju
{
    /// <summary>独立文字按钮；不继承原生图标、贴图、动画或文字布局。</summary>
    static class SolverButtonStyle
    {
        public static TMP_FontAsset FontFrom(Transform source)
        {
            if (source == null) return null;
            var label = source.GetComponentInChildren(typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
            return label == null ? null : label.font;
        }

        public static UiButton Create(UiKit ui, Transform parent, string name, string text, Action onClick,
            Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font, bool primary = false)
        {
            UiButton handle = ui.TextButton(parent, name, text, Vector2.zero, size, onClick);
            if (handle == null || !handle.IsAlive) return null;
            GameObject root = handle.GameObject;
            var rt = root.GetComponent(typeof(RectTransform)) as RectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one;
            rt.sizeDelta = size;

            var image = root.GetComponent(typeof(Image)) as Image;
            image.color = primary ? new Color(0.17f, 0.33f, 0.30f, 1f) : new Color(0.15f, 0.19f, 0.19f, 1f);
            image.raycastTarget = true;
            var border = root.AddComponent(typeof(Outline)) as Outline;
            border.effectColor = new Color(0.63f, 0.53f, 0.34f, 1f);
            border.effectDistance = new Vector2(1f, -1f);
            border.useGraphicAlpha = true;

            var button = root.GetComponent(typeof(Button)) as Button;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            var label = root.GetComponentInChildren(typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
            if (label != null)
            {
                if (font != null)
                {
                    label.font = font;
                    label.fontSharedMaterial = font.material;
                }
                label.fontStyle = FontStyles.Normal;
                label.color = new Color(0.98f, 0.96f, 0.88f, 1f);
                label.enableVertexGradient = false;
                label.outlineWidth = 0f;
                label.fontSize = fontSize;
                label.enableAutoSizing = true;
                label.fontSizeMin = fontSize * 0.85f;
                label.fontSizeMax = fontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.raycastTarget = false;
                label.extraPadding = true;
                RectTransform textRt = label.rectTransform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.localScale = Vector3.one;
                textRt.offsetMin = new Vector2(12f, 6f);
                textRt.offsetMax = new Vector2(-12f, -6f);
            }
            return handle;
        }
    }
}
