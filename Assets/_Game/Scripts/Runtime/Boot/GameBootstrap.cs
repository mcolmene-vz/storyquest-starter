using TMPro;
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
            CreatePlaceholder();
            CreateUI();
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

        void CreatePlaceholder()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Placeholder";
            cube.transform.SetParent(transform, false);
            cube.transform.position = Vector3.zero;

            // No physics on characters; the habit starts with the placeholder.
            var collider = cube.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            // A committed material is what carries the shader into the build; looking the shader up by name at runtime renders pink on device.
            var material = Resources.Load<Material>("Materials/Placeholder");
            if (material == null)
                Debug.LogError("[GameBootstrap] Resources/Materials/Placeholder.mat is missing; run Game.Editor.ProjectSetup.Run");
            else
                cube.GetComponent<Renderer>().sharedMaterial = material;

            cube.AddComponent<Spinner>().DegreesPerSecond = 90f;
        }

        void CreateUI()
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

            var labelRect = new GameObject("TapLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            labelRect.SetParent(safeArea, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(24f, -24f);
            labelRect.sizeDelta = new Vector2(1600f, 90f);

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 64f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;

            labelRect.gameObject.AddComponent<TapCounter>().Bind(label);
        }
    }
}
