using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase11;
using ProjectLike.Phase12;
using UnityEngine;
using ProjectLike.Audio;
using UnityEngine.UI;

namespace ProjectLike.Phase8
{
    public class LootPickup : Interactable
    {
        public LootData data;
        [Min(1)] public int quantity = 1;
        SpriteRenderer spriteRenderer;
        Vector3 baseScale;
        bool collected;
        public bool CanCollectBy(GameObject player)
        {
            if (!CanCollect || !player) return false;
            var powers = player.GetComponent<PlayerPowerUps>();
            if (data.powerUp && powers && !powers.CanAcquire(data.powerUp)) return false;
            var slot = player.GetComponent<PlayerConsumables>();
            return !data.consumable || (slot ? slot.CanAdd(data.consumable, quantity) : quantity <= 8);
        }
        public bool IsRevealing { get; set; }
        public bool CanCollect => data && !collected && !IsRevealing && isActiveAndEnabled;
        RectTransform nameCanvas;
        Text nameText;

        public void Setup(LootData loot, int amount)
        {
            data = loot; quantity = Mathf.Max(1, amount);
            prompt = "";
            BuildVisual();
        }

        void Start() { BuildVisual(); }

        void BuildVisual()
        {
            if (!data) return;
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (!spriteRenderer) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = data.worldSprite ? data.worldSprite : data.icon ? data.icon : data.weapon ? CombatFeedback.GetWeaponSprite(data.weapon) : CombatFeedback.WhiteSprite;
            if (!spriteRenderer.sprite) spriteRenderer.sprite = CombatFeedback.WhiteSprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 20;
            var rarityVfx = GetComponent<LootRarityVfx>();
            if (!rarityVfx) rarityVfx = gameObject.AddComponent<LootRarityVfx>();
            rarityVfx.Configure(data.rarity);
            var collider = GetComponent<Collider2D>();
            if (!collider) collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            BuildName();
        }

        void BuildName()
        {
            if (!nameCanvas)
            {
                var go = new GameObject("LootName", typeof(RectTransform), typeof(Canvas));
                go.transform.SetParent(transform, false);
                nameCanvas = go.GetComponent<RectTransform>();
                nameCanvas.sizeDelta = new Vector2(400, 36);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 40;
                var label = new GameObject("Name", typeof(RectTransform), typeof(Text), typeof(Shadow));
                label.transform.SetParent(go.transform, false);
                nameText = label.GetComponent<Text>();
                nameText.rectTransform.anchorMin = Vector2.zero;
                nameText.rectTransform.anchorMax = Vector2.one;
                nameText.rectTransform.sizeDelta = Vector2.zero;
                nameText.fontSize = 24;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
                nameText.raycastTarget = false;
                nameText.supportRichText = false;
                var shadow = label.GetComponent<Shadow>();
                shadow.effectColor = new Color(.06f, .06f, .06f, 1f);
                shadow.effectDistance = new Vector2(2, -2);
            }
            var hud = FindAnyObjectByType<PixelHUDCanvas>();
            nameText.font = hud && hud.pixelFont ? hud.pixelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.text = data.displayName;
            nameText.color = data.RarityColor;
        }

        void LateUpdate()
        {
            if (!nameCanvas || !spriteRenderer) return;
            nameCanvas.position = new Vector3(transform.position.x, spriteRenderer.bounds.max.y + .24f, transform.position.z);
            nameCanvas.rotation = Quaternion.identity;
            var scale = transform.lossyScale;
            nameCanvas.localScale = new Vector3(.012f / Mathf.Max(.001f, Mathf.Abs(scale.x)), .012f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f);
        }

        void Update()
        {
            if (!data) return;
            var pulse = 1f + Mathf.Sin(Time.time * 5f) * .08f;
            transform.localScale = baseScale * pulse;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!data || !data.autoPickup) return;
            var player = other.GetComponent<PlayerStats>();
            if (player) Collect(other.gameObject);
        }

        public override void Interact(GameObject player) => Collect(player);

        public static void Grant(GameObject player, LootData loot, int amount = 1)
        {
            if (!player || !loot) return;
            var grant = new GameObject("Grant_" + loot.displayName);
            grant.transform.position = player.transform.position;
            var pickup = grant.AddComponent<LootPickup>();
            pickup.data = loot;
            pickup.quantity = Mathf.Max(1, amount);
            pickup.Collect(player);
            if (!pickup.collected) Destroy(grant);
        }

