using UnityEngine;
using UnityEngine.UI;
using ProjectLike.Phase2;
using ProjectLike.Phase11;
using ProjectLike.Phase12;
using ProjectLike.Phase8;
using ProjectLike.Phase6;
using System.Collections.Generic;

namespace ProjectLike.Phase1
{
    public class PixelHUDCanvas : MonoBehaviour
    {
        [Tooltip("Assets/Ui/BoldPixels.ttf")] public Font pixelFont;

        PlayerStats stats;
        PlayerInteraction interaction;
        WeaponController weapons;
        PlayerConsumables consumables;
        PlayerPowerUps powerUps;
        RectTransform powerUpList;
        RectTransform lootPanel;
        readonly List<Text> lootValues = new List<Text>();
        readonly List<Text> lootLabels = new List<Text>();
        Text lootDescription, roomStatus, feedbackText, abilityStatus;
        float feedbackUntil, roomPollAt, completedUntil;
        AnimatedNotice roomNotice, actionNotice;
        bool roomNoticeChanged;
        ProjectLike.Phase5.RoomController hudRoom;
        ProjectLike.Phase5.RoomState previousRoomState;
        int powerUpVersion = -1;
        Image healthFill, shieldFill, energyFill;
        GameObject healthWarning, shieldWarning, energyWarning;
        Text healthValue, shieldValue, energyValue, coinsValue, prompt;
        Text primaryWeaponValue, secondaryWeaponValue, consumableValue;

        void Awake()
        {
            stats = FindAnyObjectByType<PlayerStats>();
            interaction = FindAnyObjectByType<PlayerInteraction>();
            weapons = FindAnyObjectByType<WeaponController>();
            consumables = FindAnyObjectByType<PlayerConsumables>();
            powerUps = FindAnyObjectByType<PlayerPowerUps>();

            var canvas = GetComponent<Canvas>();
            if (!canvas) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = GetComponent<CanvasScaler>();
            if (!scaler) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = .5f;

            var layout = Rect("PixelStatBars", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -8), new Vector2(206, 112));
            DarkFrame(layout);
            healthFill = Bar("Health", layout, new Vector2(14, -12), new Color(.84f, .19f, .18f), out healthValue);
            shieldFill = Bar("Shield", layout, new Vector2(14, -40), new Color(.60f, .63f, .63f), out shieldValue);
            energyFill = Bar("Energy", layout, new Vector2(14, -68), new Color(.035f, .37f, .76f), out energyValue);
            healthWarning = Warning(layout, -12, new Color(1f, .04f, .04f));
            shieldWarning = Warning(layout, -40, new Color(.65f, .68f, .68f));
            energyWarning = Warning(layout, -68, new Color(.04f, .4f, 1f));
            roomStatus = TextElement("RoomStatus", transform, new Vector2(.5f, .76f), new Vector2(.5f, .76f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(540, 36), 25, TextAnchor.MiddleCenter);
            feedbackText = TextElement("ActionFeedback", transform, new Vector2(.5f, .69f), new Vector2(.5f, .69f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(650, 30), 18, TextAnchor.MiddleCenter);
            roomNotice = new AnimatedNotice(roomStatus);
            actionNotice = new AnimatedNotice(feedbackText);
            abilityStatus = TextElement("TimeAbility", transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 20), new Vector2(370, 24), 14, TextAnchor.MiddleRight);
            BuildLootPanel();
            CoinCounter();
            var minimapPanel = Rect("DungeonMinimap", transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-10, -48), new Vector2(190, 160));
            PixelHUDCanvas.DarkFrame(minimapPanel);
            minimapPanel.gameObject.AddComponent<DungeonMinimap>().Initialize(stats, pixelFont);
            WeaponSlots();
            powerUpList = Rect("PowerUpList", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -128), new Vector2(232, 112));

