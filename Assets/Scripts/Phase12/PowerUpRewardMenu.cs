using System;
using System.Collections;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase8;
using ProjectLike.Phase10;
using ProjectLike.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectLike.Phase12
{
    /// <summary>Elección obligatoria de una mejora entre niveles.</summary>
    public sealed class PowerUpRewardMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        readonly List<PowerUpData> offers = new List<PowerUpData>();
        readonly List<CardView> cardViews = new List<CardView>();
        GameObject player;
        Action onSelected;
        Font pixelFont;
        float previousTimeScale;
        bool ownsPause;
        bool resolving;
        int openedFrame;

        sealed class CardView
        {
            public int index;
            public PowerUpData power;
            public RectTransform rect;
            public CanvasGroup group;
            public Button button;
            public Image border;
            public bool revealed;
        }

        public static bool Open(GameObject player, Action onSelected)
        {
            if (IsOpen || !player) return false;
            var root = new GameObject("PowerUpRewardMenu", typeof(RectTransform));
            root.AddComponent<PowerUpRewardMenu>().Initialize(player, onSelected);
            return true;
        }

        void Initialize(GameObject target, Action callback)
        {
            IsOpen = true; player = target; onSelected = callback; openedFrame = Time.frameCount;
            var hud = FindAnyObjectByType<PixelHUDCanvas>(); pixelFont = hud ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            ownsPause = true;
            Time.timeScale = 0f;
            EnsureEventSystem(); RollOffers(); BuildCanvas();
        }

        void RollOffers()
        {
            var all = Resources.LoadAll<PowerUpData>("PowerUps");
            var pool = new List<PowerUpData>();
            var inventory = player ? player.GetComponent<PlayerPowerUps>() : null;
            foreach (var power in all)
            {
                if (!power || inventory && !inventory.CanAcquire(power)) continue;
                var copies = power.rarity == LootRarity.Common ? 10 : power.rarity == LootRarity.Uncommon ? 7 : power.rarity == LootRarity.Rare ? 4 : power.rarity == LootRarity.Epic ? 2 : 1;
                for (var i = 0; i < copies; i++) pool.Add(power);
            }
            while (offers.Count < 3 && pool.Count > 0)
            {
                var chosen = pool[UnityEngine.Random.Range(0, pool.Count)];
                offers.Add(chosen);
                pool.RemoveAll(item => item == chosen);
            }
        }

        void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 700; canvas.pixelPerfect = true;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 540); scaler.matchWidthOrHeight = .5f;
            var dim = Rect("Dim", transform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero); dim.gameObject.AddComponent<Image>().color = new Color(.005f, .006f, .009f, .88f);
            TextElement("Title", dim, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -31), new Vector2(760, 42), 23, TextAnchor.MiddleCenter, "ELIGE UNA MEJORA", Color.white);
            TextElement("Subtitle", dim, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, -72), new Vector2(760, 24), 11, TextAnchor.MiddleCenter, "LA ELECCIÓN SE APLICARÁ A ESTA RUN", new Color(.68f, .7f, .75f));
            var cards = Rect("Cards", dim, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -13), new Vector2(780, 350));
            for (var i = 0; i < offers.Count; i++) BuildCard(cards, i, offers[i]);
            TextElement("Hint", dim, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(700, 24), 11, TextAnchor.MiddleCenter, "CLICK O TECLAS [1 / 2 / 3]", new Color(.78f, .8f, .84f));
            StartCoroutine(RevealCards());
        }

        void BuildCard(Transform parent, int index, PowerUpData power)
        {
            var color = LootRarityRules.Color(power.rarity);
            var x = index * 260f;
            var outer = Rect("PowerUp_" + index, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 8, -4), new Vector2(244, 336));
            var outerImage = outer.gameObject.AddComponent<Image>(); outerImage.color = color;
            var button = outer.gameObject.AddComponent<Button>(); button.targetGraphic = outerImage; button.interactable = false; var chosen = index; button.onClick.AddListener(() => Select(chosen));
            var group = outer.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
            outer.localScale = Vector3.one * .68f;
            var inner = Rect("Inner", outer, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-7, -7)); inner.gameObject.AddComponent<Image>().color = new Color(.055f, .065f, .085f, 1f);
            TextElement("Number", inner, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -8), new Vector2(24, 22), 11, TextAnchor.MiddleLeft, (index + 1).ToString(), color);
            TextElement("Name", inner, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(195, 36), 16, TextAnchor.MiddleCenter, power.displayName, Color.white);
            TextElement("Rarity", inner, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(15, -50), new Vector2(200, 20), 9, TextAnchor.MiddleCenter, ShopMenu.RarityName(power.rarity), color);
            var iconFrame = Rect("IconFrame", inner, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -76), new Vector2(150, 137)); iconFrame.gameObject.AddComponent<Image>().color = new Color(.005f, .008f, .012f, 1f); AddOutline(iconFrame, Color.Lerp(color, Color.black, .25f), 2f);
            var iconRect = Rect("Icon", iconFrame, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-20, -20)); var icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = power.icon; icon.preserveAspect = true; icon.color = power.icon ? Color.white : color;
            TextElement("Description", inner, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(15, -224), new Vector2(200, 88), 11, TextAnchor.UpperCenter, ShopMenu.ColorizeSignedValues(power.effectDescription), new Color(.9f, .9f, .9f));
            cardViews.Add(new CardView { index = index, power = power, rect = outer, group = group, button = button, border = outerImage });
        }

        IEnumerator RevealCards()
        {
            var ordered = new List<CardView>(cardViews);
            ordered.Sort((a, b) => ((int)a.power.rarity).CompareTo((int)b.power.rarity));
            yield return new WaitForSecondsRealtime(.18f);
            foreach (var card in ordered)
            {
                yield return new WaitForSecondsRealtime(.07f + (int)card.power.rarity * .055f);
                yield return RevealCard(card);
            }
        }

        IEnumerator RevealCard(CardView card)
        {
            var duration = .30f + (int)card.power.rarity * .018f;
            var elapsed = 0f;
            var startRotation = UnityEngine.Random.Range(-5f, 5f);
            card.rect.localRotation = Quaternion.Euler(0f, 0f, startRotation);
            GameSfx.Play(SfxCue.CardReveal, player.transform.position, .66f + (int)card.power.rarity * .035f, true);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                var overshoot = Mathf.Sin(t * Mathf.PI) * .10f;
                card.group.alpha = eased;
                card.rect.localScale = Vector3.one * Mathf.LerpUnclamped(.68f, 1f, eased + overshoot);
                card.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRotation, 0f, eased));
                yield return null;
            }
            card.group.alpha = 1f; card.rect.localScale = Vector3.one; card.rect.localRotation = Quaternion.identity;
            card.group.blocksRaycasts = true; card.button.interactable = true; card.revealed = true;
            SpawnRevealEffects(card);
        }

        void SpawnRevealEffects(CardView card)
        {
            var rarity = (int)card.power.rarity;
            var color = LootRarityRules.Color(card.power.rarity);
            var flash = Rect("RevealFlash", card.rect, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-10f, -10f));
            var flashImage = flash.gameObject.AddComponent<Image>(); flashImage.raycastTarget = false; flashImage.color = new Color(color.r, color.g, color.b, .34f + rarity * .045f);
            flash.gameObject.AddComponent<RewardCardFlash>().duration = .22f + rarity * .025f;
            var count = 5 + rarity * 2;
            for (var i = 0; i < count; i++)
            {
                var spark = Rect("Spark", card.rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), UnityEngine.Random.insideUnitCircle * new Vector2(95f, 140f), Vector2.one * UnityEngine.Random.Range(3f, 7f));
                var image = spark.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.color = Color.Lerp(color, Color.white, .3f);
                var motion = spark.gameObject.AddComponent<RewardCardSpark>();
                motion.velocity = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(28f, 70f + rarity * 12f);
                motion.duration = UnityEngine.Random.Range(.24f, .42f + rarity * .035f);
            }
        }

        void Select(int index)
        {
            if (resolving || index < 0 || index >= offers.Count || index >= cardViews.Count || !cardViews[index].revealed) return;
            resolving = true;
            var power = offers[index];
            var loot = ScriptableObject.CreateInstance<LootData>(); loot.displayName = power.displayName; loot.lootType = LootType.Upgrade; loot.rarity = power.rarity; loot.icon = power.icon; loot.powerUp = power; loot.modifiers = power.modifiers; loot.description = power.effectDescription;
            LootPickup.Grant(player, loot);
            RestoreTime();
            IsOpen = false;
            var callback = onSelected;
            Destroy(gameObject);
            callback?.Invoke();
        }

        void Update()
        {
            if (Time.frameCount <= openedFrame || Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) Select(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) Select(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) Select(2);
        }

        void OnDestroy() { IsOpen = false; RestoreTime(); }
        void RestoreTime() { if (!ownsPause) return; ownsPause = false; Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f; }
        void EnsureEventSystem() { if (FindAnyObjectByType<EventSystem>()) return; var events = new GameObject("RewardEventSystem"); events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
        Text TextElement(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, string value, Color color) { var rect = Rect(name, parent, min, max, pivot, position, size); var text = rect.gameObject.AddComponent<Text>(); text.font = pixelFont; text.fontSize = fontSize; text.alignment = alignment; text.alignByGeometry = true; text.resizeTextForBestFit = false; text.text = value; text.color = color; text.supportRichText = true; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1f, -1f); return text; }
        static void AddOutline(RectTransform rect, Color color, float distance) { var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = color; outline.effectDistance = new Vector2(distance, -distance); }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect; }
    }

    public sealed class RewardCardFlash : MonoBehaviour
    {
        public float duration = .25f;
        Image image; float elapsed; Color baseColor;
        void Awake() { image = GetComponent<Image>(); baseColor = image ? image.color : Color.white; }
        void Update() { elapsed += Time.unscaledDeltaTime; if (!image || elapsed >= duration) { Destroy(gameObject); return; } var c = baseColor; c.a *= 1f - elapsed / duration; image.color = c; }
    }

    public sealed class RewardCardSpark : MonoBehaviour
    {
        public Vector2 velocity;
        public float duration = .35f;
        RectTransform rect; Image image; float elapsed; Color baseColor;
        void Awake() { rect = transform as RectTransform; image = GetComponent<Image>(); baseColor = image ? image.color : Color.white; }
        void Update() { elapsed += Time.unscaledDeltaTime; if (!rect || !image || elapsed >= duration) { Destroy(gameObject); return; } rect.anchoredPosition += velocity * Time.unscaledDeltaTime; velocity *= Mathf.Pow(.08f, Time.unscaledDeltaTime); rect.Rotate(0f, 0f, 210f * Time.unscaledDeltaTime); var c = baseColor; c.a = 1f - elapsed / duration; image.color = c; }
    }
}
