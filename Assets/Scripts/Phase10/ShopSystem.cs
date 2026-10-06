using System;
using System.Collections.Generic;
using ProjectLike.Phase1;
using ProjectLike.Phase2;
using ProjectLike.Phase8;
using ProjectLike.Phase11;
using ProjectLike.Phase12;
using ProjectLike.Audio;
using UnityEngine;

namespace ProjectLike.Phase10
{
    public class ShopController : MonoBehaviour
    {
        [HideInInspector] public int productCount = 3;
        [Min(0)] public int rerollCost = 12;
        [Range(0f, .8f)] public float discount;
        public bool specialMerchant;

        List<LootData> currentItems = new List<LootData>();
        List<int> currentPrices = new List<int>();
        List<bool> soldItems = new List<bool>();
        readonly Dictionary<GameObject, CustomerState> customerStates = new Dictionary<GameObject, CustomerState>();
        System.Random random;
        ShopMenu activeMenu;

        public IReadOnlyList<LootData> CurrentItems => currentItems;
        public IReadOnlyList<int> CurrentPrices => currentPrices;
        public IReadOnlyList<bool> SoldItems => soldItems;
        const int MaximumPurchases = 3;
        const int MaximumRerolls = 5;

        sealed class CustomerState
        {
            public int purchases, rerolls;
            public bool initialized;
            public readonly List<LootData> items = new List<LootData>();
            public readonly List<int> prices = new List<int>();
            public readonly List<bool> sold = new List<bool>();
        }
        public void SelectCustomer(GameObject customer)
        {
            var state = StateFor(customer);
            currentItems = state.items; currentPrices = state.prices; soldItems = state.sold;
            if (!state.initialized) { state.initialized = true; BuildOffers(customer); }
        }

        CustomerState StateFor(GameObject customer)
        {
            if (!customerStates.TryGetValue(customer, out var state)) customerStates[customer] = state = new CustomerState();
            return state;
        }

        void Start()
        {
            random = new System.Random(StableSeed(transform.position));
            specialMerchant = random.NextDouble() < .13;
            productCount = 3;
            discount = specialMerchant ? .25f : random.NextDouble() < .18 ? .15f : 0f;
            BuildMerchant();
            BuildOffers();
        }

        int StableSeed(Vector3 position)
        {
            var seed = FindAnyObjectByType<ProjectLike.Phase6.DungeonGenerator>()?.CurrentSeed ?? "Shop";
            unchecked
            {
                var hash = 17;
                foreach (var character in seed) hash = hash * 31 + character;
                hash = hash * 31 + Mathf.RoundToInt(position.x * 10f);
                return hash * 31 + Mathf.RoundToInt(position.y * 10f);
            }
        }

        void BuildMerchant()
        {
            var merchant = new GameObject(specialMerchant ? "ComercianteEspecial" : "Comerciante");
            merchant.transform.SetParent(transform, false);
            merchant.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            var renderer = merchant.AddComponent<SpriteRenderer>();
            renderer.sprite = CombatFeedback.WhiteSprite;
            renderer.color = specialMerchant ? new Color(.75f, .24f, 1f) : new Color(.95f, .66f, .18f);
            renderer.sortingOrder = 6;
            merchant.transform.localScale = new Vector3(.75f, 1.1f, 1f);
            var collider = merchant.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = 1.1f;
            var terminal = merchant.AddComponent<ShopTerminal>(); terminal.shop = this;
            merchant.AddComponent<ShopMerchantVisual>().Configure(renderer, specialMerchant);
        }

        public void BuildOffers(GameObject customer = null)
        {
            currentItems.Clear(); currentPrices.Clear(); soldItems.Clear();
            var catalog = ShopRuntimeCatalog.Get();
            // La tienda siempre enseña una elección de cada categoría.
            for (var i = 0; i < 3; i++)
            {
                var pool = i == 0 ? catalog.weapons : i == 1 ? catalog.consumables : catalog.upgrades;
                if (pool.Count == 0) continue;
                var item = PickEligible(pool, customer);
                if (!item) continue;
                currentItems.Add(item);
                currentPrices.Add(Mathf.Max(1, Mathf.RoundToInt(item.Price * (1f - discount))));
                soldItems.Add(false);
            }
            if (activeMenu) activeMenu.Rebuild();
        }