            prompt = TextElement("InteractionPrompt", transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 55), new Vector2(500, 36), 20, TextAnchor.MiddleCenter);
        }

        RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = pos; rect.sizeDelta = size;
            return rect;
        }

        Text TextElement(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var rect = Rect(name, parent, min, max, pivot, pos, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = pixelFont ? pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = Color.white; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.04f, .04f, .04f, 1f); shadow.effectDistance = new Vector2(2, -2);
            text.raycastTarget = false;
            return text;
        }

        Image Bar(string name, Transform parent, Vector2 position, Color fillColor, out Text value)
        {
            var outer = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(178, 26));
            outer.gameObject.AddComponent<Image>().color = new Color(.34f, .34f, .34f);
            var dark = Rect("Track", outer, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), new Vector2(0, 1), new Vector2(-4, -4));
            dark.gameObject.AddComponent<Image>().color = new Color(.12f, .12f, .12f);
            var fillRect = Rect("Fill", dark, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(174, 0));
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = fillColor; fill.type = Image.Type.Simple;
            var highlight = Rect("FillHighlight", fillRect, new Vector2(0, 1), Vector2.one, new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 4));
            highlight.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, .16f);
            value = TextElement("Value", outer, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), new Vector2(0, 1), Vector2.zero, 24, TextAnchor.MiddleCenter);
            value.GetComponent<Shadow>().effectDistance = new Vector2(2, -2);
            return fill;
        }

        void Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(width, height));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }

        GameObject Warning(Transform parent, float y, Color color)
        {
            var root = Rect("LowStatWarning", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(218, y), new Vector2(10, 26));
            // Signo pixelado como en la referencia: visible ?nicamente con el valor bajo.
            Panel("StemBorder", root, 0, 0, 10, 19, Color.black);
            Panel("DotBorder", root, 0, -20, 10, 7, Color.black);
            Panel("Stem", root, 3, -2, 4, 14, color);
            Panel("Dot", root, 3, -22, 4, 3, color);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void Pixel(RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            var pixel = Rect("Pixel", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, size);
            pixel.gameObject.AddComponent<Image>().color = color;
            var outline = pixel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.08f, .06f, .04f);
            outline.effectDistance = new Vector2(1, -1);
        }

        void CoinCounter()
        {
            var outer = Rect("CoinCounter", transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-10, -10), new Vector2(100, 30));
            coinsValue = TextElement("CoinValue", outer, Vector2.zero, Vector2.one, new Vector2(1, .5f), Vector2.zero, new Vector2(96, 28), 22, TextAnchor.MiddleRight);
            coinsValue.color = new Color(1f, .88f, .38f);
        }

        void WeaponSlots()
        {
            var bottomRight = new Vector2(1, 0);
            primaryWeaponValue = TextElement("PrimaryWeapon", transform, bottomRight, bottomRight, bottomRight,
                new Vector2(-10, 104), new Vector2(310, 25), 16, TextAnchor.MiddleRight);
            secondaryWeaponValue = TextElement("SecondaryWeapon", transform, bottomRight, bottomRight, bottomRight,
                new Vector2(-10, 76), new Vector2(310, 25), 16, TextAnchor.MiddleRight);
            consumableValue = TextElement("Consumable", transform, bottomRight, bottomRight, bottomRight,
                new Vector2(-10, 48), new Vector2(360, 25), 15, TextAnchor.MiddleRight);
        }

        void Update()
        {
            if (stats && (!powerUps || powerUps.gameObject != stats.gameObject)) powerUps = stats.GetComponent<PlayerPowerUps>();
            if (powerUps && powerUps.Version != powerUpVersion) RebuildPowerUpList();
            if (stats)
            {
                healthValue.text = $"{stats.CurrentHealth:0}/{stats.maxHealth:0}";
                shieldValue.text = $"{stats.CurrentShield:0}/{stats.maxShield:0}";
                energyValue.text = $"{stats.CurrentEnergy:0}/{stats.maxEnergy:0}";
                coinsValue.text = stats.coins.ToString();
                healthWarning.SetActive(IsLow(stats.CurrentHealth, stats.maxHealth));
                shieldWarning.SetActive(IsLow(stats.CurrentShield, stats.maxShield));
                energyWarning.SetActive(IsLow(stats.CurrentEnergy, stats.maxEnergy));
                SetFill(healthFill, stats.maxHealth > 0 ? stats.CurrentHealth / stats.maxHealth : 0);
                SetFill(shieldFill, stats.maxShield > 0 ? stats.CurrentShield / stats.maxShield : 0);
                SetFill(energyFill, stats.maxEnergy > 0 ? stats.CurrentEnergy / stats.maxEnergy : 0);
            }
            if (weapons)
            {
                primaryWeaponValue.text = (weapons.ActiveSlot == 1 ? "> " : "  ") + "[1] " + (weapons.PrimaryWeapon ? weapons.PrimaryWeapon.weaponName : "Vacía");
                secondaryWeaponValue.text = (weapons.ActiveSlot == 2 ? "> " : "  ") + "[2] " + (weapons.SecondaryWeapon ? weapons.SecondaryWeapon.weaponName : "Vacía");
                primaryWeaponValue.color = weapons.ActiveSlot == 1 ? new Color(1f, .78f, .18f) : Color.white;
                secondaryWeaponValue.color = weapons.ActiveSlot == 2 ? new Color(1f, .78f, .18f) : Color.white;
            }
            if (consumableValue)
                consumableValue.text = consumables && consumables.Current ? "[Q] " + consumables.Current.displayName + "  x" + consumables.Count : "[Q] Sin consumibles";
        }

        public void ShowFeedback(string message, Color color)
        {
            if (!feedbackText) return;
            actionNotice.Set(message, color, true);
            feedbackUntil = Time.unscaledTime + 2.4f;
        }

        void UpdateRoomStatus()
        {
            if (Time.unscaledTime >= roomPollAt)
            {
                roomPollAt = Time.unscaledTime + .2f;
                ProjectLike.Phase5.RoomController found = null;
                if (stats) foreach (var room in FindObjectsByType<ProjectLike.Phase5.RoomController>())
                    if (room.WorldBounds.Contains(stats.transform.position)) { found = room; break; }
                if (found != hudRoom) { hudRoom = found; previousRoomState = found ? found.State : ProjectLike.Phase5.RoomState.Dormant; completedUntil = 0f; roomNoticeChanged = true; }
            }
            if (!hudRoom || !hudRoom.data) { roomNotice.Set("", Color.white); return; }
            if (hudRoom.State == ProjectLike.Phase5.RoomState.Cleared && previousRoomState == ProjectLike.Phase5.RoomState.Combat) completedUntil = Time.unscaledTime + 3f;
            previousRoomState = hudRoom.State;
            if (Time.unscaledTime < completedUntil) { roomNotice.Set("Sala completada", new Color(.35f, 1f, .48f)); return; }
            var type = hudRoom.data.roomType;
            var label = type == ProjectLike.Phase5.RoomType.Shop ? "Tienda" : type == ProjectLike.Phase5.RoomType.Boss ? "Mini-Boss" : type == ProjectLike.Phase5.RoomType.Elite ? "Sala Elite" : type == ProjectLike.Phase5.RoomType.Entrance ? "Entrada" : type == ProjectLike.Phase5.RoomType.Exit ? "Portal" : !hudRoom.data.RequiresCombat ? "Sala Especial" : "";
            if (hudRoom.State == ProjectLike.Phase5.RoomState.Combat)
                label += (label.Length > 0 ? "  -  " : "") + "Oleada " + Mathf.Max(1, hudRoom.CurrentWave) + "/" + Mathf.Max(1, hudRoom.TotalWaves);
            var color = type == ProjectLike.Phase5.RoomType.Shop ? new Color(1f, .80f, .24f)
                : type == ProjectLike.Phase5.RoomType.Boss ? new Color(1f, .28f, .30f)
                : type == ProjectLike.Phase5.RoomType.Elite ? new Color(1f, .52f, .20f)
                : type == ProjectLike.Phase5.RoomType.Entrance ? new Color(.65f, .85f, 1f)
                : type == ProjectLike.Phase5.RoomType.Exit ? new Color(.32f, 1f, .85f)
                : !hudRoom.data.RequiresCombat ? new Color(.82f, .54f, 1f)
                : new Color(1f, .90f, .63f);
            roomNotice.Set(label, color, roomNoticeChanged);
            roomNoticeChanged = false;
        }

        void LateUpdate()
        {
            UpdateRoomStatus();
            if (Time.unscaledTime >= feedbackUntil) actionNotice.Set("", Color.white);
            roomNotice.Tick();
            actionNotice.Tick();
            var ability = stats ? stats.GetComponent<TimeStopAbility>() : null;
            abilityStatus.text = !ability ? "" : ability.Remaining > 0f ? $"TIEMPO DETENIDO {ability.Remaining:0.0}s" : ability.CooldownRemaining > 0f ? $"[F / R3] Tiempo cero: {ability.CooldownRemaining:0}s" : "[F / R3] Tiempo cero - 150 energia";
            var target = interaction && Time.timeScale > 0f ? interaction.FindTarget() : null;
            var loot = target as LootPickup;
            prompt.text = target != null && !loot ? "[ E / A ]  " + target.Prompt : "";
            lootPanel.gameObject.SetActive(loot && loot.CanCollect);
            if (loot && loot.CanCollect) ShowLootStats(loot.data, loot.quantity);
        }

        // Dos canales independientes: los cambios de oleada no cortan los avisos de acciones.
        sealed class AnimatedNotice
        {
            readonly Text text;
            readonly CanvasGroup group;
            readonly Vector2 restingPosition;
            string requested = "";
            Color requestedColor = Color.white;
            float visibility;
            bool replacing;

            public AnimatedNotice(Text label)
            {
                text = label;
                restingPosition = label.rectTransform.anchoredPosition;
                group = label.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                text.text = "";
            }

            public void Set(string message, Color color, bool replay = false)
            {
                message = message ?? "";
                color.a = 1f;
                if (!replay && requested == message && requestedColor == color) return;
                requested = message;
                requestedColor = color;
                replacing = true;
            }

            public void Tick()
            {
                // Unscaled mantiene las transiciones suaves durante Tiempo cero.
                var dt = Time.unscaledDeltaTime;
                var hiding = replacing || string.IsNullOrEmpty(requested);
                visibility = Mathf.MoveTowards(visibility, hiding ? 0f : 1f, dt / (hiding ? .20f : .32f));
                if (replacing && visibility <= 0f)
                {
                    text.text = requested;
                    text.color = requestedColor;
                    replacing = false;
                }
                var eased = Mathf.SmoothStep(0f, 1f, visibility);
                group.alpha = eased;
                // El CanvasGroup desvanece también la sombra del texto.
                text.rectTransform.anchoredPosition = restingPosition + Vector2.up * ((1f - eased) * 10f);
            }
        }

        public static void DarkFrame(RectTransform parent)
        {
            FrameLayer(parent, "FrameShadow", 0, 4, 0, 0, .035f);
            FrameLayer(parent, "FrameEdge", 3, 2, 3, 4, .16f);
            FrameLayer(parent, "FrameTop", 7, 0, 7, 8, .42f);
            FrameLayer(parent, "FrameBody", 6, 5, 6, 9, .24f);
            FrameLayer(parent, "FrameBevel", 9, 8, 9, 12, .34f);
            FrameLayer(parent, "FrameInset", 12, 10, 12, 14, .10f);
        }

        static void FrameLayer(RectTransform parent, string name, float left, float top, float right, float bottom, float shade)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
            var image = go.GetComponent<Image>(); image.color = new Color(shade, shade, shade); image.raycastTarget = false;
        }

        void BuildLootPanel()
        {
            lootPanel = Rect("GroundItemStats", transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 52), new Vector2(680, 64));
            DarkFrame(lootPanel);
            for (var i = 0; i < 10; i++)
            {
                var value = TextElement("StatValue" + i, lootPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(130, 28), 22, TextAnchor.MiddleCenter);
                var label = TextElement("StatLabel" + i, lootPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(130, 20), 13, TextAnchor.MiddleCenter);
                lootValues.Add(value);
                lootLabels.Add(label);
            }
            lootDescription = TextElement("EffectDescription", lootPanel, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-24, -12), 15, TextAnchor.MiddleCenter);
            lootDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            lootPanel.gameObject.SetActive(false);
        }

        int visibleStats;
        static readonly Color BetterStat = new Color(.25f, 1f, .36f);
        static readonly Color WorseStat = new Color(1f, .25f, .25f);

        void Stat(string label, float value, float? previous = null, bool higherIsBetter = true, string suffix = "")
        {
            if (visibleStats >= lootValues.Count) return;
            var text = lootValues[visibleStats];
            text.text = value.ToString("0.##") + suffix;
            var equal = !previous.HasValue || Mathf.Abs(value - previous.Value) < .0001f;
            var better = previous.HasValue && (higherIsBetter ? value > previous.Value : value < previous.Value);
            text.color = equal ? Color.white : better ? BetterStat : WorseStat;
            lootLabels[visibleStats].text = label;
            visibleStats++;
        }

        static float Cooldown(WeaponData weapon) => Mathf.Max(.02f, weapon.recovery > 0f ? weapon.recovery : 1f / Mathf.Max(.01f, weapon.attackSpeed));
        static float ChargedDamage(WeaponData weapon) => weapon.damage * (weapon.charged || weapon.type == WeaponType.Bow ? 1.8f : 1f);

        void ShowLootStats(LootData item, int quantity)
        {
            visibleStats = 0;
            lootDescription.text = "";
            if (item.weapon)
            {
                var weapon = item.weapon;
                var current = weapons ? weapons.EquippedWeapon : null;
                Stat("Daño", weapon.damage, current ? current.damage : (float?)null);
                Stat("Cooldown", Cooldown(weapon), current ? Cooldown(current) : (float?)null, false, "s");
                Stat("Coste de energía", weapon.energyCost, current ? current.energyCost : (float?)null, false);
                Stat("Empuje", weapon.knockback, current ? current.knockback : (float?)null);
                Stat("Daño cargado", ChargedDamage(weapon), current ? ChargedDamage(current) : (float?)null);
            }
            else if (item.consumable)
            {
                var current = consumables ? consumables.Current : null;
                var comparable = current && current.effectType == item.consumable.effectType;
                Stat("Potencia", item.consumable.magnitude, comparable ? current.magnitude : (float?)null);
                Stat("Duración", item.consumable.duration, comparable ? current.duration : (float?)null, true, "s");
                Stat("Cantidad", quantity);
            }
            else if (item.powerUp)
            {
                lootDescription.text = ProjectLike.Phase10.ShopMenu.ColorizeSignedValues(item.powerUp.effectDescription);
            }
            else if (item.lootType == LootType.Upgrade || item.lootType == LootType.Special)
            {
                var m = item.modifiers;
                var multiplier = item.StatMultiplier * quantity;
                Modifier("Vida", m.maxHealth * multiplier);
                Modifier("Daño", m.damage * multiplier);
                Modifier("Velocidad", m.moveSpeed * multiplier);
                Modifier("Vel. ataque", m.attackSpeed * multiplier);
                Modifier("Crítico", m.criticalChance * multiplier * 100f, "%");
                Modifier("Defensa", m.defense * multiplier);
                Modifier("Vel. proyectil", m.projectileSpeed * multiplier);
                Modifier("Alcance", m.range * multiplier);
                Modifier("Red. cooldown", m.cooldownReduction * multiplier, "s");
                Modifier("Suerte", m.luck * multiplier);
                if (visibleStats == 0) lootDescription.text = string.IsNullOrWhiteSpace(item.specialEffect) ? item.description : item.specialEffect;
            }
            else if (item.lootType == LootType.Consumable)
            {
                Stat("Vida", item.effectMagnitude * item.StatMultiplier * quantity, 0f);
                Stat("Escudo", item.modifiers.defense * item.StatMultiplier * quantity, 0f);
                Stat("Energía", item.modifiers.cooldownReduction * item.StatMultiplier * quantity, 0f);
            }
            else
            {
                var amount = item.lootType == LootType.Coins ? Mathf.Max(1, Mathf.RoundToInt(item.effectMagnitude * item.StatMultiplier)) * quantity : Mathf.Max(1, Mathf.RoundToInt(item.effectMagnitude)) * quantity;
                Stat(item.lootType == LootType.Coins ? "Monedas" : "Llaves", amount);
            }
            var rows = visibleStats > 5 ? 2 : 1;
            lootPanel.sizeDelta = new Vector2(680, rows * 54 + 24);
            lootDescription.gameObject.SetActive(visibleStats == 0);
            for (var i = 0; i < lootValues.Count; i++)
            {
                var active = i < visibleStats;
                lootValues[i].gameObject.SetActive(active);
                lootLabels[i].gameObject.SetActive(active);
                if (!active) continue;
                var columns = Mathf.Min(5, visibleStats - (i / 5) * 5);
                var width = 660f / columns;
                var x = 10f + i % 5 * width;
                var y = -10f - i / 5 * 54f;
                lootValues[i].rectTransform.anchoredPosition = new Vector2(x, y);
                lootValues[i].rectTransform.sizeDelta = new Vector2(width, 28);
                lootLabels[i].rectTransform.anchoredPosition = new Vector2(x, y - 27);
                lootLabels[i].rectTransform.sizeDelta = new Vector2(width, 20);
            }
        }

        void Modifier(string label, float value, string suffix = "")
        {
            if (Mathf.Approximately(value, 0f)) return;
            Stat(label, value, 0f, true, suffix);
        }

        void RebuildPowerUpList()
        {
            powerUpVersion = powerUps ? powerUps.Version : -1;
            if (!powerUpList) return;
            for (var i = powerUpList.childCount - 1; i >= 0; i--) Destroy(powerUpList.GetChild(i).gameObject);
            if (!powerUps) return;
            var unique = new List<PowerUpData>();
            var counts = new List<int>();
            foreach (var power in powerUps.acquired)
            {
                if (!power) continue;
                var index = unique.FindIndex(item => item && item.effectId == power.effectId);
                if (index < 0) { unique.Add(power); counts.Add(1); }
                else counts[index]++;
            }
            for (var i = 0; i < unique.Count; i++) BuildPowerUpIcon(unique[i], counts[i], i);
        }

        void BuildPowerUpIcon(PowerUpData power, int stacks, int index)
        {
            const int columns = 9;
            var position = new Vector2(index % columns * 25f, -(index / columns) * 25f);
            var border = Rect(power.displayName, powerUpList, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(23, 23));
            border.gameObject.AddComponent<Image>().color = LootRarityRules.Color(power.rarity);
            var inner = Rect("Inner", border, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-3, -3));
            inner.gameObject.AddComponent<Image>().color = new Color(.025f, .03f, .04f, .94f);
            var iconRect = Rect("Icon", inner, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-2, -2));
            var icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = power.icon; icon.preserveAspect = true; icon.raycastTarget = false; icon.color = power.icon ? Color.white : LootRarityRules.Color(power.rarity);
            if (stacks <= 1) return;
            var count = TextElement("Stacks", border, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-1, 0), new Vector2(22, 12), 9, TextAnchor.LowerRight);
            count.text = "x" + stacks; count.color = Color.white;
        }

        static bool IsLow(float current, float maximum) => maximum > 0f && current / maximum <= .25f;

        void SetFill(Image fill, float ratio)
        {
            fill.rectTransform.sizeDelta = new Vector2(174 * Mathf.Clamp01(ratio), 0);
        }
    }
}
