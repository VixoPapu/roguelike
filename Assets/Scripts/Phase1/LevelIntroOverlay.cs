using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLike.Phase1
{
    public sealed class LevelIntroOverlay : MonoBehaviour
    {
        static LevelIntroOverlay active;

        public static void Show(int world, int level)
        {
            if (active) Destroy(active.gameObject);
            var root = new GameObject("LevelIntroOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            active = root.AddComponent<LevelIntroOverlay>();
            active.Build(world, level);
        }

        void Build(int world, int level)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = .5f;
            var background = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = background.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var hud = FindAnyObjectByType<PixelHUDCanvas>();
            if (hud && hud.pixelFont) font = hud.pixelFont;
            MakeLabel("World", $"MUNDO {world}", 44, new Vector2(0f, 20f), font);
            MakeLabel("Level", $"NIVEL {level}", 25, new Vector2(0f, -35f), font);
            var audioConfig = Resources.Load<MenuAudioConfig>("MenuAudioConfig");
            AudioSource source = null;
            if (audioConfig && audioConfig.levelBoom)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = .82f;
                source.clip = audioConfig.levelBoom;
                source.Play();
            }
            StartCoroutine(FadeOut(image, source));
        }

        void MakeLabel(string name, string value, int size, Vector2 position, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(800f, 68f);
            var text = go.GetComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter; text.text = value; text.color = Color.white;
            text.raycastTarget = false;
        }

        IEnumerator FadeOut(Image background, AudioSource source)
        {
            yield return new WaitForSecondsRealtime(.85f);
            var labels = GetComponentsInChildren<Text>();
            for (var elapsed = 0f; elapsed < 1.05f; elapsed += Time.unscaledDeltaTime)
            {
                var alpha = 1f - Mathf.Clamp01(elapsed / 1.05f);
                background.color = new Color(0f, 0f, 0f, alpha);
                foreach (var label in labels) if (label) label.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            while (source && source.isPlaying) yield return null;
            if (active == this) active = null;
            Destroy(gameObject);
        }
    }
}
