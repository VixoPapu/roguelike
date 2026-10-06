using ProjectLike.Phase1;
using ProjectLike.Phase2;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLike.Phase8
{
    public static class LootSpawner
    {
        public static int SpawnTable(LootTable table, Vector2 position, int rolls, float luck, int seed = 0)
        {
            if (!table) return 0;
            var random = seed == 0 ? new System.Random(System.Environment.TickCount) : new System.Random(seed);
            var results = table.Roll(random, Mathf.Max(1, rolls), luck, true);
            for (var i = 0; i < results.Count; i++)
            {
                var angle = i / (float)Mathf.Max(1, results.Count) * Mathf.PI * 2f;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (.55f + i * .08f);
                SpawnPickup(results[i].item, position + offset, results[i].quantity);
            }
            return results.Count;
        }

        public static System.Collections.IEnumerator RevealTable(LootTable table, Vector2 position, int rolls, float luck)
        {
            if (!table) yield break;
            var results = table.Roll(new System.Random(System.Environment.TickCount), Mathf.Max(1, rolls), luck, true);
            for (var i = 0; i < results.Count; i++)
            {
                var angle = i / (float)Mathf.Max(1, results.Count) * Mathf.PI * 2f;
                var destination = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .65f;
                var pickup = SpawnPickup(results[i].item, destination, results[i].quantity);
                pickup.IsRevealing = true;
                var collider = pickup.GetComponent<Collider2D>();
                collider.enabled = false;
                for (var elapsed = 0f; elapsed < .28f; elapsed += Time.deltaTime)
                {
                    if (!pickup) break;
                    var t = Mathf.Clamp01(elapsed / .28f);
                    pickup.transform.position = Vector2.Lerp(position, destination, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * .45f;
                    yield return null;
                }
                if (pickup)
                {
                    pickup.transform.position = destination;
                    pickup.IsRevealing = false;
                    collider.enabled = true;
                }
                yield return new WaitForSeconds(.15f);
            }
        }

        public static LootPickup SpawnWeapon(WeaponData weapon, LootRarity rarity, Vector2 position, bool preserveStats)
        {
            if (!weapon) return null;
            var loot = ScriptableObject.CreateInstance<LootData>();
            loot.name = "RuntimeLoot_" + weapon.weaponName;
            loot.displayName = weapon.weaponName;
            loot.description = preserveStats ? "Arma soltada por el jugador." : "Recompensa de cofre.";
            loot.lootType = LootType.Weapon;
            loot.rarity = rarity;
            loot.weapon = weapon;
            loot.worldSprite = CombatFeedback.GetWeaponSprite(weapon);
            loot.basePrice = weapon.price;
            loot.autoPickup = false;
            loot.preserveWeaponStats = preserveStats;
            return SpawnPickup(loot, position, 1);
        }

        public static int SpawnTableCapped(LootTable table, Vector2 position, int rolls, float luck, LootRarity maximumRarity)
        {
            if (!table || table.entries == null) return 0;
            var allowed = new List<LootTableEntry>();
            foreach (var entry in table.entries)
                if (entry.enabled && entry.item && (int)entry.item.rarity <= (int)maximumRarity) allowed.Add(entry);
            if (allowed.Count == 0) return 0;
            var filtered = ScriptableObject.CreateInstance<LootTable>();
            filtered.allowDuplicates = table.allowDuplicates;
            filtered.entries = allowed.ToArray();
            var spawned = SpawnTable(filtered, position, rolls, luck);
            Object.Destroy(filtered);
            return spawned;
        }

        static LootPickup SpawnPickup(LootData item, Vector2 position, int quantity)
        {
            var pickup = new GameObject("Loot_" + item.displayName);
            pickup.transform.position = position;
            var component = pickup.AddComponent<LootPickup>();
            component.Setup(item, quantity);
            return component;
        }

        public static float PlayerLuck()
        {
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            return stats ? stats.luck : 0;
        }
    }
}