        public bool TryReroll(PlayerStats stats, out string message)
        {
            message = "";
            if (!stats) { message = "Jugador no disponible"; return false; }
            SelectCustomer(stats.gameObject);
            var state = StateFor(stats.gameObject);
            if (state.purchases >= MaximumPurchases) { message = "Ya agotaste esta tienda"; return false; }
            if (state.rerolls >= MaximumRerolls) { message = "Máximo de 5 rerolls alcanzado"; return false; }
            var actualCost=GetRerollCost(stats?stats.gameObject:null);
            if (stats.coins < actualCost) { message = "Monedas insuficientes"; return false; }
            stats.coins -= actualCost;
            state.rerolls++;

            BuildOffers(stats.gameObject);
            message = "Ofertas actualizadas · " + state.rerolls + "/" + MaximumRerolls + " rerolls";
            return true;
        }

        public bool TryPurchase(int index, GameObject player, out string message)
        {
            message = "";
            if (!player) { message = "Jugador no disponible"; return false; }
            SelectCustomer(player);
            var state = StateFor(player);
            if (state.purchases >= MaximumPurchases) { message = "Ya compraste 3 objetos en esta tienda"; return false; }
            if (index < 0 || index >= currentItems.Count || soldItems[index]) { message = "Producto agotado"; return false; }
            var stats = player.GetComponent<PlayerStats>();
            var price=GetPrice(index,player);
            var inventory = player.GetComponent<PlayerPowerUps>();
            if (currentItems[index].powerUp && inventory && !inventory.CanAcquire(currentItems[index].powerUp)) { message = "Ya tienes esta mejora única"; return false; }
            if (!stats || stats.coins < price) { message = "No tienes suficientes monedas"; return false; }
            var slot = player.GetComponent<PlayerConsumables>();
            if (currentItems[index].consumable && slot && !slot.CanAdd(currentItems[index].consumable)) { message = "Agota el consumible actual o recoge el mismo tipo si queda espacio"; return false; }
            stats.coins -= price;
            LootPickup.Grant(player, currentItems[index]);
            soldItems[index] = true;
            state.purchases++;
            message = state.purchases >= MaximumPurchases ? "Compraste " + currentItems[index].displayName + " · tienda agotada" : "Compraste " + currentItems[index].displayName + " · " + state.purchases + "/" + MaximumPurchases;
            if (activeMenu) activeMenu.Rebuild();
            return true;
        }

        public int GetPrice(int index,GameObject player){if(index<0||index>=currentPrices.Count)return 0;var powers=player?player.GetComponent<PowerUpRuntime>():null;return Mathf.Max(1,Mathf.RoundToInt(currentPrices[index]*(1f-(powers?powers.ShopDiscount:0f))));}
        public int GetRerollCost(GameObject player){var powers=player?player.GetComponent<PowerUpRuntime>():null;return Mathf.Max(1,rerollCost + (player ? StateFor(player).rerolls * 6 : 0)-(((player ? StateFor(player).rerolls == 0 : true)&&powers) ? powers.RerollReduction : 0));}
        public int PurchasesFor(GameObject player) => player ? StateFor(player).purchases : 0;
        public int RerollsFor(GameObject player) => player ? StateFor(player).rerolls : 0;
        public bool IsLockedFor(GameObject player) => player && StateFor(player).purchases >= MaximumPurchases;

        public void Open(GameObject player)
        {
            if (!player) return;
            if (ProjectLike.Online.CoopSession.Active) { ProjectLike.Online.CoopSession.Instance.OpenShop(this, player); return; }
            if (activeMenu) return;
            SelectCustomer(player);
            if (IsLockedFor(player))
            {
                GameSfx.Play(SfxCue.UiDeny, player.transform.position, .62f, true);
                var hud = FindAnyObjectByType<PixelHUDCanvas>();
                if (hud) hud.ShowFeedback("Ya compraste 3 objetos en esta tienda", new Color(1f, .35f, .3f));
                return;
            }
            RefreshUnavailableOffers(player);
            activeMenu = ShopMenu.Open(this, player);
        }

        LootData PickEligible(List<LootData> pool, GameObject customer)
        {
            var inventory = customer ? customer.GetComponent<PlayerPowerUps>() : null;
            for (var attempt = 0; attempt < Mathf.Max(8, pool.Count * 2); attempt++)
            {
                var item = pool[random.Next(pool.Count)];
                if (!item || item.powerUp && inventory && !inventory.CanAcquire(item.powerUp)) continue;
                return item;
            }
            foreach (var item in pool) if (item && (!item.powerUp || !inventory || inventory.CanAcquire(item.powerUp))) return item;
            return null;
        }

        void RefreshUnavailableOffers(GameObject customer)
        {
            var inventory = customer.GetComponent<PlayerPowerUps>();
            if (!inventory) return;
            var upgrades = ShopRuntimeCatalog.Get().upgrades;
            for (var i = 0; i < currentItems.Count; i++)
            {
                if (!currentItems[i] || !currentItems[i].powerUp || inventory.CanAcquire(currentItems[i].powerUp)) continue;
                var replacement = PickEligible(upgrades, customer);
                if (!replacement) { soldItems[i] = true; continue; }
                currentItems[i] = replacement;
                currentPrices[i] = Mathf.Max(1, Mathf.RoundToInt(replacement.Price * (1f - discount)));
                soldItems[i] = false;
            }
        }

