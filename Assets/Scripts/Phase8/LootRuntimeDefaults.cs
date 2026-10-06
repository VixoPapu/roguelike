using ProjectLike.Phase2;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLike.Phase8
{
    public static class LootRuntimeDefaults
    {
        static LootTable sharedTable;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCatalog() => sharedTable = null;
        public static LootTable SharedTable
        {
            get
            {
                if (!sharedTable) sharedTable = CreateTable();
                return sharedTable;
            }
        }

        static LootTable CreateTable()
        {
            var table = ScriptableObject.CreateInstance<LootTable>();
            table.name = "Runtime Roguelike Loot Table";
            table.allowDuplicates = false;
            var coins = Item("Monedas", LootType.Coins, LootRarity.Common, 6, true);
            var potion = Item("Orbe de vida", LootType.Consumable, LootRarity.Rare, 5, true);
            var key = Item("Llave de dungeon", LootType.Key, LootRarity.Rare, 1, true);
            var legendary = Item("Espada legendaria", LootType.Weapon, LootRarity.Legendary, 1, false); legendary.weapon = RuntimeWeapon(); legendary.basePrice = 180;
            var entries = new List<LootTableEntry>
            {
                Entry(coins, 100, 4, 12), Entry(potion, 4, 1, 1), Entry(key, 34, 1, 1),
                Entry(legendary, 12, 1, 1)
            };
                foreach (var weapon in WeaponCatalog.All)
                {
                    if (!weapon) continue;
                    var item = Item(weapon.weaponName, LootType.Weapon, (LootRarity)Mathf.Clamp((int)weapon.rarity, 0, 4), 1, false);
                    item.description = "Arma para reemplazar la ranura 1 o 2 activa.";
                    item.weapon = weapon;
                    item.worldSprite = CombatFeedback.GetWeaponSprite(weapon);
                    item.basePrice = weapon.price;
                    entries.Add(Entry(item, weapon.rarity == WeaponRarity.Common ? 16f : weapon.rarity == WeaponRarity.Uncommon ? 10f : weapon.rarity == WeaponRarity.Rare ? 6f : weapon.rarity == WeaponRarity.Epic ? 3f : 1f, 1, 1));
                }
            table.entries = entries.ToArray();
            return table;
        }

        static LootData Item(string name, LootType type, LootRarity rarity, float magnitude, bool autoPickup)
        {
            var item = ScriptableObject.CreateInstance<LootData>(); item.displayName = name; item.lootType = type; item.rarity = rarity; item.effectMagnitude = magnitude; item.autoPickup = autoPickup; item.basePrice = 10; return item;
        }
        static LootTableEntry Entry(LootData item, float weight, int min, int max) => new LootTableEntry { enabled = true, item = item, weight = weight, minQuantity = min, maxQuantity = max };
        static WeaponData RuntimeWeapon()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>(); weapon.weaponName = "Espada legendaria"; weapon.type = WeaponType.Legendary; weapon.rarity = WeaponRarity.Legendary; weapon.damage = 22; weapon.attackSpeed = 4.5f; weapon.range = 1.55f; weapon.knockback = 3; weapon.criticalChance = .16f; weapon.criticalMultiplier = 2.2f; return weapon;
        }
    }
}
