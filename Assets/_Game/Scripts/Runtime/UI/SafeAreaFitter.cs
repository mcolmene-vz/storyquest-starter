using UnityEngine;

namespace Game.Runtime
{
    /// Keeps a full-stretch RectTransform inside Screen.safeArea (ARCHITECTURE §9, §13.4).
    /// Every UI root gets one; it stays for all of V1.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform? _rect;
        Rect _appliedSafeArea;
        int _appliedWidth;
        int _appliedHeight;
        ScreenOrientation _appliedOrientation;

        RectTransform Rect => _rect ??= (RectTransform)transform;

        void Awake() => Apply();

        void OnEnable() => Apply();

        void Update()
        {
            // Safe area and orientation change without a callback, so compare every frame.
            if (Screen.safeArea != _appliedSafeArea
                || Screen.width != _appliedWidth
                || Screen.height != _appliedHeight
                || Screen.orientation != _appliedOrientation)
                Apply();
        }

        public void Apply()
        {
            float width = Screen.width;
            float height = Screen.height;
            if (width <= 0f || height <= 0f)
                return;

            var safe = Screen.safeArea;
            var rect = Rect;
            rect.anchorMin = new Vector2(safe.x / width, safe.y / height);
            rect.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _appliedSafeArea = safe;
            _appliedWidth = Screen.width;
            _appliedHeight = Screen.height;
            _appliedOrientation = Screen.orientation;
        }
    }
}