        public void NotifyClosed(ShopMenu menu)
        {
            if (activeMenu == menu) activeMenu = null;
        }
    }

    public class ShopTerminal : Interactable
    {
        public ShopController shop;
        void Awake() { prompt = "Abrir tienda"; }
        public override void Interact(GameObject player) { if (shop) shop.Open(player); }
    }

    public sealed class ShopMerchantVisual : MonoBehaviour
    {
        SpriteRenderer body;
        Color bodyColor;
        Vector3 origin;
        readonly List<Transform> motes = new List<Transform>();

        public void Configure(SpriteRenderer renderer, bool special)
        {
            body = renderer;
            bodyColor = renderer ? renderer.color : Color.white;
            origin = transform.localPosition;
            var color = special ? new Color(.82f, .35f, 1f) : new Color(1f, .78f, .18f);
            for (var i = 0; i < 3; i++)
            {
                var mote = new GameObject("CoinMote_" + i);
                mote.transform.SetParent(transform, false);
                mote.transform.localScale = Vector3.one * .13f;
                var sprite = mote.AddComponent<SpriteRenderer>(); sprite.sprite = CombatFeedback.WhiteSprite; sprite.color = color; sprite.sortingOrder = 7;
                motes.Add(mote.transform);
            }
        }

        void Update()
        {
            var time = Time.unscaledTime;
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(time * 2.3f) * .10f);
            if (body) body.color = Color.Lerp(bodyColor, Color.white, .05f + Mathf.Sin(time * 4f) * .035f);
            for (var i = 0; i < motes.Count; i++)
            {
                var angle = time * (1.3f + i * .14f) + i * Mathf.PI * 2f / motes.Count;
                motes[i].localPosition = new Vector3(Mathf.Cos(angle) * 1.05f, .2f + Mathf.Sin(angle * 1.4f) * .72f, 0f);
            }
        }
    }

    public sealed class ShopCatalog
    {
        public readonly List<LootData> weapons = new List<LootData>();
        public readonly List<LootData> consumables = new List<LootData>();
        public readonly List<LootData> upgrades = new List<LootData>();
        public readonly List<LootData> exclusives = new List<LootData>();
    }

    public static class ShopRuntimeCatalog
    {
        static ShopCatalog catalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCatalog() => catalog = null;
        public static ShopCatalog Get()
        {
            // Con Enter Play Mode Options sin recarga de dominio, las referencias
            // estáticas pueden sobrevivir aunque Unity destruya sus ScriptableObjects.
            if (catalog != null && catalog.weapons.Count > 0 && catalog.weapons[0]) return catalog;
            catalog = new ShopCatalog();
            BuildWeapons(); BuildConsumables(); BuildUpgrades();
            return catalog;
        }

        static void BuildWeapons()
        {
            foreach (var weapon in WeaponCatalog.All)
            {
                if (!weapon) continue;
                var rarity = (LootRarity)Mathf.Clamp((int)weapon.rarity, 0, 4);
                var loot = Loot(weapon.weaponName, LootType.Weapon, rarity, BalancedPrice(rarity, LootType.Weapon));
                loot.weapon = weapon; loot.worldSprite = CombatFeedback.GetWeaponSprite(weapon); loot.description = weapon.description; loot.preserveWeaponStats = true;
                var weight = rarity == LootRarity.Common ? 6 : rarity == LootRarity.Uncommon ? 4 : rarity == LootRarity.Rare ? 2 : 1;
                for (var i = 0; i < weight; i++) catalog.weapons.Add(loot);
                if (rarity >= LootRarity.Epic) catalog.exclusives.Add(loot);
            }
        }

        static void BuildConsumables()
        {
            AddConsumable("Poción de vida", ConsumableEffectType.HealthPotion, 8f, 0f, 18, new Color(.9f, .08f, .12f));
            AddConsumable("Poción de energía", ConsumableEffectType.EnergyPotion, 35f, 0f, 15, new Color(.05f, .4f, 1f));
            AddConsumable("Poción de velocidad", ConsumableEffectType.SpeedPotion, 1.5f, 8f, 22, new Color(.2f, 1f, .55f));
            AddConsumable("Poción de daño", ConsumableEffectType.DamagePotion, 5f, 8f, 26, new Color(1f, .25f, .08f));
            AddConsumable("Escudo temporal", ConsumableEffectType.TemporaryShield, 8f, 10f, 24, new Color(.65f, .72f, .8f));
            AddConsumable("Invisibilidad", ConsumableEffectType.Invisibility, 0f, 5f, 32, new Color(.55f, .45f, .9f));
            AddConsumable("Crítico aumentado", ConsumableEffectType.CriticalBoost, .25f, 8f, 30, new Color(1f, .75f, .12f));
            AddConsumable("Invulnerabilidad", ConsumableEffectType.Invulnerability, 0f, 2.5f, 42, Color.white);
            AddConsumable("Bomba", ConsumableEffectType.Bomb, 25f, 0f, 24, new Color(1f, .18f, .04f), 3.2f, 1);
            AddConsumable("Cuchillos arrojadizos", ConsumableEffectType.ThrowingKnives, 8f, 0f, 20, new Color(.8f, .85f, .95f), 3f, 3);
            AddConsumable("Pergamino mágico", ConsumableEffectType.MagicScroll, 10f, 0f, 38, new Color(.5f, .18f, 1f), 4f, 8);
        }

        static void AddConsumable(string name, ConsumableEffectType effect, float power, float duration, int price, Color color, float radius = 3f, int count = 1)
        {
            var data = ScriptableObject.CreateInstance<ConsumableData>(); data.displayName = name; data.effectType = effect; data.magnitude = power; data.duration = duration; data.price = price; data.effectColor = color; data.radius = radius; data.projectileCount = count;
            var loot = Loot(name, LootType.Consumable, price >= 18 ? LootRarity.Rare : LootRarity.Common, price); loot.consumable = data; loot.effectMagnitude = power;
            catalog.consumables.Add(loot);
            if (effect == ConsumableEffectType.Invulnerability || effect == ConsumableEffectType.MagicScroll) catalog.exclusives.Add(loot);
        }

        static void BuildUpgrades()
        {
            var powerUps = Resources.LoadAll<PowerUpData>("PowerUps");
            if (powerUps != null && powerUps.Length > 0)
            {
                foreach (var powerUp in powerUps)
                {
                    if (!powerUp) continue;
                    var item = Loot(powerUp.displayName, LootType.Upgrade, powerUp.rarity, BalancedPrice(powerUp.rarity, LootType.Upgrade));
                    item.powerUp = powerUp; item.icon = powerUp.icon; item.description = powerUp.effectDescription; item.modifiers = powerUp.modifiers;
                    var weight = powerUp.rarity == LootRarity.Common ? 7 : powerUp.rarity == LootRarity.Uncommon ? 5 : powerUp.rarity == LootRarity.Rare ? 3 : powerUp.rarity == LootRarity.Epic ? 2 : 1;
                    for (var i = 0; i < weight; i++) catalog.upgrades.Add(item);
                    if (powerUp.rarity == LootRarity.Legendary || powerUp.rarity == LootRarity.Corrupted) catalog.exclusives.Add(item);
                }
                return;
            }
            AddUpgrade("Corazón reforzado", 13, new LootStatModifiers { maxHealth = 3f });
            AddUpgrade("Filo afilado", 15, new LootStatModifiers { damage = 2f });
            AddUpgrade("Botas ligeras", 12, new LootStatModifiers { moveSpeed = .4f });
            AddUpgrade("Amuleto crítico", 17, new LootStatModifiers { criticalChance = .05f });
            AddUpgrade("Armadura compacta", 14, new LootStatModifiers { defense = .65f });
            AddUpgrade("Trébol extraño", 16, new LootStatModifiers { luck = 1f });
        }

        static void AddUpgrade(string name, int price, LootStatModifiers modifiers)
        {
            var item = Loot(name, LootType.Upgrade, LootRarity.Uncommon, price); item.modifiers = modifiers; catalog.upgrades.Add(item);
        }

        static LootData Loot(string name, LootType type, LootRarity rarity, int price)
        {
            var item = ScriptableObject.CreateInstance<LootData>(); item.displayName = name; item.lootType = type; item.rarity = rarity;
            item.basePrice = Mathf.Max(1, Mathf.RoundToInt(price / LootRarityRules.PriceMultiplier(rarity)));
            return item;
        }

        public static int BalancedPrice(LootRarity rarity, LootType type)
        {
            if (type == LootType.Weapon)
                return rarity == LootRarity.Common ? 38 : rarity == LootRarity.Uncommon ? 55 : rarity == LootRarity.Rare ? 82 : rarity == LootRarity.Epic ? 120 : rarity == LootRarity.Legendary ? 180 : 145;
            return rarity == LootRarity.Common ? 28 : rarity == LootRarity.Uncommon ? 42 : rarity == LootRarity.Rare ? 65 : rarity == LootRarity.Epic ? 95 : rarity == LootRarity.Legendary ? 145 : 115;
        }
    }
}
