using System.Collections;
using ProjectLike.Phase2;
using ProjectLike.Phase6;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectLike.Phase1
{
    public class PlayerDeathSequence : MonoBehaviour
    {
        static PlayerDeathSequence active;
        PlayerController player;
        Camera sceneCamera;
        Canvas canvas;
        Text statusText;
        float originalSize;

        public static void Begin(PlayerController target)
        {
            if (active || !target) return;
            var root = new GameObject("PlayerDeathSequence");
            active = root.AddComponent<PlayerDeathSequence>();
            active.player = target;
            active.sceneCamera = Camera.main ? Camera.main : FindAnyObjectByType<Camera>();
            active.StartCoroutine(active.PlaySequence());
        }

        IEnumerator PlaySequence()
        {
            if (sceneCamera)
            {
                originalSize = sceneCamera.orthographicSize;
                var follow = sceneCamera.GetComponent<CameraFollow>();
                if (follow) { follow.SetTarget(player.transform, false); follow.SetCinematicZoom(true); follow.Shake(.32f); }
            }

            var elapsed = 0f;
            const float duration = 1.25f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var linear = Mathf.Clamp01(elapsed / duration);
                var t = linear * linear * linear * (linear * (linear * 6f - 15f) + 10f);
                if (sceneCamera && sceneCamera.orthographic)
                    sceneCamera.orthographicSize = Mathf.Lerp(originalSize, Mathf.Max(2.2f, originalSize * .58f), t);
                Time.timeScale = Mathf.Lerp(1f, .12f, t);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2.4f);
            BuildMenu();
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (!canvas) return;
            if (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) RestartRun();
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) RestartRun();
            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame) GoToLobby();
        }

        void BuildMenu()
        {
            EnsureEventSystem();
            var root = new GameObject("DeathMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);

            var shade = MakeImage(root.transform, "DeathShade", new Color(.015f, .018f, .028f, .52f));
            Stretch(shade.rectTransform);

            var panel = MakeImage(root.transform, "DeathPanel", new Color(.035f, .04f, .06f, .96f));
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(292f, 180f));
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = Color.black;
            border.effectDistance = new Vector2(5f, -5f);

            var font = ResolveFont();
            MakeText(panel.transform, "Title", "HAS MUERTO", new Vector2(0f, 56f), new Vector2(270f, 38f), 25, new Color(1f, .18f, .15f), font);
            MakeText(panel.transform, "Hint", "R / ENTER / A", new Vector2(0f, 30f), new Vector2(260f, 20f), 11, new Color(.72f, .75f, .82f), font);
            MakeButton(panel.transform, "RestartButton", "REINICIAR RUN", new Vector2(0f, -4f), new Color(.72f, .11f, .12f), font, RestartRun);
            MakeButton(panel.transform, "LobbyButton", "MENÚ PRINCIPAL", new Vector2(0f, -48f), new Color(.12f, .16f, .24f), font, GoToLobby);
            statusText = MakeText(panel.transform, "Status", "", new Vector2(0f, -78f), new Vector2(270f, 18f), 10, new Color(.95f, .72f, .25f), font);
        }

        void RestartRun()
        {
            Time.timeScale = 1f;
            var generator = FindAnyObjectByType<DungeonGenerator>();
            if (generator && player)
            {
                generator.RestartRun(player);
                var follow = sceneCamera ? sceneCamera.GetComponent<CameraFollow>() : null;
                if (follow) { follow.SetCinematicZoom(false); follow.SetTarget(player.transform, true); }
                if (sceneCamera && sceneCamera.orthographic) sceneCamera.orthographicSize = originalSize;
                Destroy(gameObject);
                return;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void GoToLobby()
        {
            Time.timeScale = 1f;
            MainMenuController.Show();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            // Una recompilación o descarga de escena no puede dejar la run pausada.
            if (Time.timeScale <= 0f && !MainMenuController.IsOpen) Time.timeScale = 1f;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.AddComponent<InputSystemUIInputModule>();
        }

        static Font ResolveFont()
        {
            var hud = FindAnyObjectByType<PixelHUDCanvas>();
            return hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static Text MakeText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, Color color, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            SetRect(rect, position, size);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        static void MakeButton(Transform parent, string name, string label, Vector2 position, Color color, Font font, UnityEngine.Events.UnityAction action)
        {
            var image = MakeImage(parent, name, color);
            SetRect(image.rectTransform, position, new Vector2(238f, 34f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, .2f);
            colors.pressedColor = Color.Lerp(color, Color.black, .2f);
            button.colors = colors;
            MakeText(image.transform, "Label", label, Vector2.zero, new Vector2(232f, 32f), 15, Color.white, font);
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
