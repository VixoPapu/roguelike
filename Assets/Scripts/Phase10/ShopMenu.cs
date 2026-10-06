using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase8;
using ProjectLike.Phase11;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using ProjectLike.Audio;

namespace ProjectLike.Phase10
{
    public class ShopMenu : MonoBehaviour
    {
        const string Better = "#55E676";
        const string Worse = "#FF5A62";
        const string Equal = "#F2F2EA";
        ShopController shop;
        GameObject player;
        PlayerStats stats;
        Font pixelFont;
        RectTransform content;
        Text feedback;
        float previousTimeScale;
        bool ownsPause;
        int openedFrame;

        public static ShopMenu Open(ShopController owner, GameObject customer)
        {
            var root = new GameObject("ShopMenu", typeof(RectTransform));
            var menu = root.AddComponent<ShopMenu>();
            menu.Initialize(owner, customer);
            return menu;
        }

        void Initialize(ShopController owner, GameObject customer)
        {
            shop = owner; player = customer; stats = customer.GetComponent<PlayerStats>(); openedFrame = Time.frameCount;
            var hud = FindAnyObjectByType<PixelHUDCanvas>(); pixelFont = hud ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            ownsPause = true;
            Time.timeScale = 0f;
            GameSfx.Play(SfxCue.UiConfirm, customer.transform.position, .5f, true);
            EnsureEventSystem(); BuildCanvas(); Rebuild();
        }

