using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Runtime
{
    /// The only component in Boot.unity. It has no serialized fields so Unity never has a reason
    /// to rewrite the scene (ARCHITECTURE §13.4); everything comes from code and Resources.
    public sealed class GameBootstrap : MonoBehaviour
    {
        FlowController? flow;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            // iPhone 11 is 60 Hz; ProMotion iPads would otherwise burn battery at 120.
            Application.targetFrameRate = 60;
            // Narration plays longer than iOS auto-lock allows between taps.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            TouchSimulation.Enable();
#endif
            CreateCamera();
            CreateLight();
            var safeArea = CreateUI();

            // §15.9: services once, then the flow opens the camp; battles start from its Next battle.
            Services.Create(gameObject);
            flow = new FlowController(this, safeArea);
            flow.Go(FlowState.Camp);
        }

        void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 1.5f, -5f);
            go.transform.LookAt(Vector3.zero);

            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.53f, 0.78f, 0.92f, 1f);
            // Without a listener Unity warns every frame; AudioService (M1) plays through this one.
            go.AddComponent<AudioListener>();
            // BattleCameras adds the CinemachineBrain when the battle view shows.
        }

        void CreateLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Hard;
        }

        RectTransform CreateUI()
        {
            var canvasGo = new GameObject("UI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.transform.SetParent(transform, false);
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var safeArea = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safeArea.SetParent(canvasGo.transform, false);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            return safeArea;
        }
    }
}
