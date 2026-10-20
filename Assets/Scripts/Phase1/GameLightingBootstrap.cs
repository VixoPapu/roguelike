using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ProjectLike.Phase1
{
    public sealed class GameLightingBootstrap : MonoBehaviour
    {
        static GameLightingBootstrap instance;
        Volume volume;
        Bloom bloom;
        ColorAdjustments colorAdjustments;
        Transform dimLayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var root = new GameObject("GameLighting");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameLightingBootstrap>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(.72f);
            colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(-.12f);
            colorAdjustments.contrast.Override(2f);
            volume.sharedProfile = profile;

            EnsureDimLayer();
        }

        void EnsureDimLayer()
        {
            if (dimLayer) return;
            var existing = transform.Find("WorldDimOverlay");
            if (existing) { dimLayer = existing; return; }
            var layer = new GameObject("WorldDimOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            dimLayer = layer.transform;
            dimLayer.SetParent(transform, false);
            var canvas = layer.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1000;
            var scaler = layer.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            var veil = new GameObject("SubtleDarkening", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(layer.transform, false);
            var rect = veil.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = veil.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, .20f);
            image.raycastTarget = false;
        }

        void LateUpdate()
        {
            EnsureDimLayer();
            // Reapply these values after script hot reloads while Unity remains in Play mode.
            if (!volume) volume = GetComponent<Volume>();
            if (volume && !bloom && volume.profile) volume.profile.TryGet(out bloom);
            if (volume && !colorAdjustments && volume.profile) volume.profile.TryGet(out colorAdjustments);
            if (bloom) { bloom.threshold.value = 1.05f; bloom.intensity.value = 1.15f; bloom.scatter.value = .72f; }
            if (colorAdjustments) { colorAdjustments.postExposure.value = -.12f; colorAdjustments.contrast.value = 2f; }
            foreach (var camera in Camera.allCameras)
            {
                camera.allowHDR = true;
                var additional = camera.GetComponent<UniversalAdditionalCameraData>();
                if (!additional) additional = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                additional.renderPostProcessing = true;
            }
        }
    }
}