        void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500; canvas.pixelPerfect = true;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 540); scaler.matchWidthOrHeight = .5f;
            var dim = Rect("Dim", transform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero); dim.gameObject.AddComponent<Image>().color = new Color(.005f, .008f, .012f, .72f);
            var panel = Rect("Panel", dim, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(520, 390));
            PixelHUDCanvas.DarkFrame(panel);
            var header = Rect("Header", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -13), new Vector2(488, 34));
            header.gameObject.AddComponent<Image>().color = new Color(.16f, .16f, .16f, 1f); AddOutline(header, new Color(.34f, .34f, .34f), 2f);
            TextElement("Title", header, new Vector2(15, -2), new Vector2(360, 35), 18, TextAnchor.MiddleLeft, shop.specialMerchant ? "OFERTAS ESPECIALES" : "OFERTAS DE TIENDA", Color.white);
            if (shop.discount > 0f) TextElement("Discount", header, new Vector2(350, -3), new Vector2(145, 34), 10, TextAnchor.MiddleRight, $"-{shop.discount * 100f:0}%", new Color(.4f, 1f, .55f));
            content = Rect("Content", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -57), new Vector2(480, 240));
            feedback = TextElement("Feedback", panel, new Vector2(20, -302), new Vector2(480, 20), 10, TextAnchor.MiddleCenter, "ELIGE UN OBJETO [1 / 2 / 3]", new Color(.82f, .84f, .8f));
            MakeButton(panel, "Reroll", new Vector2(98, -339), new Vector2(145, 32), new Color(.08f, .35f, .19f), Reroll);
            MakeButton(panel, "Salir", new Vector2(278, -339), new Vector2(145, 32), new Color(.42f, .1f, .12f), Close);
        }

        public void Rebuild()
        {
            if (!content || !shop) return;
            for (var i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            for (var i = 0; i < shop.CurrentItems.Count; i++) BuildCard(i, shop.CurrentItems[i], shop.GetPrice(i, player), shop.SoldItems[i]);
            var reroll = transform.Find("Dim/Panel/Reroll/Text");
            if (reroll) reroll.GetComponent<Text>().text = shop.RerollsFor(player) >= 5 ? "SIN REROLLS" : $"REFRESCAR {shop.GetRerollCost(player)}  [{shop.RerollsFor(player)}/5]";
        }

        void BuildCard(int index, LootData item, int price, bool sold)
        {
            var rarityColor = item.RarityColor;
            var outer = Rect("Item_" + index, content, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -index * 80f), new Vector2(480, 73));
            var outerImage = outer.gameObject.AddComponent<Image>(); outerImage.color = new Color(.43f, .43f, .40f, 1f);
            var button = outer.gameObject.AddComponent<Button>(); button.targetGraphic = outerImage; button.interactable = !sold; var selected = index; button.onClick.AddListener(() => Buy(selected));
            var inner = Rect("Inner", outer, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-4, -4)); inner.gameObject.AddComponent<Image>().color = CardBackground(item.rarity);
            TextElement("Rarity", inner, new Vector2(8, -1), new Vector2(145, 17), 10, TextAnchor.MiddleLeft, RarityName(item.rarity), rarityColor);
            var category = item.weapon ? "ARMA" : item.consumable ? "CONSUMIBLE" : "POWER-UP";
            TextElement("Category", inner, new Vector2(152, -1), new Vector2(150, 17), 9, TextAnchor.MiddleLeft, category, new Color(.64f, .67f, .7f));
            TextElement("Name", inner, new Vector2(72, -17), new Vector2(292, 20), 14, TextAnchor.MiddleLeft, item.displayName, Color.white);
            TextElement("Price", inner, new Vector2(365, -3), new Vector2(95, 20), 14, TextAnchor.MiddleRight, sold ? "AGOTADO" : price.ToString(), sold ? Color.gray : new Color(1f, .82f, .12f));
            TextElement("Details", inner, new Vector2(74, -37), new Vector2(382, 29), 10, TextAnchor.UpperLeft, Details(item), new Color(.9f, .9f, .84f));
            var iconFrame = Rect("IconFrame", inner, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(9, -19), new Vector2(55, 47)); iconFrame.gameObject.AddComponent<Image>().color = new Color(rarityColor.r * .35f, rarityColor.g * .35f, rarityColor.b * .35f, 1f); AddOutline(iconFrame, rarityColor, 1f);
            var iconRect = Rect("Icon", iconFrame, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-9, -9)); var icon = iconRect.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.sprite = ItemSprite(item); icon.color = icon.sprite && icon.sprite != CombatFeedback.WhiteSprite ? Color.white : rarityColor;
            if (sold) inner.GetComponent<Image>().color = new Color(.04f, .04f, .04f, 1f);
            outer.gameObject.AddComponent<ShopCardMotion>().Configure(index, outerImage, rarityColor, sold);
        }

        string Details(LootData item)
        {
            if (item.weapon)
            {
                var current = player.GetComponent<WeaponController>()?.EquippedWeapon;
                return $"DAÑO {Compare(current ? current.damage : 0, item.weapon.damage, true)}   FIRE RATE {Compare(current ? current.attackSpeed : 0, item.weapon.attackSpeed, true)}   ALCANCE {Compare(current ? current.range : 0, item.weapon.range, true)}\n" +
                       $"CRÍTICO {Compare(current ? current.criticalChance * 100f : 0, item.weapon.criticalChance * 100f, true, "%")}   ENERGÍA {Compare(current ? current.energyCost : 0, item.weapon.energyCost, false)}   KNOCKBACK {Compare(current ? current.knockback : 0, item.weapon.knockback, true)}";
            }
            if (item.powerUp) return ColorizeSignedValues(item.powerUp.effectDescription);
            if (item.lootType == LootType.Upgrade)
            {
                var text = new StringBuilder(); Append(text, "VIDA", item.modifiers.maxHealth); Append(text, "DAÑO", item.modifiers.damage); Append(text, "VELOCIDAD", item.modifiers.moveSpeed); Append(text, "ATAQUE", item.modifiers.attackSpeed); Append(text, "DEFENSA", item.modifiers.defense); Append(text, "ALCANCE", item.modifiers.range); Append(text, "CRÍTICO", item.modifiers.criticalChance * 100f, "%"); Append(text, "SUERTE", item.modifiers.luck);
                return text.Length > 0 ? text.ToString() : "POWER-UP ESPECIAL";
            }
            if (item.consumable)
            {
                var duration = item.consumable.duration > 0f ? $"   DURACIÓN <color={Better}>{item.consumable.duration:0.#}s</color>" : "";
                return $"{EffectName(item.consumable.effectType)}\nEFECTO <color={Better}>{item.consumable.magnitude:0.#}</color>{duration}";
            }
            return ColorizeSignedValues(string.IsNullOrWhiteSpace(item.description) ? item.lootType.ToString().ToUpperInvariant() : item.description);
        }

        static string Compare(float oldValue, float newValue, bool higherIsBetter, string suffix = "") { var equal = Mathf.Approximately(oldValue, newValue); var improves = higherIsBetter ? newValue > oldValue : newValue < oldValue; var color = equal ? Equal : improves ? Better : Worse; return $"{oldValue:0.##}{suffix} → <color={color}>{newValue:0.##}{suffix}</color>"; }
        public static string ColorizeSignedValues(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "SIN DESCRIPCIÓN";
            return Regex.Replace(value, @"(?<![\w#])(?:[+-]\s*)?\d+(?:[\.,]\d+)?\s*(?:%|s|m)?", match =>
            {
                var beforeStart = Mathf.Max(0, match.Index - 28);
                var before = value.Substring(beforeStart, match.Index - beforeStart).ToLowerInvariant();
                var afterStart = match.Index + match.Length;
                var after = value.Substring(afterStart, Mathf.Min(28, value.Length - afterStart)).ToLowerInvariant();
                var token = match.Value.TrimStart();
                var penalty = token.StartsWith("-") || before.Contains("penaliz") || before.Contains("pierde ") || before.Contains("resta ") || before.Contains("gastar hasta") || before.Contains("coste ") || before.Contains("consume ") || before.Contains("queda al") || after.Contains("daño recibido") || after.Contains("dano recibido");
                return $"<color={(penalty ? Worse : Better)}>{match.Value}</color>";
            });
        }
        static void Append(StringBuilder text, string label, float value, string suffix = "") { if (Mathf.Approximately(value, 0f)) return; if (text.Length > 0) text.Append("   "); var color = value > 0 ? Better : Worse; text.Append(label).Append($" <color={color}>").Append(value > 0 ? "+" : "").Append(value.ToString("0.##")).Append(suffix).Append("</color>"); }
        static string EffectName(ConsumableEffectType type) => type == ConsumableEffectType.HealthPotion ? "RECUPERA VIDA" : type == ConsumableEffectType.EnergyPotion ? "RECUPERA ENERGÍA" : type == ConsumableEffectType.SpeedPotion ? "AUMENTA VELOCIDAD" : type == ConsumableEffectType.DamagePotion ? "AUMENTA DAÑO" : type == ConsumableEffectType.TemporaryShield ? "ESCUDO TEMPORAL" : type == ConsumableEffectType.Invisibility ? "INVISIBILIDAD" : type == ConsumableEffectType.CriticalBoost ? "AUMENTA CRÍTICO" : type == ConsumableEffectType.Invulnerability ? "INVULNERABILIDAD" : type == ConsumableEffectType.Bomb ? "DAÑO EN ÁREA" : type == ConsumableEffectType.ThrowingKnives ? "PROYECTILES FRONTALES" : "ATAQUE MÁGICO RADIAL";
        public static string RarityName(LootRarity rarity) => rarity == LootRarity.Common ? "COMÚN" : rarity == LootRarity.Uncommon ? "POCO COMÚN" : rarity == LootRarity.Rare ? "RARO" : rarity == LootRarity.Epic ? "ÉPICO" : rarity == LootRarity.Legendary ? "LEGENDARIO" : "CORRUPTO";
        static Color CardBackground(LootRarity rarity) => new Color(.10f, .10f, .10f, 1f);
        Sprite ItemSprite(LootData item) { if (item.icon) return item.icon; if (item.powerUp && item.powerUp.icon) return item.powerUp.icon; if (item.worldSprite) return item.worldSprite; if (item.weapon) return CombatFeedback.GetWeaponSprite(item.weapon); if (item.consumable && item.consumable.icon) return item.consumable.icon; return CombatFeedback.WhiteSprite; }
        void Buy(int index) { var success=shop.TryPurchase(index, player, out var message); GameSfx.Play(success?SfxCue.ShopBuy:SfxCue.UiDeny,player.transform.position,success?.75f:.62f,true); feedback.color = success ? new Color(.35f, 1f, .5f) : new Color(1f, .35f, .3f); feedback.text = message.ToUpperInvariant(); if (success && shop.IsLockedFor(player)) StartCoroutine(CloseExhausted()); }
        IEnumerator CloseExhausted() { yield return new WaitForSecondsRealtime(.7f); Close(); }
        void Reroll() { var success = shop.TryReroll(stats, out var message); GameSfx.Play(success?SfxCue.UiConfirm:SfxCue.UiDeny,player.transform.position,success?.6f:.62f,true); feedback.color = success ? new Color(.35f, 1f, .5f) : new Color(1f, .35f, .3f); feedback.text = message.ToUpperInvariant(); }
        void Update() { if (Time.frameCount <= openedFrame || Keyboard.current == null) return; if (Keyboard.current.digit1Key.wasPressedThisFrame) Buy(0); if (Keyboard.current.digit2Key.wasPressedThisFrame) Buy(1); if (Keyboard.current.digit3Key.wasPressedThisFrame) Buy(2); if (Keyboard.current.rKey.wasPressedThisFrame) Reroll(); if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame) Close(); }
        public void Close() { RestoreTime(); if (shop) shop.NotifyClosed(this); Destroy(gameObject); }
        void OnDestroy() { RestoreTime(); }
        void RestoreTime() { if (!ownsPause) return; ownsPause = false; Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f; }
        void EnsureEventSystem() { if (FindAnyObjectByType<EventSystem>()) return; var events = new GameObject("ShopEventSystem"); events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
        Button MakeButton(Transform parent, string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action) { var rect = Rect(label, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, size); var image = rect.gameObject.AddComponent<Image>(); image.color = color; AddOutline(rect, new Color(.12f, .12f, .12f), 2f); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action); TextElement("Text", rect, Vector2.zero, size, 13, TextAnchor.MiddleCenter, label.ToUpperInvariant(), Color.white); return button; }
        Text TextElement(string name, Transform parent, Vector2 position, Vector2 size, int sizePx, TextAnchor alignment, string value, Color color) { var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, size); var text = rect.gameObject.AddComponent<Text>(); text.font = pixelFont; text.fontSize = sizePx; text.alignment = alignment; text.alignByGeometry = true; text.resizeTextForBestFit = false; text.text = value; text.color = color; text.supportRichText = true; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1f, -1f); return text; }
        static void AddOutline(RectTransform rect, Color color, float distance) { var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = color; outline.effectDistance = new Vector2(distance, -distance); }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect; }
    }

    public sealed class ShopCardMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform rect;
        CanvasGroup group;
        Image image;
        Color baseColor;
        Color rarityColor;
        float delay;
        float elapsed;
        bool hovered;
        bool sold;

        public void Configure(int index, Image target, Color rarity, bool isSold)
        {
            rect = transform as RectTransform;
            image = target;
            baseColor = target.color;
            rarityColor = rarity;
            sold = isSold;
            delay = index * .075f;
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;
            rect.localScale = Vector3.one * .86f;
            rect.anchoredPosition += Vector2.right * 36f;
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01((elapsed - delay) / .28f);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            group.alpha = eased;
            rect.anchoredPosition = Vector2.Lerp(new Vector2(36f, -transform.GetSiblingIndex() * 80f), new Vector2(0f, -transform.GetSiblingIndex() * 80f), eased);
            var pulse = hovered && !sold ? 1.018f + Mathf.Sin(Time.unscaledTime * 8f) * .006f : 1f;
            rect.localScale = Vector3.one * Mathf.Lerp(.86f, pulse, eased);
            if (image) image.color = hovered && !sold ? Color.Lerp(baseColor, rarityColor, .28f) : baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; }
    }
}
