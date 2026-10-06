using System.Collections;
using System.Collections.Generic;
using ProjectLike.Audio;
using ProjectLike.Phase6;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectLike.Phase1
{
    /// <summary>Menú principal runtime para el proyecto de una sola escena.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        const string LogoPath = "Images/Menu/imagen_2026-09-08_200152682-div6 (1)";
        const string VolumeKey = "Settings.MasterVolume";
        const string FullscreenKey = "Settings.Fullscreen";
        const string VSyncKey = "Settings.VSync";
        const string WidthKey = "Settings.Width";
        const string HeightKey = "Settings.Height";

        static MainMenuController instance;
        public static void DismissForOnline() { if (instance) Destroy(instance.gameObject); }
        public static bool IsOpen => instance && instance.gameObject.activeInHierarchy;
        CanvasGroup canvasGroup;
        RectTransform mainPage;
        RectTransform settingsPage;
        Text status;
        Text volumeValue;
        Text fullscreenValue;
        Text resolutionValue;
        Text vSyncValue;
        Slider volumeSlider;
        Button firstMainButton;
        Button firstSettingsButton;
        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        int resolutionIndex;
        bool fullScreen;
        bool starting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => Show();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void Show()
        {
            if (instance)
            {
                instance.gameObject.SetActive(true);
                instance.ShowMainPage();
                Time.timeScale = 0f;
                return;
            }
            var root = new GameObject("MainMenu", typeof(RectTransform));
            instance = root.AddComponent<MainMenuController>();
            instance.Initialize();
        }

        void Initialize()
        {
            ApplySavedSettings();
            Time.timeScale = 0f;
            EnsureEventSystem();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;
            canvas.pixelPerfect = true;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = .5f;
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            var background = Rect("BlackBackground", transform, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = Color.black;

            mainPage = Rect("MainPage", background, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            BuildMainPage();
            settingsPage = Rect("SettingsPage", background, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            BuildSettingsPage();
            settingsPage.gameObject.SetActive(false);
            StartCoroutine(FadeIn());
            Select(firstMainButton);
        }

        void BuildMainPage()
        {
            var logoRect = Rect("GameLogo", mainPage, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, new Vector2(0f, 142f), new Vector2(500f, 220f));
            var logo = logoRect.gameObject.AddComponent<Image>();
            var sprites = Resources.LoadAll<Sprite>(LogoPath);
            logo.sprite = sprites != null && sprites.Length > 0 ? sprites[0] : Resources.Load<Sprite>(LogoPath);
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            var logoGlow = logoRect.gameObject.AddComponent<Shadow>();
            logoGlow.effectColor = new Color(.55f, .12f, .95f, .5f);
            logoGlow.effectDistance = new Vector2(3f, -3f);
            logoRect.gameObject.AddComponent<MenuLogoMotion>();

            firstMainButton = MenuButton(mainPage, "SINGLE PLAYER", new Vector2(0f, 20f), StartSinglePlayer);
            MenuButton(mainPage, "MULTIPLAYER", new Vector2(0f, -30f), MultiplayerPending);
            MenuButton(mainPage, "SETTINGS", new Vector2(0f, -80f), ShowSettingsPage);
            MenuButton(mainPage, "SALIR", new Vector2(0f, -130f), QuitGame);
            status = Label("Status", mainPage, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, new Vector2(0f, -190f), new Vector2(650f, 28f), 13, "", new Color(.72f, .4f, 1f));
        }

        void BuildSettingsPage()
        {
            Label("SettingsTitle", settingsPage, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, new Vector2(0f, 188f), new Vector2(500f, 55f), 32, "SETTINGS", Color.white);
            Label("VolumeLabel", settingsPage, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, new Vector2(-165f, 105f), new Vector2(210f, 34f), 17, "VOLUMEN", Color.white).alignment = TextAnchor.MiddleLeft;
            volumeValue = Label("VolumeValue", settingsPage, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, new Vector2(174f, 105f), new Vector2(90f, 34f), 16, "100%", new Color(.72f, .4f, 1f));
            volumeSlider = BuildSlider(settingsPage, new Vector2(25f, 105f));
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
            SetVolume(volumeSlider.value);

            firstSettingsButton = SettingsButton(settingsPage, "RESOLUCIÓN", new Vector2(0f, 42f), CycleResolution, out resolutionValue);
            SettingsButton(settingsPage, "PANTALLA COMPLETA", new Vector2(0f, -12f), ToggleFullscreen, out fullscreenValue);
            SettingsButton(settingsPage, "V-SYNC", new Vector2(0f, -66f), ToggleVSync, out vSyncValue);
            MenuButton(settingsPage, "VOLVER", new Vector2(0f, -148f), ShowMainPage);
            RefreshSettingsText();
        }

        Button MenuButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(text.Replace(" ", "") + "Button", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, position, new Vector2(380f, 42f));
            var hit = rect.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, .001f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.onClick.AddListener(() => { GameSfx.Play(SfxCue.UiConfirm, Vector2.zero, .58f, true); action?.Invoke(); });
            var left = Label("LeftOrnament", rect, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.one * .5f, new Vector2(35f, 0f), new Vector2(45f, 36f), 18, "◆", new Color(.65f, .25f, 1f));
            var right = Label("RightOrnament", rect, new Vector2(1f, .5f), new Vector2(1f, .5f), Vector2.one * .5f, new Vector2(-35f, 0f), new Vector2(45f, 36f), 18, "◆", new Color(.65f, .25f, 1f));
            var label = Label("Label", rect, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, Vector2.zero, 21, text, Color.white);
            rect.gameObject.AddComponent<MainMenuButtonVisual>().Configure(label, left, right);
            return button;
        }

        Button SettingsButton(Transform parent, string title, Vector2 position, UnityEngine.Events.UnityAction action, out Text value)
        {
            var rect = Rect(title.Replace(" ", "") + "Setting", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, position, new Vector2(520f, 44f));
            var hit = rect.gameObject.AddComponent<Image>(); hit.color = new Color(.06f, .04f, .08f, .94f);
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.28f, .15f, .38f); outline.effectDistance = new Vector2(2f, -2f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.onClick.AddListener(() => { GameSfx.Play(SfxCue.UiConfirm, Vector2.zero, .5f, true); action?.Invoke(); });
            var label = Label("Name", rect, Vector2.zero, Vector2.one, new Vector2(0f, .5f), new Vector2(18f, 0f), new Vector2(300f, 40f), 15, title, Color.white); label.alignment = TextAnchor.MiddleLeft;
            value = Label("Value", rect, Vector2.zero, Vector2.one, new Vector2(1f, .5f), new Vector2(-18f, 0f), new Vector2(210f, 40f), 14, "", new Color(.72f, .4f, 1f)); value.alignment = TextAnchor.MiddleRight;
            rect.gameObject.AddComponent<SettingsRowVisual>().Configure(hit, label, value);
            return button;
        }

        Slider BuildSlider(Transform parent, Vector2 position)
        {
            var root = Rect("VolumeSlider", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.one * .5f, position, new Vector2(260f, 28f));
            var background = Rect("Background", root, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, new Vector2(0f, -16f));
            background.gameObject.AddComponent<Image>().color = new Color(.12f, .12f, .14f, 1f);
            var fillArea = Rect("Fill Area", root, Vector2.zero, Vector2.one, Vector2.one * .5f, new Vector2(5f, 0f), new Vector2(-20f, -12f));
            var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0f, .5f), Vector2.zero, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = new Color(.58f, .16f, .92f);
            var handleArea = Rect("Handle Slide Area", root, Vector2.zero, Vector2.one, Vector2.one * .5f, Vector2.zero, new Vector2(-18f, 0f));
            var handle = Rect("Handle", handleArea, Vector2.zero, Vector2.zero, Vector2.one * .5f, Vector2.zero, new Vector2(15f, 28f));
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
            var slider = root.gameObject.AddComponent<Slider>(); slider.minValue = 0f; slider.maxValue = 1f; slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            return slider;
        }

        void ApplySavedSettings()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0);
            BuildResolutionList();
            var width = PlayerPrefs.GetInt(WidthKey, Screen.width);
            var height = PlayerPrefs.GetInt(HeightKey, Screen.height);
            resolutionIndex = Mathf.Max(0, resolutions.FindIndex(item => item.x == width && item.y == height));
            if (PlayerPrefs.HasKey(WidthKey)) Screen.SetResolution(resolutions[resolutionIndex].x, resolutions[resolutionIndex].y, fullScreen);
        }

        void BuildResolutionList()
        {
            resolutions.Clear();
            foreach (var resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            if (volumeValue) volumeValue.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
        }

        void CycleResolution()
        {
            resolutionIndex = (resolutionIndex + 1) % resolutions.Count;
            var resolution = resolutions[resolutionIndex];
            Screen.SetResolution(resolution.x, resolution.y, fullScreen);
            PlayerPrefs.SetInt(WidthKey, resolution.x); PlayerPrefs.SetInt(HeightKey, resolution.y); PlayerPrefs.Save();
            RefreshSettingsText();
        }

        void ToggleFullscreen()
        {
            fullScreen = !fullScreen;
            var resolution = resolutions[resolutionIndex];
            Screen.SetResolution(resolution.x, resolution.y, fullScreen);
            PlayerPrefs.SetInt(FullscreenKey, fullScreen ? 1 : 0); PlayerPrefs.Save();
            RefreshSettingsText();
        }

        void ToggleVSync()
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount > 0 ? 0 : 1;
            PlayerPrefs.SetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0); PlayerPrefs.Save();
            RefreshSettingsText();
        }

        void RefreshSettingsText()
        {
            if (resolutions.Count > 0 && resolutionValue) resolutionValue.text = resolutions[resolutionIndex].x + " × " + resolutions[resolutionIndex].y;
            if (fullscreenValue) fullscreenValue.text = fullScreen ? "SÍ" : "NO";
            if (vSyncValue) vSyncValue.text = QualitySettings.vSyncCount > 0 ? "SÍ" : "NO";
        }

        void ShowSettingsPage()
        {
            status.text = "";
            mainPage.gameObject.SetActive(false);
            settingsPage.gameObject.SetActive(true);
            Select(firstSettingsButton);
        }

        void ShowMainPage()
        {
            if (!mainPage || !settingsPage) return;
            PlayerPrefs.Save();
            settingsPage.gameObject.SetActive(false);
            mainPage.gameObject.SetActive(true);
            if (status) status.text = "";
            Select(firstMainButton);
        }

        void MultiplayerPending()
        {
            ProjectLike.Online.CoopSession.OpenLobby();
        }

        void StartSinglePlayer()
        {
            if (starting) return;
            starting = true;
            StartCoroutine(BeginRun());
        }

        IEnumerator BeginRun()
        {
            for (var elapsed = 0f; elapsed < .18f; elapsed += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = 1f - elapsed / .18f;
                yield return null;
            }
            Time.timeScale = 1f;
            var player = FindAnyObjectByType<PlayerController>();
            var generator = FindAnyObjectByType<DungeonGenerator>();
            if (generator && player) generator.RestartRun(player);
            else if (!generator) new GameObject("DungeonGenerator").AddComponent<DungeonGenerator>();
            Destroy(gameObject);
        }

        void QuitGame()
        {
            PlayerPrefs.Save();
            Application.Quit();
#if UNITY_EDITOR
            status.text = "SALIR FUNCIONA EN LA BUILD";
#endif
        }

        IEnumerator FadeIn()
        {
            for (var elapsed = 0f; elapsed < .35f; elapsed += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / .35f);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        void Update()
        {
            var back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (settingsPage && settingsPage.gameObject.activeSelf && back) ShowMainPage();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        static void Select(Button button)
        {
            if (!button || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var events = new GameObject("MainMenuEventSystem", typeof(EventSystem));
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static Font Font()
        {
            var hud = FindAnyObjectByType<PixelHUDCanvas>();
            return hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static Text Label(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size, int fontSize, string value, Color color)
        {
            var rect = Rect(name, parent, min, max, pivot, position, size);
            var text = rect.gameObject.AddComponent<Text>(); text.font = Font(); text.fontSize = fontSize; text.text = value; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
    }

    public sealed class MainMenuButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Text label, left, right;
        bool focused;
        public void Configure(Text value, Text leftOrnament, Text rightOrnament) { label = value; left = leftOrnament; right = rightOrnament; SetFocus(false); }
        public void OnPointerEnter(PointerEventData eventData) { SetFocus(true); EventSystem.current?.SetSelectedGameObject(gameObject); }
        public void OnPointerExit(PointerEventData eventData) { if (!EventSystem.current || EventSystem.current.currentSelectedGameObject != gameObject) SetFocus(false); }
        public void OnSelect(BaseEventData eventData) => SetFocus(true);
        public void OnDeselect(BaseEventData eventData) => SetFocus(false);
        void SetFocus(bool value) { focused = value; if (label) label.color = value ? new Color(.82f, .58f, 1f) : Color.white; if (left) left.gameObject.SetActive(value); if (right) right.gameObject.SetActive(value); }
        void Update() { var target = focused ? 1.045f + Mathf.Sin(Time.unscaledTime * 7f) * .008f : 1f; transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime)); }
    }

    public sealed class SettingsRowVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Image background; Text title, value;
        public void Configure(Image image, Text name, Text current) { background = image; title = name; value = current; SetFocus(false); }
        public void OnPointerEnter(PointerEventData eventData) { SetFocus(true); EventSystem.current?.SetSelectedGameObject(gameObject); }
        public void OnPointerExit(PointerEventData eventData) { if (!EventSystem.current || EventSystem.current.currentSelectedGameObject != gameObject) SetFocus(false); }
        public void OnSelect(BaseEventData eventData) => SetFocus(true);
        public void OnDeselect(BaseEventData eventData) => SetFocus(false);
        void SetFocus(bool active) { if (background) background.color = active ? new Color(.16f, .07f, .22f, 1f) : new Color(.06f, .04f, .08f, .94f); if (title) title.color = active ? new Color(.86f, .68f, 1f) : Color.white; if (value) value.color = active ? Color.white : new Color(.72f, .4f, 1f); }
    }

    public sealed class MenuLogoMotion : MonoBehaviour
    {
        RectTransform rect; Vector2 origin;
        void Awake() { rect = transform as RectTransform; origin = rect ? rect.anchoredPosition : Vector2.zero; }
        void Update() { if (rect) rect.anchoredPosition = origin + Vector2.up * Mathf.Sin(Time.unscaledTime * 1.6f) * 3f; }
    }
}