        void Collect(GameObject player)
        {
            if (!CanCollectBy(player)) return;
            var stats = player.GetComponent<PlayerStats>();
            if (!stats) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (!inventory) inventory = player.AddComponent<PlayerInventory>();
            var multiplier = data.StatMultiplier;
            switch (data.lootType)
            {
                case LootType.Coins:
                    stats.coins += Mathf.Max(1, Mathf.RoundToInt(data.effectMagnitude * multiplier)) * quantity;
                    break;
                case LootType.Weapon:
                    if (data.weapon)
                    {
                        var controller = player.GetComponent<WeaponController>();
                        var replacedWeapon = controller ? controller.EquippedWeapon : null;
                        var weapon = Instantiate(data.weapon);
                        weapon.name = data.displayName + " (Run)";
                        // La rareza pertenece al WeaponData. Un cofre no vuelve a
                        // escalar silenciosamente un arma ni cambia su calidad.
                        if (replacedWeapon) inventory.RemoveWeapon(replacedWeapon);
                        inventory.AddWeapon(weapon);
                        if (controller) controller.Equip(weapon);
                        if (replacedWeapon && replacedWeapon != data.weapon)
                            LootSpawner.SpawnWeapon(replacedWeapon, WeaponRarityToLoot(replacedWeapon.rarity), (Vector2)player.transform.position + Random.insideUnitCircle.normalized * .65f, true);
                    }
                    break;
                case LootType.Consumable:
                    if (data.consumable)
                    {
                        var consumables = player.GetComponent<PlayerConsumables>();
                        if (!consumables) consumables = player.AddComponent<PlayerConsumables>();
                        for (var i = 0; i < quantity; i++) consumables.Add(data.consumable);
                    }
                    else
                    {
                        stats.Heal(data.effectMagnitude * multiplier * quantity);
                        stats.RestoreEnergy(data.modifiers.cooldownReduction * multiplier * quantity);
                        stats.AddShield(data.modifiers.defense * multiplier * quantity);
                    }
                    break;
                case LootType.Upgrade:
                    if (data.powerUp)
                    {
                        var powerUps = player.GetComponent<PlayerPowerUps>();
                        if (!powerUps) powerUps = player.AddComponent<PlayerPowerUps>();
                        var applied = 0;
                        for (var i = 0; i < quantity; i++) if (powerUps.Add(data.powerUp)) applied++;
                        if (applied <= 0) break;
                        break;
                    }
                    ApplyModifiers(stats, data.modifiers, (data.powerUp ? 1f : multiplier) * quantity);
                    break;
                case LootType.Key:
                    inventory.AddKeys(Mathf.Max(1, Mathf.RoundToInt(data.effectMagnitude)) * quantity);
                    break;
                case LootType.Special:
                    inventory.AddSpecial(data);
                    ApplyModifiers(stats, data.modifiers, multiplier * quantity);
                    break;
            }
            CombatVfxPool.Instance.SpawnEnemyAbilityBurst(transform.position, ProjectLike.Phase3.EnemyAttackStyle.ArcaneBurst, data.RarityColor);
            var cue = data.lootType == LootType.Coins ? SfxCue.Coin : data.lootType == LootType.Upgrade ? SfxCue.PowerUp : data.lootType == LootType.Consumable ? SfxCue.Consumable : SfxCue.Pickup;
            GameSfx.Play(cue, transform.position, data.lootType == LootType.Upgrade ? .82f : .65f, data.lootType == LootType.Upgrade);
            collected = true;
            Destroy(gameObject);
        }

        static LootRarity WeaponRarityToLoot(WeaponRarity rarity) => (LootRarity)Mathf.Clamp((int)rarity, 0, 4);

        public static void ApplyModifiers(PlayerStats stats, LootStatModifiers modifiers, float scale)
        {
            stats.ModifyMaxHealth(modifiers.maxHealth * scale);
            stats.moveSpeed = Mathf.Max(.1f, stats.moveSpeed + modifiers.moveSpeed * scale);
            stats.damage = Mathf.Max(0, stats.damage + modifiers.damage * scale);
            stats.attackSpeed = Mathf.Max(.1f, stats.attackSpeed + modifiers.attackSpeed * scale);
            stats.criticalChance = Mathf.Clamp01(stats.criticalChance + modifiers.criticalChance * scale);
            stats.defense += modifiers.defense * scale;
            stats.projectileSpeed = Mathf.Max(.1f, stats.projectileSpeed + modifiers.projectileSpeed * scale);
            stats.range = Mathf.Max(.1f, stats.range + modifiers.range * scale);
            stats.cooldown = Mathf.Max(.02f, stats.cooldown - modifiers.cooldownReduction * scale);
            stats.luck += modifiers.luck * scale;
        }
    }

    public class LootRarityVfx : MonoBehaviour
    {
        static Material sharedMaterial;
        static Sprite glowSprite;
        ParticleSystem particles;
        SpriteRenderer glow;
        Color color;
        float pulseOffset;

        void Awake()
        {
            if (!sharedMaterial) sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            var glowObject = new GameObject("RarityGlow");
            glowObject.transform.SetParent(transform, false);
            glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = GlowSprite();
            glow.sortingOrder = 18;

            var particleObject = new GameObject("RarityParticles");
            particleObject.transform.SetParent(transform, false);
            particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.duration = 1f; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .7f); main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .32f); main.startSize = new ParticleSystem.MinMaxCurve(.025f, .065f); main.maxParticles = 28; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = 6f;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .32f;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.x = new ParticleSystem.MinMaxCurve(0f); velocity.y = new ParticleSystem.MinMaxCurve(.35f); velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>(); renderer.material = sharedMaterial; renderer.sortingOrder = 22;
            pulseOffset = Random.value * 8f;
        }

        public void Configure(LootRarity rarity)
        {
            if (!particles) return;
            color = LootRarityRules.Color(rarity);
            var main = particles.main; main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, .35f), color);
            var emission = particles.emission; emission.rateOverTime = rarity == LootRarity.Common ? 3f : rarity == LootRarity.Uncommon ? 5f : rarity == LootRarity.Rare ? 8f : rarity == LootRarity.Epic ? 11f : 15f;
            glow.color = new Color(color.r, color.g, color.b, rarity == LootRarity.Common ? .4f : .65f);
            particles.Play();
        }

        static Sprite GlowSprite()
        {
            if (glowSprite) return glowSprite;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Loot radial glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            texture.Apply(false, true);
            glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            return glowSprite;
        }

        void Update()
        {
            if (!glow) return;
            var pulse = 1.25f + Mathf.Sin(Time.time * 4.5f + pulseOffset) * .12f;
            glow.transform.localScale = Vector3.one * pulse;
        }
    }
}
