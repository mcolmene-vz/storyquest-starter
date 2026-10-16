using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// Full-screen panel inside the safe area with a title, body lines and one Continue button (ARCHITECTURE §14.5).
    /// The caller decides the words: Victory, Always-Win defeat or Normal defeat.
    public sealed class ResultView
    {
        static readonly Vector2 ContinueSize = new Vector2(300f, 180f);
        static readonly Color Backdrop = new Color(0.05f, 0.06f, 0.10f, 0.88f);
        const float ContinueInset = 60f;

        RectTransform? root;

        public void Show(Transform uiRoot, string title, IReadOnlyList<string> lines, Action onContinue)
        {
            Hide();
            root = UiFactory.Panel(uiRoot, Vector2.zero, Vector2.one, Vector2.zero, Backdrop);
            root.name = "ResultView";

            var titleLabel = UiFactory.Label(root, title, 96f, TextAlignmentOptions.Center);
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleLabel.rectTransform.offsetMin = new Vector2(0f, -260f);
            titleLabel.rectTransform.offsetMax = new Vector2(0f, -80f);

            var body = UiFactory.Label(root, string.Join("\n", lines), 48f, TextAlignmentOptions.Center);
            body.rectTransform.offsetMin = new Vector2(0f, ContinueInset + ContinueSize.y);
            body.rectTransform.offsetMax = new Vector2(0f, -260f);

            var button = UiFactory.Button(root, "Continue", ContinueSize, () => { Services.Audio.Play(SfxId.MenuTap); onContinue(); });
            button.Place(new Vector2(0.5f, 0f), new Vector2(0f, ContinueInset));
        }

        public void Hide()
        {
            if (root != null)
                Object.Destroy(root.gameObject);
            root = null;
        }
    }
}
