using TMPro;
using UnityEngine;
using Finger = UnityEngine.InputSystem.EnhancedTouch.Finger;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Game.Runtime
{
    /// M0 placeholder only; deleted in M1. Counts any finger down anywhere and shows the
    /// resolution and safe-area readout that is the evidence for the two-aspect check (§13.4).
    public sealed class TapCounter : MonoBehaviour
    {
        TMP_Text? _label;
        int _count;
        int _shownCount = -1;
        int _shownWidth;
        int _shownHeight;
        Rect _shownSafeArea;

        public void Bind(TMP_Text label)
        {
            _label = label;
            _shownCount = -1;
        }

        void OnEnable() => Touch.onFingerDown += OnFingerDown;

        void OnDisable() => Touch.onFingerDown -= OnFingerDown;

        void OnFingerDown(Finger finger) => _count++;

        void Update()
        {
            if (_label == null)
                return;

            var safeArea = Screen.safeArea;
            if (_count == _shownCount && Screen.width == _shownWidth && Screen.height == _shownHeight && safeArea == _shownSafeArea)
                return;

            _shownCount = _count;
            _shownWidth = Screen.width;
            _shownHeight = Screen.height;
            _shownSafeArea = safeArea;
            _label.text = $"Taps: {_count}  {Screen.width}×{Screen.height}  safe {safeArea}";
        }
    }
}
