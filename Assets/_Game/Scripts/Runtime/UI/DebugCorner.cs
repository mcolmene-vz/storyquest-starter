using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Runtime
{
    /// Invisible 170 x 170 button in the top-right of the safe area (ARCHITECTURE §14.5): three taps within a second
    /// flip DebugSettings.Current and show a 1.5 s toast. Removed in M3 with DebugSettings. No serialized fields.
    public sealed class DebugCorner : MonoBehaviour
    {
        const float TapWindowSeconds = 1f;
        const float ToastSeconds = 1.5f;
        const int TapsToFlip = 3;
        static readonly Vector2 ToastSize = new Vector2(760f, 120f);

        readonly float[] tapTimes = new float[TapsToFlip];
        int tapCount;
        RectTransform toast = null!;
        TMP_Text toastText = null!;
        Coroutine? hideToast;

        public static DebugCorner Create(Transform safeArea)
        {
            var corner = new GameObject("DebugCorner", typeof(RectTransform)).AddComponent<DebugCorner>();
            var rect = (RectTransform)corner.transform;
            rect.SetParent(safeArea, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(UiFactory.MinTapSize, UiFactory.MinTapSize);

            var button = UiFactory.Button(rect, "", new Vector2(UiFactory.MinTapSize, UiFactory.MinTapSize), corner.OnTap);
            button.Place(new Vector2(1f, 1f), Vector2.zero);
            // Alpha 0 still receives raycasts; the Button's tints multiply it, so it never shows.
            button.Image.color = Color.clear;

            corner.toast = UiFactory.Panel(safeArea, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), ToastSize, UiFactory.PanelColour);
            corner.toast.name = "Toast";
            corner.toast.anchoredPosition = new Vector2(0f, -200f);
            corner.toastText = UiFactory.Label(corner.toast, "", 40f, TextAlignmentOptions.Center);
            corner.toast.gameObject.SetActive(false);
            return corner;
        }

        void OnTap()
        {
            float now = Time.unscaledTime;
            if (tapCount < TapsToFlip)
            {
                tapTimes[tapCount++] = now;
            }
            else
            {
                tapTimes[0] = tapTimes[1];
                tapTimes[1] = tapTimes[2];
                tapTimes[2] = now;
            }
            if (tapCount < TapsToFlip || now - tapTimes[0] > TapWindowSeconds)
                return;

            tapCount = 0;
            DebugSettings.Current = DebugSettings.Current == DebugSettings.Difficulty.AlwaysWin
                ? DebugSettings.Difficulty.Normal
                : DebugSettings.Difficulty.AlwaysWin;
            Debug.Log("[DebugCorner] difficulty -> " + DebugSettings.Current);
            ShowToast("Difficulty: " + (DebugSettings.Current == DebugSettings.Difficulty.AlwaysWin ? "Always Win" : "Normal"));
        }

        void ShowToast(string text)
        {
            toastText.text = text;
            toast.gameObject.SetActive(true);
            if (hideToast != null)
                StopCoroutine(hideToast);
            hideToast = StartCoroutine(HideToastAfter(ToastSeconds));
        }

        IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            toast.gameObject.SetActive(false);
            hideToast = null;
        }

        void OnDestroy()
        {
            if (toast != null)
                Destroy(toast.gameObject);
        }
    }
}
